# Running a playthrough

## Small complete season

```csharp
using Football.Simulation.Api;
using Football.Simulation.Core;
using Football.Simulation.Data;
using Football.Simulation.Persistence.Sqlite;

var config = WorldGenerator.Example(seed: 42, teamCount: 10);
config.StartYear = 2026;
config.UnemployedCoaches = 4;
config.Leagues[0].SeasonStartMonth = 9;
config.Leagues[0].SeasonEndMonth = 6;
config.Leagues[0].WinnerPrize = 100_000; // Abstract currency units, not real money.
config.Leagues[0].Teams[0].TargetOverall.Attack = 80;

var store = new SqlitePlaythroughStore("career.db"); // Choose a new save.
var api = SimulationApi.Create(store, config, new SimulationOptions
{
    MaxParallelMatches = 4,
    BackgroundParallelMatches = 1,
    ReservedCpuCores = 1,
    MemoryBudgetMb = 128,
    EstimatedMemoryPerMatchMb = 8,
    BatchSize = 16
});
var summary = await api.RunSeasonAsync("premier:2026");
var table = api.Standings(summary.SeasonId);
var unemployed = api.Coaches(unemployedOnly: true);
```

The generator accepts authored configuration objects, creates one employed coach per team and the
configured pool of unemployed coaches, and uses positional overall targets to produce actual squads.
Each position group's mean overall equals its target. Detailed skills vary around overall.
Minimum squad composition supports 4-4-2: one goalkeeper, four defenders, four midfielders, two attackers.
The default has 22 players (2/8/8/4) and eight free-agent players across the world. Clubs have a hard
32-player/one-coach cap, enforced in both rules and SQLite. Configuration is validated before save creation.

Season starts on day one of its start month and ends on the last day of its end month.
An end month before the start month means the following year. Equal months mean the same month/year.
The generator rejects dates too short for its five-day minimum. Each league has its own months;
the world starts at the earliest league start. All dates use the fixed 365-day calendar.
Double-round-robin rounds sit on a 5–7 day interval. Leftover days become a deterministic per-fixture
jitter (keyed by league and season id) so a round is not forced onto one date. A team’s own matches
stay at least five days apart.

## Resume and inspect

```csharp
var api = new SimulationApi(new SqlitePlaythroughStore("career.db"));
await api.AdvanceDaysAsync(3);
var recent = api.Fixtures(new FixtureFilter
{
    TeamId = "team-1",
    IsPlayed = true,
    FromDay = SimulationCalendar.DayFromDate(new SimulationDate { Year = 2026, Month = 9, Day = 1 }),
    Limit = 20,
    Offset = 0
});
var events = api.MatchEvents(recent[0].Id);
var performances = api.MatchPerformances(recent[0].Id); // Rating is null if disabled.
var history = api.History();
```

Fixture queries also accept league, season, date range and played/unplayed filters. They are paginated
and sorted oldest first. Results, events, performances, standings and season awards remain in SQLite.
Season rankings use points, goal difference, goals scored, then team ID as a deterministic last tie-break.
Completion is recorded when all fixtures finish (which may be before the configured end month).
The winner prize is credited once. Calling RunSeasonAsync again returns the recorded result.
Completion automatically creates next year's season, fixtures and empty standings, preserving previous
results and history. Team membership, dates, prize and effective fixture strategy carry forward. A season
already authored for that league/start year is not overwritten. There is no promotion/relegation yet.

## Multi-year runs and ending the simulation

```csharp
var options = new SimulationOptions { MaxYears = 2 }; // null means no year limit
var api = SimulationApi.Create(store, WorldGenerator.Example(42, 4), options);
await api.RunSeasonAsync("premier:2026"); // also creates premier:2027
await api.RunSeasonAsync("premier:2027"); // no premier:2028
var reason = api.EndReason;              // YearLimitReached once all leagues finish
```

MaxYears counts **season-start calendar years**, relative to the world's earliest saved season. With a
2026 start and MaxYears=2, seasons starting in 2026 and 2027 are allowed; a 2027/28 season can finish in
2028. It is not a strict 730-day cutoff. All leagues finish their final allowed season before the year-limit
ending is reported. Keep this configuration with the game save and pass it again when reopening the world;
it is not stored by the simulator. Configure it before generating future seasons, not retroactively after
later seasons have already been scheduled.

