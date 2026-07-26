using NUnit.Framework;
using UnityEngine;

namespace AshesOfRum.Tests
{
    public sealed class RTSCameraControllerTests
    {
        [Test]
        public void ClampPosition_PreservesHeightAndClampsMapAxes()
        {
            var result = RTSCameraController.ClampPosition(
                new Vector3(100f, 55f, -100f),
                new Vector2(-48f, 48f),
                new Vector2(-30f, 30f));

            Assert.That(result, Is.EqualTo(new Vector3(48f, 55f, -30f)));
        }
    }
}
