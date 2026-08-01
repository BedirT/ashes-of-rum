using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class BuildingScaleReviewAssetSetup
    {
        public const string HousePrefabPath = "Assets/Resources/Presentation/WorldScale/House_Complete.prefab";
        public const string StorehousePrefabPath = "Assets/Resources/Presentation/WorldScale/Storehouse_Complete.prefab";
        public const string WatchtowerPrefabPath = "Assets/Resources/Presentation/WorldScale/Watchtower_Complete.prefab";
        public const string HisarPrefabPath = "Assets/Resources/Presentation/Hisar/Hisar_Complete.prefab";

        private const string ResourceRoot = "Assets/Resources/Presentation/WorldScale";

        private readonly struct BuildingReviewAsset
        {
            public BuildingReviewAsset(string name, float targetHeight)
            {
                Name = name;
                TargetHeight = targetHeight;
            }

            public string Name { get; }
            public float TargetHeight { get; }
            public string ArtRoot => $"Assets/Art/Buildings/{Name}";
            public string ModelPath => $"{ArtRoot}/Models/{Name}_Complete.fbx";
            public string PrefabPath => $"{ResourceRoot}/{Name}_Complete.prefab";
            public string MaterialPath => $"{ResourceRoot}/Materials/{Name}_Complete.mat";
        }

        private static readonly BuildingReviewAsset[] ReviewAssets =
        {
            new("House", 3f),
            new("Storehouse", 3f),
            new("Watchtower", 4f)
        };

        public static void Configure()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Presentation");
            EnsureFolder("Assets/Resources/Presentation", "WorldScale");
            EnsureFolder(ResourceRoot, "Materials");

            foreach (var asset in ReviewAssets)
            {
                ConfigureModel(asset.ModelPath);
                ConfigureTexture($"{asset.ArtRoot}/Textures/{asset.Name}_Complete_BaseColor.png", false, true);
                ConfigureTexture($"{asset.ArtRoot}/Textures/{asset.Name}_Complete_Normal.png", true, false);
                ConfigureTexture($"{asset.ArtRoot}/Textures/{asset.Name}_Complete_Metallic.png", false, false);
                ConfigureTexture($"{asset.ArtRoot}/Textures/{asset.Name}_Complete_Roughness.png", false, false);
                ConfigureTexture($"{asset.ArtRoot}/Textures/{asset.Name}_Complete_Emission.png", false, true);
                CreatePrefab(asset, CreateMaterial(asset));
            }

            HisarAssetSetup.Configure();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured textured House, Storehouse, Watchtower, and proportion-preserving Hisar scale-review prefabs.");
        }

        private static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter
                ?? throw new InvalidOperationException($"Building model importer not found: {path}");
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.bakeAxisConversion = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool normalMap, bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException($"Building texture importer not found: {path}");
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial(BuildingReviewAsset asset)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(asset.MaterialPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP/Lit shader was not found.");
            if (material == null)
            {
                material = new Material(shader) { name = $"{asset.Name} Complete" };
                AssetDatabase.CreateAsset(material, asset.MaterialPath);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", LoadTexture(asset, "BaseColor"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", LoadTexture(asset, "Normal"));
            material.SetFloat("_BumpScale", 0.7f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.2f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(BuildingReviewAsset asset, string textureName) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{asset.ArtRoot}/Textures/{asset.Name}_Complete_{textureName}.png")
            ?? throw new InvalidOperationException($"Building texture was not imported: {asset.Name} {textureName}");

        private static void CreatePrefab(BuildingReviewAsset asset, Material material)
        {
            var source = LoadModel(asset.ModelPath);
            var root = new GameObject($"{asset.Name} Complete Scale Review");
            try
            {
                var model = Instantiate(source, root.transform, "Authored Model");
                AssignMaterial(model, material);
                NormalizeUniformByHeight(model, asset.TargetHeight);
                PrefabUtility.SaveAsPrefabAsset(root, asset.PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject LoadModel(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException($"Building model was not imported: {path}");

        private static GameObject Instantiate(GameObject source, Transform parent, string name)
        {
            var instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject
                           ?? throw new InvalidOperationException($"Could not instantiate {source.name}.");
            instance.name = name;
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void AssignMaterial(GameObject model, Material material)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void NormalizeUniformByHeight(GameObject model, float height)
        {
            var bounds = CalculateBounds(model);
            if (bounds.size.y <= 0f) throw new InvalidOperationException($"{model.name} has invalid height.");
            model.transform.localScale = Vector3.one * (height / bounds.size.y);
            GroundAndCenter(model);
        }

        private static void GroundAndCenter(GameObject model)
        {
            var bounds = CalculateBounds(model);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        }

        private static Bounds CalculateBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{model.name} has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
