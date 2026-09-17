using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BeAFootballer.Simulation.Data;
using BeAFootballer.Simulation.Match;

namespace BeAFootballer.Simulation.Core
{
    public sealed partial class SimulationEngine
    {
        private readonly SimulationHooks hooks;

        public async Task<SimulationCheckpoint> AdvanceUntilStopAsync(int maxDays, CancellationToken token = default)
        {
            try { await AdvanceDaysAsync(maxDays, token).ConfigureAwait(false); return null; }
            catch (SimulationPausedException pause) { return pause.Checkpoint; }
        }

        private void Checkpoint(SimulationCheckpoint checkpoint)
        {
            if (hooks?.TryContinue != null && !hooks.TryContinue(checkpoint)) throw new SimulationPausedException(checkpoint);
        }

        private bool IsActor(PersonKind kind, string id) => hooks?.IsActor?.Invoke(kind, id) ?? false;

        private MatchInput PrepareMatch(Fixture fixture, Dictionary<string, TeamSnapshot> teams, int seed)
        {
            var home = teams[fixture.HomeTeamId]; var away = teams[fixture.AwayTeamId];
            foreach (var team in new[] { home, away })
            {
                if (team.Coach != null) team.Coach.IsActor = IsActor(PersonKind.Coach, team.Coach.Id);
                foreach (var player in team.Players) player.Definition.IsActor = IsActor(PersonKind.Player, player.Definition.Id);
            }
            var checkpoint = new SimulationCheckpoint { Stage = SimulationStage.BeforeMatch, Day = fixture.ScheduledDay,
                Fixture = fixture, Home = home, Away = away };
            Checkpoint(checkpoint);
            return MatchPreparation.Prepare(fixture, home, away, seed,
                homeSelection: checkpoint.HomeSelection, awaySelection: checkpoint.AwaySelection);
        }
    }
}
