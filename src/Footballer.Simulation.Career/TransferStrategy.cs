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
        private static readonly int[] Coverage = { 2, 5, 5, 3 };

        public MarketUpdate Advance(MarketSnapshot snapshot, WorldRules rules, int day)
        {
            rules.Validate();
            var result = new MarketUpdate { Snapshot = snapshot };
            var clubs = snapshot.Clubs.ToDictionary(c => c.TeamId);
            var people = snapshot.People.OrderBy(p => p.Contract.Kind).ThenBy(p => p.Contract.PersonId, StringComparer.Ordinal).ToList();
            var index = new MarketIndex(people);
            void Log(string type, MarketPerson p, string from, string to, long amount) => result.History.Add(new WorldHistoryEntry {
                Day = day, Type = type, PersonId = p.Contract.PersonId, FromTeamId = from, ToTeamId = to, Amount = amount });
            foreach (var person in people)
            {
                var c = person.Contract;
                if (c.TeamId == null || c.EndDay > day) continue;
                var club = clubs[c.TeamId];
                if (club.Cash >= person.AskingWage && index.Wages(c.TeamId) - c.DailyWage + person.AskingWage <= club.WageBudget)
                {
                    index.SetWage(person, person.AskingWage);
                    c.EndDay = checked(day + rules.ContractLengthDays); c.DailyWage = person.AskingWage;
                    Log("renewal", person, c.TeamId, c.TeamId, c.DailyWage);
                }
                else
                {
                    Log("release", person, c.TeamId, null, 0);
                    index.Unassign(person);
                }
            }
            foreach (var club in snapshot.Clubs)
            {
                var net = club.DailyIncome - index.Wages(club.TeamId);
                club.Cash = checked(club.Cash + net);
            }
            if (day % rules.TransferIntervalDays != 0 || !rules.TransferWindows.Any(w => w.Contains(day))) return result;
            var moved = new HashSet<string>(StringComparer.Ordinal);
            foreach (var club in snapshot.Clubs.OrderBy(c => c.TeamId, StringComparer.Ordinal))
            {
                foreach (var kind in new[] { PersonKind.Coach, PersonKind.Player })
                {
                    var roster = index.Players(club.TeamId);
                    var incumbent = index.Coach(club.TeamId);
                    if (kind == PersonKind.Player && roster.Count >= SquadRules.MaxPlayers) continue;
                    bool Useful(MarketPerson p)
                    {
                        if (kind == PersonKind.Coach) return incumbent == null || p.Overall >= incumbent.Overall + 5;
                        var group = roster.Where(r => r.PositionGroup == p.PositionGroup).ToList();
                        return group.Count < Coverage[p.PositionGroup] || (group.Count > 0 && p.Overall >= group.Min(r => r.Overall) + 5);
                    }
                    bool CanSell(MarketPerson p)
                    {
                        if (p.Contract.TeamId == null) return true;
                        if (kind == PersonKind.Coach)
                            return index.FreeCoaches.Any(replacement =>
                                replacement.AskingWage <= clubs[p.Contract.TeamId].WageBudget - index.Wages(p.Contract.TeamId) + p.Contract.DailyWage);
                        var selling = index.Players(p.Contract.TeamId);
                        return selling.Count > 19 && selling.Count(r => r.PositionGroup == p.PositionGroup) > Coverage[p.PositionGroup];
                    }
                    var candidate = people.Where(p => p.Contract.Kind == kind && p.Contract.TeamId != club.TeamId &&
                        !moved.Contains(kind + ":" + p.Contract.PersonId) && Useful(p) && CanSell(p) &&
                        index.Wages(club.TeamId) - (kind == PersonKind.Coach ? incumbent?.Contract.DailyWage ?? 0 : 0) + p.AskingWage <= club.WageBudget &&
                        club.Cash >= (p.Contract.TeamId == null ? 0 : p.Value) + p.AskingWage * 30)
                        .OrderByDescending(p => p.Overall).ThenBy(p => p.Contract.PersonId, StringComparer.Ordinal).FirstOrDefault();
                    if (candidate == null) continue;
                    if (kind == PersonKind.Coach && incumbent != null)
                    {
                        Log("release", incumbent, club.TeamId, null, 0);
                        index.Unassign(incumbent);
                    }
                    var from = candidate.Contract.TeamId;
                    var fee = from == null ? 0 : candidate.Value;
                    if (from != null) clubs[from].Cash = checked(clubs[from].Cash + fee);
                    club.Cash -= fee;
                    index.Assign(candidate, club.TeamId, candidate.AskingWage);
                    candidate.Contract.EndDay = checked(day + rules.ContractLengthDays);
                    moved.Add(kind + ":" + candidate.Contract.PersonId);
                    Log(kind == PersonKind.Player ? "player-transfer" : "coach-transfer", candidate, from, club.TeamId, fee);
                }
            }
            foreach (var club in snapshot.Clubs.OrderBy(c => c.TeamId, StringComparer.Ordinal))
            {
                if (index.Coach(club.TeamId) != null) continue;
                var replacement = index.FreeCoaches.Where(p =>
                    p.AskingWage + index.Wages(club.TeamId) <= club.WageBudget && p.AskingWage * 30 <= club.Cash)
                    .OrderByDescending(p => p.Overall).ThenBy(p => p.Contract.PersonId, StringComparer.Ordinal).FirstOrDefault();
                if (replacement == null) continue;
                index.Assign(replacement, club.TeamId, replacement.AskingWage);
                replacement.Contract.EndDay = checked(day + rules.ContractLengthDays);
                Log("coach-transfer", replacement, null, club.TeamId, 0);
            }
            return result;
        }

        private sealed class MarketIndex
        {
            private readonly Dictionary<string, long> wages = new Dictionary<string, long>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<MarketPerson>> players = new Dictionary<string, List<MarketPerson>>(StringComparer.Ordinal);
            private readonly Dictionary<string, MarketPerson> coaches = new Dictionary<string, MarketPerson>(StringComparer.Ordinal);
            public readonly List<MarketPerson> FreeCoaches = new List<MarketPerson>();

            public MarketIndex(IReadOnlyList<MarketPerson> people)
            {
                foreach (var person in people)
                {
                    var team = person.Contract.TeamId;
                    if (team == null)
                    {
                        if (person.Contract.Kind == PersonKind.Coach) FreeCoaches.Add(person);
                        continue;
                    }
                    AddWage(team, person.Contract.DailyWage);
                    if (person.Contract.Kind == PersonKind.Coach) coaches[team] = person;
                    else Players(team).Add(person);
                }
            }

            public long Wages(string team) => wages.TryGetValue(team, out var value) ? value : 0;
            public MarketPerson Coach(string team) => coaches.TryGetValue(team, out var coach) ? coach : null;
            public List<MarketPerson> Players(string team)
            {
                if (!players.TryGetValue(team, out var roster))
                    players[team] = roster = new List<MarketPerson>();
                return roster;
            }

            public void SetWage(MarketPerson person, long wage)
            {
                AddWage(person.Contract.TeamId, wage - person.Contract.DailyWage);
            }

            public void Unassign(MarketPerson person)
            {
                var team = person.Contract.TeamId;
                if (team == null) return;
                AddWage(team, -person.Contract.DailyWage);
                if (person.Contract.Kind == PersonKind.Coach)
                {
                    if (Coach(team) == person) coaches.Remove(team);
                    FreeCoaches.Add(person);
                }
                else Players(team).Remove(person);
                person.Contract.TeamId = null;
                person.Contract.DailyWage = 0;
            }

            public void Assign(MarketPerson person, string team, long wage)
            {
                if (person.Contract.TeamId != null) Unassign(person);
                person.Contract.TeamId = team;
                person.Contract.DailyWage = wage;
                AddWage(team, wage);
                if (person.Contract.Kind == PersonKind.Coach)
                {
                    FreeCoaches.Remove(person);
                    coaches[team] = person;
                }
                else Players(team).Add(person);
            }

            private void AddWage(string team, long delta)
            {
                wages.TryGetValue(team, out var current);
                wages[team] = current + delta;
            }
        }
    }
}
