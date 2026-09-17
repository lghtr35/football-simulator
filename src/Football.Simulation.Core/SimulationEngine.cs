using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Football.Simulation.Data;
using Football.Simulation.Match;
using Football.Simulation.Life;
using Football.Simulation.Career;
using Football.Simulation.Fixtures;

namespace Football.Simulation.Core
{
    /// <summary>One coordinator per open playthrough. Workers own snapshots; only this coordinator persists.</summary>
    public sealed partial class SimulationEngine
    {
        private readonly IPlaythroughStore store;
        private readonly SimulationOptions options;
        private readonly IMatchSimulationStrategy match;
        private readonly IDailyLifeStrategy life;
        private readonly ISeasonCompletionStrategy completion;
        private readonly IPlayerDevelopmentStrategy development;
        private readonly ITransferStrategy transfers;
        private readonly IWorldEvolutionStore evolution;
        private readonly SeasonFixtureGenerator fixtureGenerator;
        private readonly int firstSeasonYear;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        public SimulationEngine(IPlaythroughStore store, SimulationOptions options = null,
            IMatchSimulationStrategy match = null, IDailyLifeStrategy life = null, ISeasonCompletionStrategy completion = null,
            IPlayerDevelopmentStrategy development = null, ITransferStrategy transfers = null, SimulationHooks hooks = null,
            SeasonFixtureGenerator fixtures = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.options = options ?? new SimulationOptions();
            this.match = match ?? new FastMatchStrategy();
            this.life = life ?? new RecoveryOnlyStrategy();
            this.completion = completion ?? new LeagueWinnerStrategy();
            this.development = development ?? new PlayerDevelopmentStrategy();
            this.transfers = transfers ?? new TransferStrategy();
            evolution = store as IWorldEvolutionStore ?? throw new ArgumentException("Store must support persistent world evolution.");
            if (store.LoadMetadata().SchemaVersion != 3)
                throw new InvalidOperationException("This runner requires a version-3 campaign created through SimulationApi.Create. Older prototype saves are not migrated.");
            this.hooks = hooks;
            fixtureGenerator = fixtures ?? new SeasonFixtureGenerator(new DoubleRoundRobinStrategy());
            firstSeasonYear = store.LoadFirstSeasonYear();
            this.options.Resolve(WorkloadMode.AdvanceDays, Environment.ProcessorCount);
            if (this.match.Mode != SimulationMode.Discrete)
                throw new ArgumentException("Daily/background jobs require a discrete strategy. Continuous sessions are host-driven.");
        }

        public SimulationDate CurrentDate => SimulationCalendar.DateFromDay(store.LoadMetadata().CurrentDay);
        public SimulationEndReason? EndReason => options.StopSimulation ? SimulationEndReason.StopRequested :
            options.MaxYears.HasValue && store.LoadUnfinishedSeasons().Count == 0 ? (SimulationEndReason?)SimulationEndReason.YearLimitReached : null;

        private void ThrowIfEnded()
        {
            options.Resolve(WorkloadMode.AdvanceDays, Environment.ProcessorCount);
            var reason = EndReason;
            if (reason.HasValue) throw new SimulationEndedException(reason.Value);
        }

        public async Task AdvanceDaysAsync(int days, CancellationToken token = default)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
            await gate.WaitAsync(token).ConfigureAwait(false);
            try { for (var i = 0; i < days; i++) await AdvanceDay(token).ConfigureAwait(false); }
            finally { gate.Release(); }
        }

