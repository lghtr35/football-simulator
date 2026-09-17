using System;
using System.Collections.Generic;

namespace BeAFootballer.Simulation.Data
{
    // Simulation boundaries, not game activities. Games decide what an interruption means.
    public enum SimulationStage { BeforeDay, BeforeLife, BeforeDevelopment, BeforeMarket, BeforeMatch, AfterSeasonCompletion }

    public sealed class SimulationCheckpoint
    {
        public SimulationStage Stage;
        public int Day;
        public Fixture Fixture;
        public TeamSnapshot Home;
        public TeamSnapshot Away;
        public FootballerState LifeState;
        public DevelopmentPlayer Development;
        public MarketSnapshot Market;
        public SeasonSummary CompletedSeason;
        // Optional inputs supplied by the host, never generated or saved as game decisions here.
        public MatchSquadSelection HomeSelection;
        public MatchSquadSelection AwaySelection;
    }

    public sealed class SimulationHooks
    {
        // Actor ownership and persistence belong to the game. Missing callbacks mean autonomous simulation.
        public Func<PersonKind, string, bool> IsActor;
        // False stops before applying this simulation step. On a retry the game may supply inputs and return true.
        // Callbacks may repeat after cancellation/reload; the game owns decision IDs and idempotency.
        public Func<SimulationCheckpoint, bool> TryContinue;
    }

    public sealed class SimulationPausedException : InvalidOperationException
    {
        public SimulationCheckpoint Checkpoint { get; }
        public SimulationPausedException(SimulationCheckpoint checkpoint) : base("Host input required before " + checkpoint.Stage)
        { Checkpoint = checkpoint; }
    }

    public sealed class MatchSquadSelection
    {
        public List<string> Starters = new List<string>();
        public List<string> Bench = new List<string>();
    }
}
