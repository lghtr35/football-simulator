using System;
using System.Collections.Generic;
using System.Linq;
using BeAFootballer.Simulation.Data;
using Microsoft.Data.Sqlite;

namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        public List<CoachDefinition> LoadCoaches(bool unemployedOnly = false)
        {
            var result = new List<CoachDefinition>();
            using (var c = OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = "SELECT Id,Name,TeamId,Ability,Tactics FROM Coaches " +
                    (unemployedOnly ? "WHERE TeamId IS NULL " : "") + "ORDER BY Id;";
                using (var r = command.ExecuteReader())
                    while (r.Read()) result.Add(new CoachDefinition { Id = r.GetString(0), Name = r.GetString(1),
                        TeamId = r.IsDBNull(2) ? null : r.GetString(2), Ability = r.GetInt32(3), PreferredTactics = (TacticalStyle)r.GetInt32(4) });
            }
            return result;
        }

        public PlaythroughMetadata LoadMetadata()
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id,Seed,SchemaVersion,CurrentDay,CreatedUtcTicks FROM PlaythroughMetadata;";
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) throw new InvalidOperationException("No playthrough in this database.");
                    return new PlaythroughMetadata { Id = reader.GetString(0), Seed = reader.GetInt32(1), SchemaVersion = reader.GetInt32(2),
                        CurrentDay = reader.GetInt32(3), CreatedUtcTicks = reader.GetInt64(4) };
                }
            }
        }

        public List<Fixture> QueryFixtures(FixtureFilter filter)
        {
            if (filter == null || filter.Limit < 1 || filter.Limit > 1000 || filter.Offset < 0)
                throw new ArgumentException("Fixture query page size must be 1–1000 and offset nonnegative.");
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                var conditions = new List<string>();
                void Where(string sql, string key, object value) { conditions.Add(sql); command.Parameters.AddWithValue(key, value); }
                if (filter.TeamId != null) Where("(f.HomeTeamId=$team OR f.AwayTeamId=$team)", "$team", filter.TeamId);
                if (filter.LeagueId != null) Where("f.LeagueId=$league", "$league", filter.LeagueId);
                if (filter.SeasonId != null) Where("s.SeasonId=$season", "$season", filter.SeasonId);
                if (filter.FromDay.HasValue) Where("f.ScheduledDay >= $from", "$from", filter.FromDay.Value);
                if (filter.ToDay.HasValue) Where("f.ScheduledDay <= $to", "$to", filter.ToDay.Value);
                if (filter.IsPlayed.HasValue) Where("f.IsPlayed=$played", "$played", filter.IsPlayed.Value ? 1 : 0);
                if (filter.ExcludeFixtureId != null) Where("f.Id<>$exclude", "$exclude", filter.ExcludeFixtureId);
                command.CommandText = @"SELECT f.Id,f.LeagueId,s.SeasonId,f.HomeTeamId,f.AwayTeamId,
f.Matchday,f.ScheduledDay,f.IsPlayed,f.HomeGoals,f.AwayGoals
FROM Fixtures f JOIN SeasonFixtures s ON s.FixtureId=f.Id " +
                    (conditions.Count == 0 ? "" : "WHERE " + string.Join(" AND ", conditions)) +
                    " ORDER BY f.ScheduledDay,f.Id LIMIT $limit OFFSET $offset;";
                command.Parameters.AddWithValue("$limit", filter.Limit);
                command.Parameters.AddWithValue("$offset", filter.Offset);
                var result = new List<Fixture>();
                using (var r = command.ExecuteReader())
                    while (r.Read()) result.Add(new Fixture { Id = r.GetString(0), LeagueId = r.GetString(1), SeasonId = r.GetString(2),
                        HomeTeamId = r.GetString(3), AwayTeamId = r.GetString(4), Matchday = r.GetInt32(5),
                        ScheduledDay = r.GetInt32(6), IsPlayed = r.GetInt32(7) != 0, HomeGoals = r.GetInt32(8), AwayGoals = r.GetInt32(9) });
                return result;
            }
        }

        /// <summary>Three set-based queries for a bounded batch, in a consistent read transaction.</summary>
        public Dictionary<string, TeamSnapshot> LoadTeams(IReadOnlyList<string> teamIds)
        {
            if (teamIds == null || teamIds.Count == 0 || teamIds.Count > 200)
                throw new ArgumentException("Load 1–200 team IDs per batch.");
            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction(deferred: true))
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                var names = teamIds.Distinct().Select((id, i) => { var key = "$t" + i; command.Parameters.AddWithValue(key, id); return key; }).ToArray();
                var ids = string.Join(",", names);
                command.CommandText = @"SELECT t.Id,t.LeagueId,t.Name,t.ShortName,t.Strength,
