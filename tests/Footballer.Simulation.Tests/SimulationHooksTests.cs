using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BeAFootballer.Simulation.Api;
using BeAFootballer.Simulation.Core;
using BeAFootballer.Simulation.Data;
using BeAFootballer.Simulation.Match;
using BeAFootballer.Simulation.Persistence.Sqlite;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace BeAFootballer.Simulation.Tests
{
    public sealed class SimulationHooksTests
    {
        private string path;
        private SqlitePlaythroughStore store;
        [SetUp] public void Setup()
        {
            path = Path.Combine(Path.GetTempPath(), "football-hooks-" + Guid.NewGuid().ToString("N") + ".db");
            store = new SqlitePlaythroughStore(path);
            SimulationApi.Create(store, WorldGenerator.Example(42, 6));
        }
        [TearDown] public void Cleanup()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        [Test] public async Task GameOwnsCoachStopAndSquadInputsAcrossReload()
        {
            MatchSquadSelection gameSelection = null;
            var hooks = new SimulationHooks {
                IsActor = (kind, id) => kind == PersonKind.Coach && id == "coach:team-1",
                TryContinue = context => {
                    if (context.Stage != SimulationStage.BeforeMatch) return true;
                    if (context.Home.Coach.IsActor) { context.HomeSelection = gameSelection; return gameSelection != null; }
                    if (context.Away.Coach.IsActor) { context.AwaySelection = gameSelection; return gameSelection != null; }
                    return true;
                }
            };
            var api = new SimulationApi(store, new SimulationOptions { BatchSize = 1 }, hooks: hooks);
            var stop = await api.AdvanceUntilStopAsync(1);
            Assert.That(stop.Stage, Is.EqualTo(SimulationStage.BeforeMatch));
            Assert.That(api.Fixtures(new FixtureFilter { IsPlayed = true }), Has.Count.EqualTo(2));
            Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(stop.Day));
            var team = stop.Home.Coach.IsActor ? stop.Home : stop.Away;
            var ids = team.Players.OrderBy(p => p.Definition.Overall).ThenBy(p => p.Definition.Id).Select(p => p.Definition.Id).ToArray();
            // This variable represents a decision owned/restored by the game, not by the simulation store.
            gameSelection = new MatchSquadSelection { Starters = ids.Take(11).ToList(), Bench = ids.Skip(11).Take(9).ToList() };
            api = new SimulationApi(new SqlitePlaythroughStore(path), hooks: hooks);
            Assert.That(await api.AdvanceUntilStopAsync(1), Is.Null);
            Assert.That(store.LoadTeams(new[] { "team-1" })["team-1"].Players.Where(p => p.State.Stats.Starts == 1).Select(p => p.Definition.Id), Is.EquivalentTo(gameSelection.Starters));
            using (var c = new SqliteConnection("Data Source=" + path + ";Pooling=False"))
            {
                c.Open(); using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('ActorControls','ActorDecisions','ManualMatchSquads','ManualMatchSquadHeaders');";
                    Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.Zero);
                }
            }
        }

        [Test] public async Task GameCanHoldPlayerMatchThenDriveInteractiveResult()
        {
            var ready = false;
            var hooks = new SimulationHooks {
                IsActor = (kind, id) => kind == PersonKind.Player && id == "team-1-player-1",
                TryContinue = c => c.Stage != SimulationStage.BeforeMatch || ready ||
                    !c.Home.Players.Concat(c.Away.Players).Any(p => p.Definition.IsActor)
            };
            var api = new SimulationApi(store, hooks: hooks);
            var stop = await api.AdvanceUntilStopAsync(1);
            Assert.That(stop.Fixture, Is.Not.Null);
            Assert.That(await api.SimulateBackgroundAsync(stop.Fixture.Id), Is.Zero);
            ready = true; // The game's match flow now owns control.
            var input = await api.PrepareInteractiveAsync(stop.Fixture.Id);
            await api.CompleteInteractiveAsync(new FastMatchStrategy().Simulate(input));
            Assert.That(await api.AdvanceUntilStopAsync(1), Is.Null);
        }

        [Test] public async Task UnknownGameEventCanStopDailyProgressWithoutSimulationKnowingItsMeaning()
        {
            var gameHasResolvedEvent = false;
            var hooks = new SimulationHooks { TryContinue = c => c.Stage != SimulationStage.BeforeDay || gameHasResolvedEvent };
            var api = new SimulationApi(store, hooks: hooks);
            var cash = api.TeamBalance("team-1");
            var stop = await api.AdvanceUntilStopAsync(3);
            Assert.That(stop.Stage, Is.EqualTo(SimulationStage.BeforeDay));
            Assert.That(api.TeamBalance("team-1"), Is.EqualTo(cash));
            Assert.That(api.Fixtures(new FixtureFilter { IsPlayed = true }), Is.Empty);
            // Sponsor / purchase / relationship logic belongs entirely to the game.
            gameHasResolvedEvent = true;
            Assert.That(await api.AdvanceUntilStopAsync(1), Is.Null);
            Assert.That(store.LoadMetadata().CurrentDay, Is.EqualTo(stop.Day + 1));
        }

        [Test] public async Task InvalidGameSquadDoesNotCommitFixtureAndRetryCanSupplyCorrectInput()
        {
            var invalid = true;
            var hooks = new SimulationHooks { TryContinue = c => {
                if (c.Stage == SimulationStage.BeforeMatch && invalid)
                    c.HomeSelection = new MatchSquadSelection { Starters = Enumerable.Repeat("wrong-club-player", 11).ToList() };
                return true;
            } };
            var api = new SimulationApi(store, hooks: hooks);
            Assert.ThrowsAsync<ArgumentException>(() => api.AdvanceDaysAsync(1));
            Assert.That(api.Fixtures(new FixtureFilter { IsPlayed = true }), Is.Empty);
            invalid = false;
            await api.AdvanceDaysAsync(1);
            Assert.That(api.Fixtures(new FixtureFilter { IsPlayed = true }), Has.Count.EqualTo(3));
        }

        [Test] public async Task PausedLifePageCanBeRetriedWithoutCommittingPartialPlayerUpdates()
        {
            await new SimulationApi(store).AdvanceDaysAsync(1);
            var blocked = true;
            var hooks = new SimulationHooks { TryContinue = c => c.Stage != SimulationStage.BeforeLife || !blocked };
            var api = new SimulationApi(store, hooks: hooks);
            var stop = await api.AdvanceUntilStopAsync(1);
            Assert.That(stop.Stage, Is.EqualTo(SimulationStage.BeforeLife));
            Assert.That(store.LoadLifePage(null, 1, stop.Day)[0].LastUpdatedDay, Is.LessThan(stop.Day));
            blocked = false;
            await api.AdvanceDaysAsync(1);
            Assert.That(store.LoadLifePage(null, 1, stop.Day), Is.Empty);
        }
    }
}
