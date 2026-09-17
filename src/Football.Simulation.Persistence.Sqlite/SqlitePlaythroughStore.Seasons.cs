using System;
using System.Collections.Generic;
using System.Linq;
using Football.Simulation.Data;
using Football.Simulation.Fixtures;
using Microsoft.Data.Sqlite;

namespace Football.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        /// <summary>Creates a season and its schedule atomically. Existing seasons are never overwritten.</summary>
        public void SaveSeason(LeagueSeason season, IReadOnlyList<Fixture> fixtures)
        {
            ValidateSeason(season, fixtures);
            Initialise();
            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                InsertSeason(connection, transaction, season, fixtures);
                transaction.Commit();
            }
        }

        private static void ValidateSeason(LeagueSeason season, IReadOnlyList<Fixture> fixtures)
        {
            if (season == null || fixtures == null) throw new ArgumentNullException();
            var start = SimulationCalendar.DayFromDate(season.StartDate);
            var end = SimulationCalendar.DayFromDate(season.EndDate);
            if (end < start) throw new ArgumentException("Season ends before it starts.");
            var teams = new HashSet<string>(season.TeamIds, StringComparer.Ordinal);
            if (teams.Count < 2 || teams.Count != season.TeamIds.Count || teams.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Season needs distinct team IDs.");
            var lastPlayed = new Dictionary<string, int>();
            if (fixtures.Count == 0 || fixtures.Select(f => f.Id).Distinct().Count() != fixtures.Count)
                throw new ArgumentException("A season needs unique fixtures.");
            foreach (var fixture in fixtures.OrderBy(f => f.ScheduledDay))
            {
                if (fixture.SeasonId != season.Id || fixture.LeagueId != season.LeagueId ||
                    fixture.HomeTeamId == fixture.AwayTeamId || !teams.Contains(fixture.HomeTeamId) ||
                    !teams.Contains(fixture.AwayTeamId) || fixture.ScheduledDay < start || fixture.ScheduledDay > end)
                    throw new ArgumentException("Fixture does not belong to this season or its date range.");
                foreach (var team in new[] { fixture.HomeTeamId, fixture.AwayTeamId })
                {
                    if (lastPlayed.TryGetValue(team, out var previous) && fixture.ScheduledDay - previous < 5)
                        throw new ArgumentException("Team fixtures must be at least five days apart.");
                    lastPlayed[team] = fixture.ScheduledDay;
                }
            }
            if (lastPlayed.Count != teams.Count) throw new ArgumentException("Every team needs fixtures.");
        }

        private static void InsertSeason(SqliteConnection connection, SqliteTransaction transaction, LeagueSeason season, IReadOnlyList<Fixture> fixtures)
        {
                var start = SimulationCalendar.DayFromDate(season.StartDate);
                var end = SimulationCalendar.DayFromDate(season.EndDate);
                Execute(connection, transaction,
                    "INSERT INTO LeagueSeasons VALUES ($id,$league,$name,$start,$end,$strategy);",
                    "$id", season.Id, "$league", season.LeagueId, "$name", season.Name ?? season.Id,
                    "$start", start, "$end", end, "$strategy", (object)season.FixtureStrategyId ?? DBNull.Value);
                foreach (var team in season.TeamIds)
                {
                    Execute(connection, transaction, "INSERT INTO SeasonTeams VALUES ($season,$team);",
                        "$season", season.Id, "$team", team);
                    Execute(connection, transaction, "INSERT INTO SeasonStandings (SeasonId,TeamId) VALUES ($season,$team);",
                        "$season", season.Id, "$team", team);
                }
                Execute(connection, transaction, "INSERT INTO SeasonPrizes VALUES ($season,$prize);",
                    "$season", season.Id, "$prize", season.WinnerPrize);
                foreach (var fixture in fixtures)
                {
                    Execute(connection, transaction,
                        "INSERT INTO Fixtures VALUES ($id,$league,$home,$away,$round,$day,$played,$hg,$ag);",
                        "$id", fixture.Id, "$league", fixture.LeagueId, "$home", fixture.HomeTeamId,
                        "$away", fixture.AwayTeamId, "$round", fixture.Matchday, "$day", fixture.ScheduledDay,
                        "$played", fixture.IsPlayed ? 1 : 0, "$hg", fixture.HomeGoals, "$ag", fixture.AwayGoals);
                    Execute(connection, transaction, "INSERT INTO SeasonFixtures VALUES ($season,$fixture);",
                        "$season", season.Id, "$fixture", fixture.Id);
                }
        }

        public int LoadFirstSeasonYear()
        {
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT MIN(StartDay) FROM LeagueSeasons;";
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value) throw new InvalidOperationException("Campaign has no seasons.");
                return SimulationCalendar.YearFromDay(Convert.ToInt32(value));
            }
        }

        public LeagueSeason LoadSeasonForYear(string leagueId, int startYear)
        {
            string id;
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                var start = SimulationCalendar.DayFromDate(new SimulationDate { Year = startYear, Month = 1, Day = 1 });
                cmd.CommandText = "SELECT Id FROM LeagueSeasons WHERE LeagueId=$league AND StartDay >= $start AND StartDay < $end ORDER BY StartDay,Id LIMIT 1;";
                cmd.Parameters.AddWithValue("$league", leagueId); cmd.Parameters.AddWithValue("$start", start);
                cmd.Parameters.AddWithValue("$end", checked(start + 365));
                id = cmd.ExecuteScalar() as string;
            }
            return id == null ? null : LoadSeason(id);
        }

        public LeagueSeason LoadSeason(string seasonId)
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT s.Id,s.LeagueId,s.Name,s.StartDay,s.EndDay,COALESCE(s.StrategyId,l.StrategyId),COALESCE(p.Prize,0) FROM LeagueSeasons s LEFT JOIN SeasonPrizes p ON p.SeasonId=s.Id LEFT JOIN LeagueSettings l ON l.LeagueId=s.LeagueId WHERE s.Id=$id;";
                command.Parameters.AddWithValue("$id", seasonId);
                LeagueSeason season;
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    season = new LeagueSeason
                    {
                        Id = reader.GetString(0), LeagueId = reader.GetString(1), Name = reader.GetString(2),
                        StartDate = SimulationCalendar.DateFromDay(reader.GetInt32(3)),
                        EndDate = SimulationCalendar.DateFromDay(reader.GetInt32(4)),
                        FixtureStrategyId = reader.IsDBNull(5) ? null : reader.GetString(5),
                        WinnerPrize = reader.GetInt64(6)
                    };
                }
                command.CommandText = "SELECT TeamId FROM SeasonTeams WHERE SeasonId=$id ORDER BY TeamId;";
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) season.TeamIds.Add(reader.GetString(0));
                return season;
            }
        }

        public List<Fixture> LoadSeasonFixtures(string seasonId)
        {
            var fixtures = new List<Fixture>();
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT f.Id,f.LeagueId,f.HomeTeamId,f.AwayTeamId,f.Matchday,
f.ScheduledDay,f.IsPlayed,f.HomeGoals,f.AwayGoals
FROM SeasonFixtures s JOIN Fixtures f ON f.Id=s.FixtureId
WHERE s.SeasonId=$id ORDER BY f.ScheduledDay,f.Id;";
                command.Parameters.AddWithValue("$id", seasonId);
                using (var reader = command.ExecuteReader())
                    while (reader.Read())
                        fixtures.Add(new Fixture
                        {
                            Id = reader.GetString(0), LeagueId = reader.GetString(1), SeasonId = seasonId,
                            HomeTeamId = reader.GetString(2), AwayTeamId = reader.GetString(3),
                            Matchday = reader.GetInt32(4), ScheduledDay = reader.GetInt32(5),
                            IsPlayed = reader.GetInt32(6) != 0, HomeGoals = reader.GetInt32(7), AwayGoals = reader.GetInt32(8)
                        });
            }
            return fixtures;
        }

    }
}
