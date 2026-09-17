namespace Football.Simulation.Data
{
    public sealed class Fixture
    {
        public string Id;
        public string LeagueId;
        public string SeasonId;
        public string HomeTeamId;
        public string AwayTeamId;
        public int Matchday;
        /// <summary>Calendar day ordinal from SimulationCalendar.DayFromDate (year 1, Jan 1 = 0).</summary>
        public int ScheduledDay;
        public bool IsPlayed;
        public int HomeGoals;
        public int AwayGoals;
    }
}
