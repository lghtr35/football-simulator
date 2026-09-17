using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Football.Simulation.Api;
using Football.Simulation.Career;
using Football.Simulation.Data;
using Football.Simulation.Life;
using Football.Simulation.Match;
using Football.Simulation.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Simulation.Tests
{
    public sealed class DevelopmentAndEconomyTests
    {
        private string path;
        private SqlitePlaythroughStore store;
        [SetUp] public void Setup()
        {
            path = Path.Combine(Path.GetTempPath(), "football-economy-" + Guid.NewGuid().ToString("N") + ".db");
            store = new SqlitePlaythroughStore(path);
        }
        [TearDown] public void Cleanup()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        private long Scalar(string sql)
        {
            using (var c = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
            { c.Open(); using (var cmd = c.CreateCommand()) { cmd.CommandText = sql; return Convert.ToInt64(cmd.ExecuteScalar()); } }
        }

        [Test] public async Task RatingsFlagChangesOnlyRatingsAndNullSurvivesStorage()
        {
            var api = SimulationApi.Create(store, WorldGenerator.Example(41, 2));
            var fixture = await api.AdvanceUntilNextFixtureAsync("team-1");
            var input = await api.PrepareInteractiveAsync(fixture.Id);
            var off = new FastMatchStrategy(false).Simulate(input);
            var on = new FastMatchStrategy(true).Simulate(input);
            string Fingerprint(MatchResult r) => r.HomeGoals + ":" + r.AwayGoals + ":" + string.Join(";", r.Players.Select(p =>
                p.FootballerId + "/" + p.Minutes + "/" + p.Goals + "/" + p.Assists + "/" + p.InjuryDays + "/" + p.YellowCards + "/" + p.RedCards));
            Assert.That(Fingerprint(off), Is.EqualTo(Fingerprint(on)));
            Assert.That(off.Players.All(p => p.Rating == null), Is.True);
            Assert.That(on.Players.All(p => p.Rating >= 1 && p.Rating <= 100), Is.True);
            await api.CompleteInteractiveAsync(off);
            Assert.That(Scalar("SELECT COUNT(*) FROM FootballerMatchPerformances WHERE Rating IS NOT NULL;"), Is.Zero);
            Assert.That(Scalar("SELECT COUNT(*) FROM FootballerMatchPerformances;"), Is.EqualTo(off.Players.Count));
            Assert.That(api.MatchPerformances(fixture.Id).All(p => p.Rating == null), Is.True);
        }

        [Test] public async Task SquadSelectionRotatesAndSubstitutionMinutesAreConserved()
        {
            var config = WorldGenerator.Example(50, 2);
            config.Leagues[0].MatchSquadSize = 19;
            var api = SimulationApi.Create(store, config);
            var fixture = await api.AdvanceUntilNextFixtureAsync("team-1");
            var input = await api.PrepareInteractiveAsync(fixture.Id);
            Assert.That(input.Home.RegisteredPlayers.Count(), Is.EqualTo(19));
            Assert.That(input.Home.Lineup.GroupBy(p => PlayerAbility.PositionGroup(p.Player.Definition.Position)).OrderBy(g => g.Key).Select(g => g.Count()), Is.EqualTo(new[] { 1, 4, 4, 2 }));
            var exhausted = input.Home.Lineup.First(p => PlayerAbility.PositionGroup(p.Player.Definition.Position) == 1).Player;
            exhausted.State.Form.Fatigue = 100; exhausted.State.Form.Fitness = 20;
            var rotated = MatchPreparation.Prepare(fixture, input.Home.Squad, input.Away.Squad, 50);
            Assert.That(rotated.Home.Lineup.Any(p => p.Player.Definition.Id == exhausted.Definition.Id), Is.False);
            var result = new FastMatchStrategy().Simulate(rotated);
            Assert.That(result.Events.Count(e => e.Type == MatchEventType.Substitution), Is.GreaterThan(0));
            foreach (var team in new[] { fixture.HomeTeamId, fixture.AwayTeamId })
            {
                Assert.That(result.Players.Where(p => p.TeamId == team).Sum(p => p.Minutes), Is.LessThanOrEqualTo(990));
                Assert.That(result.Players.Count(p => p.TeamId == team && p.Started), Is.EqualTo(11));
                Assert.That(result.Events.Count(e => e.TeamId == team && e.Type == MatchEventType.Substitution), Is.LessThanOrEqualTo(5));
            }
            foreach (var sub in result.Events.Where(e => e.Type == MatchEventType.Substitution))
            {
                var incoming = result.Players.Single(p => p.FootballerId == sub.SecondaryFootballerId);
                Assert.That(incoming.Minutes, Is.LessThanOrEqualTo(90 - sub.Minute));
                Assert.That(incoming.Started, Is.False);
            }
        }

        [Test] public void DevelopmentIsSeededAndAgeTrainingFormAndMinutesMatter()
        {
            var strategy = new PlayerDevelopmentStrategy();
            int Changes(int age, int training, int form, int minutes)
            {
                var sum = 0;
                for (var seed = 0; seed < 2000; seed++)
                {
                    var player = WorldGenerator.Create(WorldGenerator.Example(seed, 2)).Footballers[0];
                    player.Skills.Goalkeeping = player.Skills.Strength = 65;
                    var data = new DevelopmentPlayer { Definition = player, BirthDay = 7 - age * 365,
                        Training = training, Form = form, MinutesSinceDevelopment = minutes };
                    sum += strategy.Advance(data, 7, new WorldRules(), seed).Change;
                }
                return sum;
            }
            var young = Changes(19, 100, 100, 180);
            var older = Changes(38, 0, 0, 0);
            Assert.That(young, Is.GreaterThan(0));
            Assert.That(older, Is.LessThan(0));
            Assert.That(Changes(38, 100, 100, 180), Is.GreaterThan(older));
            Assert.That(Changes(19, 0, 0, 0), Is.LessThan(young));
            Assert.That(Changes(19, 100, 100, 180), Is.EqualTo(young));
        }

        [Test] public async Task WorldUpdatesIncludeFreeAgentsAndResumeDoesNotDuplicateDailyEffects()
        {
            var config = WorldGenerator.Example(3, 2);
            config.Rules.TransferWindows.Clear();
            var api = SimulationApi.Create(store, config);
            api.SetTraining("free-agent:0", 100);
            await api.AdvanceDaysAsync(8);
            var day = store.LoadMetadata().CurrentDay;
            Assert.That(Scalar("SELECT LastDevelopmentDay FROM DevelopmentData WHERE FootballerId='free-agent:0';"), Is.EqualTo(day - 1));
            Assert.That(Scalar("SELECT LastUpdatedDay FROM FootballerState WHERE FootballerId='free-agent:0';"), Is.EqualTo(day - 1));
            Assert.That(Scalar("SELECT Training FROM DevelopmentData WHERE FootballerId='free-agent:0';"), Is.EqualTo(100));
            var fixture = api.Fixtures(new FixtureFilter { TeamId = "team-1", IsPlayed = false, Limit = 1 }).Single();
            // Completing the first season now creates next year's fixtures without jumping the date.
            Assert.That(fixture.SeasonId, Is.EqualTo("premier:2027"));
            var balance = api.TeamBalance("team-1");
            var historyCount = Scalar("SELECT COUNT(*) FROM WorldHistory;");
            var yesterday = store.LoadMetadata().CurrentDay - 1;
            Assert.That(store.IsMarketDayComplete(yesterday), Is.True);
            Assert.That(new SimulationApi(new SqlitePlaythroughStore(path)).TeamBalance("team-1"), Is.EqualTo(balance));
            Assert.That(Scalar("SELECT COUNT(*) FROM WorldHistory;"), Is.EqualTo(historyCount));
            Assert.That(Scalar("SELECT COUNT(*) FROM WorldHistory WHERE Type='cashflow';"), Is.Zero);
        }

        [Test] public async Task IdleMarketAppliesCashWithoutContractRewritesOrCashflowHistory()
        {
            var config = WorldGenerator.Example(11, 2);
            config.Rules.TransferWindows.Clear();
            var api = SimulationApi.Create(store, config);
            var start = api.TeamBalance("team-1");
            await api.AdvanceDaysAsync(1);
            Assert.That(api.TeamBalance("team-1"), Is.Not.EqualTo(start));
            Assert.That(Scalar("SELECT COUNT(*) FROM WorldHistory WHERE Type='cashflow';"), Is.Zero);
            var yesterday = store.LoadMetadata().CurrentDay - 1;
            Assert.That(store.IsMarketDayComplete(yesterday), Is.True);
            Assert.That(store.LoadLifePage(null, 10, yesterday), Is.Empty);
        }

        [Test] public void TransferWindowsCapsBudgetsAndContractsAreRespected()
        {
            var config = WorldGenerator.Example(9, 2);
            SimulationApi.Create(store, config);
            var rules = store.LoadWorldRules(); rules.TransferIntervalDays = 1;
            rules.TransferWindows = new List<TransferWindow> { new TransferWindow { StartDayOfYear = 360, EndDayOfYear = 5 } };
            Assert.That(rules.TransferWindows[0].Contains(364), Is.True);
            Assert.That(rules.TransferWindows[0].Contains(0), Is.True);
            Assert.That(rules.TransferWindows[0].Contains(5), Is.False);
            var strategy = new TransferStrategy();
            Assert.That(strategy.Advance(store.LoadMarket(), rules, 100).History.Any(h => h.Type.EndsWith("-transfer")), Is.False);
            var snapshot = store.LoadMarket();
            foreach (var p in snapshot.People.Where(p => p.Contract.TeamId == null)) { p.Overall = 100; p.Value = 100; p.AskingWage = 10; }
            var cash = snapshot.Clubs.Sum(c => c.Cash);
            var net = snapshot.Clubs.Sum(c => c.DailyIncome) - snapshot.People.Sum(p => p.Contract.DailyWage);
            var update = strategy.Advance(snapshot, rules, 0);
            Assert.That(update.History.Any(h => h.Type == "player-transfer"), Is.True);
            Assert.That(snapshot.Clubs.Sum(c => c.Cash), Is.EqualTo(cash + net));
            foreach (var club in snapshot.Clubs)
            {
                Assert.That(snapshot.People.Count(p => p.Contract.TeamId == club.TeamId && p.Contract.Kind == PersonKind.Player), Is.LessThanOrEqualTo(32));
                Assert.That(snapshot.People.Count(p => p.Contract.TeamId == club.TeamId && p.Contract.Kind == PersonKind.Coach), Is.EqualTo(1));
                Assert.That(snapshot.People.Where(p => p.Contract.TeamId == club.TeamId).Sum(p => p.Contract.DailyWage), Is.LessThanOrEqualTo(club.WageBudget));
            }
            var poor = store.LoadMarket();
            foreach (var club in poor.Clubs) { club.Cash = 0; club.WageBudget = 0; club.DailyIncome = 0; }
            Assert.That(strategy.Advance(poor, rules, 0).History.Any(h => h.Type.EndsWith("-transfer")), Is.False);
            foreach (var p in poor.People) p.Contract.EndDay = 0;
            Assert.That(strategy.Advance(poor, rules, 0).History.Any(h => h.Type == "release"), Is.True);
            var renew = store.LoadMarket();
            foreach (var p in renew.People) p.Contract.EndDay = 0;
            Assert.That(strategy.Advance(renew, rules, 100).History.Any(h => h.Type == "renewal"), Is.True);
        }

        [Test] public async Task RepreparingInteractiveMatchDoesNotRepeatEconomyAndDevelopment()
        {
            var api = SimulationApi.Create(store, WorldGenerator.Example(5, 4));
            var fixture = await api.AdvanceUntilNextFixtureAsync("team-1");
            await api.PrepareInteractiveAsync(fixture.Id);
            var balance = api.TeamBalance("team-1");
            var entries = Scalar("SELECT COUNT(*) FROM WorldHistory;");
            var resumed = new SimulationApi(new SqlitePlaythroughStore(path));
            await resumed.PrepareInteractiveAsync(fixture.Id);
            await resumed.SimulateBackgroundAsync(fixture.Id);
            Assert.That(resumed.TeamBalance("team-1"), Is.EqualTo(balance));
            Assert.That(Scalar("SELECT COUNT(*) FROM WorldHistory;"), Is.EqualTo(entries));
        }

        [Test] public void GenerationRejectsInvalidRosterAndRegistrationLimits()
        {
            var config = WorldGenerator.Example(2, 2);
            config.Leagues[0].MatchSquadSize = 18;
            Assert.Throws<ArgumentException>(() => WorldGenerator.Create(config));
            config.Leagues[0].MatchSquadSize = 33;
            Assert.Throws<ArgumentException>(() => WorldGenerator.Create(config));
            config.Leagues[0].MatchSquadSize = 20;
            config.Leagues[0].Teams[0].Squad.Attackers = 15;
            Assert.Throws<ArgumentException>(() => WorldGenerator.Create(config));
        }

        [Test] public async Task DevelopmentPageCommitIsIdempotentAndKeepsSkillsInBounds()
        {
            var api = SimulationApi.Create(store, WorldGenerator.Example(13, 2));
            await api.AdvanceDaysAsync(7);
            var day = store.LoadMetadata().CurrentDay;
            var page = store.LoadDevelopmentPage(null, 3, day - 7);
            Assert.That(page, Has.Count.EqualTo(3));
            var updates = page.Select(p => new PlayerDevelopmentStrategy().Advance(p, day, store.LoadWorldRules(), 13)).ToList();
            store.CommitDevelopment(day, updates);
            var history = Scalar("SELECT COUNT(*) FROM WorldHistory;");
            store.CommitDevelopment(day, updates);
            Assert.That(Scalar("SELECT COUNT(*) FROM WorldHistory;"), Is.EqualTo(history));
            Assert.That(store.LoadDevelopmentPage(null, 100, day - 7).Any(p => page.Any(old => old.Definition.Id == p.Definition.Id)), Is.False);
            Assert.That(Scalar("SELECT MIN(Overall) FROM PlayerRatings;"), Is.GreaterThanOrEqualTo(1));
            Assert.That(Scalar("SELECT MAX(Overall) FROM PlayerRatings;"), Is.LessThanOrEqualTo(100));
            await new SimulationApi(store).AdvanceDaysAsync(1);
            Assert.That(store.LoadDevelopmentPage(null, 100, day - 7), Is.Empty);
        }

        [Test] public void TransferSalesPreservePositionalCoverageAndCannotExceedFullRoster()
        {
            SimulationApi.Create(store, WorldGenerator.Example(7, 2));
            var rules = store.LoadWorldRules(); rules.TransferIntervalDays = 1;
            var snapshot = store.LoadMarket();
            snapshot.People.RemoveAll(p => p.Contract.TeamId == null);
            var seller = snapshot.People.Where(p => p.Contract.TeamId == "team-1" && p.Contract.Kind == PersonKind.Player).ToList();
            foreach (var p in seller) { p.Overall = 99; p.Value = 100; p.AskingWage = 10; }
            var update = new TransferStrategy().Advance(snapshot, rules, 0);
            Assert.That(update.History.Any(h => h.Type == "player-transfer" && h.FromTeamId == "team-1"), Is.True);
            var remaining = snapshot.People.Where(p => p.Contract.TeamId == "team-1" && p.Contract.Kind == PersonKind.Player).ToList();
            Assert.That(remaining.Count, Is.GreaterThanOrEqualTo(19));
            Assert.That(remaining.Count(p => p.PositionGroup == 0), Is.GreaterThanOrEqualTo(2));
            Assert.That(remaining.Count(p => p.PositionGroup == 1), Is.GreaterThanOrEqualTo(5));
            Assert.That(remaining.Count(p => p.PositionGroup == 2), Is.GreaterThanOrEqualTo(5));
            Assert.That(remaining.Count(p => p.PositionGroup == 3), Is.GreaterThanOrEqualTo(3));
            var full = store.LoadMarket();
            for (var i = 0; i < 10; i++) full.People.Add(new MarketPerson { Contract = new Contract {
                PersonId = "extra-" + i, TeamId = "team-1", Kind = PersonKind.Player, EndDay = int.MaxValue }, PositionGroup = 1 });
            foreach (var p in full.People.Where(p => p.Contract.TeamId == null)) { p.Overall = 100; p.AskingWage = 1; }
            Assert.That(new TransferStrategy().Advance(full, rules, 0).History.Any(h => h.Type == "player-transfer" && h.ToTeamId == "team-1"), Is.False);
        }

        [Test] public void SqlEnforcesPlayerAndCoachCaps()
        {
            SimulationApi.Create(store, WorldGenerator.Example(8, 2));
            using (var c = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
            {
                c.Open();
                using (var cmd = c.CreateCommand())
                {
                    for (var i = 0; i < 10; i++)
                    {
                        cmd.CommandText = "INSERT INTO Footballers SELECT 'extra-' || $number,Name,TeamId,Position,PreferredFoot,Age,HeightCentimetres,WeightKilograms,Nationality,Speed,Acceleration,Stamina,StaminaRegen,Dribbling,FirstTouchControl,HitPower,Accuracy,Tackling,Strength FROM Footballers WHERE Id='team-1-player-1';";
                        cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("$number", i); cmd.ExecuteNonQuery();
                    }
                    cmd.Parameters["$number"].Value = 10;
                    Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
                    cmd.Parameters.Clear(); cmd.CommandText = "UPDATE Coaches SET TeamId='team-1' WHERE Id='unemployed:0';";
                    Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
                }
            }
        }
    }
}
