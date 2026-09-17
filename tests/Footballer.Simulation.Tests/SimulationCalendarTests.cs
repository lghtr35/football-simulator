using BeAFootballer.Simulation.Data;
using NUnit.Framework;

namespace BeAFootballer.Simulation.Tests
{
    public sealed class SimulationCalendarTests
    {
        [TestCase(0, 1, 1)]
        [TestCase(364, 1, 365)]
        [TestCase(365, 2, 1)]
        public void DerivesYearAndDayOfYearFromAbsoluteDay(int totalDays, int expectedYear, int expectedDayOfYear)
        {
            Assert.That(SimulationCalendar.YearFromDay(totalDays), Is.EqualTo(expectedYear));
            Assert.That(SimulationCalendar.DayOfYearFromDay(totalDays), Is.EqualTo(expectedDayOfYear));
        }
    }
}