COALESCE(s.Morale,50),COALESCE(s.Form,50),COALESCE(s.FanSupport,50),COALESCE(s.Tactics,1),COALESCE(l.MatchSquadSize,20),COALESCE(l.MaxSubstitutions,5)
FROM Teams t LEFT JOIN TeamRuntime s ON s.TeamId=t.Id LEFT JOIN LeagueSquadRules l ON l.LeagueId=t.LeagueId WHERE t.Id IN (" + ids + ");";
                var teams = new Dictionary<string, TeamSnapshot>(StringComparer.Ordinal);
                using (var r = command.ExecuteReader())
                    while (r.Read())
                    {
                        var id = r.GetString(0);
                        teams.Add(id, new TeamSnapshot { Definition = new TeamDefinition { Id = id, LeagueId = r.GetString(1),
                            Name = r.GetString(2), ShortName = r.GetString(3), Strength = r.GetInt32(4),
                            FanSupport = r.GetInt32(7), Tactics = (TacticalStyle)r.GetInt32(8), MatchSquadSize = r.GetInt32(9), MaxSubstitutions = r.GetInt32(10) },
                            State = new TeamState { TeamId = id, Morale = r.GetInt32(5), Form = r.GetInt32(6) } });
                    }
                if (teams.Count != names.Length) throw new InvalidOperationException("Missing team.");
                command.CommandText = "SELECT Id,Name,TeamId,Ability,Tactics FROM Coaches WHERE TeamId IN (" + ids + ");";
                using (var r = command.ExecuteReader())
                    while (r.Read()) teams[r.GetString(2)].Coach = new CoachDefinition { Id = r.GetString(0), Name = r.GetString(1),
                        TeamId = r.GetString(2), Ability = r.GetInt32(3), PreferredTactics = (TacticalStyle)r.GetInt32(4) };
                command.CommandText = @"SELECT p.Id,p.Name,p.TeamId,p.Position,p.PreferredFoot,p.Age,p.HeightCentimetres,
