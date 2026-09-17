namespace Football.Simulation.Data
{
    /// <summary>One row in the persisted playthrough metadata table.</summary>
    public sealed class PlaythroughMetadata
    {
        public string Id;
        public int Seed;
        public int SchemaVersion;
        /// <summary>Calendar ordinal of the next day to process (year 1, January 1 = 0).</summary>
        public int CurrentDay;
        public long CreatedUtcTicks;
    }

    /// <summary>One persisted performance record per footballer per completed fixture.</summary>
    public sealed class FootballerMatchPerformance
    {
        public string FixtureId;
        public string FootballerId;
        public int MinutesPlayed;
        public int? Rating;
        public int Goals;
        public int Assists;
        public int YellowCards;
        public int RedCards;
    }
}
