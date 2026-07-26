using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed class BattlefieldSceneTests
    {
        [UnityTest]
        public IEnumerator SunderedRoad_LoadsPlayableBattlefieldShell()
        {
            yield return SceneManager.LoadSceneAsync("SunderedRoad", LoadSceneMode.Single);

            Assert.That(Object.FindFirstObjectByType<RTSCameraController>(), Is.Not.Null);
            Assert.That(GameObject.Find("Karasungur Hisar"), Is.Not.Null);
            Assert.That(GameObject.Find("Alazhan Hisar"), Is.Not.Null);
            Assert.That(GameObject.Find("Battle HUD"), Is.Not.Null);
            Assert.That(GameObject.Find("Quit Button"), Is.Not.Null);
        }
    }
}
