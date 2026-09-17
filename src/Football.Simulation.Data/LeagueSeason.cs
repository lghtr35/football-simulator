using System.Collections.Generic;
using Football.Simulation.Data;

namespace Football.Simulation.Data
{
    public sealed class LeagueSeason
    {
        public string Id;
        public string LeagueId;
        public string Name;
        public long WinnerPrize;
        public SimulationDate StartDate;
        public SimulationDate EndDate;
        // Null uses the league default. An explicit value overrides it for this season.
        public string FixtureStrategyId;
        public List<string> TeamIds = new List<string>();
    }
}
