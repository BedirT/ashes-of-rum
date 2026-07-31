using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class BuildingScaleReviewAssetSetup
    {
        public const string HousePrefabPath = "Assets/Resources/Presentation/WorldScale/House_Complete.prefab";
        public const string HisarPrefabPath = "Assets/Resources/Presentation/Hisar/Hisar_Complete.prefab";

        private const string HouseModelPath = "Assets/Art/Buildings/House/Models/House_Complete.fbx";
        private const string ResourceRoot = "Assets/Resources/Presentation/WorldScale";

        public static void Configure()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Presentation");
            EnsureFolder("Assets/Resources/Presentation", "WorldScale");

            ConfigureModel(HouseModelPath);
            HisarAssetSetup.Configure();

            var houseMaterial = CreateHouseMaterial();
            CreateHousePrefab(houseMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured House and proportion-preserving Hisar scale-review prefabs.");
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

        private static Material CreateHouseMaterial()
        {
            var material = LoadOrCreateMaterial($"{ResourceRoot}/House_Untextured.mat", "House Untextured");
            material.SetColor("_BaseColor", new Color(0.72f, 0.69f, 0.62f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP/Lit shader was not found.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateHousePrefab(Material material)
        {
            var source = LoadModel(HouseModelPath);
            var root = new GameObject("House Complete Scale Review");
            try
            {
                var model = Instantiate(source, root.transform, "Authored Model");
                AssignMaterial(model, material);
                NormalizeUniformByHeight(model, 3f);
                PrefabUtility.SaveAsPrefabAsset(root, HousePrefabPath);
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
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
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
            if (bounds.size.y <= 0f) throw new InvalidOperationException("House model has invalid height.");
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
