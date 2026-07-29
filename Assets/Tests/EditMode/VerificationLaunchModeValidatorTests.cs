using NUnit.Framework;

namespace AshesOfRum.Tests
{
    public sealed class VerificationLaunchModeValidatorTests
    {
        [TestCase(false, false, false, true, null)]
        [TestCase(true, false, false, true, null)]
        [TestCase(false, true, false, true, null)]
        [TestCase(false, false, true, true, null)]
        [TestCase(true, true, false, false,
            "multiple_verification_modes:--smoke-test,--agent-script")]
        [TestCase(true, false, true, false,
            "multiple_verification_modes:--smoke-test,--agent-live-dir")]
        [TestCase(false, true, true, false,
            "multiple_verification_modes:--agent-script,--agent-live-dir")]
        [TestCase(true, true, true, false,
            "multiple_verification_modes:--smoke-test,--agent-script,--agent-live-dir")]
        public void IsValid_PermitsAtMostOneModeAndRejectsEveryCombination(bool smoke, bool scriptedAgent,
            bool liveAgent, bool expectedValid, string expectedReason)
        {
            var valid = VerificationLaunchModeValidator.IsValid(smoke, scriptedAgent, liveAgent, out var reason);

            Assert.That(valid, Is.EqualTo(expectedValid));
            Assert.That(reason, Is.EqualTo(expectedReason));
        }

        [Test]
        public void ConflictMarker_IsStable()
        {
            Assert.That(VerificationLaunchModeValidator.ConflictMarker,
                Is.EqualTo("VERIFICATION_LAUNCH_CONFLICT"));
        }
    }
}
