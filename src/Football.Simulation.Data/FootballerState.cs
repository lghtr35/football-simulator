namespace Football.Simulation.Data
{
    public sealed class FootballerState
    {
        public string FootballerId;
        public int LastUpdatedDay;
        public FootballerStats Stats;
        public FootballerForm Form;
    }


    public sealed class FootballerStats
    {
        public int Appearances;
        public int Starts;
        public int MinutesPlayed;
        public int Goals;
        public int Assists;
        public int CleanSheets;
        public int Interceptions;
        public int FoulsCommitted;
        public int BallsLost;
        public int Passes;
        public int PassesCompleted;
        public int YellowCards;
        public int RedCards;
    }

    public sealed class FootballerForm {
        public int Morale = 50;
        public int Fitness = 100;
        public int Fatigue;
        public int RecentMatchRating;
        public int PerformanceForm = 50;
        public AvailabilityStatus Availability = AvailabilityStatus.Available;
        public int InjuryDaysRemaining;
        public int SuspensionMatchesRemaining;
    }

}
