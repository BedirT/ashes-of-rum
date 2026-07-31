using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed class BuildingScaleReviewPlayModeTests
    {
        [UnityTest]
        public IEnumerator ScaleReviewAssetsInstantiateBesideEightAuthoredArchers()
        {
            var housePrefab = Resources.Load<GameObject>("Presentation/WorldScale/House_Complete");
            var hisarPrefab = Resources.Load<GameObject>("Presentation/Hisar/Hisar_Complete");
            var archerPrefab = Resources.Load<GameObject>("Presentation/ArcherMember");
            Assert.That(housePrefab, Is.Not.Null);
            Assert.That(hisarPrefab, Is.Not.Null);
            Assert.That(archerPrefab, Is.Not.Null);

            var instances = new GameObject[10];
            try
            {
                instances[0] = Object.Instantiate(housePrefab);
                instances[1] = Object.Instantiate(hisarPrefab);
                for (var index = 0; index < 8; index++)
                    instances[index + 2] = Object.Instantiate(archerPrefab,
                        new Vector3(index % 4, 0f, index / 4), Quaternion.identity);
                yield return null;

                Assert.That(instances.Skip(2).SelectMany(instance =>
                    instance.GetComponentsInChildren<ArcherMemberPresentation>()).Count(), Is.EqualTo(8));
                Assert.That(instances[0].GetComponentsInChildren<Renderer>(), Is.Not.Empty);
                Assert.That(instances[1].GetComponentsInChildren<Renderer>(), Is.Not.Empty);
            }
            finally
            {
                foreach (var instance in instances.Where(instance => instance != null)) Object.Destroy(instance);
            }
        }
    }
}
