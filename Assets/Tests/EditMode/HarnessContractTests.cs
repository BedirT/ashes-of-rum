using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Tests
{
    public sealed class HarnessContractTests
    {
        [Test]
        public void HasRequiredObjects_RequiresRootAndCamera()
        {
            Assert.That(HarnessContract.HasRequiredObjects(name => name == HarnessContract.RootObjectName), Is.False);
            Assert.That(HarnessContract.HasRequiredObjects(
                name => name == HarnessContract.RootObjectName || name == HarnessContract.CameraObjectName), Is.True);
        }

        [Test]
        public void PlayerWindow_MatchesLockedResolution()
        {
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1920));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(1080));
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
        }
    }
}
