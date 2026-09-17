using System;
using System.Collections.Generic;
using System.Linq;
using BeAFootballer.Simulation.Data;
using Microsoft.Data.Sqlite;

namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        public WorldRules LoadWorldRules()
        {
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT DevelopmentInterval,TransferInterval,ContractDays,Training,InitialCash,DailyIncome,WageBudget FROM WorldRules WHERE Id=1;";
                WorldRules result;
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) throw new InvalidOperationException("Campaign has no world rules.");
                    result = new WorldRules { DevelopmentIntervalDays = r.GetInt32(0), TransferIntervalDays = r.GetInt32(1),
                        ContractLengthDays = r.GetInt32(2), DefaultTraining = r.GetInt32(3), InitialCash = r.GetInt64(4),
                        DailyIncome = r.GetInt64(5), DailyWageBudget = r.GetInt64(6), TransferWindows = new List<TransferWindow>() };
                }
                cmd.CommandText = "SELECT StartDay,EndDay FROM TransferWindows ORDER BY StartDay,EndDay;";
                using (var r = cmd.ExecuteReader()) while (r.Read())
                    result.TransferWindows.Add(new TransferWindow { StartDayOfYear = r.GetInt32(0), EndDayOfYear = r.GetInt32(1) });
                return result;
            }
        }

        public bool IsMarketDayComplete(int day)
        {
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT MarketDay FROM WorldRules WHERE Id=1;";
                return Convert.ToInt32(cmd.ExecuteScalar()) >= day;
            }
        }

        public MarketSnapshot LoadMarket()
        {
            var result = new MarketSnapshot();
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction(deferred: true))
            using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"SELECT f.TeamId,t.Balance,f.DailyIncome,f.WageBudget
FROM ClubFinances f JOIN TeamRuntime t ON t.TeamId=f.TeamId ORDER BY f.TeamId;";
                using (var r = cmd.ExecuteReader()) while (r.Read()) result.Clubs.Add(new ClubFinance {
                    TeamId = r.GetString(0), Cash = r.GetInt64(1), DailyIncome = r.GetInt64(2), WageBudget = r.GetInt64(3) });
                cmd.CommandText = @"SELECT c.PersonId,c.Kind,c.TeamId,c.EndDay,c.DailyWage,
