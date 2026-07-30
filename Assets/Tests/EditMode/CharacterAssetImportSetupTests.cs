using System.Collections.Generic;
using System.IO;
using AshesOfRum.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Tests.EditMode
{
    public sealed class CharacterAssetImportSetupTests
    {
        private const string ArcherRoot = "Assets/Art/Characters/Archer";
        private const string TemporaryRoot = "Assets/CharacterAssetImportSetupTests";

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
    }
}
