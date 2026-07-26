using NUnit.Framework;

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
    }
}
