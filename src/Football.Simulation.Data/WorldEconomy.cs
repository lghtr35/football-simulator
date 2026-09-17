using System;
using System.Collections.Generic;

namespace Football.Simulation.Data
{
    public static class SquadRules
    {
        public const int MaxPlayers = 32;
        public const int MaxCoaches = 1;
    }

    public sealed class TransferWindow
    {
        public int StartDayOfYear;
        public int EndDayOfYear;
        public bool Contains(int day)
        {
            var d = SimulationCalendar.DayOfYearFromDay(day);
            return StartDayOfYear <= EndDayOfYear ? d >= StartDayOfYear && d <= EndDayOfYear :
                d >= StartDayOfYear || d <= EndDayOfYear;
        }
    }

    public sealed class WorldRules
    {
        public int DevelopmentIntervalDays = 7;
        public int TransferIntervalDays = 7;
        public int ContractLengthDays = 365;
        public int DefaultTraining = 60;
        public long InitialCash = 1000000;
        public long DailyIncome = 12000;
        public long DailyWageBudget = 10000;
        public List<TransferWindow> TransferWindows = new List<TransferWindow>
        {
            new TransferWindow { StartDayOfYear = 1, EndDayOfYear = 31 },
            new TransferWindow { StartDayOfYear = 182, EndDayOfYear = 243 }
        };

        public void Validate()
        {
            if (DevelopmentIntervalDays < 1 || TransferIntervalDays < 1 || ContractLengthDays < 1 ||
                DefaultTraining < 0 || DefaultTraining > 100 || InitialCash < 0 || DailyIncome < 0 || DailyWageBudget < 0 ||
                TransferWindows == null)
                throw new ArgumentException("Invalid world economy configuration.");
            foreach (var w in TransferWindows)
                if (w.StartDayOfYear < 1 || w.StartDayOfYear > 365 || w.EndDayOfYear < 1 || w.EndDayOfYear > 365)
                    throw new ArgumentException("Transfer window dates must be 1–365.");
        }
    }

    public enum PersonKind { Player, Coach }
    public sealed class Contract
    {
        public string PersonId;
        public PersonKind Kind;
        public string TeamId;
        public int EndDay;
        public long DailyWage;
    }
    public sealed class MarketPerson
    {
        public Contract Contract;
        public int Overall;
        public int Age;
        public int PositionGroup;
        public long Value;
        public long AskingWage;
        public bool Tracked;
        public string LoadedTeamId;
        public int LoadedEndDay;
        public long LoadedDailyWage;
    }
    public sealed class ClubFinance
    {
        public string TeamId;
        public long Cash;
        public long DailyIncome;
        public long WageBudget;
    }
    public sealed class MarketSnapshot
    {
        public List<MarketPerson> People = new List<MarketPerson>();
        public List<ClubFinance> Clubs = new List<ClubFinance>();
    }
    public sealed class WorldHistoryEntry
    {
        public int Day;
        public string Type;
        public string PersonId;
        public string FromTeamId;
        public string ToTeamId;
        public long Amount;
        public string Detail;
    }
    public sealed class MarketUpdate
    {
        public MarketSnapshot Snapshot;
        public List<WorldHistoryEntry> History = new List<WorldHistoryEntry>();
    }
    public sealed class DevelopmentPlayer
    {
        public FootballerDefinition Definition;
        public int BirthDay;
        public int Training;
        public int Form;
        public int LastDevelopmentDay;
        public int MinutesSinceDevelopment;
    }
    public sealed class DevelopmentUpdate
    {
        public DevelopmentPlayer Player;
        public int Day;
        public int OldOverall;
        public string Skill;
        public int Change;
    }
    public interface IWorldEvolutionStore
    {
        IDisposable BeginWorldTick();
        WorldRules LoadWorldRules();
        void StampStableLife(int day);
        List<FootballerState> LoadLifePage(string afterId, int limit, int day);
        void CommitLife(int day, IReadOnlyList<FootballerState> states);
        bool IsMarketDayComplete(int day);
        bool TryCommitIdleMarket(int day, WorldRules rules);
        MarketSnapshot LoadMarket();
        void CommitMarket(int day, MarketUpdate update);
        List<DevelopmentPlayer> LoadDevelopmentPage(string afterId, int limit, int throughDay);
        void CommitDevelopment(int day, IReadOnlyList<DevelopmentUpdate> updates);
        void SetTraining(string footballerId, int intensity);
        List<WorldHistoryEntry> LoadWorldHistory(int fromDay, int toDay, string personId = null, int limit = 100);
    }
}
