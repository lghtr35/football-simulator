using Football.Simulation.Data;
namespace Football.Simulation.Match
{
    public static class MatchSimulation
    {
        public static MatchResult Simulate(TeamDefinition home, TeamDefinition away, int seed)
        {
            var random = new SeededRandom(seed);
            var result = new MatchResult { HomeGoals = GoalsFor(home.Strength, away.Strength, true, random), AwayGoals = GoalsFor(away.Strength, home.Strength, false, random) };
            result.Events.Add(new MatchEvent { Minute = 0, Type = MatchEventType.Kickoff });
            AddGoalEvents(result.Events, result.HomeGoals, home.Id, random);
            AddGoalEvents(result.Events, result.AwayGoals, away.Id, random);
            result.Events.Sort((first, second) => first.Minute.CompareTo(second.Minute));
            result.Events.Add(new MatchEvent { Minute = 90, Type = MatchEventType.FullTime });
            return result;
        }

        private static void AddGoalEvents(System.Collections.Generic.List<MatchEvent> events, int goalCount, string teamId, SeededRandom random)
        {
            for (var goal = 0; goal < goalCount; goal++) events.Add(new MatchEvent { Minute = 1 + random.NextInt(89), Type = MatchEventType.Goal, TeamId = teamId });
        }
        private static int GoalsFor(int attack, int defence, bool isHome, SeededRandom random)
        {
            var expectedGoals = 1.1f + (attack - defence) * 0.025f + (isHome ? 0.2f : 0f); var goals = 0;
            for (var chance = 0; chance < 5; chance++) if (random.Roll(expectedGoals / 5f)) goals++;
            return goals;
        }
    }
}
