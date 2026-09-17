using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Football.Simulation.Data;

namespace Football.Simulation.Manual
{
    /// <summary>Host-side timings for AdvanceDays. Does not change the simulator; this is what a game hitch would look like.</summary>
    internal sealed class PerformanceProbe
    {
        private readonly List<DaySample> days = new List<DaySample>(1200);
        private readonly string csvPath;
        private readonly Process process = Process.GetCurrentProcess();
        private int lastCompletedSeasons;
        private long lastSeasonMarkMs;
        private readonly List<long> seasonDurationsMs = new List<long>();

        public long CreateCampaignMs { get; set; }

        public PerformanceProbe(string csvPath)
        {
            this.csvPath = csvPath;
            File.WriteAllText(csvPath,
                "simDay,year,month,day,matches,advanceMs,msPerMatch,workingSetMb,gcHeapMb,gen0,gen1,gen2,completedSeasons\n");
        }

        public void RecordDay(int simDay, SimulationDate date, int matches, long advanceMs, int completedSeasons)
        {
            process.Refresh();
            var sample = new DaySample
            {
                SimDay = simDay,
                Date = date,
                Matches = matches,
                AdvanceMs = advanceMs,
                WorkingSetMb = process.WorkingSet64 / (1024 * 1024),
                GcHeapMb = GC.GetTotalMemory(false) / (1024 * 1024),
                Gen0 = GC.CollectionCount(0),
                Gen1 = GC.CollectionCount(1),
                Gen2 = GC.CollectionCount(2),
                CompletedSeasons = completedSeasons
            };
            days.Add(sample);
            File.AppendAllText(csvPath, sample.ToCsv() + "\n");

            if (completedSeasons > lastCompletedSeasons)
            {
                var mark = TotalAdvanceMs();
                seasonDurationsMs.Add(mark - lastSeasonMarkMs);
                lastSeasonMarkMs = mark;
                lastCompletedSeasons = completedSeasons;
            }
        }

        public void PrintDay(DaySample latest)
        {
            var week = LastWindow(7);
            Console.WriteLine(
                "day {0,4}  {1:0000}-{2:00}-{3:00}  {4,3} matches  {5,8}  {6,-14}  rss {7,4}MB  heap {8,4}MB  week {9}",
                latest.SimDay, latest.Date.Year, latest.Date.Month, latest.Date.Day, latest.Matches,
                FormatMs(latest.AdvanceMs),
                latest.Matches > 0 ? FormatMs(latest.AdvanceMs / latest.Matches) + "/match" : "idle",
                latest.WorkingSetMb, latest.GcHeapMb,
                week == null ? "-" : FormatMs(week.SumMs));
        }

        public DaySample Last => days[days.Count - 1];
        public int Count => days.Count;

        public void PrintWeekIfDue()
        {
            if (days.Count == 0 || days.Count % 7 != 0) return;
            var window = LastWindow(7);
            Console.WriteLine("  WEEK {0,3}  {1}  {2} matches  avg {3}/day  peak {4}  idle {5}  matchdays {6}",
                days.Count / 7, FormatMs(window.SumMs), window.Matches,
                FormatMs(window.MeanMs), FormatMs(window.PeakMs),
                window.IdleDays, window.MatchDays);
        }

        public void PrintMonthIfDue()
        {
            if (days.Count < 2) return;
            var today = days[days.Count - 1].Date;
            var yesterday = days[days.Count - 2].Date;
            if (today.Month == yesterday.Month && today.Year == yesterday.Year) return;
            var month = days.Where(d => d.Date.Year == yesterday.Year && d.Date.Month == yesterday.Month).ToList();
            var window = Summarize(month);
            Console.WriteLine("  MONTH {0:0000}-{1:00}  {2} days  {3}  {4} matches  avg {5}/day  peak {6}",
                yesterday.Year, yesterday.Month, month.Count, FormatMs(window.SumMs),
                window.Matches, FormatMs(window.MeanMs), FormatMs(window.PeakMs));
        }