Set `options.StopSimulation = true` to request an end at a safe processing boundary. An existing committed
batch remains committed. To end specifically after awarding a season, the game's `AfterSeasonCompletion`
hook (or its completion strategy) can set the flag. This hook runs after the strategy, before persistence;
return true from it to accept completion. The award and date advance then commit, but no successor
is created. The simulator does not interpret that flag as retirement or any other game-specific ending.

`EndReason` is null while running, `StopRequested` for the flag, or `YearLimitReached` after the allowed
seasons finish. Further advancement throws `SimulationEndedException` with that reason; read-only result
queries and retrieving an already-completed season still work. This differs from `SimulationPausedException`,
which requests game input. Clearing a flag before completion can resume unfinished work; skipped rollover
at a terminal completion is not retroactively generated by clearing the flag or increasing the limit.

Season awards, successor fixtures/standings and date advancement share one transaction. If generation or
validation fails, completion remains pending and can be retried without duplicate awards. Match results
already committed earlier that day remain saved. A custom fixture generator must also be supplied when
reopening a save that uses custom strategy IDs (`new SimulationApi(store, fixtures: generator)`).
Old saves whose season was already completed before rollover existed need their successor authored through
`SaveSeason`; this feature runs when a season completes, not as a rewrite of past history.

## Interactive game plus background simulations

```csharp
var fixture = await api.AdvanceUntilNextFixtureAsync("team-1");
if (fixture != null)
{
    var input = await api.PrepareInteractiveAsync(fixture.Id);
    var background = api.SimulateBackgroundAsync(fixture.Id);
    // A future IContinuousMatchStrategy starts a host-driven session from input.
    // Until then, fast simulation is also a usable stand-in:
    var result = new Football.Simulation.Match.FastMatchStrategy().Simulate(input);
    await background;
    await api.CompleteInteractiveAsync(result);
    await api.AdvanceDaysAsync(1); // Finish this date and move to the next.
}
```

Only the game decides which footballer is controlled. Match inputs contain neutral player IDs,
eligible 11-player lineups and normalized pitch coordinates. A team with fewer than seven eligible
players forfeits (3-0; both short-handed teams produce 0-0). Match squads contain the lineup and bench,
up to the league's MatchSquadSize (19–32; default 20). Availability can leave a squad below that limit.
A 4-4-2 picker ranks eligible players by overall, fitness, fatigue and performance form; missing positions
use outfield fallbacks. It reserves a backup goalkeeper on the bench when available.
The fast strategy replaces injured players and makes fatigue-prioritized changes at minutes 60/70/80,
up to MaxSubstitutions (0–5). Sent-off players are not replaced. Played minutes and starts are tracked separately.
It models expected goals, fatigue/form/morale,
tactics, coach ability, fan support, goals/assists, yellow/red cards and injuries.
The continuous session interface supports stepping and decisions; a moving 11v11 pitch engine is not implemented.
The host must await CompleteInteractiveAsync before advancing the date.

## Strategies, resources and persistence

- Match: inject a stateless, thread-safe `IMatchSimulationStrategy` for discrete jobs.
  `IContinuousMatchStrategy` / `IMatchSession` are host-driven extension contracts.
- Fixtures: register `ISeasonFixtureStrategy` implementations in `SeasonFixtureGenerator`, passed to Create.
  League configuration selects the default; `SeasonFixtureStrategyId` overrides it for the generated season.
- Life: inject `IDailyLifeStrategy` for per-player daily recovery and `IPlayerDevelopmentStrategy` for
  periodic training/ageing. Fully recovered players (no fatigue, full fitness, no injury or ban) are
  date-stamped in one UPDATE. Core pages only the rest, including free agents, in WorldPageSize-sized
  pages (default 128). Each committed page is checkpointed for safe resume. A world tick reuses one
  SQLite writer connection for life, development and market; match commits still open their own.
- Career: inject `ISeasonCompletionStrategy` for awards; basic match consequences update player/team state.
  `ITransferStrategy` handles deterministic contract, transfer and daily cashflow decisions. Cashflow
  updates club balances but is not written to WorldHistory. Days with no expiring contracts and no
  open-window transfer tick apply cash in SQL without loading the market snapshot.

