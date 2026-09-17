using System;
using System.Collections.Generic;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        public List<DevelopmentPlayer> LoadDevelopmentPage(string afterId, int limit, int throughDay)
        {
            if (limit < 1 || limit > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
            var result = new List<DevelopmentPlayer>();
            using (var c = OpenConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"SELECT p.Id,p.Name,p.TeamId,p.Position,p.PreferredFoot,p.Age,p.HeightCentimetres,
p.WeightKilograms,p.Nationality,p.Speed,p.Acceleration,p.Stamina,p.StaminaRegen,p.Dribbling,p.FirstTouchControl,
p.HitPower,p.Accuracy,p.Tackling,p.Strength,q.Overall,q.Goalkeeping,d.BirthDay,d.Training,d.Form,d.LastDevelopmentDay,
t.Minutes-d.MinutesCheckpoint,(SELECT CurrentDay FROM PlaythroughMetadata) FROM DevelopmentData d JOIN Footballers p ON p.Id=d.FootballerId
JOIN PlayerRatings q ON q.FootballerId=p.Id JOIN PlayerTotals t ON t.FootballerId=p.Id
WHERE ($after IS NULL OR p.Id>$after) AND d.LastDevelopmentDay<=$through ORDER BY p.Id LIMIT $limit;";
                cmd.Parameters.AddWithValue("$after", (object)afterId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$limit", limit); cmd.Parameters.AddWithValue("$through", throughDay);
                using (var r = cmd.ExecuteReader()) while (r.Read())
                {
                    var p = ReadFootballer(r);
                    ApplyDerivedAbility(p, r.GetInt32(20), r.GetInt32(21), r.GetInt32(26));
                    result.Add(new DevelopmentPlayer { Definition = p, BirthDay = r.GetInt32(21), Training = r.GetInt32(22),
                        Form = r.GetInt32(23), LastDevelopmentDay = r.GetInt32(24), MinutesSinceDevelopment = r.GetInt32(25) });
                }
            }
            return result;
        }

        public void CommitDevelopment(int day, IReadOnlyList<DevelopmentUpdate> updates)
        {
            using (var c = OpenConnection())
            using (var tx = c.BeginTransaction())
            {
                CheckDay(c, tx, day);
                foreach (var update in updates)
                {
                    if (update == null || update.Day != day) throw new ArgumentException("Invalid development date.");
                    var p = update.Player.Definition; var s = p.Skills;
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = @"UPDATE DevelopmentData SET LastDevelopmentDay=$day,
MinutesCheckpoint=(SELECT Minutes FROM PlayerTotals WHERE FootballerId=$id)
WHERE FootballerId=$id AND LastDevelopmentDay=$previous AND LastDevelopmentDay<$day;";
                        cmd.Parameters.AddWithValue("$day", day); cmd.Parameters.AddWithValue("$id", p.Id);
                        cmd.Parameters.AddWithValue("$previous", update.Player.LastDevelopmentDay);
                        if (cmd.ExecuteNonQuery() == 0) continue;
                    }
                    Execute(c, tx, @"UPDATE Footballers SET Age=$age,Speed=$speed,Acceleration=$acceleration,Stamina=$stamina,
StaminaRegen=$regen,Dribbling=$dribbling,FirstTouchControl=$touch,HitPower=$power,Accuracy=$accuracy,Tackling=$tackling,Strength=$strength WHERE Id=$id;",
                        "$id", p.Id, "$age", p.Attributes.Age, "$speed", s.Speed, "$acceleration", s.Acceleration,
                        "$stamina", s.Stamina, "$regen", s.StaminaRegen, "$dribbling", s.Dribbling, "$touch", s.FirstTouchControl,
                        "$power", s.HitPower, "$accuracy", s.Accuracy, "$tackling", s.Tackling, "$strength", s.Strength);
                    if (update.Change != 0)
                    {
                        Execute(c, tx, "UPDATE PlayerRatings SET Overall=$overall,Goalkeeping=$gk WHERE FootballerId=$id;",
                            "$id", p.Id, "$overall", p.Overall, "$gk", s.Goalkeeping);
                        InsertHistory(c, tx, new WorldHistoryEntry { Day = day, Type = "development",
                            PersonId = p.Id, ToTeamId = p.TeamId, Amount = update.Change, Detail = update.Skill + ": overall " + update.OldOverall + " -> " + p.Overall });
                    }
                }
                tx.Commit();
            }
        }
    }
}
