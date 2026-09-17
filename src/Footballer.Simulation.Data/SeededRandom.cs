namespace BeAFootballer.Simulation.Data
{
    public sealed class SeededRandom
    {
        private uint state;
        public SeededRandom(int seed) { state = (uint)seed; if (state == 0) state = 0x6D2B79F5u; }
        public int NextInt(int exclusiveMax) { if (exclusiveMax <= 0) throw new System.ArgumentOutOfRangeException("exclusiveMax"); return (int)(NextUInt() % (uint)exclusiveMax); }
        public float NextFloat() { return (NextUInt() >> 8) * (1.0f / 16777216.0f); }
        public bool Roll(float probability) { return NextFloat() < probability; }
        private uint NextUInt() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
    }
}
