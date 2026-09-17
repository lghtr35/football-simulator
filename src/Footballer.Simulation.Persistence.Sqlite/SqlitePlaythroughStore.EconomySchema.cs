namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        private static readonly string[] EconomySchemaStatements =
        {
            @"CREATE TABLE IF NOT EXISTS WorldRules (
                Id INTEGER PRIMARY KEY CHECK(Id=1),
                DevelopmentInterval INTEGER NOT NULL,
                TransferInterval INTEGER NOT NULL,
                ContractDays INTEGER NOT NULL,
                Training INTEGER NOT NULL,
                InitialCash INTEGER NOT NULL,
                DailyIncome INTEGER NOT NULL,
                WageBudget INTEGER NOT NULL,
                MarketDay INTEGER NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS TransferWindows (
                StartDay INTEGER NOT NULL,
                EndDay INTEGER NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS LeagueSquadRules (
                LeagueId TEXT PRIMARY KEY REFERENCES Leagues(Id),
                MatchSquadSize INTEGER NOT NULL CHECK(MatchSquadSize BETWEEN 19 AND 32),
                MaxSubstitutions INTEGER NOT NULL CHECK(MaxSubstitutions BETWEEN 0 AND 5)
            );",
            @"CREATE TABLE IF NOT EXISTS ClubFinances (
                TeamId TEXT PRIMARY KEY REFERENCES Teams(Id),
                DailyIncome INTEGER NOT NULL,
                WageBudget INTEGER NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS Contracts (
                PersonId TEXT NOT NULL,
                Kind INTEGER NOT NULL CHECK(Kind IN (0,1)),
                TeamId TEXT REFERENCES Teams(Id),
                EndDay INTEGER NOT NULL,
                DailyWage INTEGER NOT NULL CHECK(DailyWage>=0),
                PRIMARY KEY(Kind,PersonId)
            );",
            "CREATE INDEX IF NOT EXISTS IX_Contracts_Team ON Contracts(TeamId);",
            @"CREATE TABLE IF NOT EXISTS DevelopmentData (
                FootballerId TEXT PRIMARY KEY REFERENCES Footballers(Id),
                BirthDay INTEGER NOT NULL,
                Training INTEGER NOT NULL CHECK(Training BETWEEN 0 AND 100),
                Form INTEGER NOT NULL DEFAULT 50,
                LastDevelopmentDay INTEGER NOT NULL,
                MinutesCheckpoint INTEGER NOT NULL DEFAULT 0
            );",
            "CREATE INDEX IF NOT EXISTS IX_Development_Day ON DevelopmentData(LastDevelopmentDay,FootballerId);",
            @"CREATE TABLE IF NOT EXISTS WorldHistory (
                Id INTEGER PRIMARY KEY,
                Day INTEGER NOT NULL,
                Type TEXT NOT NULL,
                PersonId TEXT,
                FromTeamId TEXT,
                ToTeamId TEXT,
                Amount INTEGER NOT NULL,
                Detail TEXT
            );",
            "CREATE INDEX IF NOT EXISTS IX_WorldHistory_Day ON WorldHistory(Day,Id);",
            "CREATE INDEX IF NOT EXISTS IX_WorldHistory_Person ON WorldHistory(PersonId,Day);",
            @"CREATE TRIGGER IF NOT EXISTS Footballers_Cap_Insert BEFORE INSERT ON Footballers
                WHEN NEW.TeamId IS NOT NULL AND (SELECT COUNT(*) FROM Footballers WHERE TeamId=NEW.TeamId)>=32
                BEGIN SELECT RAISE(ABORT,'A club may employ at most 32 players.'); END;",
            @"CREATE TRIGGER IF NOT EXISTS Footballers_Cap_Update BEFORE UPDATE OF TeamId ON Footballers
                WHEN NEW.TeamId IS NOT NULL AND NEW.TeamId IS NOT OLD.TeamId
                AND (SELECT COUNT(*) FROM Footballers WHERE TeamId=NEW.TeamId)>=32
                BEGIN SELECT RAISE(ABORT,'A club may employ at most 32 players.'); END;"
        };
    }
}
