using System;
using System.Collections.Generic;
using System.Linq;
using Football.Simulation.Data;
using Microsoft.Data.Sqlite;

namespace Football.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        public void CommitMatches(int expectedDay, IReadOnlyList<MatchResult> results)
        {
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction())
            {
                CheckDay(c, tx, expectedDay);
                foreach (var result in results)
                {
                    string home, away, season;
                    using (var command = c.CreateCommand())
                    {
                        command.Transaction = tx;
                        command.CommandText = @"SELECT f.HomeTeamId,f.AwayTeamId,s.SeasonId FROM Fixtures f
JOIN SeasonFixtures s ON s.FixtureId=f.Id WHERE f.Id=$id AND f.ScheduledDay=$day AND f.IsPlayed=0;";
                        command.Parameters.AddWithValue("$id", result.FixtureId);
                        command.Parameters.AddWithValue("$day", expectedDay);
                        using (var r = command.ExecuteReader())
                        {
                            if (!r.Read()) throw new InvalidOperationException("Stale, duplicate or unscheduled match result.");
                            home = r.GetString(0); away = r.GetString(1); season = r.GetString(2);
                        }
                    }
                    if (result.HomeGoals < 0 || result.AwayGoals < 0 ||
                        (!result.Forfeit && (result.Players.Where(p => p.TeamId == home).Sum(p => p.Goals) != result.HomeGoals ||
                        result.Players.Where(p => p.TeamId == away).Sum(p => p.Goals) != result.AwayGoals)))
                        throw new ArgumentException("Score and player goals disagree.");
                    Execute(c, tx, "UPDATE Fixtures SET IsPlayed=1,HomeGoals=$home,AwayGoals=$away WHERE Id=$id;",
                        "$home", result.HomeGoals, "$away", result.AwayGoals, "$id", result.FixtureId);
                    Execute(c, tx, "INSERT INTO MatchDetails VALUES ($id,$home,$away,$forfeit);",
                        "$id", result.FixtureId, "$home", result.HomeExpectedGoals, "$away", result.AwayExpectedGoals, "$forfeit", result.Forfeit ? 1 : 0);
                    UpdateTable(c, tx, season, home, result.HomeGoals, result.AwayGoals);
                    UpdateTable(c, tx, season, away, result.AwayGoals, result.HomeGoals);
                    foreach (var player in result.Players)
                    {
                        if ((player.TeamId != home && player.TeamId != away) || player.Minutes < 0 || player.Minutes > 90)
                            throw new ArgumentException("Invalid player performance.");
                        Execute(c, tx, @"INSERT INTO FootballerMatchPerformances VALUES ($fixture,$player,$minutes,$rating,$goals,$assists,$yellow,$red);",
                            "$fixture", result.FixtureId, "$player", player.FootballerId, "$minutes", player.Minutes,
                            "$rating", player.Rating, "$goals", player.Goals, "$assists", player.Assists, "$yellow", player.YellowCards, "$red", player.RedCards);
                        Execute(c, tx, @"UPDATE PlayerTotals SET Appearances=Appearances+1,Starts=Starts+$starts,Minutes=Minutes+$minutes,
Goals=Goals+$goals,Assists=Assists+$assists,YellowCards=YellowCards+$yellow,RedCards=RedCards+$red WHERE FootballerId=$id;",
                            "$id", player.FootballerId, "$starts", player.Started ? 1 : 0, "$minutes", player.Minutes, "$goals", player.Goals, "$assists", player.Assists,
                            "$yellow", player.YellowCards, "$red", player.RedCards);
                    }
                    foreach (var state in result.UpdatedPlayers)
                    {
                        var f = state.Form;
                        Execute(c, tx, "UPDATE DevelopmentData SET Form=$form WHERE FootballerId=$id;", "$form", f.PerformanceForm, "$id", state.FootballerId);
                        using (var command = c.CreateCommand())
                        {
                            command.Transaction = tx;
                            command.CommandText = @"UPDATE FootballerState SET LastUpdatedDay=$day,Morale=$morale,Fitness=$fitness,
Fatigue=$fatigue,RecentMatchRating=$rating,Availability=$availability,InjuryDaysRemaining=$injury,
SuspensionMatchesRemaining=$ban WHERE FootballerId=$id
AND FootballerId IN (SELECT Id FROM Footballers WHERE TeamId=$home OR TeamId=$away);";
                            foreach (var pair in new Dictionary<string, object> { ["$day"] = expectedDay, ["$morale"] = f.Morale,
                                ["$fitness"] = f.Fitness, ["$fatigue"] = f.Fatigue, ["$rating"] = f.RecentMatchRating,
                                ["$availability"] = (int)f.Availability, ["$injury"] = f.InjuryDaysRemaining, ["$ban"] = f.SuspensionMatchesRemaining,
                                ["$id"] = state.FootballerId, ["$home"] = home, ["$away"] = away })
                                command.Parameters.AddWithValue(pair.Key, pair.Value);
                            if (command.ExecuteNonQuery() != 1) throw new ArgumentException("Player state is outside the fixture.");
                        }
                    }
                    foreach (var state in result.UpdatedTeams)
                    {
                        if (state.TeamId != home && state.TeamId != away) throw new ArgumentException("Invalid team state.");
                        Execute(c, tx, "UPDATE TeamRuntime SET Morale=$morale,Form=$form WHERE TeamId=$id;",
                            "$morale", state.Morale, "$form", state.Form, "$id", state.TeamId);
                    }
                    for (var index = 0; index < result.Events.Count; index++)
                    {
                        var e = result.Events[index];
                        Execute(c, tx, "INSERT INTO MatchEvents VALUES ($id,$seq,$minute,$type,$team,$primary,$secondary);",
                            "$id", result.FixtureId, "$seq", index, "$minute", e.Minute, "$type", (int)e.Type,
                            "$team", (object)e.TeamId ?? DBNull.Value, "$primary", (object)e.PrimaryFootballerId ?? DBNull.Value,
                            "$secondary", (object)e.SecondaryFootballerId ?? DBNull.Value);
                    }
                }
                tx.Commit();
            }
        }

        public void CompleteDay(int expectedDay, IReadOnlyList<SeasonSummary> summaries, IReadOnlyList<SeasonSchedule> nextSeasons = null)
        {
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction())
            {
                CheckDay(c, tx, expectedDay);
                using (var command = c.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = "SELECT COUNT(*) FROM Fixtures WHERE ScheduledDay <= $day AND IsPlayed=0;";
                    command.Parameters.AddWithValue("$day", expectedDay);
                    if (Convert.ToInt32(command.ExecuteScalar()) != 0) throw new InvalidOperationException("Unplayed fixtures block date advancement.");
                }
                foreach (var summary in summaries)
                {
                    using (var command = c.CreateCommand())
                    {
                        command.Transaction = tx;
                        command.CommandText = @"SELECT COUNT(*),SUM(f.IsPlayed) FROM Fixtures f
JOIN SeasonFixtures s ON s.FixtureId=f.Id WHERE s.SeasonId=$id;";
                        command.Parameters.AddWithValue("$id", summary.SeasonId);
                        using (var r = command.ExecuteReader())
                        {
                            r.Read();
                            if (r.GetInt32(0) == 0 || r.GetInt32(0) != r.GetInt32(1))
                                throw new InvalidOperationException("Season is not complete.");
                        }
                    }
                    Execute(c, tx, "INSERT INTO SeasonHistory VALUES ($id,$winner,$prize,$day);",
                        "$id", summary.SeasonId, "$winner", summary.WinnerTeamId, "$prize", summary.Prize, "$day", expectedDay);
                    Execute(c, tx, "UPDATE TeamRuntime SET Balance=Balance+$prize WHERE TeamId=$id;",
                        "$prize", summary.Prize, "$id", summary.WinnerTeamId);
                }
                if (nextSeasons != null)
                    foreach (var schedule in nextSeasons)
                    {
                        ValidateSeason(schedule.Season, schedule.Fixtures);
                        if (SimulationCalendar.DayFromDate(schedule.Season.StartDate) <= expectedDay)
                            throw new ArgumentException("A successor season must start after today.");
                        InsertSeason(c, tx, schedule.Season, schedule.Fixtures);
                    }
                Execute(c, tx, "UPDATE PlaythroughMetadata SET CurrentDay=$next;", "$next", checked(expectedDay + 1));
                tx.Commit();
            }
        }

        private static void CheckDay(SqliteSession c, SqliteTransaction tx, int day) => CheckDay(c.Connection, tx, day);

        private static void CheckDay(SqliteConnection c, SqliteTransaction tx, int day)
        {
            using (var command = c.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = "SELECT CurrentDay FROM PlaythroughMetadata;";
                var value = command.ExecuteScalar();
                if (value == null || Convert.ToInt32(value) != day) throw new InvalidOperationException("Stale simulation date.");
            }
        }

        private static void UpdateTable(SqliteConnection c, SqliteTransaction tx, string season, string team, int scored, int conceded)
        {
            var win = scored > conceded ? 1 : 0;
            var draw = scored == conceded ? 1 : 0;
            Execute(c, tx, @"UPDATE SeasonStandings SET Played=Played+1,Won=Won+$win,Drawn=Drawn+$draw,Lost=Lost+$loss,
GoalsFor=GoalsFor+$scored,GoalsAgainst=GoalsAgainst+$conceded,Points=Points+$points WHERE SeasonId=$season AND TeamId=$team;",
                "$win", win, "$draw", draw, "$loss", 1 - win - draw, "$scored", scored, "$conceded", conceded,
                "$points", win * 3 + draw, "$season", season, "$team", team);
        }
    }
}