CASE WHEN c.Kind=0 THEN q.Overall ELSE h.Ability END,
COALESCE(((SELECT CurrentDay FROM PlaythroughMetadata)-d.BirthDay)/365,40),COALESCE(p.Position,0)
FROM Contracts c LEFT JOIN Footballers p ON c.Kind=0 AND p.Id=c.PersonId
LEFT JOIN PlayerRatings q ON q.FootballerId=p.Id LEFT JOIN DevelopmentData d ON d.FootballerId=p.Id
LEFT JOIN Coaches h ON c.Kind=1 AND h.Id=c.PersonId
ORDER BY c.Kind,c.PersonId;";
                using (var r = cmd.ExecuteReader()) while (r.Read())
                {
                    var kind = (PersonKind)r.GetInt32(1);
                    var overall = r.GetInt32(5); var age = r.GetInt32(6);
                    result.People.Add(new MarketPerson { Contract = new Contract { PersonId = r.GetString(0), Kind = kind,
                        TeamId = r.IsDBNull(2) ? null : r.GetString(2), EndDay = r.GetInt32(3), DailyWage = r.GetInt64(4) },
                        Overall = overall, Age = age, PositionGroup = PlayerAbility.PositionGroup((PositionType)r.GetInt32(7)),
                        Value = PlayerAbility.Value(overall, age, kind), AskingWage = PlayerAbility.DailyWage(overall, kind) });
                }
                tx.Commit();
            }
            return result;
        }

        public void CommitMarket(int day, MarketUpdate update)
        {
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction())
            {
                CheckDay(c, tx, day);
                using (var cmd = c.CreateCommand())
                {
                    cmd.Transaction = tx; cmd.CommandText = "SELECT MarketDay FROM WorldRules WHERE Id=1;";
                    if (Convert.ToInt32(cmd.ExecuteScalar()) >= day) return;
                }
                var people = update.Snapshot.People;
                if (people.GroupBy(p => (p.Contract.Kind, p.Contract.PersonId)).Any(g => g.Count() != 1) ||
                    people.Any(p => p.Contract.DailyWage < 0) || people.Where(p => p.Contract.TeamId != null)
                    .GroupBy(p => (p.Contract.Kind, p.Contract.TeamId)).Any(g => g.Count() > (g.Key.Kind == PersonKind.Player ? 32 : 1)))
                    throw new ArgumentException("Invalid contract roster.");
                // Release changed memberships first, so transfers/replacements never transiently violate caps.
                foreach (var p in people)
                {
                    var table = p.Contract.Kind == PersonKind.Player ? "Footballers" : "Coaches";
                    Execute(c, tx, "UPDATE " + table + " SET TeamId=NULL WHERE Id=$id AND TeamId IS NOT $team;",
                        "$id", p.Contract.PersonId, "$team", p.Contract.TeamId);
                }
                foreach (var p in people)
                {
                    var contract = p.Contract;
                    var table = contract.Kind == PersonKind.Player ? "Footballers" : "Coaches";
                    Execute(c, tx, "UPDATE " + table + " SET TeamId=$team WHERE Id=$id AND TeamId IS NOT $team;", "$team", contract.TeamId, "$id", contract.PersonId);
                    Execute(c, tx, "UPDATE Contracts SET TeamId=$team,EndDay=$end,DailyWage=$wage WHERE PersonId=$id AND Kind=$kind;",
                        "$team", contract.TeamId, "$end", contract.EndDay, "$wage", contract.DailyWage, "$id", contract.PersonId, "$kind", (int)contract.Kind);
                }
                foreach (var club in update.Snapshot.Clubs)
                    Execute(c, tx, "UPDATE TeamRuntime SET Balance=$cash WHERE TeamId=$id;", "$cash", club.Cash, "$id", club.TeamId);
                foreach (var entry in update.History) InsertHistory(c, tx, entry);
                Execute(c, tx, "UPDATE Footballers SET Age=($day-(SELECT BirthDay FROM DevelopmentData WHERE FootballerId=Footballers.Id))/365;", "$day", day);
                Execute(c, tx, "UPDATE WorldRules SET MarketDay=$day WHERE Id=1;", "$day", day);
                tx.Commit();
            }
        }

        public void SetTraining(string footballerId, int intensity)
        {
            if (intensity < 0 || intensity > 100) throw new ArgumentOutOfRangeException(nameof(intensity));
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "UPDATE DevelopmentData SET Training=$training WHERE FootballerId=$id;";
                cmd.Parameters.AddWithValue("$training", intensity); cmd.Parameters.AddWithValue("$id", footballerId);
                if (cmd.ExecuteNonQuery() != 1) throw new ArgumentException("Unknown footballer.");
            }
        }

        public List<WorldHistoryEntry> LoadWorldHistory(int fromDay, int toDay, string personId = null, int limit = 100)
        {
            if (fromDay < 0 || toDay < fromDay || limit < 1 || limit > 1000) throw new ArgumentException("Invalid history range or limit.");
            var result = new List<WorldHistoryEntry>();
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"SELECT Day,Type,PersonId,FromTeamId,ToTeamId,Amount,Detail FROM WorldHistory
WHERE Day BETWEEN $from AND $to AND ($person IS NULL OR PersonId=$person) ORDER BY Day DESC,Id DESC LIMIT $limit;";
                cmd.Parameters.AddWithValue("$from", fromDay); cmd.Parameters.AddWithValue("$to", toDay);
                cmd.Parameters.AddWithValue("$person", (object)personId ?? DBNull.Value); cmd.Parameters.AddWithValue("$limit", limit);
                using (var r = cmd.ExecuteReader()) while (r.Read()) result.Add(new WorldHistoryEntry { Day = r.GetInt32(0), Type = r.GetString(1),
                    PersonId = r.IsDBNull(2) ? null : r.GetString(2), FromTeamId = r.IsDBNull(3) ? null : r.GetString(3),
                    ToTeamId = r.IsDBNull(4) ? null : r.GetString(4), Amount = r.GetInt64(5), Detail = r.IsDBNull(6) ? null : r.GetString(6) });
            }
            return result;
        }

        private static void InsertHistory(SqliteConnection c, SqliteTransaction tx, WorldHistoryEntry entry) =>
            Execute(c, tx, "INSERT INTO WorldHistory (Day,Type,PersonId,FromTeamId,ToTeamId,Amount,Detail) VALUES ($day,$type,$person,$from,$to,$amount,$detail);",
                "$day", entry.Day, "$type", entry.Type, "$person", entry.PersonId, "$from", entry.FromTeamId, "$to", entry.ToTeamId,
                "$amount", entry.Amount, "$detail", entry.Detail);
    }
}
