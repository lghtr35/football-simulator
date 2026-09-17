using Football.Simulation.Data;
using NUnit.Framework;
namespace Football.Simulation.Tests
{
    public sealed class SeededRandomTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var first = new SeededRandom(42); var second = new SeededRandom(42);
            for (var index = 0; index < 10; index++) Assert.That(first.NextInt(1000), Is.EqualTo(second.NextInt(1000)));
        }
    }
}
