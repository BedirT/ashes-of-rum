using System.IO;
using AshesOfRum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshesOfRum.Editor
{
    public static class BootstrapProject
    {
        private const string ScenePath = "Assets/Scenes/SunderedRoad.unity";

        [MenuItem("Ashes of Rum/Regenerate Battlefield Shell")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            CreateBattlefield();
            CreateStronghold("Karasungur Hisar", new Vector3(-38f, 1.5f, 0f), new Color(0.08f, 0.3f, 0.7f));
            CreateStronghold("Alazhan Hisar", new Vector3(38f, 1.5f, 0f), new Color(0.72f, 0.12f, 0.08f));
            CreateCamera();
            CreateHud();

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

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Highland Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.82f, 0.62f);
            light.intensity = 1.4f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            RenderSettings.ambientLight = new Color(0.28f, 0.3f, 0.32f);
        }

        private static void CreateBattlefield()
        {
            CreateBlock("Dry Highland Ground", Vector3.zero, new Vector3(120f, 1f, 80f), new Color(0.35f, 0.3f, 0.2f));
            CreateBlock("Sundered Road", new Vector3(0f, 0.55f, 0f), new Vector3(112f, 0.15f, 9f), new Color(0.48f, 0.39f, 0.25f));

            var obstacleColor = new Color(0.25f, 0.22f, 0.18f);
            var positions = new[]
            {
                new Vector3(-18f, 2f, -18f), new Vector3(-10f, 2f, 20f),
                new Vector3(4f, 2f, -22f), new Vector3(13f, 2f, 19f),
                new Vector3(25f, 2f, -16f), new Vector3(-29f, 2f, 15f)
            };
            for (var i = 0; i < positions.Length; i++)
            {
                CreateBlock($"Rock Outcrop {i + 1}", positions[i], new Vector3(7f, 4f, 6f), obstacleColor);
            }
        }

        private static void CreateStronghold(string name, Vector3 position, Color color)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            CreateBlock("Keep", position, new Vector3(10f, 5f, 10f), color, root.transform);
            var offsets = new[]
            {
                new Vector3(-6f, 1f, -6f), new Vector3(6f, 1f, -6f),
                new Vector3(-6f, 1f, 6f), new Vector3(6f, 1f, 6f)
            };
            for (var i = 0; i < offsets.Length; i++)
            {
                CreateBlock($"Tower {i + 1}", position + offsets[i], new Vector3(3f, 7f, 3f), color, root.transform);
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("RTS Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 55f, -42f), Quaternion.Euler(52f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 26f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.38f, 0.45f, 0.48f);
            cameraObject.AddComponent<RTSCameraController>();
        }

        private static void CreateHud()
        {
            var canvasObject = new GameObject("Battle HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Header");
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(0f, 78f);
            panel.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.08f, 0.9f);

            CreateText(panel.transform, "ASHES OF RUM  |  SUNDERED ROAD", new Vector2(28f, -18f), 28, TextAnchor.UpperLeft);
            CreateText(panel.transform, "Karasungur Beylik  |  WASD / edge pan  |  Middle-drag  |  Wheel zoom  |  Esc quit", new Vector2(28f, -49f), 18, TextAnchor.UpperLeft);

            var buttonObject = new GameObject("Quit Button");
            buttonObject.transform.SetParent(panel.transform, false);
            var buttonRect = buttonObject.AddComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-24f, -18f);
            buttonRect.sizeDelta = new Vector2(120f, 44f);
            var button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<Image>().color = new Color(0.5f, 0.12f, 0.08f, 1f);
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(Object.FindFirstObjectByType<RTSCameraController>().QuitGame);
            CreateCenteredText(buttonObject.transform, "QUIT", 20);

            var eventSystemObject = new GameObject("Event System");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void CreateText(Transform parent, string value, Vector2 position, int size, TextAnchor alignment)
        {
            var textObject = new GameObject(value);
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1350f, 30f);
            ConfigureText(textObject.AddComponent<Text>(), value, size, alignment);
        }

        private static void CreateCenteredText(Transform parent, string value, int size)
        {
            var textObject = new GameObject("Label");
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            ConfigureText(textObject.AddComponent<Text>(), value, size, TextAnchor.MiddleCenter);
        }

        private static void ConfigureText(Text text, string value, int size, TextAnchor alignment)
        {
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color, Transform parent = null)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, true);
            block.transform.position = position;
            block.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }
    }
}
