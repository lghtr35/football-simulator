using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Football.Simulation.Api;
using Football.Simulation.Core;
using Football.Simulation.Data;
using Football.Simulation.Persistence.Sqlite;

namespace Football.Simulation.Manual
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var root = AppContext.BaseDirectory;
            var dbPath = args.Length > 0 ? args[0] : Path.Combine(root, "ten-leagues.db");
            if (File.Exists(dbPath))
            {
                Console.WriteLine("Save already exists: {0}", dbPath);
                Console.WriteLine("Delete it first, or pass a new path.");
                return 1;
            }

            var config = WorldCatalog.Create(2026);
            var teams = config.Leagues.Sum(l => l.Teams.Count);
            Console.WriteLine("World: {0} leagues, {1} clubs, 3 seasons, no stop flag.", config.Leagues.Count, teams);
            foreach (var league in config.Leagues)
                Console.WriteLine("  {0}: {1} clubs, prize {2}", league.Name, league.Teams.Count, league.WinnerPrize);

            var options = new SimulationOptions
            {
                MaxYears = 3,
                StopSimulation = false,
                MaxParallelMatches = Math.Max(4, Environment.ProcessorCount - 1),
                BackgroundParallelMatches = 1,
                ReservedCpuCores = 1,
                MemoryBudgetMb = 4096,
                EstimatedMemoryPerMatchMb = 8,
                BatchSize = 100,
                WorldPageSize = 256
            };

            var csvPath = Path.ChangeExtension(dbPath, ".profile.csv");
            var probe = new PerformanceProbe(csvPath);
            var createClock = Stopwatch.StartNew();
            var store = new SqlitePlaythroughStore(dbPath);
            var api = SimulationApi.Create(store, config, options);
            probe.CreateCampaignMs = createClock.ElapsedMilliseconds;
            Console.WriteLine("Save: {0}", dbPath);
            Console.WriteLine("Create campaign: {0}", PerformanceProbe.FormatMs(probe.CreateCampaignMs));
            Console.WriteLine("Profiling AdvanceDays. CSV: {0}", csvPath);
            Console.WriteLine("Start {0:yyyy-MM-dd HH:mm:ss}. Advancing until year limit.", DateTime.Now);

            var dayClock = new Stopwatch();
            var days = 0;
            try
            {
                while (true)
                {
                    var date = api.CurrentDate;
                    var dayOrdinal = SimulationCalendar.DayFromDate(date);
                    var matches = api.Fixtures(new FixtureFilter
                    {
                        FromDay = dayOrdinal,
                        ToDay = dayOrdinal,
                        IsPlayed = false,
                        Limit = 500
                    }).Count;

                    dayClock.Restart();
                    await api.AdvanceDaysAsync(1).ConfigureAwait(false);
                    dayClock.Stop();
                    days++;

                    var completed = api.History().Count;
                    probe.RecordDay(days, date, matches, dayClock.ElapsedMilliseconds, completed);
                    probe.PrintDay(probe.Last);
                    probe.PrintWeekIfDue();
                    probe.PrintMonthIfDue();
                }
            }
            catch (SimulationEndedException ex)
            {
                Console.WriteLine("Ended: {0} after {1} days", ex.Reason, days);
            }

            probe.PrintReport();

            var history = api.History();
            Console.WriteLine();
            Console.WriteLine("Completed seasons: {0}", history.Count);
            foreach (var summary in history)
            {
                var table = api.Standings(summary.SeasonId);
                var winner = table.FirstOrDefault(r => r.TeamId == summary.WinnerTeamId);
                Console.WriteLine("{0}: {1}  {2} pts  {3}-{4}  prize {5}",
                    summary.SeasonId, summary.WinnerTeamId,
                    winner != null ? winner.Points : 0,
                    winner != null ? winner.GoalsFor : 0,
                    winner != null ? winner.GoalsAgainst : 0,
                    summary.Prize);
            }
            Console.WriteLine("End reason: {0}", api.EndReason);
            Console.WriteLine("Final date: {0:0000}-{1:00}-{2:00}", api.CurrentDate.Year, api.CurrentDate.Month, api.CurrentDate.Day);
            return 0;
        }
    }
}
