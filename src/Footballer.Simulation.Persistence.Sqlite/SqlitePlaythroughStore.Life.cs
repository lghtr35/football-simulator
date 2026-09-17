using System;
using System.Collections.Generic;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
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
WHERE s.LastUpdatedDay<$day AND ($after IS NULL OR s.FootballerId>$after) ORDER BY s.FootballerId LIMIT $limit;";
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
            {
                CheckDay(c, tx, day);
                foreach (var state in states)
                {
                    if (state.LastUpdatedDay != day) throw new ArgumentException("Life strategy must advance the player's date.");
                    var f = state.Form;
                    Execute(c, tx, @"UPDATE DevelopmentData SET Form=$form WHERE FootballerId=$id
AND EXISTS(SELECT 1 FROM FootballerState WHERE FootballerId=$id AND LastUpdatedDay<$day);",
                        "$form", f.PerformanceForm, "$id", state.FootballerId, "$day", day);
                    Execute(c, tx, @"UPDATE FootballerState SET LastUpdatedDay=$day,Morale=$morale,Fitness=$fitness,Fatigue=$fatigue,
RecentMatchRating=$rating,Availability=$availability,InjuryDaysRemaining=$injury,SuspensionMatchesRemaining=$ban
WHERE FootballerId=$id AND LastUpdatedDay<$day;", "$day", day, "$morale", f.Morale, "$fitness", f.Fitness,
                        "$fatigue", f.Fatigue, "$rating", f.RecentMatchRating, "$availability", (int)f.Availability,
                        "$injury", f.InjuryDaysRemaining, "$ban", f.SuspensionMatchesRemaining, "$id", state.FootballerId);
                }
                tx.Commit();
            }
        }
    }
}
