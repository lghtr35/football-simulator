using System;
using System.Linq;
using Football.Simulation.Data;

namespace Football.Simulation.Career
{
    public static class MatchConsequences
    {
        public static void Apply(MatchInput input, MatchResult result)
        {
            result.UpdatedPlayers.Clear();
            result.UpdatedTeams.Clear();
            var outcomes = result.Players.ToDictionary(p => p.FootballerId);
            foreach (var team in new[] { input.Home.Squad, input.Away.Squad })
            {
                foreach (var player in team.Players)
                {
                    var state = player.State;
                    var form = state.Form;
                    // A suspended player serves one ban match while their club plays.
                    form.SuspensionMatchesRemaining = Math.Max(0, form.SuspensionMatchesRemaining - 1);
                    if (outcomes.TryGetValue(player.Definition.Id, out var outcome))
                    {
                        form.Fatigue = Math.Min(100, form.Fatigue + outcome.Minutes / 3);
                        form.Fitness = Math.Max(0, form.Fitness - outcome.Minutes / 10);
                        form.InjuryDaysRemaining = Math.Max(form.InjuryDaysRemaining, outcome.InjuryDays);
                        form.SuspensionMatchesRemaining += outcome.RedCards > 0 ? 1 : 0;
                        if (outcome.Rating.HasValue) form.RecentMatchRating = outcome.Rating.Value;
                        var playerResult = result.HomeGoals - result.AwayGoals;
                        if (team.Definition.Id == input.Fixture.AwayTeamId) playerResult = -playerResult;
                        form.PerformanceForm = Math.Max(0, Math.Min(100, form.PerformanceForm + Math.Sign(playerResult) * 3));
                    }
                    form.Availability = form.InjuryDaysRemaining > 0 ? AvailabilityStatus.Injured :
                        form.SuspensionMatchesRemaining > 0 ? AvailabilityStatus.Suspended : AvailabilityStatus.Available;
                    state.LastUpdatedDay = input.Fixture.ScheduledDay;
                    result.UpdatedPlayers.Add(state);
                }
                var difference = result.HomeGoals - result.AwayGoals;
                if (team.Definition.Id == input.Fixture.AwayTeamId) difference = -difference;
                var change = Math.Sign(difference) * 5;
                team.State.Form = Math.Max(0, Math.Min(100, team.State.Form + change));
                team.State.Morale = Math.Max(0, Math.Min(100, team.State.Morale + change));
                result.UpdatedTeams.Add(team.State);
            }
        }
    }
}
