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
        public IEnumerator TexturedReviewAssetsInstantiateBesideEightAuthoredArchers()
        {
            var housePrefab = Resources.Load<GameObject>("Presentation/WorldScale/House_Complete");
            var storehousePrefab = Resources.Load<GameObject>("Presentation/WorldScale/Storehouse_Complete");
            var watchtowerPrefab = Resources.Load<GameObject>("Presentation/WorldScale/Watchtower_Complete");
            var hisarPrefab = Resources.Load<GameObject>("Presentation/Hisar/Hisar_Complete");
            var archerPrefab = Resources.Load<GameObject>("Presentation/ArcherMember");
            Assert.That(housePrefab, Is.Not.Null);
            Assert.That(storehousePrefab, Is.Not.Null);
            Assert.That(watchtowerPrefab, Is.Not.Null);
            Assert.That(hisarPrefab, Is.Not.Null);
            Assert.That(archerPrefab, Is.Not.Null);

            var instances = new GameObject[12];
            try
            {
                instances[0] = Object.Instantiate(housePrefab);
                instances[1] = Object.Instantiate(storehousePrefab);
                instances[2] = Object.Instantiate(watchtowerPrefab);
                instances[3] = Object.Instantiate(hisarPrefab);
                for (var index = 0; index < 8; index++)
                    instances[index + 4] = Object.Instantiate(archerPrefab,
                        new Vector3(index % 4, 0f, index / 4), Quaternion.identity);
                yield return null;

                Assert.That(instances.Skip(4).SelectMany(instance =>
                    instance.GetComponentsInChildren<ArcherMemberPresentation>()).Count(), Is.EqualTo(8));
                Assert.That(instances[0].GetComponentsInChildren<Renderer>(), Is.Not.Empty);
                Assert.That(instances[1].GetComponentsInChildren<Renderer>(), Is.Not.Empty);
                Assert.That(instances[2].GetComponentsInChildren<Renderer>(), Is.Not.Empty);
                Assert.That(instances[3].GetComponentsInChildren<Renderer>(), Is.Not.Empty);
            }
            finally
            {
                foreach (var instance in instances.Where(instance => instance != null)) Object.Destroy(instance);
            }
        }
    }
}
