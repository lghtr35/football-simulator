using System;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Life
{
    public interface IDailyLifeStrategy
    {
        // Core invokes this once per date for every persisted player, in bounded pages.
        void Advance(FootballerState state, int day);
    }

    public sealed class RecoveryOnlyStrategy : IDailyLifeStrategy
    {
        public void Advance(FootballerState state, int day)
        {
                if (day < state.LastUpdatedDay) throw new InvalidOperationException("Cannot rewind player state.");
                var elapsed = Math.Min(365, day - state.LastUpdatedDay);
                state.Form.Fatigue = Math.Max(0, state.Form.Fatigue - elapsed * 10);
                state.Form.Fitness = Math.Min(100, state.Form.Fitness + elapsed * 4);
                state.Form.InjuryDaysRemaining = Math.Max(0, state.Form.InjuryDaysRemaining - (day - state.LastUpdatedDay));
                state.Form.Availability = state.Form.InjuryDaysRemaining > 0 ? AvailabilityStatus.Injured :
                    state.Form.SuspensionMatchesRemaining > 0 ? AvailabilityStatus.Suspended : AvailabilityStatus.Available;
                state.LastUpdatedDay = day;
        }
    }
}
