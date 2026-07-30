using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class ArcherPresentationPreviewRunner : MonoBehaviour
    {
        [Serializable]
        private sealed class PreviewResult
        {
            public bool passed;
            public int friendlyArchers;
            public int hostileArchers;
            public float lowestFootY;
            public float highestFootY;
            public string screenshotPath;
            public string error;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            if (!HasArgument("--archer-preview")) return;
            var runner = new GameObject("ArcherPresentationPreviewRunner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<ArcherPresentationPreviewRunner>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();

            var result = new PreviewResult();
            StartingEconomyController economy;
            FormationAgent friendly;
            FormationAgent hostile;
            try
            {
                economy = FindAnyObjectByType<StartingEconomyController>()
                    ?? throw new InvalidOperationException("Starting economy was not found.");
                economy.SetOpponentEnabledForAutomation(false);

                friendly = economy.DeployFriendlyForAutomation(FormationType.Archers,
                    new Vector3(-2.6f, 0f, 5.5f));
                hostile = economy.DeployEnemyForAutomation(FormationType.Archers,
                    new Vector3(2.6f, 0f, 5.5f));
                hostile.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var showFactionMarkers = HasArgument("--archer-preview-show-factions");

                var camera = Camera.main ?? throw new InvalidOperationException("Main camera was not found.");
                var controller = camera.GetComponent<RtsCameraController>();
                if (controller != null) controller.enabled = false;
                var target = new Vector3(0f, 0.8f, 4.8f);
                camera.transform.position = target + new Vector3(0f, 7.2f, -7.4f);
                camera.transform.LookAt(target);
                camera.fieldOfView = showFactionMarkers ? 36f : 32f;

                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    canvas.gameObject.SetActive(false);
                HideGameplayMarkers(friendly, !showFactionMarkers);
                HideGameplayMarkers(hostile, !showFactionMarkers);

                if (HasArgument("--archer-preview-single"))
                {
                    var friendlyPresentations = friendly.GetComponentsInChildren<ArcherMemberPresentation>();
                    foreach (var presentation in friendlyPresentations.Skip(1))
                        presentation.gameObject.SetActive(false);
                    hostile.gameObject.SetActive(false);

                    var inspectedArcher = friendlyPresentations[0].transform.position;
                    target = new Vector3(inspectedArcher.x, 0.9f, inspectedArcher.z);
                    var cameraOffset = HasArgument("--archer-preview-front")
                        ? new Vector3(0f, 1.2f, 3.2f)
                        : new Vector3(0f, 1.2f, -3.2f);
                    camera.transform.position = target + cameraOffset;
                    camera.transform.LookAt(target);
                    camera.fieldOfView = 28f;
                }
            }
            catch (Exception exception)
            {
                result.error = exception.Message;
                Finish(result);
                yield break;
            }

            for (var frame = 0; frame < 14; frame++) yield return new WaitForEndOfFrame();

            var singlePreview = HasArgument("--archer-preview-single");
            var presentations = friendly.GetComponentsInChildren<ArcherMemberPresentation>(true)
                .Concat(hostile.GetComponentsInChildren<ArcherMemberPresentation>(true))
                .Where(presentation => !singlePreview || presentation.gameObject.activeInHierarchy).ToArray();
            result.friendlyArchers = friendly.MemberCount;
            result.hostileArchers = hostile.MemberCount;
            result.lowestFootY = presentations.Min(presentation => presentation.WorldBottomY);
            result.highestFootY = presentations.Max(presentation => presentation.WorldBottomY);

            if (result.friendlyArchers != 8 || result.hostileArchers != 8)
                result.error = "The preview did not spawn two complete Archer formations.";
            else if (presentations.Length != (singlePreview ? 1 : 16) || presentations.Any(presentation =>
                         Mathf.Abs(presentation.WorldBottomY) > 0.1f))
                result.error = "One or more animated Archers are not grounded.";
            result.screenshotPath = GetArgumentValue("--archer-preview-screenshot")
                ?? Path.Combine(Application.persistentDataPath, "archer-preview.png");
            var screenshotDirectory = Path.GetDirectoryName(result.screenshotPath);
            if (!string.IsNullOrEmpty(screenshotDirectory)) Directory.CreateDirectory(screenshotDirectory);
            ScreenCapture.CaptureScreenshot(result.screenshotPath);
            var screenshotDeadline = Time.realtimeSinceStartup + 15f;
            while ((!File.Exists(result.screenshotPath) || new FileInfo(result.screenshotPath).Length == 0) &&
                   Time.realtimeSinceStartup < screenshotDeadline)
                yield return null;
            if (!File.Exists(result.screenshotPath) || new FileInfo(result.screenshotPath).Length == 0)
                result.error = "The close-up Archer screenshot was not created.";
            result.passed = string.IsNullOrEmpty(result.error);
            Finish(result);
        }

        private static void Finish(PreviewResult result)
        {
            var outputPath = GetArgumentValue("--archer-preview-output")
                ?? Path.Combine(Application.persistentDataPath, "archer-preview.json");
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            Debug.Log($"ARCHER_PREVIEW:{(result.passed ? "PASS" : "FAIL")}:{outputPath}");
            Application.Quit(result.passed ? 0 : 1);
        }

        private static void HideGameplayMarkers(FormationAgent formation, bool hideFactionMarkers)
        {
            foreach (var itemRenderer in formation.GetComponentsInChildren<Renderer>(true))
            {
                if (itemRenderer.GetComponent<FormationSelectionRing>() != null ||
                    itemRenderer.GetComponent<FormationFrontIndicator>() != null ||
                    hideFactionMarkers && (itemRenderer.name == "Black Falcon Diamond" ||
                                           itemRenderer.name == "Living Flame Square"))
                    itemRenderer.gameObject.SetActive(false);
            }
        }

        private static bool HasArgument(string name) =>
            Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string GetArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
