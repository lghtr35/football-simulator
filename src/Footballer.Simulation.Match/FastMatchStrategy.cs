using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Match
{
    public sealed class FastMatchStrategy : IAsyncMatchSimulationStrategy
    {
        public string Id => "fast-v1";
        public SimulationMode Mode => SimulationMode.Discrete;
        public bool CalculatePlayerRatings { get; }

        public FastMatchStrategy(bool calculatePlayerRatings = false)
        {
            CalculatePlayerRatings = calculatePlayerRatings;
        }

        public Task<MatchResult> SimulateAsync(MatchInput input, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => Simulate(input, cancellationToken), cancellationToken);
        }

        public MatchResult Simulate(MatchInput input, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var random = new SeededRandom(input.Seed);
            var result = new MatchResult { FixtureId = input.Fixture.Id };
            result.Events.Add(new MatchEvent { Type = MatchEventType.Kickoff });
            if (input.Home.Lineup.Count < 7 || input.Away.Lineup.Count < 7)
            {
                result.Forfeit = true;
                result.HomeGoals = input.Home.Lineup.Count >= 7 ? 3 : 0;
                result.AwayGoals = input.Away.Lineup.Count >= 7 ? 3 : 0;
                result.Events.Add(new MatchEvent { Type = MatchEventType.FullTime, Minute = 90 });
                return result;
            }
            result.HomeExpectedGoals = Expected(input.Home.Attack, input.Away.Defence,
                0.15 + input.Home.Squad.Definition.FanSupport * 0.003);
            result.AwayExpectedGoals = Expected(input.Away.Attack, input.Home.Defence, 0);
            var home = CreatePlayers(input.Home);
            var away = CreatePlayers(input.Away);
            var registered = input.Home.RegisteredPlayers.Concat(input.Away.RegisteredPlayers).ToDictionary(p => p.Definition.Id);
            var scoringWeights = registered.Values.ToDictionary(p => p.Definition.Id,
                p => MatchPreparation.PositionGroup(p.Definition.Position) == 0 ? 0 :
                    (MatchPreparation.PositionGroup(p.Definition.Position) + 1) * Math.Max(1, p.Definition.Skills.Accuracy));
            var homeBench = input.Home.Bench.ToList();
            var awayBench = input.Away.Bench.ToList();
            result.Players.AddRange(home);
            result.Players.AddRange(away);
            for (var minute = 1; minute <= 90; minute++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlayMinute(home, result.HomeExpectedGoals, true, minute, random, result, scoringWeights);
                PlayMinute(away, result.AwayExpectedGoals, false, minute, random, result, scoringWeights);
                Substitute(home, homeBench, input.Home, minute, result, registered);
                Substitute(away, awayBench, input.Away, minute, result, registered);
            }
            if (CalculatePlayerRatings)
                foreach (var player in result.Players)
                    player.Rating = Math.Max(1, Math.Min(100, 60 + player.Goals * 12 + player.Assists * 6 - player.RedCards * 20 - player.YellowCards * 3));
            result.Events.Add(new MatchEvent { Type = MatchEventType.FullTime, Minute = 90 });
            return result;
        }

        private static double Expected(double attack, double defence, double homeBonus) =>
            Math.Max(0.15, Math.Min(4.5, 1.15 * Math.Exp((attack - defence) / 35.0) + homeBonus));

        private static List<PlayerMatchOutcome> CreatePlayers(PreparedTeam team) =>
            team.Lineup.Select(p => new PlayerMatchOutcome { FootballerId = p.Player.Definition.Id,
                TeamId = team.Squad.Definition.Id, Started = true }).ToList();

        private static void Substitute(List<PlayerMatchOutcome> active, List<PlayerSnapshot> bench,
            PreparedTeam team, int minute, MatchResult result, Dictionary<string, PlayerSnapshot> registered)
        {
            if (minute == 90 || bench.Count == 0 || team.Bench.Count - bench.Count >= team.Squad.Definition.MaxSubstitutions) return;
            var outgoing = active.FirstOrDefault(p => p.InjuryDays > 0 && p.RedCards == 0);
            if (outgoing == null && (minute == 60 || minute == 70 || minute == 80))
                outgoing = active.Where(p => p.RedCards == 0 && MatchPreparation.PositionGroup(registered[p.FootballerId].Definition.Position) != 0)
                    .OrderByDescending(p => registered[p.FootballerId].State.Form.Fatigue + p.Minutes).ThenBy(p => p.FootballerId, StringComparer.Ordinal).FirstOrDefault();
            if (outgoing == null) return;
            var group = MatchPreparation.PositionGroup(registered[outgoing.FootballerId].Definition.Position);
            var incoming = bench.Where(p => MatchPreparation.PositionGroup(p.Definition.Position) == group)
                .OrderByDescending(MatchPreparation.PickRating).FirstOrDefault()
                ?? bench.FirstOrDefault(p => group == 0 || MatchPreparation.PositionGroup(p.Definition.Position) != 0);
            if (incoming == null) return;
            bench.Remove(incoming);
            active.Remove(outgoing);
            var outcome = new PlayerMatchOutcome { FootballerId = incoming.Definition.Id, TeamId = team.Squad.Definition.Id };
            active.Add(outcome);
            result.Players.Add(outcome);
            result.Events.Add(new MatchEvent { Type = MatchEventType.Substitution, Minute = minute,
                TeamId = outcome.TeamId, PrimaryFootballerId = outgoing.FootballerId, SecondaryFootballerId = outcome.FootballerId });
        }

        private static void PlayMinute(List<PlayerMatchOutcome> players, double expected, bool home, int minute,
            SeededRandom random, MatchResult result, Dictionary<string, int> scoringWeights)
        {
            var active = players.Where(p => p.RedCards == 0 && p.InjuryDays == 0).ToList();
            if (active.Count == 0) return;
            foreach (var player in active) player.Minutes++;
            var subject = active[random.NextInt(active.Count)];
            if (random.Roll(0.018f))
            {
                subject.YellowCards++;
                result.Events.Add(Event(subject, minute, MatchEventType.YellowCard));
                if (subject.YellowCards == 2)
                {
                    subject.RedCards = 1;
                    result.Events.Add(Event(subject, minute, MatchEventType.RedCard));
                }
            }
            if (subject.RedCards == 0 && random.Roll(0.0015f))
            {
                subject.InjuryDays = 2 + random.NextInt(20);
                result.Events.Add(Event(subject, minute, MatchEventType.Injury));
            }
            active = players.Where(p => p.RedCards == 0 && p.InjuryDays == 0).ToList();
            if (active.Count == 0 || !random.Roll((float)(expected / 90 * active.Count / 11))) return;
            var totalWeight = active.Sum(p => scoringWeights[p.FootballerId]);
            if (totalWeight == 0) return;
            var roll = random.NextInt(totalWeight);
            var scorer = active[0];
            foreach (var player in active)
            {
                roll -= scoringWeights[player.FootballerId];
                if (roll < 0) { scorer = player; break; }
            }
            scorer.Goals++;
            if (home) result.HomeGoals++; else result.AwayGoals++;
            var goal = Event(scorer, minute, MatchEventType.Goal);
            if (active.Count > 1 && random.Roll(0.7f))
            {
                var assistants = active.Where(p => p != scorer).ToList();
                var assistant = assistants[random.NextInt(assistants.Count)];
                assistant.Assists++;
                goal.SecondaryFootballerId = assistant.FootballerId;
            }
            result.Events.Add(goal);
        }

        private static MatchEvent Event(PlayerMatchOutcome player, int minute, MatchEventType type) =>
            new MatchEvent { Minute = minute, Type = type, TeamId = player.TeamId, PrimaryFootballerId = player.FootballerId };
    }
}
