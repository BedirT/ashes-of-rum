using NUnit.Framework;

namespace AshesOfRum.Tests
{
    public sealed class AgentVerificationSpeedTests
    {
        [Test]
        public void TryRead_UsesDefaultWhenArgumentIsAbsent()
        {
            Assert.That(AgentVerificationSpeed.TryRead(new[] { "player" }, out var speed, out var error), Is.True);
            Assert.That(speed, Is.EqualTo(AgentVerificationSpeed.Default));
            Assert.That(error, Is.Null);
        }

        [TestCase("0")]
        [TestCase("101")]
        [TestCase("nope")]
        public void TryRead_RejectsInvalidSpeed(string value)
        {
            Assert.That(AgentVerificationSpeed.TryRead(
                new[] { "player", AgentVerificationSpeed.Argument, value }, out var speed, out var error), Is.False);
            Assert.That(speed, Is.EqualTo(AgentVerificationSpeed.Default));
            Assert.That(error, Does.Contain(AgentVerificationSpeed.Argument));
        }

        [Test]
        public void TryRead_AcceptsInvariantAcceleratedSpeed()
        {
            Assert.That(AgentVerificationSpeed.TryRead(
                new[] { "player", AgentVerificationSpeed.Argument, "20" }, out var speed, out var error), Is.True);
            Assert.That(speed, Is.EqualTo(20f));
            Assert.That(error, Is.Null);
        }
    }
}
