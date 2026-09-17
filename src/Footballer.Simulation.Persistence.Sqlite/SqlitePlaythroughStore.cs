using System;
using System.Collections.Generic;
using System.IO;
using BeAFootballer.Simulation.Data;
using Microsoft.Data.Sqlite;

namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    /// <summary>
    /// Windows MVP SQLite adapter. It owns file/schema concerns; simulators consume data contracts only.
    /// Each operation opens its own connection, allowing independent read workers.
    /// </summary>
    public sealed partial class SqlitePlaythroughStore : IPlaythroughStore, IWorldEvolutionStore
    {
        private readonly string databasePath;
        private SqliteConnection worldTick;

        public SqlitePlaythroughStore(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("A database path is required.", "databasePath");
            this.databasePath = Path.GetFullPath(databasePath);
        }

        public IDisposable BeginWorldTick()
        {
            if (worldTick != null) throw new InvalidOperationException("A world tick is already open.");
            worldTick = CreateConnection();
            return new WorldTickScope(this);
        }

        public void Initialise()
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                foreach (var statements in new[] { BaseSchemaStatements, SeasonSchemaStatements, RuntimeSchemaStatements, EconomySchemaStatements })
                {
                    foreach (var statement in statements)
                    {
                        command.CommandText = statement;
                        command.ExecuteNonQuery();
                    }
                }
                command.CommandText = "PRAGMA journal_mode = WAL;";
                command.ExecuteNonQuery();
            }
        }

        public void CreatePlaythrough(PlaythroughMetadata metadata, WorldDefinition world, WorldState state)
        {
            if (metadata == null) throw new ArgumentNullException("metadata");
            if (world == null) throw new ArgumentNullException("world");
            if (state == null) throw new ArgumentNullException("state");

            Initialise();
            var stateByFootballerId = new Dictionary<string, FootballerState>();
            foreach (var footballerState in state.Footballers) stateByFootballerId.Add(footballerState.FootballerId, footballerState);

            using (var connection = OpenConnection())
            using (var transaction = connection.BeginTransaction())
            {
                InsertMetadata(connection, transaction, metadata);
                foreach (var league in world.Leagues)
                {
                    InsertLeague(connection, transaction, league);
                    foreach (var team in league.Teams) InsertTeam(connection, transaction, team);
                }

                foreach (var footballer in world.Footballers)
                {
                    InsertFootballer(connection, transaction, footballer);
                    FootballerState footballerState;
                    if (!stateByFootballerId.TryGetValue(footballer.Id, out footballerState)) throw new InvalidOperationException("Missing state for footballer " + footballer.Id + ".");
                    InsertFootballerState(connection, transaction, footballerState);
                }

                transaction.Commit();
            }
        }

        public TeamDefinition LoadTeam(string teamId)
        {
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT Id, LeagueId, Name, ShortName, Strength FROM Teams WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", teamId);
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    return new TeamDefinition { Id = reader.GetString(0), LeagueId = reader.GetString(1), Name = reader.GetString(2), ShortName = reader.GetString(3), Strength = reader.GetInt32(4) };
                }
            }
        }

        public List<FootballerDefinition> LoadFootballersForTeam(string teamId)
        {
            var footballers = new List<FootballerDefinition>();
            using (var connection = OpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT p.Id, p.Name, p.TeamId, p.Position, p.PreferredFoot, p.Age, p.HeightCentimetres, p.WeightKilograms, p.Nationality, p.Speed, p.Acceleration, p.Stamina, p.StaminaRegen, p.Dribbling, p.FirstTouchControl, p.HitPower, p.Accuracy, p.Tackling, p.Strength,
COALESCE(q.Goalkeeping,50),COALESCE(d.BirthDay,0),(SELECT CurrentDay FROM PlaythroughMetadata)
FROM Footballers p LEFT JOIN PlayerRatings q ON q.FootballerId=p.Id LEFT JOIN DevelopmentData d ON d.FootballerId=p.Id
WHERE p.TeamId = $teamId ORDER BY p.Id;";
                command.Parameters.AddWithValue("$teamId", teamId);
                using (var reader = command.ExecuteReader()) while (reader.Read())
                {
                    var footballer = ReadFootballer(reader);
                    ApplyDerivedAbility(footballer, reader.GetInt32(19), reader.GetInt32(20), reader.GetInt32(21));
                    footballers.Add(footballer);
                }
            }
            return footballers;
        }

        private SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
            connection.Open();
            using (var command = connection.CreateCommand()) { command.CommandText = "PRAGMA foreign_keys = ON;"; command.ExecuteNonQuery(); }
            return connection;
        }

        private SqliteSession OpenConnection()
        {
            if (worldTick != null) return new SqliteSession(worldTick, false);
            return new SqliteSession(CreateConnection(), true);
        }

        private static void ApplyDerivedAbility(FootballerDefinition player, int goalkeeping, int birthDay, int currentDay)
        {
            player.Skills.Goalkeeping = goalkeeping;
            player.Overall = PlayerAbility.Overall(player);
            player.Attributes.Age = Math.Max(0, (currentDay - birthDay) / SimulationCalendar.DaysPerYear);
        }

        private sealed class SqliteSession : IDisposable
        {
            public readonly SqliteConnection Connection;
            private readonly bool owns;
            public SqliteSession(SqliteConnection connection, bool owns) { Connection = connection; this.owns = owns; }
            public SqliteCommand CreateCommand() => Connection.CreateCommand();
            public SqliteTransaction BeginTransaction() => Connection.BeginTransaction();
            public SqliteTransaction BeginTransaction(bool deferred) => Connection.BeginTransaction(deferred);
            public static implicit operator SqliteConnection(SqliteSession session) => session.Connection;
            public void Dispose() { if (owns) Connection.Dispose(); }
        }

        private sealed class WorldTickScope : IDisposable
        {
            private readonly SqlitePlaythroughStore store;
            public WorldTickScope(SqlitePlaythroughStore store) { this.store = store; }
            public void Dispose()
            {
                store.worldTick?.Dispose();
                store.worldTick = null;
            }
        }

        private static void InsertMetadata(SqliteConnection connection, SqliteTransaction transaction, PlaythroughMetadata value)
        {
            Execute(connection, transaction, "INSERT INTO PlaythroughMetadata (Id, Seed, SchemaVersion, CurrentDay, CreatedUtcTicks) VALUES ($id, $seed, $schemaVersion, $currentDay, $createdUtcTicks);", "$id", value.Id, "$seed", value.Seed, "$schemaVersion", value.SchemaVersion, "$currentDay", value.CurrentDay, "$createdUtcTicks", value.CreatedUtcTicks);
        }

        private static void InsertLeague(SqliteConnection connection, SqliteTransaction transaction, LeagueDefinition value)
        {
            Execute(connection, transaction, "INSERT INTO Leagues (Id, Name, SeasonNumber) VALUES ($id, $name, $seasonNumber);", "$id", value.Id, "$name", value.Name, "$seasonNumber", value.SeasonNumber);
        }

        private static void InsertTeam(SqliteConnection connection, SqliteTransaction transaction, TeamDefinition value)
        {
            Execute(connection, transaction, "INSERT INTO Teams (Id, LeagueId, Name, ShortName, Strength) VALUES ($id, $leagueId, $name, $shortName, $strength);", "$id", value.Id, "$leagueId", value.LeagueId, "$name", value.Name, "$shortName", value.ShortName, "$strength", value.Strength);
        }

        private static void InsertFootballer(SqliteConnection connection, SqliteTransaction transaction, FootballerDefinition value)
        {
            var skills = value.Skills;
            var attributes = value.Attributes;
            Execute(connection, transaction, "INSERT INTO Footballers (Id, Name, TeamId, Position, PreferredFoot, Age, HeightCentimetres, WeightKilograms, Nationality, Speed, Acceleration, Stamina, StaminaRegen, Dribbling, FirstTouchControl, HitPower, Accuracy, Tackling, Strength) VALUES ($id, $name, $teamId, $position, $preferredFoot, $age, $height, $weight, $nationality, $speed, $acceleration, $stamina, $staminaRegen, $dribbling, $firstTouchControl, $hitPower, $accuracy, $tackling, $strength);", "$id", value.Id, "$name", value.Name, "$teamId", value.TeamId, "$position", (int)value.Position, "$preferredFoot", (int)value.PreferredFoot, "$age", attributes.Age, "$height", attributes.HeightCentimetres, "$weight", attributes.WeightKilograms, "$nationality", attributes.Nationality, "$speed", skills.Speed, "$acceleration", skills.Acceleration, "$stamina", skills.Stamina, "$staminaRegen", skills.StaminaRegen, "$dribbling", skills.Dribbling, "$firstTouchControl", skills.FirstTouchControl, "$hitPower", skills.HitPower, "$accuracy", skills.Accuracy, "$tackling", skills.Tackling, "$strength", skills.Strength);
        }

        private static void InsertFootballerState(SqliteConnection connection, SqliteTransaction transaction, FootballerState value)
        {
            var form = value.Form;
            Execute(connection, transaction, "INSERT INTO FootballerState (FootballerId, LastUpdatedDay, Morale, Fitness, Fatigue, RecentMatchRating, Availability, InjuryDaysRemaining, SuspensionMatchesRemaining) VALUES ($id, $lastUpdatedDay, $morale, $fitness, $fatigue, $recentMatchRating, $availability, $injuryDays, $suspensionMatches);", "$id", value.FootballerId, "$lastUpdatedDay", value.LastUpdatedDay, "$morale", form.Morale, "$fitness", form.Fitness, "$fatigue", form.Fatigue, "$recentMatchRating", form.RecentMatchRating, "$availability", (int)form.Availability, "$injuryDays", form.InjuryDaysRemaining, "$suspensionMatches", form.SuspensionMatchesRemaining);
        }

        private static FootballerDefinition ReadFootballer(SqliteDataReader reader)
        {
            return new FootballerDefinition
            {
                Id = reader.GetString(0), Name = reader.GetString(1), TeamId = reader.IsDBNull(2) ? null : reader.GetString(2), Position = (PositionType)reader.GetInt32(3), PreferredFoot = (PreferredFoot)reader.GetInt32(4),
                Attributes = new FootballerAttributes { Age = reader.GetInt32(5), HeightCentimetres = reader.GetInt32(6), WeightKilograms = reader.GetInt32(7), Nationality = reader.GetString(8) },
                Skills = new FootballerSkills { Speed = reader.GetInt32(9), Acceleration = reader.GetInt32(10), Stamina = reader.GetInt32(11), StaminaRegen = reader.GetInt32(12), Dribbling = reader.GetInt32(13), FirstTouchControl = reader.GetInt32(14), HitPower = reader.GetInt32(15), Accuracy = reader.GetInt32(16), Tackling = reader.GetInt32(17), Strength = reader.GetInt32(18) }
            };
        }

        private static void Execute(SqliteSession connection, SqliteTransaction transaction, string sql, params object[] parameterPairs) =>
            Execute(connection.Connection, transaction, sql, parameterPairs);

        private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params object[] parameterPairs)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                for (var index = 0; index < parameterPairs.Length; index += 2) command.Parameters.AddWithValue((string)parameterPairs[index], parameterPairs[index + 1] ?? DBNull.Value);
                command.ExecuteNonQuery();
            }
        }

    }
}
