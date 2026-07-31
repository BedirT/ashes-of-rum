using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AshesOfRum.Tests
{
    public sealed class HisarPresentationAssetTests
    {
        private static readonly HisarBuildState[] States =
        {
            HisarBuildState.Foundation,
            HisarBuildState.RaisedFrame,
            HisarBuildState.CanvasInstallation,
            HisarBuildState.Complete
        };

        [Test]
        public void BuildStates_AreGroundedNormalizedAndWithinRealtimeTriangleBudget()
        {
            var complete = HisarPresentation.Create(null, HisarBuildState.Complete);
            var approvedScale = complete.transform.Find("Authored Model").localScale.x;
            Object.DestroyImmediate(complete);
            foreach (var state in States)
            {
                var instance = HisarPresentation.Create(null, state);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>();
                    Assert.That(renderers, Is.Not.Empty, $"{state} must contain authored renderers.");
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.01f), $"{state} must sit on the ground.");
                    if (state == HisarBuildState.Complete)
                        Assert.That(bounds.size.y, Is.EqualTo(3.25f).Within(0.01f),
                            "Complete Hisar must use the approved visual height.");
                    if (state == HisarBuildState.Complete)
                    {
                        Assert.That(bounds.size.x,
                            Is.LessThanOrEqualTo(HisarPresentation.FootprintSize.x + 0.01f),
                            "Complete Hisar exceeds the gameplay footprint width.");
                        Assert.That(bounds.size.z,
                            Is.LessThanOrEqualTo(HisarPresentation.FootprintSize.z + 0.01f),
                            "Complete Hisar exceeds the gameplay footprint depth.");
                    }
                    var scale = instance.transform.Find("Authored Model").localScale;
                    Assert.That(scale.x, Is.EqualTo(scale.y).Within(0.0001f));
                    Assert.That(scale.x, Is.EqualTo(scale.z).Within(0.0001f));
                    Assert.That(scale.x, Is.EqualTo(approvedScale).Within(0.0001f),
                        $"{state} must share the complete Hisar scale.");

                    var triangles = instance.GetComponentsInChildren<MeshFilter>()
                        .Select(filter => filter.sharedMesh)
                        .Where(mesh => mesh != null)
                        .Distinct()
                        .Sum(mesh => mesh.triangles.Length / 3);
                    Assert.That(triangles, Is.InRange(1, 20000), $"{state} exceeds the approved realtime budget.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void CompleteHisars_UseDistinctFriendlyAndHostileTextileTreatments()
        {
            var friendly = HisarPresentation.Create(null, HisarBuildState.Complete);
            var hostile = HisarPresentation.Create(null, HisarBuildState.Complete, false);
            try
            {
                var friendlyMaterial = friendly.GetComponentInChildren<Renderer>().sharedMaterial;
                var hostileMaterial = hostile.GetComponentInChildren<Renderer>().sharedMaterial;
                Assert.That(friendlyMaterial, Is.Not.SameAs(hostileMaterial));
                Assert.That(friendlyMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(hostileMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                Assert.That(friendlyMaterial.GetTexture("_BaseMap"),
                    Is.Not.SameAs(hostileMaterial.GetTexture("_BaseMap")));
                Assert.That(friendlyMaterial.GetTexture("_BumpMap"), Is.Not.Null);
                Assert.That(friendlyMaterial.GetTexture("_MetallicGlossMap"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(friendly);
                Object.DestroyImmediate(hostile);
            }
        }
    }
}
