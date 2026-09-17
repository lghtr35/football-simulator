using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BeAFootballer.Simulation.Api;
using BeAFootballer.Simulation.Core;
using BeAFootballer.Simulation.Data;
using BeAFootballer.Simulation.Fixtures;
using BeAFootballer.Simulation.Persistence.Sqlite;
using NUnit.Framework;

namespace BeAFootballer.Simulation.Tests
{
    public sealed class SeasonRolloverTests
    {
        private string path;
        private SqlitePlaythroughStore store;
        [SetUp] public void Setup()
        {
            path = Path.Combine(Path.GetTempPath(), "football-rollover-" + Guid.NewGuid().ToString("N") + ".db");
            store = new SqlitePlaythroughStore(path);
        }
        [TearDown] public void Cleanup()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        [Test] public async Task TwoStartYearsAcrossLeaguesKeepChampionsDevelopmentAndResumeWithoutThirdSeason()
        {
            var config = WorldGenerator.Example(42, 2);
            config.Leagues[0].SeasonStartMonth = 1; config.Leagues[0].SeasonEndMonth = 3;
            var other = WorldGenerator.Example(42, 2).Leagues[0]; other.Id = "second";
            other.SeasonStartMonth = 2; other.SeasonEndMonth = 4;
            foreach (var team in other.Teams) team.Id = "second-" + team.Id;
            config.Leagues.Add(other);
            var options = new SimulationOptions { MaxYears = 2 };
            var api = SimulationApi.Create(store, config, options);
            var originalAge = store.LoadDevelopmentPage(null, 128, int.MaxValue).Single(p => p.Definition.Id == "free-agent:0").Definition.Attributes.Age;
            await api.RunSeasonAsync("premier:2026");
            Assert.That(store.LoadSeason("premier:2027"), Is.Not.Null);
            Assert.That(store.LoadStandings("premier:2027").All(s => s.Played == 0), Is.True);
            api = new SimulationApi(new SqlitePlaythroughStore(path), options);
            await api.RunSeasonAsync("premier:2027");
            Assert.That(api.EndReason, Is.Null); // The other league's final allowed season is still running.
            await api.RunSeasonAsync("second:2027");
            Assert.That(api.History(), Has.Count.EqualTo(4));
            Assert.That(store.LoadSeason("premier:2028"), Is.Null);
            Assert.That(store.LoadSeason("second:2028"), Is.Null);
            Assert.That(api.EndReason, Is.EqualTo(SimulationEndReason.YearLimitReached));
            Assert.That(store.LoadDevelopmentPage(null, 128, int.MaxValue).Single(p => p.Definition.Id == "free-agent:0").Definition.Attributes.Age,
                Is.EqualTo(originalAge + 1));
            var day = store.LoadMetadata().CurrentDay;
            var cash = api.TeamBalance("team-1");
            var end = Assert.ThrowsAsync<SimulationEndedException>(() => api.AdvanceDaysAsync(1));
            Assert.That(end.Reason, Is.EqualTo(SimulationEndReason.YearLimitReached));
            await api.RunSeasonAsync("premier:2027"); // Reading completed results doesn't award twice.
            Assert.That(api.TeamBalance("team-1"), Is.EqualTo(cash));
            Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(day));
        }

        [Test] public async Task StopAtCompletionRecordsAwardButDoesNotCreateSuccessor()
        {
            var options = new SimulationOptions();
            var hooks = new SimulationHooks { TryContinue = c => {
                if (c.Stage == SimulationStage.AfterSeasonCompletion) options.StopSimulation = true;
                return true;
            } };
            var api = SimulationApi.Create(store, WorldGenerator.Example(42, 2), options, hooks: hooks);
            var summary = await api.RunSeasonAsync("premier:2026");
            Assert.That(summary.WinnerTeamId, Is.Not.Null);
            Assert.That(api.History(), Has.Count.EqualTo(1));
            Assert.That(store.LoadSeason("premier:2027"), Is.Null);
            Assert.That(api.EndReason, Is.EqualTo(SimulationEndReason.StopRequested));
            Assert.ThrowsAsync<SimulationEndedException>(() => api.AdvanceDaysAsync(1));
            Assert.ThrowsAsync<SimulationEndedException>(() => api.AdvanceUntilNextFixtureAsync("team-1"));
        }

