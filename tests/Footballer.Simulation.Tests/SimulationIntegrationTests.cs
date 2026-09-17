using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeAFootballer.Simulation.Api;
using BeAFootballer.Simulation.Core;
using BeAFootballer.Simulation.Data;
using BeAFootballer.Simulation.Match;
using BeAFootballer.Simulation.Persistence.Sqlite;
using NUnit.Framework;

namespace BeAFootballer.Simulation.Tests
{
    public sealed class SimulationIntegrationTests
    {
        private readonly List<string> paths = new List<string>();
        private SqlitePlaythroughStore Store()
        {
            var path = Path.Combine(Path.GetTempPath(), "football-season-" + Guid.NewGuid().ToString("N") + ".db");
            paths.Add(path);
            return new SqlitePlaythroughStore(path);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var path in paths)
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            paths.Clear();
        }

        [Test]
        public async Task CompleteSeasonPersistsResultsTablePlayerHistoryAndPrizeOnce()
        {
            var store = Store();
            var api = SimulationApi.Create(store, WorldGenerator.Example(42, 6));
            var summary = await api.RunSeasonAsync("premier:2026");
            var results = api.Fixtures(new FixtureFilter { SeasonId = summary.SeasonId, IsPlayed = true });
            Assert.That(results, Has.Count.EqualTo(30));
            Assert.That(api.Standings(summary.SeasonId).All(t => t.Played == 10), Is.True);
            var table = api.Standings(summary.SeasonId);
            Assert.That(table.Sum(t => t.GoalsFor), Is.EqualTo(table.Sum(t => t.GoalsAgainst)));
            Assert.That(table.Sum(t => t.Points), Is.EqualTo(results.Sum(f => f.HomeGoals == f.AwayGoals ? 2 : 3)));
            Assert.That(summary.WinnerTeamId, Is.EqualTo(table[0].TeamId));
            var winnerBalance = store.LoadTeamBalance(summary.WinnerTeamId);
            Assert.That(summary.Prize, Is.EqualTo(100000));
            Assert.That(winnerBalance, Is.GreaterThan(1000000)); // Starting funds plus daily net income and prize.
            var goals = store.LoadTeams(table.Select(t => t.TeamId).ToArray()).Values
                .SelectMany(t => t.Players).Sum(p => p.State.Stats.Goals);
            Assert.That(goals, Is.EqualTo(results.Sum(f => f.HomeGoals + f.AwayGoals)));
            Assert.That(api.MatchEvents(results[0].Id).Last().Type, Is.EqualTo(MatchEventType.FullTime));
            var reopened = new SimulationApi(new SqlitePlaythroughStore(paths[0]));
            await reopened.RunSeasonAsync(summary.SeasonId);
            Assert.That(reopened.History(), Has.Count.EqualTo(1));
            Assert.That(store.LoadTeamBalance(summary.WinnerTeamId), Is.EqualTo(winnerBalance));
        }

        [Test]
        public async Task SequentialParallelAndResumeHaveIdenticalResults()
        {
            var sequentialStore = Store();
            var sequential = SimulationApi.Create(sequentialStore, WorldGenerator.Example(71, 4),
                new SimulationOptions { MaxParallelMatches = 1, BatchSize = 1 });
            await sequential.RunSeasonAsync("premier:2026");
            var parallelStore = Store();
            var parallel = SimulationApi.Create(parallelStore, WorldGenerator.Example(71, 4),
                new SimulationOptions { MaxParallelMatches = 4, BatchSize = 8, ReservedCpuCores = 0 });
            await parallel.AdvanceDaysAsync(8);
            var resumed = new SimulationApi(new SqlitePlaythroughStore(paths[1]),
                new SimulationOptions { MaxParallelMatches = 3, BatchSize = 3 });
            await resumed.RunSeasonAsync("premier:2026");
            string[] Fingerprint(SimulationApi api) => api.Fixtures(new FixtureFilter()).Select(f =>
                f.Id + ":" + f.HomeGoals + ":" + f.AwayGoals + ":" +
                string.Join(",", api.MatchEvents(f.Id).Select(e => e.Minute + "/" + e.Type + "/" + e.PrimaryFootballerId))).ToArray();
            Assert.That(Fingerprint(resumed), Is.EqualTo(Fingerprint(sequential)));
        }

