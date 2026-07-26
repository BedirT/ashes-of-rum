using System.IO;
using AshesOfRum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class BootstrapProject
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string TuningPath = "Assets/Settings/StartingEconomyTuning.asset";

        [MenuItem("Ashes of Rum/Regenerate Bootstrap Scene")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var tuning = LoadOrCreateTuning();
            var root = new GameObject(HarnessContract.RootObjectName);
            var economy = root.AddComponent<StartingEconomyController>();
            economy.Configure(tuning);
            CreateCamera();
            CreateLighting();
            CreateNeutralGround();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Ashes of Rum";
            PlayerSettings.companyName = "Ashes of Rum Prototype";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"BOOTSTRAP_COMPLETE:{ScenePath}");
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject(HarnessContract.CameraObjectName);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 19f, -18f), Quaternion.Euler(48f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.2f);
            cameraObject.AddComponent<RtsCameraController>();
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Bootstrap Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreateNeutralGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Bootstrap Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 7f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.43f, 0.34f, 0.23f)
            };
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static EconomyTuning LoadOrCreateTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<EconomyTuning>(TuningPath);
            if (tuning != null) return tuning;
            tuning = ScriptableObject.CreateInstance<EconomyTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
            return tuning;
        }
    }
}
