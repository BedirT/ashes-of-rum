using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesOfRum
{
    public sealed class HisarConstructionPreviewRunner : MonoBehaviour
    {
        public const string PreviewArgument = "-hisar-preview";
        public const string OutputArgument = "-hisar-preview-output";
        public const int FramesPerSecond = 24;

        private const float StageDuration = 2.5f;

        private static readonly HisarBuildState[] States =
        {
            HisarBuildState.Foundation,
            HisarBuildState.RaisedFrame,
            HisarBuildState.CanvasInstallation,
            HisarBuildState.Complete
        };

        private static readonly string[] StageNames =
        {
            "FOUNDATION",
            "RAISED TIMBER FRAME",
            "CANVAS INSTALLATION",
            "COMPLETE HISAR"
        };

        private static readonly string[] StageDescriptions =
        {
            "Ground beams, working deck, and first structural joints",
            "Load-bearing posts and roof frame establish the silhouette",
            "Partial felt roofing closes the command shelter without looking finished",
            "A compact beylik field citadel ready to anchor the encampment"
        };

        private Camera previewCamera;
        private GameObject currentState;
        private int currentStateIndex = -1;
        private float elapsed;
        private string outputDirectory;
        private GUIStyle titleStyle;
        private GUIStyle stageStyle;
        private GUIStyle descriptionStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, PreviewArgument) < 0) return;
            new GameObject("Hisar Construction Preview", typeof(HisarConstructionPreviewRunner));
        }

        private void Awake()
        {
            outputDirectory = ReadArgument(Environment.GetCommandLineArgs(), OutputArgument);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == gameObject || root.GetComponentInChildren<Camera>() != null ||
                    root.GetComponentInChildren<Light>() != null || root.name == "Bootstrap Ground") continue;
                root.SetActive(false);
            }

            previewCamera = Camera.main;
            if (previewCamera == null) throw new InvalidOperationException("Hisar preview requires the main camera.");
            var cameraController = previewCamera.GetComponent<RtsCameraController>();
            if (cameraController != null) cameraController.enabled = false;
            previewCamera.fieldOfView = 36f;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            var ground = GameObject.Find("Bootstrap Ground");
            if (ground != null) ground.transform.localScale = new Vector3(20f, 1f, 20f);
            var groundRenderer = ground?.GetComponent<Renderer>();
            previewCamera.backgroundColor = groundRenderer != null
                ? groundRenderer.sharedMaterial.color
                : new Color(0.36f, 0.31f, 0.25f);

            Time.captureFramerate = string.IsNullOrEmpty(outputDirectory) ? 0 : FramesPerSecond;
            StartCoroutine(PlayPreview());
        }

        private IEnumerator PlayPreview()
        {
            var frame = 0;
            var totalDuration = States.Length * StageDuration;
            while (elapsed < totalDuration)
            {
                var stateIndex = Mathf.Min(States.Length - 1, Mathf.FloorToInt(elapsed / StageDuration));
                if (stateIndex != currentStateIndex) ShowState(stateIndex);

                var stageTime = elapsed - stateIndex * StageDuration;
                currentState.transform.localScale = Vector3.one * Mathf.SmoothStep(0.94f, 1f,
                    Mathf.Clamp01(stageTime / 0.35f));
                PositionCamera(elapsed, totalDuration);

                yield return new WaitForEndOfFrame();
                if (!string.IsNullOrEmpty(outputDirectory)) CaptureFrame(frame++);
                elapsed += string.IsNullOrEmpty(outputDirectory)
                    ? Mathf.Max(Time.unscaledDeltaTime, 0.001f)
                    : 1f / FramesPerSecond;
            }

            elapsed = totalDuration;
            yield return new WaitForEndOfFrame();
            if (string.IsNullOrEmpty(outputDirectory)) yield break;

            File.WriteAllText(Path.Combine(outputDirectory, "preview-complete.json"),
                $"{{\"frames\":{frame},\"fps\":{FramesPerSecond},\"durationSeconds\":{totalDuration:0.0}}}");
            Debug.Log($"Hisar construction preview captured {frame} frames to {outputDirectory}.");
            Application.Quit(0);
        }

        private void ShowState(int index)
        {
            if (currentState != null) Destroy(currentState);
            currentStateIndex = index;
            currentState = HisarPresentation.Create(transform, States[index]);
            currentState.transform.localScale = Vector3.one * 0.94f;
        }

        private void PositionCamera(float time, float totalDuration)
        {
            var progress = time / totalDuration;
            var angle = Mathf.Lerp(-4f, 4f, progress) * Mathf.Deg2Rad;
            var target = new Vector3(0f, 1.3f, 0f);
            previewCamera.transform.position = target + new Vector3(Mathf.Sin(angle) * 7.2f, 4.4f,
                -Mathf.Cos(angle) * 7.2f);
            previewCamera.transform.LookAt(target);
        }

        private void CaptureFrame(int frame)
        {
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            texture.Apply(false);
            File.WriteAllBytes(Path.Combine(outputDirectory, $"frame-{frame:D4}.png"), texture.EncodeToPNG());
            Destroy(texture);
        }

        private void OnGUI()
        {
            if (currentStateIndex < 0) return;
            BuildStyles();
            var width = Mathf.Min(Screen.width * 0.76f, 920f);
            var left = (Screen.width - width) * 0.5f;

            GUI.Label(new Rect(left, 24f, width, 44f), "HISAR CONSTRUCTION CONCEPT", titleStyle);
            GUI.Label(new Rect(left, 66f, width, 38f),
                $"{currentStateIndex + 1} / {States.Length}   {StageNames[currentStateIndex]}", stageStyle);
            GUI.Label(new Rect(left, 102f, width, 32f),
                StageDescriptions[currentStateIndex], descriptionStyle);

            var progressRect = new Rect(left, Screen.height - 56f, width, 8f);
            DrawColorRect(progressRect, new Color(0.24f, 0.20f, 0.15f, 0.95f));
            DrawColorRect(new Rect(progressRect.x, progressRect.y,
                    progressRect.width * Mathf.Clamp01(elapsed / (States.Length * StageDuration)), progressRect.height),
                new Color(0.76f, 0.46f, 0.20f, 1f));
        }

        private void BuildStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.93f, 0.84f, 0.68f) }
            };
            stageStyle = new GUIStyle(titleStyle)
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };
            descriptionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                normal = { textColor = new Color(0.84f, 0.78f, 0.68f) }
            };
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
