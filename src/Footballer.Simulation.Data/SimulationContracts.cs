using System.Collections.Generic;

namespace BeAFootballer.Simulation.Data
{
    public enum TacticalStyle { Defensive, Balanced, Attacking }
    public enum SimulationMode { Discrete, Continuous }
    public enum WorkloadMode { AdvanceDays, InteractiveBackground }

    public sealed class CoachDefinition
    {
        public string Id;
        public bool IsActor; // Runtime host annotation; game saves own actor identity.
        public string Name;
        public string TeamId; // Null means unemployed.
        public int Ability;
        public TacticalStyle PreferredTactics;
    }

    public sealed class PlayerSnapshot
    {
        public FootballerDefinition Definition;
        public FootballerState State;
    }

    public sealed class TeamSnapshot
    {
        public TeamDefinition Definition;
        public TeamState State;
        public CoachDefinition Coach;
        public List<PlayerSnapshot> Players = new List<PlayerSnapshot>();
    }

    public sealed class PreparedPlayer
    {
        public PlayerSnapshot Player;
        // Normalized pitch coordinates, home attacks towards x=1.
        public float X;
        public float Y;
    }

    public sealed class PreparedTeam
    {
        public TeamSnapshot Squad;
        public List<PreparedPlayer> Lineup = new List<PreparedPlayer>();
        public List<PlayerSnapshot> Bench = new List<PlayerSnapshot>();
        public IEnumerable<PlayerSnapshot> RegisteredPlayers
        {
            get
            {
                foreach (var player in Lineup) yield return player.Player;
                foreach (var player in Bench) yield return player;
            }
        }
        public double Attack;
        public double Defence;
    }

    public sealed class MatchInput
    {
        public Fixture Fixture;
        public int Seed;
        public PreparedTeam Home;
        public PreparedTeam Away;
    }

    public sealed class PlayerMatchOutcome
    {
        public string FootballerId;
        public string TeamId;
        public int Minutes;
        public bool Started;
        public int Goals;
        public int Assists;
        public int YellowCards;
        public int RedCards;
        public int InjuryDays;
        public int? Rating;
    }

    public sealed class MatchResult
    {
        public string FixtureId;
        public int HomeGoals;
        public int AwayGoals;
        public double HomeExpectedGoals;
        public double AwayExpectedGoals;
        public bool Forfeit;
        public List<MatchEvent> Events = new List<MatchEvent>();
        public List<PlayerMatchOutcome> Players = new List<PlayerMatchOutcome>();
        public List<FootballerState> UpdatedPlayers = new List<FootballerState>();
        public List<TeamState> UpdatedTeams = new List<TeamState>();
    }

    public sealed class FixtureFilter
    {
        public string TeamId;
        public string LeagueId;
        public string SeasonId;
        public int? FromDay;
        public int? ToDay;
        public bool? IsPlayed;
        public string ExcludeFixtureId;
        public int Limit = 100;
        public int Offset;
    }

    public sealed class SeasonSummary
    {
        public string SeasonId;
        public string WinnerTeamId;
        public long Prize;
        public int CompletedDay;
    }

    public sealed class SeasonSchedule
    {
        public LeagueSeason Season;
        public IReadOnlyList<Fixture> Fixtures;
    }

    /// <summary>All times are calendar day ordinals; CurrentDay is the next day to process.
    /// Implementations commit batches atomically and reject stale/duplicate results.</summary>
    public interface IPlaythroughStore
    {
        void CreateCampaign(PlaythroughMetadata metadata, WorldDefinition world, WorldState state,
            IReadOnlyList<SeasonSchedule> schedules);
        PlaythroughMetadata LoadMetadata();
        List<Fixture> QueryFixtures(FixtureFilter filter);
        Dictionary<string, TeamSnapshot> LoadTeams(IReadOnlyList<string> teamIds);
        void CommitMatches(int expectedDay, IReadOnlyList<MatchResult> results);
        void CompleteDay(int expectedDay, IReadOnlyList<SeasonSummary> summaries, IReadOnlyList<SeasonSchedule> nextSeasons = null);
        LeagueSeason LoadSeason(string seasonId);
        LeagueSeason LoadSeasonForYear(string leagueId, int startYear);
        int LoadFirstSeasonYear();
        List<LeagueSeason> LoadUnfinishedSeasons();
        List<LeagueTableEntry> LoadStandings(string seasonId);
        List<SeasonSummary> LoadSeasonHistory();
        List<MatchEvent> LoadMatchEvents(string fixtureId);
        List<FootballerMatchPerformance> LoadMatchPerformances(string fixtureId);
        long LoadTeamBalance(string teamId);
        List<CoachDefinition> LoadCoaches(bool unemployedOnly = false);
    }
}
