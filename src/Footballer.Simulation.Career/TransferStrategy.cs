using System;
using System.Collections.Generic;
using System.Linq;
using BeAFootballer.Simulation.Data;

namespace BeAFootballer.Simulation.Career
{
    public interface ITransferStrategy
    {
        MarketUpdate Advance(MarketSnapshot snapshot, WorldRules rules, int day);
    }

    public sealed class TransferStrategy : ITransferStrategy
    {
        public MarketUpdate Advance(MarketSnapshot snapshot, WorldRules rules, int day)
        {
            rules.Validate();
            // ponytail: compact world-wide market scan; index candidate pools if world size outgrows the MVP.
            var result = new MarketUpdate { Snapshot = snapshot };
            var clubs = snapshot.Clubs.ToDictionary(c => c.TeamId);
            var people = snapshot.People.OrderBy(p => p.Contract.Kind).ThenBy(p => p.Contract.PersonId, StringComparer.Ordinal).ToList();
            long Wages(string team) => people.Where(p => p.Contract.TeamId == team).Sum(p => p.Contract.DailyWage);
            List<MarketPerson> Roster(string team) => people.Where(p => p.Contract.TeamId == team && p.Contract.Kind == PersonKind.Player).ToList();
            void Log(string type, MarketPerson p, string from, string to, long amount) => result.History.Add(new WorldHistoryEntry {
                Day = day, Type = type, PersonId = p.Contract.PersonId, FromTeamId = from, ToTeamId = to, Amount = amount });
            foreach (var person in people)
            {
                var c = person.Contract;
                if (c.TeamId == null || c.EndDay > day) continue;
                var club = clubs[c.TeamId];
                if (club.Cash >= person.AskingWage && Wages(c.TeamId) - c.DailyWage + person.AskingWage <= club.WageBudget)
                {
                    c.EndDay = checked(day + rules.ContractLengthDays); c.DailyWage = person.AskingWage;
                    Log("renewal", person, c.TeamId, c.TeamId, c.DailyWage);
                }
                else { Log("release", person, c.TeamId, null, 0); c.TeamId = null; c.DailyWage = 0; }
            }
            foreach (var club in snapshot.Clubs)
            {
                var net = club.DailyIncome - Wages(club.TeamId);
                club.Cash = checked(club.Cash + net);
                result.History.Add(new WorldHistoryEntry { Day = day, Type = "cashflow", ToTeamId = club.TeamId, Amount = net });
            }
            if (day % rules.TransferIntervalDays != 0 || !rules.TransferWindows.Any(w => w.Contains(day))) return result;
            var moved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var club in snapshot.Clubs.OrderBy(c => c.TeamId, StringComparer.Ordinal))
            {
                foreach (var kind in new[] { PersonKind.Coach, PersonKind.Player })
                {
                    var roster = Roster(club.TeamId);
                    var incumbent = people.FirstOrDefault(p => p.Contract.Kind == PersonKind.Coach && p.Contract.TeamId == club.TeamId);
                    if (kind == PersonKind.Player && roster.Count >= SquadRules.MaxPlayers) continue;
                    var minimum = new[] { 2, 5, 5, 3 };
                    bool Useful(MarketPerson p)
                    {
                        if (kind == PersonKind.Coach) return incumbent == null || p.Overall >= incumbent.Overall + 5;
                        var group = roster.Where(r => r.PositionGroup == p.PositionGroup).ToList();
                        return group.Count < minimum[p.PositionGroup] || (group.Count > 0 && p.Overall >= group.Min(r => r.Overall) + 5);
                    }
                    bool CanSell(MarketPerson p)
                    {
                        if (p.Contract.TeamId == null) return true;
                        if (kind == PersonKind.Coach)
                            return people.Any(replacement => replacement.Contract.Kind == PersonKind.Coach && replacement.Contract.TeamId == null &&
                                replacement.AskingWage <= clubs[p.Contract.TeamId].WageBudget - Wages(p.Contract.TeamId) + p.Contract.DailyWage);
                        var selling = Roster(p.Contract.TeamId);
                        return selling.Count > 19 && selling.Count(r => r.PositionGroup == p.PositionGroup) > minimum[p.PositionGroup];
                    }
                    var candidate = people.Where(p => p.Contract.Kind == kind && p.Contract.TeamId != club.TeamId &&
                        !moved.Contains(kind + ":" + p.Contract.PersonId) && Useful(p) && CanSell(p) &&
                        Wages(club.TeamId) - (kind == PersonKind.Coach ? incumbent?.Contract.DailyWage ?? 0 : 0) + p.AskingWage <= club.WageBudget &&
                        club.Cash >= (p.Contract.TeamId == null ? 0 : p.Value) + p.AskingWage * 30)
                        .OrderByDescending(p => p.Overall).ThenBy(p => p.Contract.PersonId, StringComparer.Ordinal).FirstOrDefault();
                    if (candidate == null) continue;
                    if (kind == PersonKind.Coach && incumbent != null)
                    { Log("release", incumbent, club.TeamId, null, 0); incumbent.Contract.TeamId = null; incumbent.Contract.DailyWage = 0; }
                    var from = candidate.Contract.TeamId;
                    var fee = from == null ? 0 : candidate.Value;
                    if (from != null) clubs[from].Cash = checked(clubs[from].Cash + fee);
                    club.Cash -= fee;
                    candidate.Contract.TeamId = club.TeamId;
                    candidate.Contract.DailyWage = candidate.AskingWage;
                    candidate.Contract.EndDay = checked(day + rules.ContractLengthDays);
                    moved.Add(kind + ":" + candidate.Contract.PersonId);
                    Log(kind == PersonKind.Player ? "player-transfer" : "coach-transfer", candidate, from, club.TeamId, fee);
                }
            }
            // A poached coach leaves a vacancy even if the seller already took its turn today.
            foreach (var club in snapshot.Clubs.OrderBy(c => c.TeamId, StringComparer.Ordinal))
            {
                if (people.Any(p => p.Contract.Kind == PersonKind.Coach && p.Contract.TeamId == club.TeamId)) continue;
                var replacement = people.Where(p => p.Contract.Kind == PersonKind.Coach && p.Contract.TeamId == null &&
                    p.AskingWage + Wages(club.TeamId) <= club.WageBudget && p.AskingWage * 30 <= club.Cash)
                    .OrderByDescending(p => p.Overall).ThenBy(p => p.Contract.PersonId, StringComparer.Ordinal).FirstOrDefault();
                if (replacement == null) continue;
                replacement.Contract.TeamId = club.TeamId;
                replacement.Contract.DailyWage = replacement.AskingWage;
                replacement.Contract.EndDay = checked(day + rules.ContractLengthDays);
                Log("coach-transfer", replacement, null, club.TeamId, 0);
            }
            return result;
        }
    }
}
