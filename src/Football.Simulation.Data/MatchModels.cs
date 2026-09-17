using System.Collections.Generic;

namespace Football.Simulation.Data
{
    public enum MatchEventType
    {
        Kickoff,
        Goal,
        YellowCard,
        RedCard,
        Substitution,
        Injury,
        FullTime
    }

    /// <summary>A neutral match fact. Presentation decides whether an event needs player intervention.</summary>
    public sealed class MatchEvent
    {
        public int Minute;
        public MatchEventType Type;
        public string TeamId;
        public string PrimaryFootballerId;
        public string SecondaryFootballerId;
    }

    public sealed class MatchState
    {
        public string FixtureId;
        public int CurrentMinute;
        public int HomeGoals;
        public int AwayGoals;
        public bool IsComplete;
        public List<MatchEvent> Events = new List<MatchEvent>();
    }
}
