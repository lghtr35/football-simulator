using System;

namespace BeAFootballer.Simulation.Data
{
    public static class PlayerAbility
    {
        public static int PositionGroup(PositionType position)
        {
            if (position == PositionType.Goalkeeper) return 0;
            if (position == PositionType.RightBack || position == PositionType.CentreBack || position == PositionType.LeftBack) return 1;
            if (position == PositionType.RightWing || position == PositionType.LeftWing || position == PositionType.CenterForward || position == PositionType.Striker) return 3;
            return 2;
        }

        public static int Overall(FootballerDefinition player)
        {
            var s = player.Skills;
            switch (PositionGroup(player.Position))
            {
                case 0: return (s.Goalkeeping * 3 + s.Strength) / 4;
                case 1: return (s.Tackling * 2 + s.Strength + s.Speed) / 4;
                case 2: return (s.FirstTouchControl * 2 + s.Accuracy + s.Stamina) / 4;
                default: return (s.Accuracy * 2 + s.Dribbling + s.Speed) / 4;
            }
        }

        public static long DailyWage(int overall, PersonKind kind) => Math.Max(1, overall * overall / (kind == PersonKind.Coach ? 12 : 20));
        public static long Value(int overall, int age, PersonKind kind) =>
            (long)overall * overall * (kind == PersonKind.Coach ? 15 : Math.Max(10, 70 - Math.Max(0, age - 23) * 4));
    }
}
