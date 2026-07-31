using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed partial class StartingEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator StartingBases_UseSelectableAuthoredCompleteHisarsForBothFactions()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var friendly = economy.FriendlyHisar;
            var hostile = economy.EnemyHisar;

            Assert.That(friendly.transform.Find("Authored Hisar"), Is.Not.Null);
            Assert.That(hostile.transform.Find("Authored Hostile Hisar"), Is.Not.Null);
            Assert.That(friendly.GetComponentInChildren<BoxCollider>(), Is.Not.Null);
            Assert.That(hostile.GetComponentInChildren<BoxCollider>(), Is.Not.Null);
            Assert.That(friendly.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
            Assert.That(hostile.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
            Assert.That(GameObject.Find("Hisar Keep"), Is.Null);
            Assert.That(GameObject.Find("Black Falcon Marker"), Is.Not.Null);
            Assert.That(GameObject.Find("Living Flame Marker"), Is.Not.Null);

            var restingMaterial = friendly.GetComponentInChildren<Renderer>().sharedMaterial;
            var restingColor = restingMaterial.GetColor("_BaseColor");
            friendly.ApplyStructuralDamage(1);
            yield return new WaitForSeconds(0.2f);
            Assert.That(restingMaterial.GetColor("_BaseColor"), Is.EqualTo(restingColor),
                "Hit feedback must not mutate the shared authored material.");
        }
    }
}