        public void PrintReport()
        {
            if (days.Count == 0)
            {
                Console.WriteLine("No days were simulated.");
                return;
            }

            var all = Summarize(days);
            var idle = Summarize(days.Where(d => d.Matches == 0).ToList());
            var busy = Summarize(days.Where(d => d.Matches > 0).ToList());
            var weeks = Tumbling(7);
            var months = Tumbling(30);

            Console.WriteLine();
            Console.WriteLine("=== Performance (AdvanceDays only; fixture queries are excluded) ===");
            Console.WriteLine("Create campaign:     {0}", FormatMs(CreateCampaignMs));
            Console.WriteLine("Simulated days:      {0}", days.Count);
            Console.WriteLine("Matches played:      {0}", all.Matches);
            Console.WriteLine("Wall advance:        {0}", FormatMs(all.SumMs));
            Console.WriteLine();
            WriteRow("One calendar day", all);
            if (idle.Count > 0) WriteRow("Idle day (no matches)", idle);
            if (busy.Count > 0) WriteRow("Matchday", busy);
            if (weeks.Count > 0) WriteRow("Skip 7 days (game week)", weeks);
            if (months.Count > 0) WriteRow("Skip 30 days", months);
            if (busy.Matches > 0)
                Console.WriteLine("Per match on matchdays: mean {0}", FormatMs(busy.SumMs / Math.Max(1, busy.Matches)));
            if (seasonDurationsMs.Count > 0)
                Console.WriteLine("Between season awards:  {0} samples  mean {1}  peak {2}",
                    seasonDurationsMs.Count, FormatMs((long)seasonDurationsMs.Average()), FormatMs(seasonDurationsMs.Max()));

            var peak = days.OrderByDescending(d => d.AdvanceMs).First();
            Console.WriteLine();
            Console.WriteLine("Worst hitch: day {0} {1:0000}-{2:00}-{3:00}  {4} matches  {5}  rss {6}MB",
                peak.SimDay, peak.Date.Year, peak.Date.Month, peak.Date.Day, peak.Matches,
                FormatMs(peak.AdvanceMs), peak.WorkingSetMb);
            Console.WriteLine("CSV: {0}", csvPath);
        }

        private long TotalAdvanceMs()
        {
            long sum = 0;
            for (var i = 0; i < days.Count; i++) sum += days[i].AdvanceMs;
            return sum;
        }

        private Window LastWindow(int size)
        {
            if (days.Count < size) return days.Count == 0 ? null : Summarize(days);
            return Summarize(days.Skip(days.Count - size).ToList());
        }

        private Window Tumbling(int size)
        {
            var windows = new List<DaySample>();
            for (var start = 0; start + size <= days.Count; start += size)
            {
                long ms = 0;
                var matches = 0;
                for (var i = 0; i < size; i++)
                {
                    ms += days[start + i].AdvanceMs;
                    matches += days[start + i].Matches;
                }
                windows.Add(new DaySample { AdvanceMs = ms, Matches = matches, SimDay = start / size + 1 });
            }
            return Summarize(windows);
        }

        private static Window Summarize(IReadOnlyList<DaySample> samples)
        {
            var window = new Window { Count = samples.Count };
            if (samples.Count == 0) return window;
            var values = new long[samples.Count];
            long sum = 0;
            long peak = 0;
            for (var i = 0; i < samples.Count; i++)
            {
                var ms = samples[i].AdvanceMs;
                values[i] = ms;
                sum += ms;
                if (ms > peak) peak = ms;
                window.Matches += samples[i].Matches;
                if (samples[i].Matches == 0) window.IdleDays++;
                else window.MatchDays++;
            }
            Array.Sort(values);
            window.SumMs = sum;
            window.PeakMs = peak;
            window.MeanMs = sum / samples.Count;
            window.P50Ms = Percentile(values, 50);
            window.P95Ms = Percentile(values, 95);
            window.MinMs = values[0];
            return window;
        }

        private static long Percentile(long[] sorted, int pct)
        {
            if (sorted.Length == 0) return 0;
            var index = (int)Math.Ceiling(pct / 100.0 * sorted.Length) - 1;
            if (index < 0) index = 0;
            if (index >= sorted.Length) index = sorted.Length - 1;
            return sorted[index];
        }

        private static void WriteRow(string label, Window window)
        {
            Console.WriteLine("{0,-24} n={1,4}  min {2,8}  p50 {3,8}  p95 {4,8}  max {5,8}  mean {6,8}",
                label, window.Count, FormatMs(window.MinMs), FormatMs(window.P50Ms),
                FormatMs(window.P95Ms), FormatMs(window.PeakMs), FormatMs(window.MeanMs));
        }

        internal static string FormatMs(long ms)
        {
            if (ms < 1000) return ms.ToString(CultureInfo.InvariantCulture) + "ms";
            if (ms < 60000) return (ms / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + "s";
            return TimeSpan.FromMilliseconds(ms).ToString(@"h\:mm\:ss");
        }

        internal sealed class DaySample
        {
            public int SimDay;
            public SimulationDate Date;
            public int Matches;
            public long AdvanceMs;
            public long WorkingSetMb;
            public long GcHeapMb;
            public int Gen0, Gen1, Gen2;
            public int CompletedSeasons;

            public string ToCsv()
            {
                return string.Join(",",
                    SimDay, Date.Year, Date.Month, Date.Day, Matches, AdvanceMs,
                    Matches == 0 ? "" : (AdvanceMs / (double)Matches).ToString("0.0", CultureInfo.InvariantCulture),
                    WorkingSetMb, GcHeapMb, Gen0, Gen1, Gen2, CompletedSeasons);
            }
        }

        private sealed class Window
        {
            public int Count, Matches, IdleDays, MatchDays;
            public long SumMs, MinMs, MeanMs, P50Ms, P95Ms, PeakMs;
        }
    }
}
