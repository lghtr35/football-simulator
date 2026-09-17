using System.Threading;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Match
{
    /// <summary>Implementations are stateless and safe to invoke concurrently on separate inputs.</summary>
    public interface IMatchSimulationStrategy
    {
        string Id { get; }
        SimulationMode Mode { get; }
        MatchResult Simulate(MatchInput input, CancellationToken cancellationToken);
    }

    /// <summary>Optional async entry. Core awaits a day's matches together under the worker cap.</summary>
    public interface IAsyncMatchSimulationStrategy : IMatchSimulationStrategy
    {
        System.Threading.Tasks.Task<MatchResult> SimulateAsync(MatchInput input, CancellationToken cancellationToken);
    }

    // Extension for a future pitch engine. The host controls stepping and may inject decisions for any ID.
    public interface IContinuousMatchStrategy
    {
        string Id { get; }
        IMatchSession Start(MatchInput input);
    }

    public interface IMatchSession
    {
        MatchState State { get; }
        void Step(float simulatedSeconds, CancellationToken cancellationToken);
        void ApplyDecision(string footballerId, string action, float targetX, float targetY);
        MatchResult Finish();
    }
}
