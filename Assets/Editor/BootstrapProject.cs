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

        [MenuItem("Ashes of Rum/Regenerate Bootstrap Scene")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject(HarnessContract.RootObjectName);
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
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 8f, -10f), Quaternion.Euler(30f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.2f);
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Bootstrap Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreateNeutralGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Bootstrap Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.3f, 0.32f, 0.34f)
            };
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
