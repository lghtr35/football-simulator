using System;
using System.Collections.Generic;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Fixtures
{
    /// <summary>Circle-method rounds; odd squads of teams receive a bye. Second leg reverses venues.
    /// When the season has leftover days, each fixture is shifted by a deterministic jitter so a round
    /// is not forced onto a single date. Team gaps stay at least five days.</summary>
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
            var lastBase = start + (totalRounds - 1) * interval;
            var slack = Math.Min(interval - 5, end - lastBase);

            var result = new List<Fixture>();
            for (var round = 0; round < roundsPerLeg; round++)
            {
                for (var pair = 0; pair < teams.Count / 2; pair++)
                {
                    var home = teams[pair];
                    var away = teams[teams.Count - 1 - pair];
                    if (home == null || away == null) continue;
                    if (round % 2 != 0) { var swap = home; home = away; away = swap; }
                    result.Add(Create(season, round, pair, Day(season, start, interval, slack, round, pair), home, away));
                    var returnRound = round + roundsPerLeg;
                    result.Add(Create(season, returnRound, pair, Day(season, start, interval, slack, returnRound, pair), away, home));
                }
                var last = teams[teams.Count - 1];
                teams.RemoveAt(teams.Count - 1);
                teams.Insert(1, last);
            }
            AnchorRoundsToBaseDay(result, start, interval);
            result.Sort((a, b) => a.ScheduledDay != b.ScheduledDay
                ? a.ScheduledDay.CompareTo(b.ScheduledDay) : StringComparer.Ordinal.Compare(a.Id, b.Id));
            return result;
        }

        private static void AnchorRoundsToBaseDay(List<Fixture> fixtures, int start, int interval)
        {
            if (fixtures.Count == 0) return;
            var groups = new Dictionary<int, List<Fixture>>();
            foreach (var fixture in fixtures)
            {
                if (!groups.TryGetValue(fixture.Matchday, out var group))
                    groups[fixture.Matchday] = group = new List<Fixture>();
                group.Add(fixture);
            }
            foreach (var pair in groups)
            {
                var baseDay = start + (pair.Key - 1) * interval;
                var min = pair.Value[0].ScheduledDay;
                for (var i = 1; i < pair.Value.Count; i++)
                    if (pair.Value[i].ScheduledDay < min) min = pair.Value[i].ScheduledDay;
                var shift = min - baseDay;
                if (shift <= 0) continue;
                foreach (var fixture in pair.Value) fixture.ScheduledDay -= shift;
            }
        }

        private static int Day(LeagueSeason season, int start, int interval, int slack, int round, int pair) =>
            start + round * interval + Jitter(season, round, pair, slack);

        // FNV-1a 32-bit. Deterministic mixer so jitter is stable across processes.
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        private static int Jitter(LeagueSeason season, int round, int pair, int slack)
        {
            if (slack <= 0) return 0;
            unchecked
            {
                var hash = Fold(Fold(FnvOffset, season.LeagueId), season.Id);
                hash = (hash ^ (uint)(round + 1)) * FnvPrime;
                hash = (hash ^ (uint)(pair + 1)) * FnvPrime;
                return (int)(hash % (uint)(slack + 1));
            }
        }

        private static uint Fold(uint hash, string value)
        {
            if (string.IsNullOrEmpty(value)) return hash;
            for (var i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * FnvPrime;
            return hash;
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
