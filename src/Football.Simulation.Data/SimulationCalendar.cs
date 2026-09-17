using System;
using System.Collections.Generic;

namespace Football.Simulation.Data
{
    /// <summary>Calendar rules shared by all simulations. Years always have 365 days.</summary>
    public static class SimulationCalendar
    {
        public const int DaysPerYear = 365;

        private readonly static Dictionary<int, int> DaysPerMonth = new Dictionary<int, int> {
            { 1, 31 },
            { 2, 28 },
            { 3, 31 },
            { 4, 30 },
            { 5, 31 },
            { 6, 30 },
            { 7, 31 },
            { 8, 31 },
            { 9, 30 },
            { 10, 31 },
            { 11, 30 },
            { 12, 31 },
        };

        public static SimulationDate DateFromDay(int totalDays)
        {
            if (totalDays < 0) throw new ArgumentOutOfRangeException(nameof(totalDays));
            int year = YearFromDay(totalDays);
            int dayOfYear = DayOfYearFromDay(totalDays);
            int month = MonthFromDayOfYear(dayOfYear);
            for (var previousMonth = 1; previousMonth < month; previousMonth++)
                dayOfYear -= DaysPerMonth[previousMonth];
            return new SimulationDate { Year = year, Month = month, Day = dayOfYear };
        }

        /// <summary>Zero-based calendar day: year 1, January 1 is day 0. No leap years.</summary>
        public static int DayFromDate(SimulationDate date)
        {
            if (date == null) throw new ArgumentNullException(nameof(date));
            if (date.Year < 1 || date.Month < 1 || date.Month > 12 ||
                date.Day < 1 || date.Day > DaysPerMonth[date.Month])
                throw new ArgumentOutOfRangeException(nameof(date), "Invalid fixed-365-day date.");
            var days = checked((date.Year - 1) * DaysPerYear + date.Day - 1);
            for (var month = 1; month < date.Month; month++)
                days = checked(days + DaysPerMonth[month]);
            return days;
        }

        public static int YearFromDay(int totalDays) { return totalDays / DaysPerYear + 1; }
        public static int DaysInMonth(int month)
        {
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));
            return DaysPerMonth[month];
        }
        public static int DayOfYearFromDay(int totalDays) { return totalDays % DaysPerYear + 1; }
        public static int MonthFromDayOfYear(int dayOfYear) { 
            int month = 1;
            while (dayOfYear > DaysPerMonth[month]) {
                dayOfYear -= DaysPerMonth[month];
                month++;
            }
            return month;
        }
    }

    public class SimulationDate
    {
        public int Year { get; set; } = 2026;
        public int Month { get; set; } = 1;
        public int Day { get; set; } = 1;
    }
}
