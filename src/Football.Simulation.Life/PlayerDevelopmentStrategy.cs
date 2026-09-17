using System;
using Football.Simulation.Data;

namespace Football.Simulation.Life
{
    public interface IPlayerDevelopmentStrategy
    {
        DevelopmentUpdate Advance(DevelopmentPlayer player, int day, WorldRules rules, int seed);
    }

    public sealed class PlayerDevelopmentStrategy : IPlayerDevelopmentStrategy
    {
        public DevelopmentUpdate Advance(DevelopmentPlayer player, int day, WorldRules rules, int seed)
        {
            if (day - player.LastDevelopmentDay < rules.DevelopmentIntervalDays) return null;
            var p = player.Definition;
            p.Attributes.Age = Math.Max(0, (day - player.BirthDay) / SimulationCalendar.DaysPerYear);
            var update = new DevelopmentUpdate { Player = player, Day = day, OldOverall = p.Overall };
            var hash = seed ^ day;
            unchecked { foreach (var ch in p.Id) hash = hash * 31 + ch; }
            var random = new SeededRandom(hash);
            var support = player.Training / 100f * 0.5f + player.Form / 100f * 0.3f + Math.Min(180, player.MinutesSinceDevelopment) / 180f * 0.2f;
            var growth = Math.Max(0.005f, (30 - p.Attributes.Age) * 0.025f) * (0.2f + support);
            var decline = Math.Max(0, p.Attributes.Age - 28) * 0.03f * (1 - support * 0.8f);
            var roll = random.NextFloat();
            var change = roll < growth ? 1 : roll < growth + decline ? -1 : 0;
            var s = p.Skills;
            // One skill per development tick: cheap and persistent; no hidden overall-only upgrades.
            var values = new[] { s.Speed, s.Acceleration, s.Stamina, s.StaminaRegen, s.Dribbling,
                s.FirstTouchControl, s.HitPower, s.Accuracy, s.Tackling, s.Strength, s.Goalkeeping };
            var names = new[] { "Speed", "Acceleration", "Stamina", "StaminaRegen", "Dribbling",
                "FirstTouchControl", "HitPower", "Accuracy", "Tackling", "Strength", "Goalkeeping" };
            var index = random.NextInt(p.Position == PositionType.Goalkeeper ? 11 : 10);
            var next = Math.Max(1, Math.Min(100, values[index] + change));
            update.Change = next - values[index];
            update.Skill = names[index];
            values[index] = next;
            s.Speed = values[0]; s.Acceleration = values[1]; s.Stamina = values[2]; s.StaminaRegen = values[3];
            s.Dribbling = values[4]; s.FirstTouchControl = values[5]; s.HitPower = values[6]; s.Accuracy = values[7];
            s.Tackling = values[8]; s.Strength = values[9]; s.Goalkeeping = values[10];
            p.Overall = PlayerAbility.Overall(p);
            return update;
        }
    }
}
