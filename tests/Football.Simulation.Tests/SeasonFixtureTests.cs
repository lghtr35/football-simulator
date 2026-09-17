using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Football.Simulation.Data;
using Football.Simulation.Fixtures;
using Football.Simulation.Persistence.Sqlite;
using NUnit.Framework;

namespace Football.Simulation.Tests
{
    public sealed class SeasonFixtureTests
    {
        private static LeagueSeason Season(int teams, int duration = 300)
        {
            var start = new SimulationDate { Year = 2026, Month = 9, Day = 1 };
            return new LeagueSeason
            {
                Id = "premier-2026", LeagueId = "premier", Name = "2026/27",
                StartDate = start,
                EndDate = SimulationCalendar.DateFromDay(SimulationCalendar.DayFromDate(start) + duration),
                TeamIds = Enumerable.Range(1, teams).Select(i => "team-" + i).ToList()
            };
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(10)]
        [TestCase(19)]
        [TestCase(20)]
        public void EveryPairPlaysAtBothVenuesWithoutCollisions(int count)
        {
            var season = Season(count);
            var fixtures = new DoubleRoundRobinStrategy().Generate(season);
            Assert.That(fixtures.Count, Is.EqualTo(count * (count - 1)));
            Assert.That(fixtures.Select(f => f.Id).Distinct().Count(), Is.EqualTo(fixtures.Count));
            foreach (var home in season.TeamIds)
            {
                foreach (var away in season.TeamIds.Where(t => t != home))
                    Assert.That(fixtures.Count(f => f.HomeTeamId == home && f.AwayTeamId == away), Is.EqualTo(1));
                var days = fixtures.Where(f => f.HomeTeamId == home || f.AwayTeamId == home)
                    .Select(f => f.ScheduledDay).OrderBy(d => d).ToArray();
                for (var i = 1; i < days.Length; i++) Assert.That(days[i] - days[i - 1], Is.GreaterThanOrEqualTo(5));
            }
            Assert.That(fixtures.All(f => f.ScheduledDay <= SimulationCalendar.DayFromDate(season.EndDate)), Is.True);
            var expected = fixtures.Select(f => f.Id + f.HomeTeamId + f.AwayTeamId).ToArray();
            season.TeamIds.Reverse();
            Assert.That(new DoubleRoundRobinStrategy().Generate(season).Select(f => f.Id + f.HomeTeamId + f.AwayTeamId), Is.EqualTo(expected));
        }

        [TestCase(85, 5)]
        [TestCase(102, 6)]
        [TestCase(119, 7)]
        public void FitsSeasonWithoutBreakingMinimumGap(int duration, int interval)
        {
            var fixtures = new DoubleRoundRobinStrategy().Generate(Season(10, duration));
            Assert.That(fixtures.First(f => f.Matchday == 2).ScheduledDay - fixtures[0].ScheduledDay, Is.EqualTo(interval));
        }

