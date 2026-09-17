using System;
using System.Collections.Generic;
using System.Linq;
using Football.Simulation.Data;

namespace Football.Simulation.Match
{
    public static class MatchPreparation
    {
        public static int PositionGroup(PositionType position)
        {
            return PlayerAbility.PositionGroup(position);
        }

        public static MatchInput Prepare(Fixture fixture, TeamSnapshot home, TeamSnapshot away, int worldSeed,
            IReadOnlyList<string> homeRegistration = null, IReadOnlyList<string> awayRegistration = null,
            MatchSquadSelection homeSelection = null, MatchSquadSelection awaySelection = null)
        {
            return new MatchInput { Fixture = fixture, Home = PrepareTeam(home, false, homeRegistration, homeSelection), Away = PrepareTeam(away, true, awayRegistration, awaySelection),
                Seed = FixtureSeed(worldSeed, fixture.Id) };
        }

        public static int FixtureSeed(int worldSeed, string fixtureId)
        {
            unchecked
            {
                uint hash = (uint)worldSeed ^ 2166136261u;
                foreach (var ch in fixtureId) hash = (hash ^ ch) * 16777619;
                return (int)hash;
            }
        }

        private static PreparedTeam PrepareTeam(TeamSnapshot squad, bool away, IReadOnlyList<string> registration, MatchSquadSelection selection)
        {
            if (squad.Players.Count > SquadRules.MaxPlayers || squad.Definition.MatchSquadSize < 19 || squad.Definition.MatchSquadSize > 32)
                throw new ArgumentException("Invalid club roster or match squad size.");
            var result = new PreparedTeam { Squad = squad };
            var available = squad.Players.Where(p => p.State.Form.Availability == AvailabilityStatus.Available)
                .OrderByDescending(PickRating)
                .ThenBy(p => p.Definition.Id, StringComparer.Ordinal).ToList();
            if (selection != null)
            {
                if (selection.Starters == null || selection.Bench == null || selection.Starters.Count != Math.Min(11, available.Count))
                    throw new ArgumentException("Select eleven starters, or every available player when fewer than eleven remain.");
                registration = selection.Starters.Concat(selection.Bench).ToArray();
            }
            if (registration != null)
            {
                if (registration.Count < Math.Min(19, available.Count) || registration.Count > squad.Definition.MatchSquadSize ||
                    registration.Distinct().Count() != registration.Count || registration.Any(id => available.All(p => p.Definition.Id != id)))
                    throw new ArgumentException("Registration must contain unique, available club players within the league limit.");
                available = available.Where(p => registration.Contains(p.Definition.Id)).ToList();
            }
            var slots = new[] { 1, 4, 4, 2 };
            if (selection != null)
            {
                foreach (var id in selection.Starters)
                {
                    var player = available.Single(p => p.Definition.Id == id);
                    var x = new[] { 0.05f, 0.25f, 0.5f, 0.75f }[PositionGroup(player.Definition.Position)];
                    result.Lineup.Add(new PreparedPlayer { Player = player, X = away ? 1 - x : x, Y = (result.Lineup.Count + 1f) / 12 });
                    available.Remove(player);
                }
            }
            else
            for (var group = 0; group < 4; group++)
            {
                for (var i = 0; i < slots[group]; i++)
                {
                    var player = available.FirstOrDefault(p => PositionGroup(p.Definition.Position) == group)
                        ?? available.FirstOrDefault(p => group == 0 || PositionGroup(p.Definition.Position) != 0);
                    if (player == null) break;
                    available.Remove(player);
                    var x = new[] { 0.05f, 0.25f, 0.5f, 0.75f }[group];
                    result.Lineup.Add(new PreparedPlayer { Player = player, X = away ? 1 - x : x, Y = (i + 1f) / (slots[group] + 1) });
                }
            }
            if (selection != null) result.Bench.AddRange(selection.Bench.Select(id => available.Single(p => p.Definition.Id == id)));
            else
            {
            var reserveKeeper = available.FirstOrDefault(p => PositionGroup(p.Definition.Position) == 0);
            if (reserveKeeper != null) { result.Bench.Add(reserveKeeper); available.Remove(reserveKeeper); }
            result.Bench.AddRange(available.Take(Math.Max(0, squad.Definition.MatchSquadSize - result.Lineup.Count - result.Bench.Count)));
            }
            if (result.Lineup.Count == 0) return result;
            double Quality(PreparedPlayer p) => p.Player.Definition.Overall * 0.8 +
                (PositionGroup(p.Player.Definition.Position) == 0 ? p.Player.Definition.Skills.Goalkeeping :
                 PositionGroup(p.Player.Definition.Position) == 1 ? p.Player.Definition.Skills.Tackling :
                 p.Player.Definition.Skills.Accuracy) * 0.2
                + (p.Player.State.Form.Fitness - 75) * 0.08 - p.Player.State.Form.Fatigue * 0.12
                + (p.Player.State.Form.Morale - 50) * 0.04;
            var attack = result.Lineup.Where(p => PositionGroup(p.Player.Definition.Position) >= 2).ToList();
            var defence = result.Lineup.Where(p => PositionGroup(p.Player.Definition.Position) <= 1).ToList();
            var coach = ((squad.Coach?.Ability ?? 50) - 50) * 0.05;
            var form = (squad.State.Form - 50) * 0.08 + (squad.State.Morale - 50) * 0.04;
            var tactic = ((int)squad.Definition.Tactics - 1) * 5;
            result.Attack = (attack.Count > 0 ? attack : result.Lineup).Average(Quality) + coach + form + tactic;
            result.Defence = (defence.Count > 0 ? defence : result.Lineup).Average(Quality) + coach + form - tactic;
            return result;
        }

        public static double PickRating(PlayerSnapshot player) => player.Definition.Overall +
            (player.State.Form.PerformanceForm - 50) * 0.15 + (player.State.Form.Fitness - 100) * 0.2 -
            player.State.Form.Fatigue * 0.35;
    }
}
