using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Football.Simulation.Core;
using Football.Simulation.Data;
using Football.Simulation.Fixtures;
using Football.Simulation.Match;
using Football.Simulation.Life;
using Football.Simulation.Career;

namespace Football.Simulation.Api
{
    /// <summary>Shared Unity/console entry point. Inject a platform-compatible store; use one instance per save.</summary>
    public sealed class SimulationApi
    {
        private readonly IPlaythroughStore store;
        private readonly SimulationEngine engine;

        public SimulationApi(IPlaythroughStore store, SimulationOptions options = null,
            IMatchSimulationStrategy match = null, IDailyLifeStrategy life = null, ISeasonCompletionStrategy completion = null,
            IPlayerDevelopmentStrategy development = null, ITransferStrategy transfers = null, SimulationHooks hooks = null,
            SeasonFixtureGenerator fixtures = null)
        {
            this.store = store;
            engine = new SimulationEngine(store, options, match, life, completion, development, transfers, hooks, fixtures);
        }

        public static SimulationApi Create(IPlaythroughStore store, PlaythroughGenerationConfiguration config,
            SimulationOptions options = null, SeasonFixtureGenerator fixtures = null, SimulationHooks hooks = null)
        {
            (options ?? new SimulationOptions()).Resolve(WorkloadMode.AdvanceDays, System.Environment.ProcessorCount);
            CampaignFactory.Create(store, config, fixtures);
            return new SimulationApi(store, options, hooks: hooks, fixtures: fixtures);
        }

        public SimulationDate CurrentDate => engine.CurrentDate;
        public SimulationEndReason? EndReason => engine.EndReason;
        public Task<SimulationCheckpoint> AdvanceUntilStopAsync(int maxDays, CancellationToken token = default) => engine.AdvanceUntilStopAsync(maxDays, token);
        public Task AdvanceDaysAsync(int days, CancellationToken token = default) => engine.AdvanceDaysAsync(days, token);
        public Task<Fixture> AdvanceUntilNextFixtureAsync(string teamId, CancellationToken token = default) =>
            engine.AdvanceUntilNextFixtureAsync(teamId, token);
        public Task<SeasonSummary> RunSeasonAsync(string seasonId, CancellationToken token = default) => engine.RunSeasonAsync(seasonId, token);
        public Task<int> SimulateBackgroundAsync(string interactiveFixtureId, CancellationToken token = default) =>
            engine.SimulateBackgroundAsync(interactiveFixtureId, token);
        public Task<MatchInput> PrepareInteractiveAsync(string fixtureId, CancellationToken token = default) =>
            engine.PrepareInteractiveAsync(fixtureId, token);
        public Task CompleteInteractiveAsync(MatchResult result, CancellationToken token = default) =>
            engine.CompleteInteractiveAsync(result, token);
        public List<Fixture> Fixtures(FixtureFilter filter) => store.QueryFixtures(filter);
        public List<LeagueTableEntry> Standings(string seasonId) => store.LoadStandings(seasonId);
        public List<SeasonSummary> History() => store.LoadSeasonHistory();
        public List<MatchEvent> MatchEvents(string fixtureId) => store.LoadMatchEvents(fixtureId);
        public List<FootballerMatchPerformance> MatchPerformances(string fixtureId) => store.LoadMatchPerformances(fixtureId);
        public List<CoachDefinition> Coaches(bool unemployedOnly = false) => store.LoadCoaches(unemployedOnly);
        public long TeamBalance(string teamId) => store.LoadTeamBalance(teamId);
        public void SetTraining(string footballerId, int intensity) => ((IWorldEvolutionStore)store).SetTraining(footballerId, intensity);
        public MarketSnapshot Market() => ((IWorldEvolutionStore)store).LoadMarket();
        public List<WorldHistoryEntry> WorldHistory(int fromDay, int toDay, string personId = null, int limit = 100) =>
            ((IWorldEvolutionStore)store).LoadWorldHistory(fromDay, toDay, personId, limit);
    }
}