Core owns daily progression and orchestration. It pages fixtures, loads a bounded batch of teams with
set-based SQL queries, simulates separate snapshots concurrently, and commits in fixture order.
Fast-match work is awaited together under `MaxParallelMatches` (`IAsyncMatchSimulationStrategy` when
the strategy provides it; otherwise `Task.Run` around `Simulate`). Seeds derive from the saved world
seed and fixture ID, never task ordering or process-specific hash codes.
Worker count is limited by CPU reserve, profile cap and estimated memory capacity. Loaded batch size is
also limited by that estimate. This is admission control, not a hard memory/CPU quota; tune estimates by profiling.
Background mode uses its smaller worker cap and holds the selected fixture/date open.

Each batch transaction applies scores, standings, individual performances, events and player/team changes.
Duplicate/stale results are rejected. Cancellation discards an unfinished batch; previously committed batches
remain, and resume selects only unplayed fixtures. Daily completion advances the date and awards titles/prizes
atomically. Use one API/coordinator per save; concurrent independent coordinators are not supported.
Keep SQLite access outside the Unity render loop; pure simulation workers never call Unity APIs.

Version-3 saves are created through SimulationApi.Create. Start a new save for this feature set.
Version-1/2 prototype saves are not automatically migrated; the runner rejects them without overwriting them.
SQLite is currently tested through .NET on Windows. Api/Core and rule libraries are Unity-independent;
the Unity SQLite managed/native dependency deployment still needs platform integration and validation.

## Development, economy and optional ratings

Configure rules before creating the save; they are persisted rather than regenerated on resume:

```csharp
config.FreeAgentPlayers = 16;
config.Leagues[0].MatchSquadSize = 20;
config.Leagues[0].MaxSubstitutions = 5;
config.Rules.DevelopmentIntervalDays = 7;
config.Rules.TransferIntervalDays = 7;
config.Rules.DefaultTraining = 60;       // 0–100
config.Rules.ContractLengthDays = 365;
config.Rules.InitialCash = 1_000_000;
config.Rules.DailyIncome = 12_000;
config.Rules.DailyWageBudget = 10_000;
config.Rules.TransferWindows.Clear();
config.Rules.TransferWindows.Add(new TransferWindow { StartDayOfYear = 1, EndDayOfYear = 31 });
config.Rules.TransferWindows.Add(new TransferWindow { StartDayOfYear = 182, EndDayOfYear = 243 });
```

Window bounds are inclusive, shared by all leagues and may wrap across year-end. Transfer decisions
run when the absolute calendar day is divisible by TransferIntervalDays, within an open window.
Each club attempts one player addition and one coach hire/upgrade per market tick; a final pass fills
affordable coaching vacancies. Prices and daily wages derive from ability and, for player value, age.
There are no negotiations. Purchases require cash for the fee plus 30 days of the new wage and must
fit the wage budget. Player sales retain at least 19 players and coverage of 2 GK / 5 DEF / 5 MID / 3 ATT.
An expiring contract renews if affordable; otherwise the person becomes a free agent. Expiry/renewal
and daily wages/income happen outside transfer windows too. Cash may become negative from payroll;
indebted clubs cannot make new signings. Initial authored squads are not automatically trimmed to budget.

Younger players have greater growth probability. Training intensity, match minutes since the last
development tick and performance form raise growth and reduce age-related decline; neither outcome is
guaranteed. A tick can change one skill by one point, bounded to 1–100. Overall is derived from positional
skills on load; `PlayerRatings.Overall` is a cache written only when a skill actually changes.
Age is derived from birth day and the current calendar day when a player is loaded.
Ratings and development/form RNG are independent: disabling match ratings does not change future results or development.

```csharp
api.SetTraining("team-1-player-1", 80);
var contractsAndValues = api.Market();
var changes = api.WorldHistory(fromDay, toDay, personId: "team-1-player-1");

// Disabled by default. Integer 74 means a display rating of 7.4/10.
var ratedApi = new SimulationApi(store,
    match: new Football.Simulation.Match.FastMatchStrategy(calculatePlayerRatings: true));
// Use only one coordinator instance actively per save.
```

WorldHistory returns at most 1000 newest entries in a date range; narrow the range/person for longer histories.
Transfers, renewals/releases and actual skill changes are persisted transactionally. Daily cash is applied
to club balances without a per-club history row.
The compact market snapshot currently covers the whole world: match memory admission limits do not cap
that snapshot. For very large worlds, replace the MVP market scan with indexed candidate queries.

