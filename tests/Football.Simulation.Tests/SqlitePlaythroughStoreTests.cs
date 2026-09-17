using System;
using System.IO;
using Football.Simulation.Data;
using Football.Simulation.Persistence.Sqlite;
using NUnit.Framework;

namespace Football.Simulation.Tests
{
    public sealed class SqlitePlaythroughStoreTests
    {
        [Test]
        public void PersistsAndQueriesASeededWorld()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), "beafootballer-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var world = WorldGenerator.Create(42);
                var store = new SqlitePlaythroughStore(databasePath);
                store.CreatePlaythrough(new PlaythroughMetadata { Id = "career-1", Seed = 42, SchemaVersion = 1, CreatedUtcTicks = DateTime.UtcNow.Ticks }, world, WorldGenerator.CreateInitialState(world));

                var team = store.LoadTeam("team-1");
                var footballers = store.LoadFootballersForTeam("team-1");

                Assert.That(team, Is.Not.Null);
                Assert.That(team.LeagueId, Is.EqualTo("premier"));
                Assert.That(footballers, Has.Count.EqualTo(22));
            }
            finally
            {
                DeleteIfPresent(databasePath);
                DeleteIfPresent(databasePath + "-wal");
                DeleteIfPresent(databasePath + "-shm");
            }
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
