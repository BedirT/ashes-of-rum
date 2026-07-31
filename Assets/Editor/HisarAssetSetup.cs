using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class HisarAssetSetup
    {
        private const string ArtRoot = "Assets/Art/Buildings/Hisar";
        private const string ResourceRoot = "Assets/Resources/Presentation/Hisar";
        private const float TargetHeight = 3.25f;

        private static readonly string[] States =
        {
            "Foundation",
            "RaisedFrame",
            "CanvasInstallation",
            "Complete"
        };

        public static void Configure()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "Presentation");
            EnsureFolder("Assets/Resources/Presentation", "Hisar");
            EnsureFolder(ResourceRoot, "Materials");

            foreach (var state in States)
            {
                ConfigureModel($"{ArtRoot}/Models/Hisar_{state}.fbx");
                ConfigureTexture($"{ArtRoot}/Textures/Hisar_{state}_BaseColor.png", false, true);
                ConfigureTexture($"{ArtRoot}/Textures/Hisar_{state}_Normal.png", true, false);
                ConfigureTexture($"{ArtRoot}/Textures/Hisar_{state}_MaskMap.png", false, false);
            }

            ConfigureTexture($"{ArtRoot}/Textures/Hisar_Complete_Hostile_BaseColor.png", false, true);
            var uniformScale = CalculateUniformScale();

            foreach (var state in States)
            {
                var material = CreateMaterial(state, false);
                CreatePrefab(state, material, false, uniformScale);
            }

            var hostileMaterial = CreateMaterial("Complete", true);
            CreatePrefab("Complete", hostileMaterial, true, uniformScale);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Configured four uniformly scaled Hisar states and the hostile complete presentation.");
        }

        private static void ConfigureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter
                ?? throw new InvalidOperationException($"Hisar model importer not found: {path}");
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.bakeAxisConversion = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool normalMap, bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException($"Hisar texture importer not found: {path}");
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = path.EndsWith("_MaskMap.png", StringComparison.Ordinal)
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial(string state, bool hostile)
        {
            var suffix = hostile ? "_Hostile" : string.Empty;
            var path = $"{ResourceRoot}/Materials/Hisar_{state}{suffix}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException("URP/Lit shader was not found.");
                material = new Material(shader) { name = $"Hisar {state}{suffix}" };
                AssetDatabase.CreateAsset(material, path);
            }

            var baseName = hostile ? "Hisar_Complete_Hostile_BaseColor" : $"Hisar_{state}_BaseColor";
            material.SetTexture("_BaseMap", LoadTexture($"{ArtRoot}/Textures/{baseName}.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", LoadTexture($"{ArtRoot}/Textures/Hisar_{state}_Normal.png"));
            material.SetFloat("_BumpScale", 0.75f);
            material.SetTexture("_MetallicGlossMap", LoadTexture($"{ArtRoot}/Textures/Hisar_{state}_MaskMap.png"));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string path) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidOperationException($"Hisar texture was not imported: {path}");

        private static void CreatePrefab(string state, Material material, bool hostile, float uniformScale)
        {
            var modelPath = $"{ArtRoot}/Models/Hisar_{state}.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)
                ?? throw new InvalidOperationException($"Hisar model was not imported: {modelPath}");
            var root = new GameObject(hostile ? "Hisar Complete Hostile" : $"Hisar {state}");
            try
            {
                var model = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject
                    ?? throw new InvalidOperationException($"Could not instantiate Hisar model: {modelPath}");
                model.name = "Authored Model";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
                model.transform.localScale = Vector3.one;

                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                NormalizeModel(model, uniformScale, state == "Complete");
                var suffix = hostile ? "_Hostile" : string.Empty;
                PrefabUtility.SaveAsPrefabAsset(root, $"{ResourceRoot}/Hisar_{state}{suffix}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static float CalculateUniformScale()
        {
            var path = $"{ArtRoot}/Models/Hisar_Complete.fbx";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new InvalidOperationException($"Hisar model was not imported: {path}");
            var root = new GameObject("Hisar Scale Probe");
            try
            {
                var model = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject
                    ?? throw new InvalidOperationException($"Could not instantiate Hisar model: {path}");
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f));
                model.transform.localScale = Vector3.one;
                var bounds = CalculateBounds(model);
                if (bounds.size.y <= 0f) throw new InvalidOperationException("Complete Hisar has invalid height.");
                return TargetHeight / bounds.size.y;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void NormalizeModel(GameObject model, float uniformScale, bool requireGameplayFootprint)
        {
            var bounds = CalculateBounds(model);
            if (bounds.size.x <= 0f || bounds.size.z <= 0f)
                throw new InvalidOperationException($"Hisar model has invalid bounds: {model.name}");

            model.transform.localScale = Vector3.one * uniformScale;
            bounds = CalculateBounds(model);
            model.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            bounds = CalculateBounds(model);
            if (requireGameplayFootprint && (bounds.size.x > HisarPresentation.FootprintSize.x + 0.01f ||
                bounds.size.z > HisarPresentation.FootprintSize.z + 0.01f))
                throw new InvalidOperationException(
                    $"Hisar model {model.name} exceeds the logical footprint: {bounds.size}");
        }

        private static Bounds CalculateBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException($"Hisar model has no renderers: {model.name}");
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
