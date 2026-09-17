# Be A Footballer Simulation

Open `Footballer.Simulation.sln` in Cursor. The shared API and rule libraries target `netstandard2.1`.
Start with [Running a playthrough](RUNNING.md) for complete API and integration-test examples.

Dependency direction: Api → Core → Match/Fixtures/Life/Career → Data.
Persistence.Sqlite implements Data's `IPlaythroughStore` and `IWorldEvolutionStore`; Core accepts these
boundaries and has no SQL dependency. Life owns recovery/development, Career owns transfers/contracts,
and Match owns squad selection, substitutions and optional individual ratings. New campaigns use schema version 3.
The console project is only an example host of the API.

## Unity import

Run `./Build-UnityLibraries.ps1` from this folder. It builds the solution and copies the `netstandard2.1` simulation DLLs to `Game/Assets/Plugins/Simulation`, where Unity automatically imports them. The CLI and test assemblies are never copied to Unity.

Do not use Unity-specific APIs or platform-specific .NET APIs in a simulation library. The game knows the controlled footballer by ID; simulation assemblies treat every footballer equally.

`Footballer.Simulation.Persistence.Sqlite` is the Windows MVP SQLite adapter and is deliberately excluded from Unity DLL import. The simulation models remain portable; a Unity-compatible SQLite binding will implement the same persistence boundary when mobile work begins.

## Dated league seasons

`LeagueSeason` holds an ID, league ID, inclusive start/end dates and the participating team IDs.
`SimulationDate`, calendar helpers, RNG, fixture and match records now live in Data.
Dates use a fixed 365-day year, with ordinary month lengths and no February 29.
`SimulationCalendar.DayFromDate` converts dates to calendar ordinals (year 1, January 1 = 0);
`Fixture.ScheduledDay` and persisted `CurrentDay` use the same ordinals.
CurrentDay is the next date to process, not an elapsed-day counter.

```csharp
var season = new LeagueSeason
{
    Id = "premier-2026",
    LeagueId = league.Id,
    Name = "2026/27",
    StartDate = new SimulationDate { Year = 2026, Month = 9, Day = 1 },
    EndDate = new SimulationDate { Year = 2027, Month = 6, Day = 1 },
    TeamIds = league.Teams.Select(team => team.Id).ToList()
};
var generator = new SeasonFixtureGenerator(new DoubleRoundRobinStrategy());
var fixtures = generator.Generate(league, season);
store.SaveSeason(season, fixtures);
// Later, without regeneration:
var savedSeason = store.LoadSeason(season.Id);
var savedFixtures = store.LoadSeasonFixtures(season.Id);
```

The example uses the Data and Fixtures namespaces and System.Linq.
Each team plays every other team once at home and once away. Rounds begin on the season start date,
normally seven days apart; short seasons use six or five days. An impossible season is rejected.
Odd team counts get byes. Input team order does not affect the generated schedule.

Register additional `ISeasonFixtureStrategy` implementations in `SeasonFixtureGenerator`.
`LeagueDefinition.FixtureStrategyId` selects the league default; a season's non-null
`FixtureStrategyId` overrides it. Existing persisted fixtures do not change when a strategy changes.
SaveSeason creates a new season transactionally and refuses to overwrite one that already exists.
Team collisions and five-day spacing are validated within the season. Multi-competition scheduling,
playoffs, rescheduling and mid-season membership changes are deferred.
