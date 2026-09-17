using System.Collections.Generic;

namespace BeAFootballer.Simulation.Data
{
    /// <summary>Mutable state saved as the simulated world advances.</summary>
    public sealed class WorldState
    {
        /// <summary>Calendar ordinal, on the same timeline as fixture ScheduledDay.</summary>
        public int CurrentDay;
        public List<FootballerState> Footballers = new List<FootballerState>();
        public List<TeamState> Teams = new List<TeamState>();
    }

    public sealed class TeamState
    {
        public string TeamId;
        public int Morale = 50;
        public int Form = 50;
    }
}
