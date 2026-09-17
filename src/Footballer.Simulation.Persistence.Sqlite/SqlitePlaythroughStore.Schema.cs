namespace BeAFootballer.Simulation.Persistence.Sqlite
{
    public sealed partial class SqlitePlaythroughStore
    {
        // Executed in dependency order at startup. Each entry is one SQL statement.
        // CREATE IF NOT EXISTS is additive initialization, not an existing-schema migration.
        private static readonly string[] SeasonSchemaStatements =
        {
            @"CREATE TABLE IF NOT EXISTS LeagueSeasons (
                Id TEXT PRIMARY KEY,
                LeagueId TEXT NOT NULL REFERENCES Leagues(Id),
                Name TEXT NOT NULL,
                StartDay INTEGER NOT NULL,
                EndDay INTEGER NOT NULL CHECK(EndDay>=StartDay),
                StrategyId TEXT
            );",

            "CREATE INDEX IF NOT EXISTS IX_LeagueSeasons_LeagueId ON LeagueSeasons(LeagueId);",

            @"CREATE TABLE IF NOT EXISTS SeasonTeams (
                SeasonId TEXT NOT NULL REFERENCES LeagueSeasons(Id),
                TeamId TEXT NOT NULL REFERENCES Teams(Id),
                PRIMARY KEY(SeasonId,TeamId)
            );",

            @"CREATE TABLE IF NOT EXISTS SeasonFixtures (
                SeasonId TEXT NOT NULL REFERENCES LeagueSeasons(Id),
                FixtureId TEXT NOT NULL UNIQUE REFERENCES Fixtures(Id),
                PRIMARY KEY(SeasonId,FixtureId)
            );"
        };

        private static readonly string[] BaseSchemaStatements =
        {
            @"CREATE TABLE IF NOT EXISTS PlaythroughMetadata (
                Id TEXT PRIMARY KEY,
                Seed INTEGER NOT NULL,
                SchemaVersion INTEGER NOT NULL,
                CurrentDay INTEGER NOT NULL,
                CreatedUtcTicks INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS Leagues (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SeasonNumber INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS Teams (
                Id TEXT PRIMARY KEY,
                LeagueId TEXT NOT NULL REFERENCES Leagues(Id),
                Name TEXT NOT NULL,
                ShortName TEXT NOT NULL,
                Strength INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS Footballers (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                TeamId TEXT REFERENCES Teams(Id),
                Position INTEGER NOT NULL,
                PreferredFoot INTEGER NOT NULL,
                Age INTEGER NOT NULL,
                HeightCentimetres INTEGER NOT NULL,
                WeightKilograms INTEGER NOT NULL,
                Nationality TEXT NOT NULL,
                Speed INTEGER NOT NULL,
                Acceleration INTEGER NOT NULL,
                Stamina INTEGER NOT NULL,
                StaminaRegen INTEGER NOT NULL,
                Dribbling INTEGER NOT NULL,
                FirstTouchControl INTEGER NOT NULL,
                HitPower INTEGER NOT NULL,
                Accuracy INTEGER NOT NULL,
                Tackling INTEGER NOT NULL,
                Strength INTEGER NOT NULL
            );",

            "CREATE INDEX IF NOT EXISTS IX_Footballers_TeamId ON Footballers(TeamId);",

            @"CREATE TABLE IF NOT EXISTS FootballerState (
                FootballerId TEXT PRIMARY KEY REFERENCES Footballers(Id),
                LastUpdatedDay INTEGER NOT NULL,
                Morale INTEGER NOT NULL,
                Fitness INTEGER NOT NULL,
                Fatigue INTEGER NOT NULL,
                RecentMatchRating INTEGER NOT NULL,
                Availability INTEGER NOT NULL,
                InjuryDaysRemaining INTEGER NOT NULL,
                SuspensionMatchesRemaining INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS Fixtures (
                Id TEXT PRIMARY KEY,
                LeagueId TEXT NOT NULL REFERENCES Leagues(Id),
                HomeTeamId TEXT NOT NULL REFERENCES Teams(Id),
                AwayTeamId TEXT NOT NULL REFERENCES Teams(Id),
                Matchday INTEGER NOT NULL,
                ScheduledDay INTEGER NOT NULL,
                IsPlayed INTEGER NOT NULL,
                HomeGoals INTEGER NOT NULL,
                AwayGoals INTEGER NOT NULL
            );",

            "CREATE INDEX IF NOT EXISTS IX_Fixtures_ScheduledDay ON Fixtures(ScheduledDay);",

            @"CREATE TABLE IF NOT EXISTS LeagueTableEntries (
                LeagueId TEXT NOT NULL REFERENCES Leagues(Id),
                TeamId TEXT NOT NULL REFERENCES Teams(Id),
                Played INTEGER NOT NULL,
                Won INTEGER NOT NULL,
                Drawn INTEGER NOT NULL,
                Lost INTEGER NOT NULL,
                GoalsFor INTEGER NOT NULL,
                GoalsAgainst INTEGER NOT NULL,
                Points INTEGER NOT NULL,
                PRIMARY KEY (LeagueId, TeamId)
            );",

            @"CREATE TABLE IF NOT EXISTS FootballerMatchPerformances (
                FixtureId TEXT NOT NULL REFERENCES Fixtures(Id),
                FootballerId TEXT NOT NULL REFERENCES Footballers(Id),
                MinutesPlayed INTEGER NOT NULL,
                Rating INTEGER,
                Goals INTEGER NOT NULL,
                Assists INTEGER NOT NULL,
                YellowCards INTEGER NOT NULL,
                RedCards INTEGER NOT NULL,
                PRIMARY KEY (FixtureId, FootballerId)
            );"
        };

        private static readonly string[] RuntimeSchemaStatements =
        {
            @"CREATE TABLE IF NOT EXISTS LeagueSettings (
                LeagueId TEXT PRIMARY KEY REFERENCES Leagues(Id),
                StartMonth INTEGER NOT NULL,
                EndMonth INTEGER NOT NULL,
                StrategyId TEXT NOT NULL,
                WinnerPrize INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS Coaches (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                TeamId TEXT UNIQUE REFERENCES Teams(Id),
                Ability INTEGER NOT NULL,
                Tactics INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS TeamRuntime (
                TeamId TEXT PRIMARY KEY REFERENCES Teams(Id),
                Morale INTEGER NOT NULL,
                Form INTEGER NOT NULL,
                Balance INTEGER NOT NULL,
                FanSupport INTEGER NOT NULL,
                Tactics INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS PlayerRatings (
                FootballerId TEXT PRIMARY KEY REFERENCES Footballers(Id),
                Overall INTEGER NOT NULL,
                Goalkeeping INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS PlayerTotals (
                FootballerId TEXT PRIMARY KEY REFERENCES Footballers(Id),
                Appearances INTEGER NOT NULL DEFAULT 0,
                Starts INTEGER NOT NULL DEFAULT 0,
                Minutes INTEGER NOT NULL DEFAULT 0,
                Goals INTEGER NOT NULL DEFAULT 0,
                Assists INTEGER NOT NULL DEFAULT 0,
                YellowCards INTEGER NOT NULL DEFAULT 0,
                RedCards INTEGER NOT NULL DEFAULT 0
            );",

            @"CREATE TABLE IF NOT EXISTS SeasonStandings (
                SeasonId TEXT NOT NULL REFERENCES LeagueSeasons(Id),
                TeamId TEXT NOT NULL REFERENCES Teams(Id),
                Played INTEGER NOT NULL DEFAULT 0,
                Won INTEGER NOT NULL DEFAULT 0,
                Drawn INTEGER NOT NULL DEFAULT 0,
                Lost INTEGER NOT NULL DEFAULT 0,
                GoalsFor INTEGER NOT NULL DEFAULT 0,
                GoalsAgainst INTEGER NOT NULL DEFAULT 0,
                Points INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY(SeasonId,TeamId)
            );",

            @"CREATE TABLE IF NOT EXISTS SeasonPrizes (
                SeasonId TEXT PRIMARY KEY REFERENCES LeagueSeasons(Id),
                Prize INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS SeasonHistory (
                SeasonId TEXT PRIMARY KEY REFERENCES LeagueSeasons(Id),
                WinnerTeamId TEXT NOT NULL REFERENCES Teams(Id),
                Prize INTEGER NOT NULL,
                CompletedDay INTEGER NOT NULL
            );",

            @"CREATE TABLE IF NOT EXISTS MatchEvents (
                FixtureId TEXT NOT NULL REFERENCES Fixtures(Id),
                Sequence INTEGER NOT NULL,
                Minute INTEGER NOT NULL,
                Type INTEGER NOT NULL,
                TeamId TEXT,
                PrimaryFootballerId TEXT,
                SecondaryFootballerId TEXT,
                PRIMARY KEY(FixtureId,Sequence)
            );",

            @"CREATE TABLE IF NOT EXISTS MatchDetails (
                FixtureId TEXT PRIMARY KEY REFERENCES Fixtures(Id),
                HomeXg REAL NOT NULL,
                AwayXg REAL NOT NULL,
                Forfeit INTEGER NOT NULL
            );",

            "CREATE INDEX IF NOT EXISTS IX_Fixtures_DayPlayed ON Fixtures(ScheduledDay,IsPlayed);",

            "CREATE INDEX IF NOT EXISTS IX_Fixtures_HomeDay ON Fixtures(HomeTeamId,ScheduledDay);",

            "CREATE INDEX IF NOT EXISTS IX_Fixtures_AwayDay ON Fixtures(AwayTeamId,ScheduledDay);",

            "CREATE INDEX IF NOT EXISTS IX_Performances_Player ON FootballerMatchPerformances(FootballerId);"
        };
    }
}
