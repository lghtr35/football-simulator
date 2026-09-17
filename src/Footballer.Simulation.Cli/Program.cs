using System;
using System.IO;
using System.Threading.Tasks;
using BeAFootballer.Simulation.Api;
using BeAFootballer.Simulation.Data;
using BeAFootballer.Simulation.Persistence.Sqlite;
namespace BeAFootballer.Simulation.Cli
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            if (args.Length < 2 || (args[0] != "create" && args[0] != "resume"))
            {
                Console.WriteLine("Example: create <new.db> [team-count=10] [seed=42] | resume <existing.db>");
                return;
            }
            var store = new SqlitePlaythroughStore(args[1]);
            SimulationApi api;
            if (args[0] == "create")
            {
                if (File.Exists(args[1])) throw new InvalidOperationException("Choose a new database path.");
                var config = WorldGenerator.Example(args.Length > 3 ? int.Parse(args[3]) : 42, args.Length > 2 ? int.Parse(args[2]) : 10);
                api = SimulationApi.Create(store, config);
            }
            else
            {
                if (!File.Exists(args[1])) throw new FileNotFoundException("Save does not exist.", args[1]);
                api = new SimulationApi(store);
            }
            var summary = await api.RunSeasonAsync("premier:2026");
            Console.WriteLine("Winner: {0}; prize: {1}", summary.WinnerTeamId, summary.Prize);
            foreach (var row in api.Standings(summary.SeasonId))
                Console.WriteLine("{0}: {1} points, {2}-{3} goals", row.TeamId, row.Points, row.GoalsFor, row.GoalsAgainst);
        }
    }
}
