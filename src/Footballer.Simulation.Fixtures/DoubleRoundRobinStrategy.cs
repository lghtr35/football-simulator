using System;
using System.Collections.Generic;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Fixtures
{
    /// <summary>Circle-method rounds; odd squads of teams receive a bye. Second leg reverses venues.</summary>
    public sealed class DoubleRoundRobinStrategy : ISeasonFixtureStrategy
    {
        public string Id => "double-round-robin";

        public IReadOnlyList<Fixture> Generate(LeagueSeason season)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            if (string.IsNullOrWhiteSpace(season.Id) || string.IsNullOrWhiteSpace(season.LeagueId))
                throw new ArgumentException("Season and league IDs are required.");
            if (season.TeamIds == null || season.TeamIds.Count < 2)
                throw new ArgumentException("A league season needs at least two teams.");
            var teams = new List<string>(season.TeamIds);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var team in teams)
                if (string.IsNullOrWhiteSpace(team) || !unique.Add(team))
                    throw new ArgumentException("Team IDs must be nonempty and unique.");
            // Stable output regardless of database retrieval order.
            teams.Sort(StringComparer.Ordinal);
            if (teams.Count % 2 != 0) teams.Add(null);
            var start = SimulationCalendar.DayFromDate(season.StartDate);
            var end = SimulationCalendar.DayFromDate(season.EndDate);
            var roundsPerLeg = teams.Count - 1;
            var totalRounds = roundsPerLeg * 2;
            var interval = Math.Min(7, (end - start) / (totalRounds - 1));
            if (end < start || interval < 5)
                throw new ArgumentException("Season is too short: rounds need at least five days between them.");

            var result = new List<Fixture>();
            for (var round = 0; round < roundsPerLeg; round++)
            {
                for (var pair = 0; pair < teams.Count / 2; pair++)
                {
                    var home = teams[pair];
                    var away = teams[teams.Count - 1 - pair];
                    if (home == null || away == null) continue;
                    if (round % 2 != 0) { var swap = home; home = away; away = swap; }
                    result.Add(Create(season, round, pair, start + round * interval, home, away));
                    var returnRound = round + roundsPerLeg;
                    result.Add(Create(season, returnRound, pair, start + returnRound * interval, away, home));
                }
                var last = teams[teams.Count - 1];
                teams.RemoveAt(teams.Count - 1);
                teams.Insert(1, last);
            }
            result.Sort((a, b) => a.ScheduledDay != b.ScheduledDay
                ? a.ScheduledDay.CompareTo(b.ScheduledDay) : StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        private static Fixture Create(LeagueSeason season, int round, int pair, int day, string home, string away)
        {
            return new Fixture
            {
                Id = season.Id + ":" + (round + 1) + ":" + (pair + 1),
                LeagueId = season.LeagueId, SeasonId = season.Id,
                Matchday = round + 1, ScheduledDay = day,
                HomeTeamId = home, AwayTeamId = away
            };
        }
    }
}
