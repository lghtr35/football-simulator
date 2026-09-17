using System.Collections.Generic;

namespace BeAFootballer.Simulation.Data
{
    public sealed class LeagueState
    {
        public string LeagueId;
        public int CurrentMatchday;
        public List<Fixture> Fixtures = new List<Fixture>();
        public List<LeagueTableEntry> Table = new List<LeagueTableEntry>();
    }

    public sealed class LeagueTableEntry
    {
        public string TeamId;
        public int Played;
        public int Won;
        public int Drawn;
        public int Lost;
        public int GoalsFor;
        public int GoalsAgainst;
        public int Points;
    }
}
