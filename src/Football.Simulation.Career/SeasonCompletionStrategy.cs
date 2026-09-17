using System.Collections.Generic;
using System.Linq;
using Football.Simulation.Data;

namespace Football.Simulation.Career
{
    public interface ISeasonCompletionStrategy
    {
        SeasonSummary Complete(LeagueSeason season, IReadOnlyList<LeagueTableEntry> standings, int day);
    }

    public sealed class LeagueWinnerStrategy : ISeasonCompletionStrategy
    {
        public SeasonSummary Complete(LeagueSeason season, IReadOnlyList<LeagueTableEntry> standings, int day)
        {
            var winner = standings.OrderByDescending(t => t.Points)
                .ThenByDescending(t => t.GoalsFor - t.GoalsAgainst).ThenByDescending(t => t.GoalsFor)
                .ThenBy(t => t.TeamId, System.StringComparer.Ordinal).First();
            return new SeasonSummary { SeasonId = season.Id, WinnerTeamId = winner.TeamId,
                Prize = season.WinnerPrize, CompletedDay = day };
        }
    }
}
