using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Editor.Tests
{
    public sealed class BuildingScaleReviewAssetTests
    {
        [Test]
        public void HouseAndHisarUseUniformScaleWhileHisarHasRoomyLogicalFootprint()
        {
            AssertBounds(BuildingScaleReviewAssetSetup.HousePrefabPath, expectedHeight: 3f,
                expectedFootprint: null, requireUniformScale: true);
            AssertBounds(BuildingScaleReviewAssetSetup.HisarPrefabPath, expectedHeight: 3.25f,
                expectedFootprint: new Vector2(HisarPresentation.FootprintSize.x,
                    HisarPresentation.FootprintSize.z), requireUniformScale: true);
        }

        private static void AssertBounds(string path, float expectedHeight, Vector2? expectedFootprint,
            bool requireUniformScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Missing scale-review prefab: {path}");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.03f));
            Assert.That(bounds.size.y, Is.EqualTo(expectedHeight).Within(0.05f));
            if (requireUniformScale)
            {
                var model = prefab.transform.Find("Authored Model");
                Assert.That(model, Is.Not.Null);
                Assert.That(model.localScale.x, Is.EqualTo(model.localScale.y).Within(0.0001f));
                Assert.That(model.localScale.x, Is.EqualTo(model.localScale.z).Within(0.0001f));
            }
            if (!expectedFootprint.HasValue) return;
            Assert.That(expectedFootprint.Value.x, Is.GreaterThanOrEqualTo(bounds.size.x));
            Assert.That(expectedFootprint.Value.y, Is.GreaterThanOrEqualTo(bounds.size.z));
        }
    }
}
