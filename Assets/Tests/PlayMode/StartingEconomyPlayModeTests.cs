using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed class StartingEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator Worker_GathersDepositsAndReturnsAutomatically()
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            Assert.That(economy, Is.Not.Null);
            Assert.That(economy.Workers, Has.Count.EqualTo(StartingEconomyController.WorkerCount));
            Assert.That(GameObject.Find(StartingEconomyController.HisarObjectName), Is.Not.Null);

            var worker = economy.Workers[0];
            economy.SelectOnly(worker);
            economy.IssueGatherForSmoke(economy.Caches[0]);
            var deadline = Time.realtimeSinceStartup + 12f;
            while (economy.Supplies <= economy.StartingSupplies && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(economy.Supplies, Is.EqualTo(economy.StartingSupplies + 10));
            Assert.That(economy.Caches[0].Remaining, Is.EqualTo(190));
            Assert.That(worker.IsSelected, Is.True);
            Assert.That(worker.CurrentActivity, Is.EqualTo(WorkerAgent.Activity.GoingToCache));
        }
    }
}