        [Test] public async Task StopFlagBlocksWritesAndSingleYearStillFinishesCrossYearSeason()
        {
            var options = new SimulationOptions { MaxYears = 1, StopSimulation = true };
            var config = WorldGenerator.Example(7, 4); // Six rounds extend December into the next year.
            config.Leagues[0].SeasonStartMonth = 12; config.Leagues[0].SeasonEndMonth = 6;
            var api = SimulationApi.Create(store, config, options);
            var day = store.LoadMetadata().CurrentDay;
            Assert.ThrowsAsync<SimulationEndedException>(() => api.AdvanceDaysAsync(1));
            Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(day));
            options.StopSimulation = false;
            await api.RunSeasonAsync("premier:2026");
            Assert.That(api.CurrentDate.Year, Is.EqualTo(2027));
            Assert.That(store.LoadSeason("premier:2027"), Is.Null);
            Assert.That(api.EndReason, Is.EqualTo(SimulationEndReason.YearLimitReached));
        }

        [Test] public async Task InvalidSuccessorRollsBackCompletionAndCanRetryWithoutDuplicatePrize()
        {
            var invalid = new InvalidNextStrategy();
            var config = WorldGenerator.Example(5, 2);
            config.Leagues[0].FixtureStrategyId = invalid.Id;
            var generator = new SeasonFixtureGenerator(invalid);
            var api = SimulationApi.Create(store, config, fixtures: generator);
            Assert.ThrowsAsync<ArgumentException>(() => api.RunSeasonAsync("premier:2026"));
            Assert.That(api.History(), Is.Empty);
            Assert.That(store.LoadSeason("premier:2027"), Is.Null);
            var cash = api.TeamBalance("team-1") + api.TeamBalance("team-2");
            invalid.Fail = false;
            await api.RunSeasonAsync("premier:2026");
            Assert.That(api.History(), Has.Count.EqualTo(1));
            Assert.That(api.TeamBalance("team-1") + api.TeamBalance("team-2"), Is.EqualTo(cash + 100000));
            Assert.That(store.LoadSeason("premier:2027").FixtureStrategyId, Is.EqualTo(invalid.Id));
            await api.RunSeasonAsync("premier:2026");
            Assert.That(api.History(), Has.Count.EqualTo(1));
        }

        [Test] public async Task AuthoredSuccessorWithCustomIdIsPreserved()
        {
            var api = SimulationApi.Create(store, WorldGenerator.Example(3, 2));
            var next = store.LoadSeason("premier:2026");
            next.Id = "custom-next-season"; next.StartDate.Year++; next.EndDate.Year++;
            store.SaveSeason(next, new DoubleRoundRobinStrategy().Generate(next));
            await api.RunSeasonAsync("premier:2026");
            Assert.That(store.LoadSeason("premier:2027"), Is.Null);
            Assert.That(store.LoadUnfinishedSeasons().Select(s => s.Id), Is.EqualTo(new[] { "custom-next-season" }));
        }

        private sealed class InvalidNextStrategy : ISeasonFixtureStrategy
        {
            public string Id => "test-rollover";
            public bool Fail = true;
            public System.Collections.Generic.IReadOnlyList<Fixture> Generate(LeagueSeason season)
            {
                var fixtures = new DoubleRoundRobinStrategy().Generate(season).ToList();
                if (season.StartDate.Year > 2026 && Fail) fixtures[0].ScheduledDay--;
                return fixtures;
            }
        }
    }
}
