using System;
using System.Collections.Generic;
using System.Linq;

namespace Football.Simulation.Data
{
    public static class WorldGenerator
    {
        public static PlaythroughGenerationConfiguration Example(int seed, int teamCount = 10)
        {
            if (teamCount < 2) throw new ArgumentOutOfRangeException(nameof(teamCount));
            var league = new LeagueGenerationConfiguration { Id = "premier", Name = "National League" };
            for (var i = 1; i <= teamCount; i++)
                league.Teams.Add(new TeamGenerationConfiguration { Id = "team-" + i, Name = "Club " + i, ShortName = "C" + i });
            return new PlaythroughGenerationConfiguration { Seed = seed, Leagues = new List<LeagueGenerationConfiguration> { league } };
        }

        public static WorldDefinition Create(int seed) => Create(Example(seed));

        public static WorldDefinition Create(PlaythroughGenerationConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.StartYear < 1 || config.UnemployedCoaches < 0 || config.UnemployedCoaches > 1000 ||
                config.Leagues == null || config.Leagues.Count == 0) throw new ArgumentException("Invalid world configuration.");
            if (config.Rules == null || config.FreeAgentPlayers < 0 || config.FreeAgentPlayers > 10000) throw new ArgumentException("Invalid world rules.");
            config.Rules.Validate();
            var world = new WorldDefinition { Seed = config.Seed, Rules = config.Rules };
            var random = new SeededRandom(config.Seed);
            var leagueIds = new HashSet<string>(StringComparer.Ordinal);
            var teamIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in config.Leagues.OrderBy(l => l.Id, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(source.Id) || !leagueIds.Add(source.Id) || source.Teams.Count < 2 ||
                    source.SeasonStartMonth < 1 || source.SeasonStartMonth > 12 ||
                    source.SeasonEndMonth < 1 || source.SeasonEndMonth > 12 || source.WinnerPrize < 0 ||
                    source.MatchSquadSize < 19 || source.MatchSquadSize > SquadRules.MaxPlayers || source.MaxSubstitutions < 0 || source.MaxSubstitutions > 5)
                    throw new ArgumentException("Invalid league configuration.");
                var league = new LeagueDefinition
                {
                    Id = source.Id, Name = source.Name ?? source.Id, SeasonStartMonth = source.SeasonStartMonth,
                    SeasonEndMonth = source.SeasonEndMonth, WinnerPrize = source.WinnerPrize,
                    FixtureStrategyId = source.FixtureStrategyId, MatchSquadSize = source.MatchSquadSize, MaxSubstitutions = source.MaxSubstitutions
                };
                foreach (var input in source.Teams.OrderBy(t => t.Id, StringComparer.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(input.Id) || !teamIds.Add(input.Id))
                        throw new ArgumentException("Team IDs must be unique across the world.");
                    var counts = new[] { input.Squad.Goalkeepers, input.Squad.Defenders, input.Squad.Midfielders, input.Squad.Attackers };
                    var targets = new[] { input.TargetOverall.Goalkeeper, input.TargetOverall.Defence, input.TargetOverall.Midfield, input.TargetOverall.Attack };
                    if (counts[0] < 1 || counts[1] < 4 || counts[2] < 4 || counts[3] < 2 || counts.Sum() > SquadRules.MaxPlayers ||
                        targets.Any(v => v < 1 || v > 100) || input.TargetOverall.AllowedVariance < 0 ||
                        input.TargetOverall.AllowedVariance > 30 || input.FanSupport < 0 || input.FanSupport > 100 ||
                        !Enum.IsDefined(typeof(TacticalStyle), input.Tactics))
                        throw new ArgumentException("Squads need at least 1 GK, 4 defenders, 4 midfielders and 2 attackers; ratings are 1–100.");
                    var team = new TeamDefinition { Id = input.Id, LeagueId = league.Id, Name = input.Name ?? input.Id,
                        ShortName = input.ShortName ?? input.Id, Strength = (int)targets.Average(), FanSupport = input.FanSupport, Tactics = input.Tactics,
                        MatchSquadSize = source.MatchSquadSize, MaxSubstitutions = source.MaxSubstitutions };
                    var positions = new[] {
                        new[] { PositionType.Goalkeeper },
                        new[] { PositionType.RightBack, PositionType.CentreBack, PositionType.CentreBack, PositionType.LeftBack },
                        new[] { PositionType.RightMid, PositionType.Mid, PositionType.DefensiveMid, PositionType.LeftMid },
                        new[] { PositionType.Striker, PositionType.CenterForward }
                    };
                    for (var group = 0; group < counts.Length; group++)
                    {
                        // Paired deviations keep each group's average overall exactly on target.
                        var deviation = 0;
                        for (var i = 0; i < counts[group]; i++)
                        {
                            var variance = Math.Min(input.TargetOverall.AllowedVariance, Math.Min(targets[group] - 1, 100 - targets[group]));
                            if (i % 2 == 0) deviation = i == counts[group] - 1 ? 0 : random.NextInt(variance * 2 + 1) - variance;
                            else deviation = -deviation;
                            var overall = targets[group] + deviation;
                            var player = CreatePlayer(team, team.FootballerIds.Count + 1, positions[group][i % positions[group].Length], overall, random);
                            world.Footballers.Add(player);
                            team.FootballerIds.Add(player.Id);
                        }
                    }
                    league.Teams.Add(team);
                    world.Coaches.Add(new CoachDefinition { Id = "coach:" + team.Id, Name = "Coach " + team.Name,
                        TeamId = team.Id, Ability = 50 + random.NextInt(41), PreferredTactics = team.Tactics });
                }
                world.Leagues.Add(league);
            }
            for (var i = 0; i < config.UnemployedCoaches; i++)
                world.Coaches.Add(new CoachDefinition { Id = "unemployed:" + i, Name = "Free Coach " + (i + 1),
                    Ability = 40 + random.NextInt(51), PreferredTactics = (TacticalStyle)random.NextInt(3) });
            for (var i = 0; i < config.FreeAgentPlayers; i++)
            {
                var position = new[] { PositionType.Goalkeeper, PositionType.CentreBack, PositionType.Mid, PositionType.Striker }[i % 4];
                var player = CreatePlayer(new TeamDefinition { Id = "free", Name = "Free Agent" }, i + 1, position, 45 + random.NextInt(36), random);
                player.Id = "free-agent:" + i;
                player.TeamId = null;
                if (world.Footballers.Any(p => p.Id == player.Id)) throw new ArgumentException("Reserved free-agent ID collision.");
                world.Footballers.Add(player);
            }
            return world;
        }

        public static WorldState CreateInitialState(WorldDefinition world, int day = 0)
        {
            var state = new WorldState { CurrentDay = day };
            foreach (var player in world.Footballers)
                state.Footballers.Add(new FootballerState { FootballerId = player.Id, LastUpdatedDay = day,
                    Stats = new FootballerStats(), Form = new FootballerForm() });
            foreach (var team in world.Leagues.SelectMany(l => l.Teams))
                state.Teams.Add(new TeamState { TeamId = team.Id });
            return state;
        }

        private static FootballerDefinition CreatePlayer(TeamDefinition team, int number, PositionType position, int overall, SeededRandom random)
        {
            int Skill() => Math.Max(1, Math.Min(100, overall + random.NextInt(15) - 7));
            var player = new FootballerDefinition
            {
                Id = team.Id + "-player-" + number, Name = team.Name + " Player " + number,
                TeamId = team.Id, Position = position, Overall = overall,
                PreferredFoot = random.Roll(0.2f) ? PreferredFoot.Left : PreferredFoot.Right,
                Attributes = new FootballerAttributes { Age = 18 + random.NextInt(18), HeightCentimetres = 165 + random.NextInt(31),
                    WeightKilograms = 60 + random.NextInt(31), Nationality = "Fictional" },
                Skills = new FootballerSkills { Speed = Skill(), Acceleration = Skill(), Stamina = Skill(), StaminaRegen = Skill(),
                    Dribbling = Skill(), FirstTouchControl = Skill(), HitPower = Skill(), Accuracy = Skill(),
                    Tackling = Skill(), Strength = Skill(), Goalkeeping = position == PositionType.Goalkeeper ? Skill() : 10 }
            };
            // Keep authored group targets exact while retaining variation in secondary skills.
            var s = player.Skills;
            switch (PlayerAbility.PositionGroup(position))
            {
                case 0: s.Goalkeeping = s.Strength = overall; break;
                case 1: s.Tackling = s.Strength = s.Speed = overall; break;
                case 2: s.FirstTouchControl = s.Accuracy = s.Stamina = overall; break;
                default: s.Accuracy = s.Dribbling = s.Speed = overall; break;
            }
            return player;
        }
    }
}
