using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed class BootstrapSceneTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_LoadsRequiredHarnessObjects()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);

            Assert.That(HarnessContract.HasRequiredObjects(name => GameObject.Find(name) != null), Is.True);
        }
    }
}