        public async Task<Fixture> AdvanceUntilNextFixtureAsync(string teamId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(teamId)) throw new ArgumentException("Team ID required.");
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var day = store.LoadMetadata().CurrentDay;
                ThrowIfEnded();
                var next = store.QueryFixtures(new FixtureFilter { TeamId = teamId, FromDay = day, IsPlayed = false, Limit = 1 }).FirstOrDefault();
                if (next == null) return null;
                while (store.LoadMetadata().CurrentDay < next.ScheduledDay) await AdvanceDay(token).ConfigureAwait(false);
                return next; // Stop before playing this fixture or any others on its date.
            }
            finally { gate.Release(); }
        }

        public async Task<SeasonSummary> RunSeasonAsync(string seasonId, CancellationToken token = default)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var season = store.LoadUnfinishedSeasons().FirstOrDefault(s => s.Id == seasonId);
                if (season == null)
                    return store.LoadSeasonHistory().FirstOrDefault(s => s.SeasonId == seasonId)
                        ?? throw new ArgumentException("Unknown season.");
                var lastDay = SimulationCalendar.DayFromDate(season.EndDate);
                while (store.LoadMetadata().CurrentDay <= lastDay)
                {
                    await AdvanceDay(token).ConfigureAwait(false);
                    var summary = store.LoadSeasonHistory().FirstOrDefault(s => s.SeasonId == seasonId);
                    if (summary != null) return summary;
                }
                throw new InvalidOperationException("Season did not finish by its configured end date.");
            }
            finally { gate.Release(); }
        }

        public async Task<int> SimulateBackgroundAsync(string interactiveFixtureId, CancellationToken token = default)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var metadata = store.LoadMetadata();
                ThrowIfEnded();
                RequireTodayFixture(interactiveFixtureId, metadata.CurrentDay);
                return await SimulateBatches(metadata, WorkloadMode.InteractiveBackground, interactiveFixtureId, token).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }

        public async Task<MatchInput> PrepareInteractiveAsync(string fixtureId, CancellationToken token = default)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var metadata = store.LoadMetadata();
                ThrowIfEnded();
                var fixture = RequireTodayFixture(fixtureId, metadata.CurrentDay);
                AdvanceWorld(metadata, token);
                var teams = store.LoadTeams(new[] { fixture.HomeTeamId, fixture.AwayTeamId });
                return PrepareMatch(fixture, teams, metadata.Seed);
            }
            finally { gate.Release(); }
        }

        public async Task CompleteInteractiveAsync(MatchResult result, CancellationToken token = default)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                var metadata = store.LoadMetadata();
                ThrowIfEnded();
                var fixture = RequireTodayFixture(result.FixtureId, metadata.CurrentDay);
                AdvanceWorld(metadata, token);
                var teams = store.LoadTeams(new[] { fixture.HomeTeamId, fixture.AwayTeamId });
                var input = PrepareMatch(fixture, teams, metadata.Seed);
                ValidatePlayers(input, result);
                MatchConsequences.Apply(input, result);
                ThrowIfEnded();
                store.CommitMatches(metadata.CurrentDay, new[] { result });
            }
            finally { gate.Release(); }
        }

        private Fixture RequireTodayFixture(string id, int day)
        {
            // Paged so large worlds do not silently omit a fixture beyond the first page.
            for (var offset = 0; ; offset += 1000)
            {
                var page = store.QueryFixtures(new FixtureFilter { FromDay = day, ToDay = day, IsPlayed = false, Limit = 1000, Offset = offset });
                var fixture = page.FirstOrDefault(f => f.Id == id);
                if (fixture != null) return fixture;
                if (page.Count < 1000) throw new InvalidOperationException("Fixture is not unplayed on the current date.");
            }
        }

        private async Task AdvanceDay(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfEnded();
            var metadata = store.LoadMetadata();
            await SimulateBatches(metadata, WorkloadMode.AdvanceDays, null, token).ConfigureAwait(false);
            var summaries = new List<SeasonSummary>();
            foreach (var season in store.LoadUnfinishedSeasons())
            {
                if (SimulationCalendar.DayFromDate(season.StartDate) > metadata.CurrentDay) continue;
                if (store.QueryFixtures(new FixtureFilter { SeasonId = season.Id, IsPlayed = false, Limit = 1 }).Count == 0)
                {
                    var summary = completion.Complete(season, store.LoadStandings(season.Id), metadata.CurrentDay);
                    summaries.Add(summary);
                    Checkpoint(new SimulationCheckpoint { Stage = SimulationStage.AfterSeasonCompletion, Day = metadata.CurrentDay, CompletedSeason = summary });
                }
            }
            var nextSeasons = new List<SeasonSchedule>();
            if (!options.StopSimulation)
                foreach (var summary in summaries)
                {
                    var previous = store.LoadSeason(summary.SeasonId);
                    var year = checked(previous.StartDate.Year + 1);
                    if (options.MaxYears.HasValue && (long)year >= (long)firstSeasonYear + options.MaxYears.Value) continue;
                    var next = new LeagueSeason { Id = previous.LeagueId + ":" + year, LeagueId = previous.LeagueId,
                        Name = year + "/" + (previous.EndDate.Year + 1), WinnerPrize = previous.WinnerPrize,
                        StartDate = new SimulationDate { Year = year, Month = previous.StartDate.Month, Day = previous.StartDate.Day },
                        EndDate = new SimulationDate { Year = checked(previous.EndDate.Year + 1), Month = previous.EndDate.Month, Day = previous.EndDate.Day },
                        FixtureStrategyId = previous.FixtureStrategyId, TeamIds = previous.TeamIds.ToList() };
                    // A host may already have authored the next season. Never replace it.
                    if (store.LoadSeasonForYear(next.LeagueId, year) != null) continue;
                    var league = new LeagueDefinition { Id = previous.LeagueId, FixtureStrategyId = previous.FixtureStrategyId };
                    nextSeasons.Add(new SeasonSchedule { Season = next, Fixtures = fixtureGenerator.Generate(league, next) });
                }
            token.ThrowIfCancellationRequested();
            if (options.StopSimulation) nextSeasons.Clear();
            store.CompleteDay(metadata.CurrentDay, summaries, nextSeasons);
        }

        private async Task<int> SimulateBatches(PlaythroughMetadata metadata, WorkloadMode mode, string excluded, CancellationToken token)
        {
            AdvanceWorld(metadata, token);
            var limits = options.Resolve(mode, Environment.ProcessorCount);
            var total = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                ThrowIfEnded();
                var fixtures = new List<Fixture>();
                SimulationCheckpoint pending = null;
                var prepared = new List<MatchInput>();
                for (var offset = 0; fixtures.Count == 0; offset += limits.Batch)
                {
                    var page = store.QueryFixtures(new FixtureFilter { FromDay = metadata.CurrentDay, ToDay = metadata.CurrentDay,
                        IsPlayed = false, ExcludeFixtureId = excluded, Limit = limits.Batch, Offset = offset });
                    var pageIds = page.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToArray();
                    if (pageIds.Distinct().Count() != pageIds.Length) throw new InvalidOperationException("A team has colliding fixtures on this date.");
                    var pageTeams = page.Count == 0 ? null : store.LoadTeams(pageIds);
                    foreach (var fixture in page)
                    {
                        try { prepared.Add(PrepareMatch(fixture, pageTeams, metadata.Seed)); fixtures.Add(fixture); }
                        catch (SimulationPausedException pause) { if (pending == null) pending = pause.Checkpoint; }
                    }
                    if (page.Count < limits.Batch)
                    {
                        if (fixtures.Count == 0 && mode == WorkloadMode.AdvanceDays && pending != null) throw new SimulationPausedException(pending);
                        break;
                    }
                }
                if (fixtures.Count == 0) return total;
                var inputs = prepared.ToArray();
                var results = await SimulateMatchesAsync(fixtures, inputs, limits.Workers, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                ThrowIfEnded();
                // Fixture order, never worker completion order, determines commit order.
                store.CommitMatches(metadata.CurrentDay, results);
                total += results.Length;
            }
        }

        private async Task<MatchResult[]> SimulateMatchesAsync(IReadOnlyList<Fixture> fixtures,
            MatchInput[] inputs, int workers, CancellationToken token)
        {
            var gate = new SemaphoreSlim(Math.Max(1, workers));
            var asyncMatch = match as IAsyncMatchSimulationStrategy;
            var tasks = new Task<MatchResult>[fixtures.Count];
            for (var i = 0; i < fixtures.Count; i++)
                tasks[i] = Run(i);
            try { return await Task.WhenAll(tasks).ConfigureAwait(false); }
            finally { gate.Dispose(); }

            async Task<MatchResult> Run(int index)
            {
                await gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    var fixture = fixtures[index];
                    var input = inputs[index];
                    var result = asyncMatch != null
                        ? await asyncMatch.SimulateAsync(input, token).ConfigureAwait(false)
                        : await Task.Run(() => match.Simulate(input, token), token).ConfigureAwait(false);
                    if (result.FixtureId != fixture.Id) throw new InvalidOperationException("Strategy returned another fixture.");
                    ValidatePlayers(input, result);
                    MatchConsequences.Apply(input, result);
                    return result;
                }
                finally { gate.Release(); }
            }
        }

        private void AdvanceWorld(PlaythroughMetadata metadata, CancellationToken token)
        {
            using (evolution.BeginWorldTick())
            {
                ThrowIfEnded();
                var day = metadata.CurrentDay;
                if (evolution.IsMarketDayComplete(day)) return;
                Checkpoint(new SimulationCheckpoint { Stage = SimulationStage.BeforeDay, Day = day });
                ThrowIfEnded();
                var rules = evolution.LoadWorldRules();
                rules.Validate();
                evolution.StampStableLife(day);
                string after = null;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var page = evolution.LoadLifePage(after, options.WorldPageSize, day);
                    if (page.Count == 0) break;
                    foreach (var player in page)
                    {
                        Checkpoint(new SimulationCheckpoint { Stage = SimulationStage.BeforeLife, Day = day, LifeState = player });
                        life.Advance(player, day);
                    }
                    ThrowIfEnded();
                    evolution.CommitLife(day, page);
                    after = page[page.Count - 1].FootballerId;
                }
                after = null;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var page = evolution.LoadDevelopmentPage(after, options.WorldPageSize, day - rules.DevelopmentIntervalDays);
                    if (page.Count == 0) break;
                    var updates = new List<DevelopmentUpdate>();
                    foreach (var player in page)
                    {
                        player.Definition.IsActor = IsActor(PersonKind.Player, player.Definition.Id);
                        Checkpoint(new SimulationCheckpoint { Stage = SimulationStage.BeforeDevelopment, Day = day, Development = player });
                        var update = development.Advance(player, day, rules, metadata.Seed);
                        if (update != null) updates.Add(update);
                    }
                    ThrowIfEnded();
                    evolution.CommitDevelopment(day, updates);
                    after = page[page.Count - 1].Definition.Id;
                }
                token.ThrowIfCancellationRequested();
                if (evolution.TryCommitIdleMarket(day, rules)) return;
                var market = evolution.LoadMarket();
                Checkpoint(new SimulationCheckpoint { Stage = SimulationStage.BeforeMarket, Day = day, Market = market });
                ThrowIfEnded();
                var marketUpdate = transfers.Advance(market, rules, day);
                ThrowIfEnded();
                evolution.CommitMarket(day, marketUpdate);
            }
        }

        private static void ValidatePlayers(MatchInput input, MatchResult result)
        {
            var eligible = input.Home.RegisteredPlayers.Concat(input.Away.RegisteredPlayers).ToDictionary(p => p.Definition.Id, p => p.Definition.TeamId);
            var starters = input.Home.Lineup.Concat(input.Away.Lineup).Select(p => p.Player.Definition.Id);
            foreach (var team in new[] { input.Home, input.Away })
            {
                var players = result.Players.Where(p => p.TeamId == team.Squad.Definition.Id).ToList();
                if (players.Count > team.Lineup.Count + team.Squad.Definition.MaxSubstitutions || players.Sum(p => p.Minutes) > 990)
                    throw new ArgumentException("Performance exceeds substitution or minute limits.");
            }
            if (result.Players.Select(p => p.FootballerId).Distinct().Count() != result.Players.Count ||
                (!result.Forfeit && starters.Except(result.Players.Select(p => p.FootballerId)).Any()) ||
                result.Players.Any(p => !eligible.TryGetValue(p.FootballerId, out var team) || team != p.TeamId ||
                    p.Started != starters.Contains(p.FootballerId) || p.Minutes < 0 || p.Minutes > 90 || p.Goals < 0 || p.Assists < 0 || p.InjuryDays < 0 ||
                    p.YellowCards < 0 || p.YellowCards > 2 || p.RedCards < 0 || p.RedCards > 1 || p.Rating < 0 || p.Rating > 100))
                throw new ArgumentException("Performance includes duplicate or ineligible players.");
        }
    }
}
