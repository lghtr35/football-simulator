using Football.Simulation.Data;
using static Football.Simulation.Data.TacticalStyle;
using static Football.Simulation.Manual.PerformanceLevel;

namespace Football.Simulation.Manual
{
    internal enum PerformanceLevel { Poor, Fair, Average, Strong, WorldClass }

    /// <summary>2026–27 club lists and stadium capacities from Wikipedia season pages (retrieved 17 Sep 2026). Players/coaches are generated.</summary>
    internal static class WorldCatalog
    {
        public static PlaythroughGenerationConfiguration Create(int seed = 2026)
        {
            var config = new PlaythroughGenerationConfiguration
            {
                Seed = seed,
                StartYear = 2026,
                UnemployedCoaches = 24,
                FreeAgentPlayers = 80
            };
            foreach (var league in Leagues)
                config.Leagues.Add(league.ToGeneration());
            return config;
        }

        private static readonly LeagueSpec[] Leagues =
        {
            new LeagueSpec("premier", "Premier League", 5000000, 72, new[]
            {
                new ClubSpec("Arsenal", 60704, Attacking, Strong, Strong, WorldClass, WorldClass),
                new ClubSpec("Aston Villa", 36887, Balanced, Average, Strong, Strong, Average),
                new ClubSpec("Bournemouth", 12357, Attacking, Average, Average, Average, Average),
                new ClubSpec("Brentford", 17250, Balanced, Average, Average, Average, Average),
                new ClubSpec("Brighton & Hove Albion", 32176, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Chelsea", 40044, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Coventry City", 32609, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Crystal Palace", 25194, Balanced, Average, Average, Average, Average),
                new ClubSpec("Everton", 52769, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Fulham", 28107, Balanced, Average, Average, Average, Average),
                new ClubSpec("Hull City", 24983, Defensive, Fair, Fair, Poor, Fair),
                new ClubSpec("Ipswich Town", 30056, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Leeds United", 37633, Attacking, Average, Average, Average, Average),
                new ClubSpec("Liverpool", 61276, Attacking, Strong, Strong, WorldClass, WorldClass),
                new ClubSpec("Manchester City", 61038, Attacking, WorldClass, WorldClass, WorldClass, WorldClass),
                new ClubSpec("Manchester United", 74158, Balanced, Strong, Average, Average, Strong),
                new ClubSpec("Newcastle United", 52729, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Nottingham Forest", 31212, Balanced, Average, Average, Average, Average),
                new ClubSpec("Sunderland", 48095, Balanced, Fair, Average, Fair, Fair),
                new ClubSpec("Tottenham Hotspur", 62850, Attacking, Strong, Average, Strong, Strong),
            }),
            new LeagueSpec("championship", "Championship", 500000, 60, new[]
            {
                new ClubSpec("Birmingham City", 29409, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Blackburn Rovers", 31367, Balanced, Fair, Average, Fair, Fair),
                new ClubSpec("Bolton Wanderers", 28723, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Bristol City", 26462, Balanced, Average, Average, Fair, Average),
                new ClubSpec("Burnley", 21990, Defensive, Average, Strong, Average, Average),
                new ClubSpec("Cardiff City", 33280, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Charlton Athletic", 27111, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Derby County", 33597, Balanced, Average, Average, Average, Fair),
                new ClubSpec("Lincoln City", 11400, Defensive, Poor, Fair, Poor, Poor),
                new ClubSpec("Middlesbrough", 34742, Balanced, Average, Average, Average, Average),
                new ClubSpec("Millwall", 20146, Defensive, Fair, Average, Fair, Fair),
                new ClubSpec("Norwich City", 27359, Attacking, Average, Fair, Average, Average),
                new ClubSpec("Portsmouth", 20867, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Preston North End", 23408, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Queens Park Rangers", 18439, Attacking, Fair, Fair, Average, Fair),
                new ClubSpec("Sheffield United", 32050, Balanced, Average, Strong, Average, Average),
                new ClubSpec("Southampton", 32384, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Stoke City", 30089, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Swansea City", 21088, Attacking, Fair, Fair, Average, Fair),
                new ClubSpec("Watford", 22200, Balanced, Average, Fair, Average, Average),
                new ClubSpec("West Bromwich Albion", 26850, Balanced, Average, Average, Average, Average),
                new ClubSpec("West Ham United", 62500, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Wolverhampton Wanderers", 31750, Balanced, Strong, Strong, Average, Strong),
                new ClubSpec("Wrexham", 10771, Balanced, Fair, Fair, Fair, Fair),
            }),
            new LeagueSpec("ligue-1", "Ligue 1", 2500000, 66, new[]
            {
                new ClubSpec("Angers", 18752, Defensive, Fair, Fair, Fair, Poor),
                new ClubSpec("Auxerre", 21379, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Brest", 15931, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Le Havre", 25178, Defensive, Fair, Fair, Fair, Fair),
                new ClubSpec("Le Mans", 25000, Balanced, Fair, Fair, Poor, Fair),
                new ClubSpec("Lens", 37705, Attacking, Average, Strong, Average, Average),
                new ClubSpec("Lille", 50186, Balanced, Strong, Strong, Strong, Average),
                new ClubSpec("Lorient", 18890, Attacking, Fair, Fair, Average, Fair),
                new ClubSpec("Lyon", 59186, Attacking, Strong, Average, Strong, Strong),
                new ClubSpec("Marseille", 67394, Attacking, Average, Average, Strong, Strong),
                new ClubSpec("Monaco", 18523, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Nice", 35624, Balanced, Average, Strong, Average, Average),
                new ClubSpec("Paris FC", 20000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Paris Saint-Germain", 47926, Attacking, WorldClass, WorldClass, WorldClass, WorldClass),
                new ClubSpec("Rennes", 29778, Attacking, Average, Average, Average, Average),
                new ClubSpec("Strasbourg", 29230, Balanced, Average, Average, Average, Average),
                new ClubSpec("Toulouse", 33150, Balanced, Average, Average, Fair, Average),
                new ClubSpec("Troyes", 20400, Defensive, Fair, Fair, Poor, Fair),
            }),
            new LeagueSpec("bundesliga", "Bundesliga", 3000000, 68, new[]
            {
                new ClubSpec("1. FC Köln", 49698, Balanced, Average, Average, Fair, Average),
                new ClubSpec("Bayer Leverkusen", 30210, Attacking, Strong, Strong, WorldClass, Strong),
                new ClubSpec("Bayern Munich", 75000, Attacking, WorldClass, WorldClass, WorldClass, WorldClass),
                new ClubSpec("Borussia Dortmund", 81365, Attacking, Strong, Strong, Strong, WorldClass),
                new ClubSpec("Borussia Mönchengladbach", 54057, Balanced, Average, Average, Average, Average),
                new ClubSpec("Eintracht Frankfurt", 59500, Attacking, Average, Average, Strong, Strong),
                new ClubSpec("FC Augsburg", 30660, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Hamburger SV", 57000, Balanced, Average, Average, Average, Average),
                new ClubSpec("Mainz 05", 33305, Balanced, Average, Average, Average, Fair),
                new ClubSpec("RB Leipzig", 47800, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("SC Freiburg", 34700, Balanced, Average, Strong, Average, Average),
                new ClubSpec("SC Paderborn", 15000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Schalke 04", 62271, Balanced, Fair, Average, Fair, Fair),
                new ClubSpec("SV Elversberg", 10000, Defensive, Poor, Fair, Poor, Poor),
                new ClubSpec("TSG Hoffenheim", 30150, Attacking, Average, Fair, Average, Average),
                new ClubSpec("Union Berlin", 22012, Defensive, Average, Strong, Fair, Fair),
                new ClubSpec("VfB Stuttgart", 60058, Attacking, Average, Average, Strong, Strong),
                new ClubSpec("Werder Bremen", 42100, Balanced, Average, Average, Average, Average),
            }),
            new LeagueSpec("la-liga", "La Liga", 4000000, 70, new[]
            {
                new ClubSpec("Alavés", 19840, Defensive, Fair, Average, Fair, Fair),
                new ClubSpec("Athletic Bilbao", 53289, Attacking, Strong, Strong, Average, Strong),
                new ClubSpec("Atlético Madrid", 70692, Defensive, Strong, WorldClass, Strong, Strong),
                new ClubSpec("Barcelona", 105000, Attacking, WorldClass, Strong, WorldClass, WorldClass),
                new ClubSpec("Celta Vigo", 24870, Attacking, Average, Average, Average, Average),
                new ClubSpec("Deportivo A Coruña", 32660, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Elche", 31388, Defensive, Fair, Fair, Fair, Poor),
                new ClubSpec("Espanyol", 37776, Balanced, Average, Average, Fair, Average),
                new ClubSpec("Getafe", 16500, Defensive, Average, Strong, Fair, Fair),
                new ClubSpec("Levante", 26354, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Málaga", 30044, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Osasuna", 23576, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Racing Santander", 22308, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Rayo Vallecano", 14500, Attacking, Fair, Fair, Average, Average),
                new ClubSpec("Real Betis", 70000, Attacking, Average, Average, Strong, Strong),
                new ClubSpec("Real Madrid", 83186, Attacking, WorldClass, WorldClass, WorldClass, WorldClass),
                new ClubSpec("Real Sociedad", 39313, Balanced, Strong, Strong, Strong, Average),
                new ClubSpec("Sevilla", 43883, Balanced, Average, Average, Average, Average),
                new ClubSpec("Valencia", 49430, Balanced, Average, Average, Fair, Average),
                new ClubSpec("Villarreal", 23008, Attacking, Average, Average, Strong, Strong),
            }),
            new LeagueSpec("serie-a", "Serie A", 3500000, 68, new[]
            {
                new ClubSpec("AC Milan", 75710, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Atalanta", 23439, Attacking, Average, Average, Strong, Strong),
                new ClubSpec("Bologna", 38279, Balanced, Average, Strong, Average, Average),
                new ClubSpec("Cagliari", 16416, Defensive, Fair, Fair, Fair, Fair),
                new ClubSpec("Como", 13602, Balanced, Fair, Fair, Average, Fair),
                new ClubSpec("Fiorentina", 43118, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Frosinone", 16227, Defensive, Fair, Fair, Poor, Fair),
                new ClubSpec("Genoa", 33205, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Inter Milan", 75710, Attacking, WorldClass, WorldClass, Strong, Strong),
                new ClubSpec("Juventus", 41507, Balanced, Strong, Strong, Strong, Strong),
                new ClubSpec("Lazio", 70634, Balanced, Strong, Average, Strong, Average),
                new ClubSpec("Lecce", 30354, Defensive, Fair, Fair, Fair, Fair),
                new ClubSpec("Monza", 17102, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Napoli", 54732, Attacking, Strong, Strong, Strong, WorldClass),
                new ClubSpec("Parma", 22352, Balanced, Fair, Average, Fair, Fair),
                new ClubSpec("Roma", 70634, Attacking, Strong, Strong, Average, Strong),
                new ClubSpec("Sassuolo", 21515, Attacking, Fair, Fair, Average, Average),
                new ClubSpec("Torino", 28177, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Udinese", 25132, Balanced, Average, Average, Average, Fair),
                new ClubSpec("Venezia", 12048, Defensive, Fair, Fair, Poor, Poor),
            }),
            new LeagueSpec("super-lig", "Süper Lig", 800000, 60, new[]
            {
                new ClubSpec("Alanyaspor", 9789, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Amedspor", 30480, Balanced, Fair, Fair, Poor, Fair),
                new ClubSpec("Başakşehir", 17067, Balanced, Average, Average, Average, Fair),
                new ClubSpec("Beşiktaş", 42684, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Erzurumspor", 20347, Defensive, Fair, Fair, Poor, Poor),
                new ClubSpec("Eyüpspor", 13797, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Fenerbahçe", 47911, Attacking, Strong, Strong, WorldClass, Strong),
                new ClubSpec("Galatasaray", 53387, Attacking, Strong, Strong, Strong, WorldClass),
                new ClubSpec("Gaziantep", 30320, Defensive, Fair, Average, Fair, Fair),
                new ClubSpec("Gençlerbirliği", 20672, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Göztepe", 23376, Balanced, Average, Average, Fair, Average),
                new ClubSpec("Kasımpaşa", 13797, Attacking, Fair, Fair, Average, Fair),
                new ClubSpec("Kocaelispor", 34829, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Konyaspor", 41135, Defensive, Average, Average, Fair, Fair),
                new ClubSpec("Rizespor", 14879, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Samsunspor", 34346, Balanced, Average, Average, Average, Average),
                new ClubSpec("Trabzonspor", 41061, Attacking, Average, Average, Strong, Strong),
                new ClubSpec("Çorum", 13119, Defensive, Poor, Fair, Poor, Poor),
            }),
            new LeagueSpec("primeira", "Primeira Liga", 1200000, 62, new[]
            {
                new ClubSpec("Académico de Viseu", 6912, Balanced, Poor, Fair, Poor, Poor),
                new ClubSpec("Alverca", 6932, Defensive, Poor, Fair, Poor, Poor),
                new ClubSpec("Arouca", 5000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Benfica", 68100, Attacking, Strong, Strong, WorldClass, WorldClass),
                new ClubSpec("Braga", 30286, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Casa Pia", 7000, Defensive, Fair, Fair, Fair, Poor),
                new ClubSpec("Estoril Praia", 5094, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Estrela da Amadora", 9288, Defensive, Fair, Fair, Poor, Fair),
                new ClubSpec("Famalicão", 5186, Balanced, Fair, Fair, Average, Fair),
                new ClubSpec("Gil Vicente", 12046, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Marítimo", 10600, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Moreirense", 6150, Defensive, Fair, Average, Fair, Fair),
                new ClubSpec("Nacional", 5200, Defensive, Fair, Fair, Poor, Fair),
                new ClubSpec("Porto", 50033, Attacking, Strong, WorldClass, Strong, Strong),
                new ClubSpec("Rio Ave", 5300, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Santa Clara", 12500, Balanced, Average, Average, Fair, Fair),
                new ClubSpec("Sporting CP", 52095, Attacking, Strong, Strong, WorldClass, Strong),
                new ClubSpec("Vitória de Guimarães", 30029, Balanced, Average, Average, Average, Average),
            }),
            new LeagueSpec("pro-league", "Belgian Pro League", 600000, 58, new[]
            {
                new ClubSpec("Anderlecht", 21500, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Antwerp", 21000, Balanced, Average, Average, Average, Average),
                new ClubSpec("Beveren", 8190, Defensive, Poor, Fair, Poor, Poor),
                new ClubSpec("Cercle Brugge", 29042, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Charleroi", 14000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Club Brugge", 29042, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Genk", 24956, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Gent", 20000, Balanced, Average, Average, Average, Average),
                new ClubSpec("Kortrijk", 9399, Defensive, Fair, Fair, Poor, Fair),
                new ClubSpec("La Louvière", 8050, Defensive, Poor, Fair, Poor, Poor),
                new ClubSpec("Lommel SK", 8000, Balanced, Poor, Fair, Fair, Poor),
                new ClubSpec("Mechelen", 16700, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("OH Leuven", 10000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Sint-Truiden", 14600, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Standard Liège", 30023, Balanced, Average, Average, Average, Fair),
                new ClubSpec("Union SG", 9400, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Westerlo", 8035, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Zulte Waregem", 12500, Balanced, Fair, Fair, Fair, Fair),
            }),
            new LeagueSpec("eredivisie", "Eredivisie", 900000, 62, new[]
            {
                new ClubSpec("ADO Den Haag", 15000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Ajax", 55865, Attacking, Strong, Strong, WorldClass, Strong),
                new ClubSpec("AZ", 19478, Attacking, Average, Average, Strong, Average),
                new ClubSpec("Cambuur", 15000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("Excelsior", 4500, Defensive, Poor, Fair, Fair, Poor),
                new ClubSpec("Feyenoord", 47500, Attacking, Strong, Strong, Strong, Strong),
                new ClubSpec("Fortuna Sittard", 12000, Defensive, Fair, Fair, Fair, Fair),
                new ClubSpec("Go Ahead Eagles", 10000, Balanced, Fair, Fair, Average, Fair),
                new ClubSpec("Groningen", 22550, Balanced, Fair, Average, Fair, Fair),
                new ClubSpec("Heerenveen", 26100, Attacking, Fair, Fair, Average, Average),
                new ClubSpec("N.E.C.", 12650, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("PEC Zwolle", 14000, Balanced, Fair, Fair, Fair, Fair),
                new ClubSpec("PSV", 35000, Attacking, Strong, Strong, WorldClass, WorldClass),
                new ClubSpec("Sparta Rotterdam", 11026, Defensive, Fair, Average, Fair, Fair),
                new ClubSpec("Telstar", 5338, Defensive, Poor, Fair, Poor, Poor),
                new ClubSpec("Twente", 30205, Balanced, Average, Strong, Average, Average),
                new ClubSpec("Utrecht", 23750, Balanced, Average, Average, Average, Average),
                new ClubSpec("Willem II", 15220, Balanced, Fair, Fair, Fair, Fair),
            }),
        };

        private sealed class LeagueSpec
        {
            public readonly string Id, Name;
            public readonly long Prize;
            public readonly int BaseOverall;
            public readonly ClubSpec[] Clubs;
            public LeagueSpec(string id, string name, long prize, int baseOverall, ClubSpec[] clubs)
            { Id = id; Name = name; Prize = prize; BaseOverall = baseOverall; Clubs = clubs; }
            public LeagueGenerationConfiguration ToGeneration()
            {
                var league = new LeagueGenerationConfiguration
                {
                    Id = Id, Name = Name, WinnerPrize = Prize,
                    SeasonStartMonth = 8, SeasonEndMonth = 5
                };
                foreach (var club in Clubs) league.Teams.Add(club.ToTeam(Id, BaseOverall));
                return league;
            }
        }

        private sealed class ClubSpec
        {
            public readonly string Name;
            public readonly int Capacity;
            public readonly TacticalStyle Tactics;
            public readonly PerformanceLevel Goalkeeper, Defence, Midfield, Attack;
            public ClubSpec(string name, int capacity, TacticalStyle tactics,
                PerformanceLevel goalkeeper, PerformanceLevel defence, PerformanceLevel midfield, PerformanceLevel attack)
            {
                Name = name; Capacity = capacity; Tactics = tactics;
                Goalkeeper = goalkeeper; Defence = defence; Midfield = midfield; Attack = attack;
            }
            public TeamGenerationConfiguration ToTeam(string leagueId, int baseOverall)
            {
                var elite = EliteBonus(Name);
                return new TeamGenerationConfiguration
                {
                    Id = leagueId + ":" + Slug(Name),
                    Name = Name,
                    ShortName = Short(Name),
                    FanSupport = Clamp((int)(15 + Capacity / 900.0), 15, 95),
                    Tactics = Tactics,
                    TargetOverall = new PositionGroupOverallTargets
                    {
                        Goalkeeper = Target(baseOverall, elite, Goalkeeper),
                        Defence = Target(baseOverall, elite, Defence),
                        Midfield = Target(baseOverall, elite, Midfield),
                        Attack = Target(baseOverall, elite, Attack),
                        AllowedVariance = 6
                    }
                };
            }
        }

        private static int Target(int baseOverall, int elite, PerformanceLevel level) =>
            Clamp(baseOverall + elite + Offset(level), 45, 94);

        private static int Offset(PerformanceLevel level)
        {
            if (level == Poor) return -10;
            if (level == Fair) return -5;
            if (level == Strong) return 5;
            if (level == WorldClass) return 10;
            return 0;
        }

        private static int EliteBonus(string name)
        {
            if (Contains(name, "Real Madrid") || Contains(name, "Barcelona") || Contains(name, "Bayern") || Contains(name, "Paris Saint-Germain")) return 12;
            if (Contains(name, "Manchester City") || Contains(name, "Liverpool") || Contains(name, "Arsenal")) return 11;
            if (Contains(name, "Inter Milan") || Contains(name, "Atlético") || Contains(name, "Dortmund")) return 9;
            if (Contains(name, "AC Milan") || Contains(name, "Juventus") || Contains(name, "Chelsea") || Contains(name, "Manchester United")) return 8;
            if (Contains(name, "Napoli") || Contains(name, "Benfica") || Contains(name, "Sporting CP") || Contains(name, "Porto") || Contains(name, "Ajax") || name == "PSV") return 8;
            if (Contains(name, "Galatasaray") || Contains(name, "Fenerbah") || Contains(name, "Leverkusen") || Contains(name, "Tottenham") || Contains(name, "Newcastle")) return 7;
            if (Contains(name, "Beşiktaş") || Contains(name, "Club Brugge") || Contains(name, "Feyenoord") || Contains(name, "Marseille") || Contains(name, "Lyon")) return 6;
            return 0;
        }

        private static bool Contains(string name, string part) => name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0;
        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        private static string Slug(string name)
        {
            var chars = new char[name.Length];
            var n = 0;
            var dash = false;
            foreach (var c in name.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) { chars[n++] = c; dash = false; }
                else if (n > 0 && !dash) { chars[n++] = '-'; dash = true; }
            }
            if (n > 0 && chars[n - 1] == '-') n--;
            return new string(chars, 0, n);
        }
        private static string Short(string name)
        {
            var letters = new char[3];
            var n = 0;
            foreach (var c in name.ToUpperInvariant())
            {
                if (c >= 'A' && c <= 'Z') { letters[n++] = c; if (n == 3) break; }
            }
            return n == 0 ? "CLB" : new string(letters, 0, n);
        }
    }
}
