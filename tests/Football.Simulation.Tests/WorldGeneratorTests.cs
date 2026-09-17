using Football.Simulation.Data;
using NUnit.Framework;

namespace Football.Simulation.Tests
{
    public sealed class WorldGeneratorTests
    {
        [Test]
        public void CreateBuildsASeededLeagueWithCompleteInitialState()
        {
            var world = WorldGenerator.Create(42);
            var state = WorldGenerator.CreateInitialState(world);

            Assert.That(world.Leagues, Has.Count.EqualTo(1));
            Assert.That(world.Leagues[0].Teams, Has.Count.EqualTo(10));
            Assert.That(world.Footballers, Has.Count.EqualTo(228)); // 10 x 22 club players + 8 free agents.
            Assert.That(state.Footballers, Has.Count.EqualTo(world.Footballers.Count));
            Assert.That(state.Teams, Has.Count.EqualTo(10));
        }
    }
}
