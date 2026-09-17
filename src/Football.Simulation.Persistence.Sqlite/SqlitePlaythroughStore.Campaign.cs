using System;
using System.Collections.Generic;
using System.Linq;
using Football.Simulation.Data;
using Microsoft.Data.Sqlite;

namespace Football.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        public void CreateCampaign(PlaythroughMetadata metadata, WorldDefinition world, WorldState state,
            IReadOnlyList<SeasonSchedule> schedules)
        {
            Initialise();
            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                using (var check = connection.CreateCommand())
                {
                    check.Transaction = transaction;
                    check.CommandText = "SELECT COUNT(*) FROM PlaythroughMetadata;";
                    if (Convert.ToInt32(check.ExecuteScalar()) != 0) throw new InvalidOperationException("Save already exists.");
                }
                InsertMetadata(connection, transaction, metadata);
                var rules = world.Rules;
                rules.Validate();
                Execute(connection, transaction, "INSERT INTO WorldRules VALUES (1,$dev,$transfer,$days,$training,$cash,$income,$budget,$day);",
                    "$dev", rules.DevelopmentIntervalDays, "$transfer", rules.TransferIntervalDays, "$days", rules.ContractLengthDays,
                    "$training", rules.DefaultTraining, "$cash", rules.InitialCash, "$income", rules.DailyIncome, "$budget", rules.DailyWageBudget, "$day", metadata.CurrentDay - 1);
                foreach (var window in rules.TransferWindows)
                    Execute(connection, transaction, "INSERT INTO TransferWindows VALUES ($start,$end);", "$start", window.StartDayOfYear, "$end", window.EndDayOfYear);
                foreach (var league in world.Leagues)
                {
                    InsertLeague(connection, transaction, league);
                    Execute(connection, transaction, "INSERT INTO LeagueSquadRules VALUES ($id,$size,$subs);", "$id", league.Id, "$size", league.MatchSquadSize, "$subs", league.MaxSubstitutions);
                    Execute(connection, transaction, "INSERT INTO LeagueSettings VALUES ($id,$start,$end,$strategy,$prize);",
                        "$id", league.Id, "$start", league.SeasonStartMonth, "$end", league.SeasonEndMonth,
                        "$strategy", league.FixtureStrategyId, "$prize", league.WinnerPrize);
                    foreach (var team in league.Teams)
                    {
                        InsertTeam(connection, transaction, team);
                        Execute(connection, transaction, "INSERT INTO TeamRuntime VALUES ($id,50,50,$cash,$fans,$tactics);",
                            "$id", team.Id, "$cash", rules.InitialCash, "$fans", team.FanSupport, "$tactics", (int)team.Tactics);
                        Execute(connection, transaction, "INSERT INTO ClubFinances VALUES ($id,$income,$budget);", "$id", team.Id, "$income", rules.DailyIncome, "$budget", rules.DailyWageBudget);
                    }
                }
                var states = state.Footballers.ToDictionary(s => s.FootballerId);
                foreach (var player in world.Footballers)
                {
                    InsertFootballer(connection, transaction, player);
                    InsertFootballerState(connection, transaction, states[player.Id]);
                    Execute(connection, transaction, "INSERT INTO PlayerRatings VALUES ($id,$overall,$gk);",
                        "$id", player.Id, "$overall", player.Overall, "$gk", player.Skills.Goalkeeping);
                    Execute(connection, transaction, "INSERT INTO PlayerTotals (FootballerId) VALUES ($id);", "$id", player.Id);
                    Execute(connection, transaction, "INSERT INTO DevelopmentData (FootballerId,BirthDay,Training,LastDevelopmentDay) VALUES ($id,$birth,$training,$day);",
                        "$id", player.Id, "$birth", metadata.CurrentDay - player.Attributes.Age * 365, "$training", rules.DefaultTraining, "$day", metadata.CurrentDay);
                    InsertContract(connection, transaction, player.Id, PersonKind.Player, player.TeamId, player.Overall, metadata.CurrentDay, rules);
                }
                foreach (var coach in world.Coaches)
                {
                    Execute(connection, transaction, "INSERT INTO Coaches VALUES ($id,$name,$team,$ability,$tactics);",
                        "$id", coach.Id, "$name", coach.Name, "$team", (object)coach.TeamId ?? DBNull.Value,
                        "$ability", coach.Ability, "$tactics", (int)coach.PreferredTactics);
                    InsertContract(connection, transaction, coach.Id, PersonKind.Coach, coach.TeamId, coach.Ability, metadata.CurrentDay, rules);
                }
                foreach (var schedule in schedules)
                {
                    var season = schedule.Season;
                    Execute(connection, transaction, "INSERT INTO LeagueSeasons VALUES ($id,$league,$name,$start,$end,$strategy);",
                        "$id", season.Id, "$league", season.LeagueId, "$name", season.Name,
                        "$start", SimulationCalendar.DayFromDate(season.StartDate), "$end", SimulationCalendar.DayFromDate(season.EndDate),
                        "$strategy", (object)season.FixtureStrategyId ?? DBNull.Value);
                    Execute(connection, transaction, "INSERT INTO SeasonPrizes VALUES ($id,$prize);", "$id", season.Id, "$prize", season.WinnerPrize);
                    foreach (var team in season.TeamIds)
                    {
                        Execute(connection, transaction, "INSERT INTO SeasonTeams VALUES ($season,$team);", "$season", season.Id, "$team", team);
                        Execute(connection, transaction, "INSERT INTO SeasonStandings (SeasonId,TeamId) VALUES ($season,$team);", "$season", season.Id, "$team", team);
                    }
                    foreach (var fixture in schedule.Fixtures)
                    {
                        Execute(connection, transaction, "INSERT INTO Fixtures VALUES ($id,$league,$home,$away,$round,$day,0,0,0);",
                            "$id", fixture.Id, "$league", fixture.LeagueId, "$home", fixture.HomeTeamId,
                            "$away", fixture.AwayTeamId, "$round", fixture.Matchday, "$day", fixture.ScheduledDay);
                        Execute(connection, transaction, "INSERT INTO SeasonFixtures VALUES ($season,$fixture);",
                            "$season", season.Id, "$fixture", fixture.Id);
                    }
                }
                transaction.Commit();
            }
        }

        private static void InsertContract(SqliteConnection c, SqliteTransaction tx, string id, PersonKind kind, string team, int ability, int day, WorldRules rules)
        {
            Execute(c, tx, "INSERT INTO Contracts VALUES ($id,$kind,$team,$end,$wage);", "$id", id, "$kind", (int)kind,
                "$team", team, "$end", checked(day + rules.ContractLengthDays), "$wage", team == null ? 0 : PlayerAbility.DailyWage(ability, kind));
        }

    }
}
