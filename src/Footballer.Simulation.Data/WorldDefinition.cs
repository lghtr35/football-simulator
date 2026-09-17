using System.Collections.Generic;
namespace BeAFootballer.Simulation.Data
{
    /// <summary>Generator output. Persist its individual records; do not load this full aggregate for normal simulation jobs.</summary>
    public sealed class WorldDefinition
    {
        public int Seed;
        public WorldRules Rules = new WorldRules();
        public List<LeagueDefinition> Leagues = new List<LeagueDefinition>();
        public List<FootballerDefinition> Footballers = new List<FootballerDefinition>();
        public List<CoachDefinition> Coaches = new List<CoachDefinition>();
    }

    public sealed class LeagueDefinition
    {
        public string Id;
        public string Name;
        public int SeasonNumber = 1;
        public int MatchSquadSize = 20;
        public int MaxSubstitutions = 5;
        public int SeasonStartMonth = 9;
        public int SeasonEndMonth = 6;
        public long WinnerPrize = 100000;
        public string FixtureStrategyId = "double-round-robin";
        public List<TeamDefinition> Teams = new List<TeamDefinition>();
    }

    public sealed class TeamDefinition
    {
        public string Id;
        public string LeagueId;
        public string Name;
        public string ShortName;
        public int Strength;
        public int MatchSquadSize = 20;
        public int MaxSubstitutions = 5;
        public int FanSupport = 50;
        public TacticalStyle Tactics;
        public List<string> FootballerIds = new List<string>();
    }
}
