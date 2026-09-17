using System.Collections.Generic;
using Football.Simulation.Data;

namespace Football.Simulation.Fixtures
{
    public interface ISeasonFixtureStrategy
    {
        string Id { get; }
        IReadOnlyList<Fixture> Generate(LeagueSeason season);
    }

    public sealed class SeasonFixtureGenerator
    {
        private readonly Dictionary<string, ISeasonFixtureStrategy> strategies =
            new Dictionary<string, ISeasonFixtureStrategy>(System.StringComparer.Ordinal);

        public SeasonFixtureGenerator(params ISeasonFixtureStrategy[] strategies)
        {
            foreach (var strategy in strategies) this.strategies.Add(strategy.Id, strategy);
        }

        public IReadOnlyList<Fixture> Generate(LeagueDefinition league, LeagueSeason season)
        {
            if (league == null || season == null) throw new System.ArgumentNullException();
            if (season.LeagueId != league.Id)
                throw new System.ArgumentException("Season must belong to the supplied league.");
            var id = season.FixtureStrategyId ?? league.FixtureStrategyId;
            if (id == null || !strategies.TryGetValue(id, out var strategy))
                throw new System.ArgumentException("Unknown fixture strategy: " + id);
            return strategy.Generate(season);
        }
    }
}
