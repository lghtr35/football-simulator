using System.Collections.Generic;

namespace BeAFootballer.Simulation.Data
{
    /// <summary>Authored input used once to create a new persisted playthrough.</summary>
    public sealed class PlaythroughGenerationConfiguration
    {
        public int Seed;
        public int StartYear = 2026;
        public int UnemployedCoaches = 3;
        public int FreeAgentPlayers = 8;
        public WorldRules Rules = new WorldRules();
        public List<LeagueGenerationConfiguration> Leagues = new List<LeagueGenerationConfiguration>();
    }

    public sealed class LeagueGenerationConfiguration
    {
        public string Id;
        public string Name;
        public int SeasonStartMonth = 9;
        public int SeasonEndMonth = 6;
        public long WinnerPrize = 100000;
        public string FixtureStrategyId = "double-round-robin";
        public string SeasonFixtureStrategyId;
        public int MatchSquadSize = 20;
        public int MaxSubstitutions = 5;
        public List<TeamGenerationConfiguration> Teams = new List<TeamGenerationConfiguration>();
    }

    public sealed class TeamGenerationConfiguration
    {
        public string Id;
        public string Name;
        public string ShortName;
        public int FanSupport = 50;
        public TacticalStyle Tactics = TacticalStyle.Balanced;
        public SquadComposition Squad = new SquadComposition();
        public PositionGroupOverallTargets TargetOverall = new PositionGroupOverallTargets();
    }

    public sealed class SquadComposition
    {
        public int Goalkeepers = 2;
        public int Defenders = 8;
        public int Midfielders = 8;
        public int Attackers = 4;
    }

    public sealed class PositionGroupOverallTargets
    {
        public int Goalkeeper = 65;
        public int Defence = 65;
        public int Midfield = 65;
        public int Attack = 65;
        public int AllowedVariance = 5;
    }
}