        [Test]
        public void JittersRoundDatesWhenTheSeasonHasSlack()
        {
            var season = Season(20, 300);
            var fixtures = new DoubleRoundRobinStrategy().Generate(season);
            var roundDays = fixtures.Where(f => f.Matchday == 1).Select(f => f.ScheduledDay).Distinct().Count();
            Assert.That(roundDays, Is.GreaterThan(1));
            Assert.That(fixtures.Max(f => f.ScheduledDay), Is.LessThanOrEqualTo(SimulationCalendar.DayFromDate(season.EndDate)));
            var other = Season(20, 300);
            other.Id = "la-liga-2026";
            other.LeagueId = "la-liga";
            var otherDays = new DoubleRoundRobinStrategy().Generate(other)
                .Where(f => f.Matchday == 1).OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => f.ScheduledDay).ToArray();
            var firstDays = fixtures.Where(f => f.Matchday == 1).OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => f.ScheduledDay).ToArray();
            Assert.That(otherDays, Is.Not.EqualTo(firstDays));
            var again = new DoubleRoundRobinStrategy().Generate(season);
            Assert.That(again.Select(f => f.Id + f.ScheduledDay + f.HomeTeamId), Is.EqualTo(fixtures.Select(f => f.Id + f.ScheduledDay + f.HomeTeamId)));
        }

        [Test]
        public void TightSeasonsKeepASingleDayPerRound()
        {
            var fixtures = new DoubleRoundRobinStrategy().Generate(Season(10, 119));
            Assert.That(fixtures.Where(f => f.Matchday == 1).Select(f => f.ScheduledDay).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void RejectsImpossibleOrDuplicateTeams()
        {
            Assert.Throws<ArgumentException>(() => new DoubleRoundRobinStrategy().Generate(Season(10, 84)));
            var season = Season(3);
            season.TeamIds[1] = season.TeamIds[0];
            Assert.Throws<ArgumentException>(() => new DoubleRoundRobinStrategy().Generate(season));
        }

        [Test]
        public void SeasonStrategyOverridesLeagueDefault()
        {
            var alternate = new EmptyStrategy();
            var generator = new SeasonFixtureGenerator(new DoubleRoundRobinStrategy(), alternate);
            var league = new LeagueDefinition { Id = "premier" };
            var season = Season(4);
            Assert.That(generator.Generate(league, season), Has.Count.EqualTo(12));
            league.FixtureStrategyId = alternate.Id;
            Assert.That(generator.Generate(league, season), Is.Empty);
            season.FixtureStrategyId = "double-round-robin";
            Assert.That(generator.Generate(league, season), Has.Count.EqualTo(12));
        }

        private sealed class EmptyStrategy : ISeasonFixtureStrategy
        {
            public string Id => "custom";
            public IReadOnlyList<Fixture> Generate(LeagueSeason season) => new List<Fixture>();
        }

        [Test]
        public void SeasonAndFixturesSurviveReopeningAndFailedWrites()
        {
            var path = Path.Combine(Path.GetTempPath(), "season-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                var store = new SqlitePlaythroughStore(path);
                var world = WorldGenerator.Create(42);
                store.CreatePlaythrough(new PlaythroughMetadata { Id = "test", Seed = 42, SchemaVersion = 1 },
                    world, WorldGenerator.CreateInitialState(world));
                var season = Season(10);
                var fixtures = new DoubleRoundRobinStrategy().Generate(season);
                store.SaveSeason(season, fixtures);
                var reopened = new SqlitePlaythroughStore(path);
                Assert.That(reopened.LoadSeason(season.Id).StartDate.Day, Is.EqualTo(1));
                Assert.That(reopened.LoadSeason(season.Id).StartDate.Month, Is.EqualTo(9));
                Assert.That(reopened.LoadSeason(season.Id).TeamIds, Has.Count.EqualTo(10));
                Assert.That(reopened.LoadSeasonFixtures(season.Id), Has.Count.EqualTo(90));
                // A duplicate fixture fails after inserting the second season: transaction must roll back.
                var second = Season(10);
                second.Id = "second-season";
                var secondFixtures = new DoubleRoundRobinStrategy().Generate(second).ToList();
                secondFixtures[0].Id = fixtures[0].Id;
                Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => store.SaveSeason(second, secondFixtures));
                Assert.That(reopened.LoadSeason(second.Id), Is.Null);
                Assert.That(reopened.LoadSeasonFixtures(season.Id), Has.Count.EqualTo(90));
            }
            finally
            {
                foreach (var suffix in new[] { "", "-wal", "-shm" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }
        }

        [Test]
        public void CalendarRoundTripsEveryDayInAFixedYear()
        {
            var first = SimulationCalendar.DayFromDate(new SimulationDate { Year = 2026, Month = 1, Day = 1 });
            for (var day = first; day < first + 730; day++)
                Assert.That(SimulationCalendar.DayFromDate(SimulationCalendar.DateFromDay(day)), Is.EqualTo(day));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SimulationCalendar.DayFromDate(new SimulationDate { Year = 2028, Month = 2, Day = 29 }));
        }
    }
}
