using System;
using System.Collections.Generic;
using BeAFootballer.Simulation.Data;
using Microsoft.Data.Sqlite;

namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        public void StampStableLife(int day)
        {
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction())
            {
                CheckDay(c, tx, day);
                Execute(c, tx, @"UPDATE FootballerState SET LastUpdatedDay=$day
WHERE LastUpdatedDay<$day AND Fatigue=0 AND Fitness=100 AND InjuryDaysRemaining=0 AND SuspensionMatchesRemaining=0;",
                    "$day", day);
                tx.Commit();
            }
        }

        public List<FootballerState> LoadLifePage(string afterId, int limit, int day)
        {
            if (limit < 1 || limit > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
            var result = new List<FootballerState>();
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"SELECT s.FootballerId,s.LastUpdatedDay,s.Morale,s.Fitness,s.Fatigue,s.RecentMatchRating,
s.Availability,s.InjuryDaysRemaining,s.SuspensionMatchesRemaining,d.Form FROM FootballerState s
JOIN DevelopmentData d ON d.FootballerId=s.FootballerId
WHERE s.LastUpdatedDay<$day AND ($after IS NULL OR s.FootballerId>$after)
AND (s.Fatigue>0 OR s.Fitness<100 OR s.InjuryDaysRemaining>0 OR s.SuspensionMatchesRemaining>0)
ORDER BY s.FootballerId LIMIT $limit;";
                cmd.Parameters.AddWithValue("$day", day); cmd.Parameters.AddWithValue("$after", (object)afterId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$limit", limit);
                using (var r = cmd.ExecuteReader()) while (r.Read()) result.Add(new FootballerState {
                    FootballerId = r.GetString(0), LastUpdatedDay = r.GetInt32(1), Stats = new FootballerStats(),
                    Form = new FootballerForm { Morale = r.GetInt32(2), Fitness = r.GetInt32(3), Fatigue = r.GetInt32(4),
                        RecentMatchRating = r.GetInt32(5), Availability = (AvailabilityStatus)r.GetInt32(6), InjuryDaysRemaining = r.GetInt32(7),
                        SuspensionMatchesRemaining = r.GetInt32(8), PerformanceForm = r.GetInt32(9) } });
            }
            return result;
        }

        public void CommitLife(int day, IReadOnlyList<FootballerState> states)
        {
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction())
            using (var form = c.CreateCommand())
            using (var state = c.CreateCommand())
            {
                CheckDay(c, tx, day);
                form.Transaction = tx;
                form.CommandText = @"UPDATE DevelopmentData SET Form=$form WHERE FootballerId=$id
AND EXISTS(SELECT 1 FROM FootballerState WHERE FootballerId=$id AND LastUpdatedDay<$day);";
                form.Parameters.Add("$form", SqliteType.Integer);
                form.Parameters.Add("$id", SqliteType.Text);
                form.Parameters.Add("$day", SqliteType.Integer);
                state.Transaction = tx;
                state.CommandText = @"UPDATE FootballerState SET LastUpdatedDay=$day,Morale=$morale,Fitness=$fitness,Fatigue=$fatigue,
RecentMatchRating=$rating,Availability=$availability,InjuryDaysRemaining=$injury,SuspensionMatchesRemaining=$ban
WHERE FootballerId=$id AND LastUpdatedDay<$day;";
                state.Parameters.Add("$day", SqliteType.Integer);
                state.Parameters.Add("$morale", SqliteType.Integer);
                state.Parameters.Add("$fitness", SqliteType.Integer);
                state.Parameters.Add("$fatigue", SqliteType.Integer);
                state.Parameters.Add("$rating", SqliteType.Integer);
                state.Parameters.Add("$availability", SqliteType.Integer);
                state.Parameters.Add("$injury", SqliteType.Integer);
                state.Parameters.Add("$ban", SqliteType.Integer);
                state.Parameters.Add("$id", SqliteType.Text);
                foreach (var row in states)
                {
                    if (row.LastUpdatedDay != day) throw new ArgumentException("Life strategy must advance the player's date.");
                    var f = row.Form;
                    form.Parameters["$form"].Value = f.PerformanceForm;
                    form.Parameters["$id"].Value = row.FootballerId;
                    form.Parameters["$day"].Value = day;
                    form.ExecuteNonQuery();
                    state.Parameters["$day"].Value = day;
                    state.Parameters["$morale"].Value = f.Morale;
                    state.Parameters["$fitness"].Value = f.Fitness;
                    state.Parameters["$fatigue"].Value = f.Fatigue;
                    state.Parameters["$rating"].Value = f.RecentMatchRating;
                    state.Parameters["$availability"].Value = (int)f.Availability;
                    state.Parameters["$injury"].Value = f.InjuryDaysRemaining;
                    state.Parameters["$ban"].Value = f.SuspensionMatchesRemaining;
                    state.Parameters["$id"].Value = row.FootballerId;
                    state.ExecuteNonQuery();
                }
                tx.Commit();
            }
        }
    }
}