Standalone match callers can pass explicit player-ID registrations to `MatchPreparation.Prepare` as
`homeRegistration`/`awayRegistration`; the shared campaign runner auto-registers unless the host supplies
`HomeSelection`/`AwaySelection` at the `BeforeMatch` checkpoint.
The supplied lists must contain available club players within the league cap (at least 19 when enough
are available). Season-long registration lists, loans, negotiations, retirement/replacement youth intake
and a detailed training activity UI are deferred.

## Game-owned actor stops and decisions

The simulation does not define sponsor offers, relationships, shopping, manager menus, decision IDs,
delegation buttons, or game-save formats. It exposes neutral boundaries through `SimulationHooks`:
`BeforeDay`, `BeforeLife` (only players who still need recovery), `BeforeDevelopment`, `BeforeMarket`
(skipped on idle cash-only market days), `BeforeMatch`, and `AfterSeasonCompletion`.
The game decides which boundaries matter, whether an actor needs input, and how to save that input.

```csharp
// These variables represent state owned by the GAME, restored from its save if necessary.
string controlledCoachId = "coach:team-1";
var gameSquads = new Dictionary<string, MatchSquadSelection>(); // Example host state, not a sim service.
var hooks = new SimulationHooks
{
    IsActor = (kind, id) => kind == PersonKind.Coach && id == controlledCoachId,
    TryContinue = checkpoint =>
    {
        if (checkpoint.Stage != SimulationStage.BeforeMatch) return true;
        bool home = checkpoint.Home.Coach?.IsActor == true;
        bool away = checkpoint.Away.Coach?.IsActor == true;
        if (!home && !away) return true;
        if (!gameSquads.TryGetValue(checkpoint.Fixture.Id, out var chosen)) return false;
        if (home) checkpoint.HomeSelection = chosen;
        else checkpoint.AwaySelection = chosen;
        return true;
    }
};
var api = new SimulationApi(store, hooks: hooks);
var stopped = await api.AdvanceUntilStopAsync(30);
// If stopped != null: present the game's UI, record its decision in the game save,
// populate gameSquads with its chosen starters/bench, then invoke the runner again.
```

`IsActor` on player/coach definitions is a runtime annotation populated from the host's resolver for
match snapshots (and player development). It is not a simulation database column. No callback means
fully autonomous simulation. There is no built-in "delegate to AI" command or saved pending-decision registry.
`MatchSquadSelection` is only a validated input DTO containing IDs, not a selection service or game save.

`TryContinue` runs on the coordinator, before workers. Return false to stop before that step is committed;
on retry, mutate the supplied input snapshot or supply squad inputs and return true. Do not replace snapshot
references or re-enter coordinator APIs from the callback. Game-specific payloads are translated by the game
into simulation inputs; custom simulation rules can still be supplied through the existing strategy interfaces.
For example, a game's training choice can set `checkpoint.Development.Training` before development runs.
Market inputs still pass through the chosen transfer strategy: an input hook is not a transaction editor.

Callbacks can repeat when retrying a page/batch, after cancellation, and during interactive preparation and
completion. Keep decisions replayable until the corresponding simulation result commits; the game owns their
keys and idempotency. No whole-game save coordination is attempted by these libraries. A game save must
coordinate its world snapshot with its actor identity and pending/applied decisions.

`AdvanceUntilStopAsync` returns a checkpoint or null after completing the requested number of days. Existing
advance/run methods surface `SimulationPausedException.Checkpoint`. Already committed work remains committed.
Other matches may complete on the stopped date; the date itself cannot advance past an unplayed fixture.
Background simulation skips stopped fixtures, including ones other than the explicitly excluded fixture.
For interactive play, the game supplies/accepts the `BeforeMatch` inputs, calls `PrepareInteractiveAsync`,
drives its match session, and submits the result through `CompleteInteractiveAsync`. In-match choices belong
to that game/session, not to new sponsor/training/gameplay enums in Core.

## Runnable examples and tests

From the repository root:

```powershell
dotnet run --project src/Football.Simulation.Cli -- create career.db 10 42
dotnet run --project src/Football.Simulation.Cli -- resume career.db
dotnet test Football.Simulation.sln
```

The console only calls the shared API. See `SimulationIntegrationTests.cs` for small complete seasons,
parallel versus sequential determinism, resume, interactive/background coordination, transactional
rollback, cancellation, multiple leagues and year transitions.
`DevelopmentAndEconomyTests.cs` covers development, windows, transfers, budgets, database roster caps,
rotation/substitutions, null ratings, deterministic rating toggles and world-update checkpoints.
