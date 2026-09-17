using System;
using System.Linq;
using System.Collections.Generic;
using Football.Simulation.Data;
using Football.Simulation.Fixtures;

namespace Football.Simulation.Core
{
    public static class CampaignFactory
    {
        public static void Create(IPlaythroughStore store, PlaythroughGenerationConfiguration configuration,
            SeasonFixtureGenerator fixtureGenerator = null)
        {
            var world = WorldGenerator.Create(configuration);
            var generator = fixtureGenerator ?? new SeasonFixtureGenerator(new DoubleRoundRobinStrategy());
            var schedules = new List<SeasonSchedule>();
            foreach (var league in world.Leagues)
            {
                var endYear = configuration.StartYear + (league.SeasonEndMonth < league.SeasonStartMonth ? 1 : 0);
                var season = new LeagueSeason
                {
                    Id = league.Id + ":" + configuration.StartYear, LeagueId = league.Id,
                    Name = configuration.StartYear + "/" + endYear,
                    StartDate = new SimulationDate { Year = configuration.StartYear, Month = league.SeasonStartMonth, Day = 1 },
                    EndDate = new SimulationDate { Year = endYear, Month = league.SeasonEndMonth, Day = SimulationCalendar.DaysInMonth(league.SeasonEndMonth) },
                    FixtureStrategyId = configuration.Leagues.Single(l => l.Id == league.Id).SeasonFixtureStrategyId ?? league.FixtureStrategyId,
                    WinnerPrize = league.WinnerPrize,
                    TeamIds = league.Teams.Select(t => t.Id).ToList()
                };
                var fixtures = generator.Generate(league, season);
                if (fixtures.Count == 0 || fixtures.Select(f => f.Id).Distinct().Count() != fixtures.Count)
                    throw new ArgumentException("A season must have fixtures with unique IDs.");
                foreach (var fixture in fixtures)
                    if (fixture.SeasonId != season.Id || fixture.LeagueId != league.Id || fixture.HomeTeamId == fixture.AwayTeamId ||
                        !season.TeamIds.Contains(fixture.HomeTeamId) || !season.TeamIds.Contains(fixture.AwayTeamId) ||
                        fixture.ScheduledDay < SimulationCalendar.DayFromDate(season.StartDate) ||
                        fixture.ScheduledDay > SimulationCalendar.DayFromDate(season.EndDate))
                        throw new ArgumentException("Fixture strategy produced an invalid fixture.");
                foreach (var team in season.TeamIds)
                {
                    var dates = fixtures.Where(f => f.HomeTeamId == team || f.AwayTeamId == team).Select(f => f.ScheduledDay).OrderBy(d => d).ToArray();
                    if (dates.Length == 0 || dates.Skip(1).Where((day, i) => day - dates[i] < 5).Any())
                        throw new ArgumentException("Each team needs fixtures spaced at least five days apart.");
                }
                schedules.Add(new SeasonSchedule { Season = season, Fixtures = fixtures });
            }
            var firstDay = schedules.Min(s => SimulationCalendar.DayFromDate(s.Season.StartDate));
            store.CreateCampaign(new PlaythroughMetadata { Id = Guid.NewGuid().ToString("N"), Seed = configuration.Seed,
                SchemaVersion = 3, CurrentDay = firstDay, CreatedUtcTicks = DateTime.UtcNow.Ticks },
                world, WorldGenerator.CreateInitialState(world, firstDay), schedules);
        }
    }
}