p.WeightKilograms,p.Nationality,p.Speed,p.Acceleration,p.Stamina,p.StaminaRegen,p.Dribbling,p.FirstTouchControl,
p.HitPower,p.Accuracy,p.Tackling,p.Strength,COALESCE(q.Overall,65),COALESCE(q.Goalkeeping,50),
s.LastUpdatedDay,s.Morale,s.Fitness,s.Fatigue,s.RecentMatchRating,s.Availability,s.InjuryDaysRemaining,s.SuspensionMatchesRemaining,
COALESCE(x.Appearances,0),COALESCE(x.Minutes,0),COALESCE(x.Goals,0),COALESCE(x.Assists,0),COALESCE(x.YellowCards,0),COALESCE(x.RedCards,0),COALESCE(d.Form,50),COALESCE(x.Starts,0),
COALESCE(d.BirthDay,0),(SELECT CurrentDay FROM PlaythroughMetadata)
FROM Footballers p JOIN FootballerState s ON s.FootballerId=p.Id
LEFT JOIN PlayerRatings q ON q.FootballerId=p.Id LEFT JOIN PlayerTotals x ON x.FootballerId=p.Id
LEFT JOIN DevelopmentData d ON d.FootballerId=p.Id
WHERE p.TeamId IN (" + ids + ") ORDER BY p.Id;";
                using (var r = command.ExecuteReader())
                    while (r.Read())
                    {
                        var definition = ReadFootballer(r);
                        ApplyDerivedAbility(definition, r.GetInt32(20), r.GetInt32(37), r.GetInt32(38));
                        teams[definition.TeamId].Players.Add(new PlayerSnapshot { Definition = definition,
                            State = new FootballerState { FootballerId = definition.Id, LastUpdatedDay = r.GetInt32(21),
                                Form = new FootballerForm { Morale = r.GetInt32(22), Fitness = r.GetInt32(23), Fatigue = r.GetInt32(24),
                                    RecentMatchRating = r.GetInt32(25), PerformanceForm = r.GetInt32(35), Availability = (AvailabilityStatus)r.GetInt32(26),
                                    InjuryDaysRemaining = r.GetInt32(27), SuspensionMatchesRemaining = r.GetInt32(28) },
                                Stats = new FootballerStats { Appearances = r.GetInt32(29), Starts = r.GetInt32(36), MinutesPlayed = r.GetInt32(30),
                                    Goals = r.GetInt32(31), Assists = r.GetInt32(32), YellowCards = r.GetInt32(33), RedCards = r.GetInt32(34) } } });
                        teams[definition.TeamId].Definition.FootballerIds.Add(definition.Id);
                    }
                transaction.Commit();
                return teams;
            }
        }

        public List<LeagueTableEntry> LoadStandings(string seasonId)
        {
            using (var c = OpenConnection()) return ReadStandings(c.Connection, null, seasonId);
        }

        private static List<LeagueTableEntry> ReadStandings(SqliteConnection c, SqliteTransaction transaction, string id)
        {
            using (var command = c.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"SELECT TeamId,Played,Won,Drawn,Lost,GoalsFor,GoalsAgainst,Points FROM SeasonStandings
WHERE SeasonId=$id ORDER BY Points DESC,(GoalsFor-GoalsAgainst) DESC,GoalsFor DESC,TeamId;";
                command.Parameters.AddWithValue("$id", id);
                var result = new List<LeagueTableEntry>();
                using (var r = command.ExecuteReader())
                    while (r.Read()) result.Add(new LeagueTableEntry { TeamId = r.GetString(0), Played = r.GetInt32(1),
                        Won = r.GetInt32(2), Drawn = r.GetInt32(3), Lost = r.GetInt32(4), GoalsFor = r.GetInt32(5),
                        GoalsAgainst = r.GetInt32(6), Points = r.GetInt32(7) });
                return result;
            }
        }

        public List<LeagueSeason> LoadUnfinishedSeasons()
        {
            var result = new List<LeagueSeason>();
            using (var c = OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = @"SELECT s.Id,s.LeagueId,s.Name,s.StartDay,s.EndDay,s.StrategyId,COALESCE(p.Prize,0)
FROM LeagueSeasons s LEFT JOIN SeasonPrizes p ON p.SeasonId=s.Id
WHERE NOT EXISTS(SELECT 1 FROM SeasonHistory h WHERE h.SeasonId=s.Id) ORDER BY s.Id;";
                using (var r = command.ExecuteReader())
                    while (r.Read()) result.Add(new LeagueSeason { Id = r.GetString(0), LeagueId = r.GetString(1), Name = r.GetString(2),
                        StartDate = SimulationCalendar.DateFromDay(r.GetInt32(3)), EndDate = SimulationCalendar.DateFromDay(r.GetInt32(4)),
                        FixtureStrategyId = r.IsDBNull(5) ? null : r.GetString(5), WinnerPrize = r.GetInt64(6) });
            }
            return result;
        }

        public List<SeasonSummary> LoadSeasonHistory()
        {
            var result = new List<SeasonSummary>();
            using (var c = OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = "SELECT SeasonId,WinnerTeamId,Prize,CompletedDay FROM SeasonHistory ORDER BY CompletedDay,SeasonId;";
                using (var r = command.ExecuteReader())
                    while (r.Read()) result.Add(new SeasonSummary { SeasonId = r.GetString(0), WinnerTeamId = r.GetString(1),
                        Prize = r.GetInt64(2), CompletedDay = r.GetInt32(3) });
            }
            return result;
        }

        public long LoadTeamBalance(string teamId)
        {
            using (var c = OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = "SELECT Balance FROM TeamRuntime WHERE TeamId=$id;";
                command.Parameters.AddWithValue("$id", teamId);
                return Convert.ToInt64(command.ExecuteScalar() ?? throw new ArgumentException("Unknown team."));
            }
        }

        public List<FootballerMatchPerformance> LoadMatchPerformances(string fixtureId)
        {
            var result = new List<FootballerMatchPerformance>();
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT FootballerId,MinutesPlayed,Rating,Goals,Assists,YellowCards,RedCards FROM FootballerMatchPerformances WHERE FixtureId=$id ORDER BY FootballerId;";
                cmd.Parameters.AddWithValue("$id", fixtureId);
                using (var r = cmd.ExecuteReader()) while (r.Read()) result.Add(new FootballerMatchPerformance {
                    FixtureId = fixtureId, FootballerId = r.GetString(0), MinutesPlayed = r.GetInt32(1), Rating = r.IsDBNull(2) ? (int?)null : r.GetInt32(2),
                    Goals = r.GetInt32(3), Assists = r.GetInt32(4), YellowCards = r.GetInt32(5), RedCards = r.GetInt32(6) });
            }
            return result;
        }

        public List<MatchEvent> LoadMatchEvents(string fixtureId)
        {
            var result = new List<MatchEvent>();
            using (var c = OpenConnection())
            using (var command = c.CreateCommand())
            {
                command.CommandText = "SELECT Minute,Type,TeamId,PrimaryFootballerId,SecondaryFootballerId FROM MatchEvents WHERE FixtureId=$id ORDER BY Sequence;";
                command.Parameters.AddWithValue("$id", fixtureId);
                using (var r = command.ExecuteReader())
                    while (r.Read()) result.Add(new MatchEvent { Minute = r.GetInt32(0), Type = (MatchEventType)r.GetInt32(1),
                        TeamId = r.IsDBNull(2) ? null : r.GetString(2), PrimaryFootballerId = r.IsDBNull(3) ? null : r.GetString(3),
                        SecondaryFootballerId = r.IsDBNull(4) ? null : r.GetString(4) });
            }
            return result;
        }
    }
}
