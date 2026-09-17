namespace Football.Simulation.Career
{
    public sealed class CareerState
    {
        public string FootballerId;
        public string TeamId;
        public int CurrentDay;
        public int Overall;
        public int Fitness = 100;
        public int Morale = 50;
        public int Form = 50;
        public int Injury = 0;
        public int Suspension = 0;
        public int RedCard = 0;
        public int YellowCard = 0;
    }
}
