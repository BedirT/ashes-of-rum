using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesOfRum
{
    public sealed class SmokeTestRunner : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeResult
        {
            public bool passed;
            public string scene;
            public string[] checks;
            public string error;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            if (!HasArgument("--smoke-test")) return;
            var runner = new GameObject("SmokeTestRunner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<SmokeTestRunner>().StartCoroutine(Run());
        }

        private static IEnumerator Run()
        {
            var screenshotPath = GetArgumentValue("--smoke-screenshot");
            var graphical = !string.IsNullOrEmpty(screenshotPath);
            if (graphical) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();

            if (graphical)
            {
                var screenshotDirectory = Path.GetDirectoryName(screenshotPath);
                if (!string.IsNullOrEmpty(screenshotDirectory)) Directory.CreateDirectory(screenshotDirectory);
                ScreenCapture.CaptureScreenshot(screenshotPath);
                yield return new WaitForEndOfFrame();
                yield return new WaitUntil(() => File.Exists(screenshotPath));
            }

            var checks = graphical
                ? new[]
                {
                    "Bootstrap scene loaded",
                    "Required bootstrap objects available",
                    "Development player running",
                    "1920x1080 window configured",
                    "Graphical frame captured"
                }
                : new[]
                {
                    "Bootstrap scene loaded",
                    "Required bootstrap objects available",
                    "Development player running"
                };
            var result = new SmokeResult
            {
                scene = SceneManager.GetActiveScene().name,
                checks = checks
            };

            try
            {
                Require(result.scene == HarnessContract.SceneName, checks[0]);
                Require(HarnessContract.HasRequiredObjects(name => GameObject.Find(name) != null), checks[1]);
                Require(Debug.isDebugBuild, checks[2]);
                if (graphical)
                {
                    Require(Screen.width == 1920 && Screen.height == 1080, checks[3]);
                    Require(File.Exists(screenshotPath), checks[4]);
                }

                result.passed = true;
            }
            catch (Exception exception)
            {
                result.error = exception.Message;
            }

            var outputPath = GetArgumentValue("--smoke-output")
                ?? Path.Combine(Application.persistentDataPath, "smoke-result.json");
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            Debug.Log($"SMOKE_TEST:{(result.passed ? "PASS" : "FAIL")}:{outputPath}");
            yield return null;
            Application.Quit(result.passed ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
