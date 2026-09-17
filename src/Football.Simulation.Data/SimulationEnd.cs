using System;

namespace Football.Simulation.Data
{
    public enum SimulationEndReason { StopRequested, YearLimitReached }

    // A run ending is different from an actor checkpoint waiting for input.
    public sealed class SimulationEndedException : InvalidOperationException
    {
        public SimulationEndReason Reason { get; }
        public SimulationEndedException(SimulationEndReason reason) : base("Simulation ended: " + reason) { Reason = reason; }
    }
}
