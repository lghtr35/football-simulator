using System;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Core
{
    public sealed class SimulationOptions
    {
        public int MaxParallelMatches = 4;
        public int BackgroundParallelMatches = 1;
        public int ReservedCpuCores = 1;
        public int MemoryBudgetMb = 128;
        public int EstimatedMemoryPerMatchMb = 8;
        public int BatchSize = 16;
        public int WorldPageSize = 128;
        // Game-owned run controls; restore these from the game's save when reopening the world.
        public volatile bool StopSimulation;
        public int? MaxYears;

        // Admission control based on estimates, not an OS-enforced memory ceiling.
        public (int Workers, int Batch) Resolve(WorkloadMode mode, int processorCount)
        {
            if (MaxParallelMatches < 1 || BackgroundParallelMatches < 1 || ReservedCpuCores < 0 ||
                EstimatedMemoryPerMatchMb < 1 || MemoryBudgetMb < EstimatedMemoryPerMatchMb ||
                BatchSize < 1 || BatchSize > 100 || WorldPageSize < 1 || WorldPageSize > 1000 || MaxYears < 1)
                throw new ArgumentException("Invalid simulation resource limits.");
            var memorySlots = MemoryBudgetMb / EstimatedMemoryPerMatchMb;
            var batch = Math.Min(BatchSize, memorySlots);
            var desired = mode == WorkloadMode.InteractiveBackground ? BackgroundParallelMatches : MaxParallelMatches;
            return (Math.Min(batch, Math.Min(desired, Math.Max(1, processorCount - ReservedCpuCores))), batch);
        }
    }
}
