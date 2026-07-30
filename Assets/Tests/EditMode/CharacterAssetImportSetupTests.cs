using System.Collections.Generic;
using System.IO;
using System.Linq;
using AshesOfRum.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Tests.EditMode
{
    public sealed class CharacterAssetImportSetupTests
    {
        private const string ArcherRoot = "Assets/Art/Characters/Archer";
        private const string ArcherManifest = "SourceAssets/Archer/ANIMATION_IMPORT.json";
        private const string TemporaryRoot = "Assets/CharacterAssetImportSetupTests";

        [Test]
        public void ArcherImporterRerunRestoresEveryManifestLoopSetting()
        {
            const string loopingPath = "Assets/Art/Characters/Archer/Animations/Archer_Idle.fbx";
            const string nonLoopingPath = "Assets/Art/Characters/Archer/Animations/Archer_DrawArrow.fbx";
            var restored = false;

            try
            {
                SetLoopTime(loopingPath, false);
                SetLoopTime(nonLoopingPath, true);

                CharacterAssetImportSetup.ConfigureRole("Archer", ArcherManifest);
                restored = true;

                var expectedLoops = CharacterAssetImportSetup.ReadLoopMotions("Archer", ArcherManifest);
                var animationPaths = AssetDatabase.FindAssets("t:Model", new[] { $"{ArcherRoot}/Animations" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(path => path)
                    .ToArray();

                Assert.That(animationPaths, Has.Length.EqualTo(39));
                Assert.That(expectedLoops, Has.Count.EqualTo(17));
                foreach (var path in animationPaths)
                {
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    Assert.That(importer.clipAnimations, Has.Length.EqualTo(1), path);

                    var motionName = Path.GetFileNameWithoutExtension(path)["Archer_".Length..];
                    var expectedLoop = expectedLoops.Contains(motionName);
                    Assert.That(importer.clipAnimations[0].loopTime, Is.EqualTo(expectedLoop), path);
                    Assert.That(importer.clipAnimations[0].loopPose, Is.EqualTo(expectedLoop), path);
                }
            }
            finally
            {
                if (!restored)
                {
                    SetLoopTime(loopingPath, true);
                    SetLoopTime(nonLoopingPath, false);
                }
            }
        }

        [Test]
        public void ArcherPbrDataTexturesUseProductionImportSemantics()
        {
            var expected = new Dictionary<string, TextureImporterType>
            {
                [$"{ArcherRoot}/Model/Textures/Archer_Normal.png"] = TextureImporterType.NormalMap,
                [$"{ArcherRoot}/Model/Textures/Archer_Metallic.png"] = TextureImporterType.Default,
                [$"{ArcherRoot}/Model/Textures/Archer_Roughness.png"] = TextureImporterType.Default,
                [$"{ArcherRoot}/Equipment/Bow/Archer_Bow_Normal.png"] = TextureImporterType.NormalMap,
                [$"{ArcherRoot}/Equipment/Bow/Archer_Bow_Metallic.png"] = TextureImporterType.Default,
                [$"{ArcherRoot}/Equipment/Bow/Archer_Bow_Roughness.png"] = TextureImporterType.Default,
                [$"{ArcherRoot}/Equipment/Arrow/Archer_Arrow_Normal.png"] = TextureImporterType.NormalMap,
                [$"{ArcherRoot}/Equipment/Arrow/Archer_Arrow_Metallic.png"] = TextureImporterType.Default,
                [$"{ArcherRoot}/Equipment/Arrow/Archer_Arrow_Roughness.png"] = TextureImporterType.Default,
            };

            foreach (var (path, expectedType) in expected)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType, Is.EqualTo(expectedType), path);
                Assert.That(importer.sRGBTexture, Is.False, path);
            }
        }

        [Test]
        public void ConfigurePbrTexturesAppliesFilenameSemanticsForAnyRole()
        {
            AssetDatabase.DeleteAsset(TemporaryRoot);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TemporaryRoot));

            try
            {
                CreateTexture("FutureRole_Normal.png");
                CreateTexture("FutureRole_Metallic.png");
                CreateTexture("FutureRole_Roughness.png");
                CreateTexture("FutureRole_BaseColor.png");

                Assert.That(CharacterAssetImportSetup.ConfigurePbrTextures(TemporaryRoot), Is.EqualTo(3));

                AssertImporter("FutureRole_Normal.png", TextureImporterType.NormalMap, false);
                AssertImporter("FutureRole_Metallic.png", TextureImporterType.Default, false);
                AssertImporter("FutureRole_Roughness.png", TextureImporterType.Default, false);
                AssertImporter("FutureRole_BaseColor.png", TextureImporterType.Default, true);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemporaryRoot);
            }
        }

        [Test]
        public void ArcherRuntimeAssetsUseTheApprovedHumanoidClipsAndEquipment()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherRuntimeAssetSetup.MemberPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var presentation = prefab.GetComponent<ArcherMemberPresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.Animator, Is.Not.Null);
            Assert.That(presentation.Animator.applyRootMotion, Is.False);
            Assert.That(presentation.Animator.avatar, Is.Not.Null);
            Assert.That(presentation.Animator.avatar.isHuman, Is.True);
            Assert.That(prefab.transform.Find("Archer Model"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true)
                .Any(itemRenderer => itemRenderer.name.Contains("Archer Bow")), Is.True);
            var bodyRenderer = prefab.transform.Find("Archer Model").GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(AssetDatabase.GetAssetPath(bodyRenderer.sharedMesh), Is.EqualTo(ArcherRuntimeAssetSetup.BodyMeshPath));
            var spineIndex = System.Array.FindIndex(bodyRenderer.bones,
                bone => bone.name.EndsWith("Spine2", System.StringComparison.Ordinal));
            var correctedWeights = bodyRenderer.sharedMesh.boneWeights;
            var correctedVertices = bodyRenderer.sharedMesh.vertices
                .Select((vertex, index) => (vertex, index))
                .Count(item => item.vertex.x < -0.0018f && item.vertex.y > 0.0109f &&
                               item.vertex.z < -0.0009f &&
                               correctedWeights[item.index].boneIndex0 == spineIndex &&
                               correctedWeights[item.index].weight0 > 0.999f);
            Assert.That(correctedVertices, Is.GreaterThan(50),
                "The authored arrows must remain rigidly attached to the upper spine instead of the head.");
            var authoredHeight = bodyRenderer.localBounds.size.y * bodyRenderer.transform.lossyScale.y;
            Assert.That(authoredHeight, Is.InRange(1.5f, 2.1f),
                "The generated character must be normalized to a readable gameplay height.");

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                ArcherRuntimeAssetSetup.ControllerPath);
            var states = controller.layers[0].stateMachine.states.ToDictionary(
                child => child.state.name, child => child.state.motion.name);
            Assert.That(states, Is.EquivalentTo(new Dictionary<string, string>
            {
                [ArcherMemberPresentation.IdleState] = "Idle",
                [ArcherMemberPresentation.MoveState] = "WalkForward",
                [ArcherMemberPresentation.AttackState] = "AimRecoil",
                [ArcherMemberPresentation.HitState] = "HitFront",
                [ArcherMemberPresentation.DeathState] = "DeathBackward"
            }));

            var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(
                ArcherRuntimeAssetSetup.ProjectilePrefabPath);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.GetComponent<AuthoredArrowProjectile>(), Is.Not.Null);
            var projectileRenderers = projectile.GetComponentsInChildren<Renderer>(true);
            Assert.That(projectileRenderers, Is.Not.Empty);
            var projectileBounds = projectileRenderers[0].bounds;
            foreach (var itemRenderer in projectileRenderers.Skip(1))
                projectileBounds.Encapsulate(itemRenderer.bounds);
            Assert.That(projectileBounds.size.z, Is.InRange(0.6f, 0.9f));
            Assert.That(projectileBounds.size.z, Is.GreaterThan(projectileBounds.size.x));
            Assert.That(projectileBounds.size.z, Is.GreaterThan(projectileBounds.size.y));
        }

        private static void CreateTexture(string fileName)
        {
            var texture = new Texture2D(1, 1);
            try
            {
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(TemporaryRoot, fileName), texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset($"{TemporaryRoot}/{fileName}", ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AssertImporter(string fileName, TextureImporterType expectedType, bool expectedSrgb)
        {
            var path = $"{TemporaryRoot}/{fileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(expectedType), path);
            Assert.That(importer.sRGBTexture, Is.EqualTo(expectedSrgb), path);
        }

        private static void SetLoopTime(string path, bool loopTime)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.clipAnimations, Has.Length.EqualTo(1), path);

            var clip = importer.clipAnimations[0];
            clip.loopTime = loopTime;
            clip.loopPose = loopTime;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
        }
    }
}