        [Test]
        public async Task InteractiveFixtureCanBeHeldWhileOtherMatchesRun()
        {
            var store = Store();
            var api = SimulationApi.Create(store, WorldGenerator.Example(42, 4));
            var fixture = await api.AdvanceUntilNextFixtureAsync("team-1");
            var day = store.LoadMetadata().CurrentDay;
            var input = await api.PrepareInteractiveAsync(fixture.Id);
            Assert.That(input.Home.Lineup, Has.Count.EqualTo(11));
            Assert.That(input.Away.Lineup, Has.Count.EqualTo(11));
            var otherToday = api.Fixtures(new FixtureFilter
            {
                FromDay = day, ToDay = day, IsPlayed = false, ExcludeFixtureId = fixture.Id, Limit = 50
            }).Count;
            var background = api.SimulateBackgroundAsync(fixture.Id);
            var interactiveResult = new FastMatchStrategy().Simulate(input);
            Assert.That(await background, Is.EqualTo(otherToday));
            Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(day));
            Assert.That(api.Fixtures(new FixtureFilter { IsPlayed = false, ToDay = day }), Has.Count.EqualTo(1));
            await api.CompleteInteractiveAsync(interactiveResult);
            Assert.ThrowsAsync<InvalidOperationException>(() => api.CompleteInteractiveAsync(interactiveResult));
            await api.AdvanceDaysAsync(1);
            Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(day + 1));
            Assert.That(api.Fixtures(new FixtureFilter { TeamId = "team-1", IsPlayed = true }), Has.Count.EqualTo(1));
        }

        [Test]
        public async Task CancellationBeforeBatchCommitLeavesAnUnplayedDayThatCanResume()
        {
            var store = Store();
            SimulationApi.Create(store, WorldGenerator.Example(42, 4));
            using (var source = new CancellationTokenSource())
            {
                var api = new SimulationApi(store, match: new CancellingStrategy(source));
                var day = store.LoadMetadata().CurrentDay;
                Assert.ThrowsAsync<OperationCanceledException>(() => api.AdvanceDaysAsync(1, source.Token));
                Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(day));
                Assert.That(api.Fixtures(new FixtureFilter { IsPlayed = true }), Is.Empty);
            }
            await new SimulationApi(store).RunSeasonAsync("premier:2026");
            Assert.That(store.LoadSeasonHistory(), Has.Count.EqualTo(1));
        }

        private sealed class CancellingStrategy : IMatchSimulationStrategy
        {
            private readonly CancellationTokenSource source;
            public CancellingStrategy(CancellationTokenSource source) { this.source = source; }
            public string Id => "cancel";
            public SimulationMode Mode => SimulationMode.Discrete;
            public MatchResult Simulate(MatchInput input, CancellationToken token)
            {
                source.Cancel();
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException();
            }
        }

        [Test]
        public async Task DuplicateBatchRollsBackAllEffectsAndThenSucceedsOnce()
        {
            var store = Store();
            var api = SimulationApi.Create(store, WorldGenerator.Example(42, 4));
            var fixture = await api.AdvanceUntilNextFixtureAsync("team-1");
            var input = await api.PrepareInteractiveAsync(fixture.Id);
            var result = new FastMatchStrategy().Simulate(input);
            BeAFootballer.Simulation.Career.MatchConsequences.Apply(input, result);
            Assert.Throws<InvalidOperationException>(() => store.CommitMatches(fixture.ScheduledDay, new[] { result, result }));
            Assert.That(store.LoadStandings(fixture.SeasonId).Sum(t => t.Played), Is.Zero);
            Assert.That(store.LoadMatchEvents(fixture.Id), Is.Empty);
            await api.CompleteInteractiveAsync(result);
            Assert.That(store.LoadStandings(fixture.SeasonId).Sum(t => t.Played), Is.EqualTo(2));
        }

        [Test]
        public void ConfiguredRatingsCoachesAndResourceBudgetsAreRespected()
        {
            var config = WorldGenerator.Example(123, 5);
            config.UnemployedCoaches = 4;
            config.Leagues[0].Teams[0].TargetOverall.Attack = 90;
            var world = WorldGenerator.Create(config);
            Assert.That(world.Coaches.Count(c => c.TeamId == null), Is.EqualTo(4));
            Assert.That(world.Coaches.Where(c => c.TeamId != null).Select(c => c.TeamId).Distinct().Count(), Is.EqualTo(5));
            Assert.That(world.Footballers.Where(p => p.TeamId == "team-1" && MatchPreparation.PositionGroup(p.Position) == 3)
                .Average(p => p.Overall), Is.EqualTo(90));
            var limits = new SimulationOptions { MemoryBudgetMb = 16, EstimatedMemoryPerMatchMb = 8, MaxParallelMatches = 32, BatchSize = 32 };
            Assert.That(limits.Resolve(WorkloadMode.AdvanceDays, 64), Is.EqualTo((2, 2)));
            Assert.That(limits.Resolve(WorkloadMode.InteractiveBackground, 64).Workers, Is.EqualTo(1));
            limits.MemoryBudgetMb = 1;
            Assert.Throws<ArgumentException>(() => limits.Resolve(WorkloadMode.AdvanceDays, 8));
        }

        [Test]
        public async Task CalendarCrossesYearBoundaryAndMultipleLeaguesComplete()
        {
            var config = WorldGenerator.Example(9, 4);
            config.Leagues[0].SeasonStartMonth = 12;
            config.Leagues[0].SeasonEndMonth = 2;
            var other = WorldGenerator.Example(9, 3).Leagues[0];
            other.Id = "second";
            other.SeasonStartMonth = 12;
            other.SeasonEndMonth = 2;
            foreach (var team in other.Teams) team.Id = "second-" + team.Id;
            config.Leagues.Add(other);
            var store = Store();
            var api = SimulationApi.Create(store, config);
            await api.RunSeasonAsync("premier:2026");
            await api.RunSeasonAsync("second:2026");
            Assert.That(api.History(), Has.Count.EqualTo(2));
            Assert.That(api.CurrentDate.Year, Is.EqualTo(2027));
            Assert.That(api.Fixtures(new FixtureFilter { LeagueId = "second", IsPlayed = true }), Has.Count.EqualTo(6));
        }
    }
}
