using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesOfRum
{
    public sealed class SmokeTestRunner : MonoBehaviour
    {
        private const float ScreenshotTimeoutSeconds = 15f;
        private const float EconomyTimeoutSeconds = 20f;
        private const float ConstructionTimeoutSeconds = 20f;

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

            var economy = FindAnyObjectByType<StartingEconomyController>();
            var economyStarted = economy != null && economy.Workers.Count == StartingEconomyController.WorkerCount;
            if (economyStarted)
            {
                economy.IssueGatherForSmoke(economy.Caches[0]);
                var economyDeadline = Time.realtimeSinceStartup + EconomyTimeoutSeconds;
                while (economy.Supplies <= economy.StartingSupplies && Time.realtimeSinceStartup < economyDeadline)
                    yield return null;
            }
            var economyCompleted = economyStarted && economy.Supplies > economy.StartingSupplies;
            var houseStarted = economyCompleted &&
                               economy.TryPlaceHouse(economy.Workers[0], new Vector3(12f, 0f, -1f));
            if (houseStarted)
            {
                var constructionDeadline = Time.realtimeSinceStartup + ConstructionTimeoutSeconds;
                while (economy.PopulationCapacity == 12 && Time.realtimeSinceStartup < constructionDeadline)
                    yield return null;
            }
            var houseCompleted = houseStarted && economy.PopulationCapacity == 20 &&
                                 economy.Houses.Count == 1 && economy.Houses[0].IsComplete;
            yield return null;

            if (graphical)
            {
                var screenshotDirectory = Path.GetDirectoryName(screenshotPath);
                if (!string.IsNullOrEmpty(screenshotDirectory)) Directory.CreateDirectory(screenshotDirectory);
                ScreenCapture.CaptureScreenshot(screenshotPath);
                yield return new WaitForEndOfFrame();
                var deadline = Time.realtimeSinceStartup + ScreenshotTimeoutSeconds;
                while (!HasContent(screenshotPath) && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
            }

            var checks = graphical
                ? new[]
                {
                    "Bootstrap scene loaded",
                    "Required bootstrap objects available",
                    "Development player running",
                    "Starting economy available",
                    "Worker gather deposit completed",
                    "House construction completed",
                    "Population capacity increased",
                    "1920x1080 window configured",
                    "Graphical frame captured"
                }
                : new[]
                {
                    "Bootstrap scene loaded",
                    "Required bootstrap objects available",
                    "Development player running",
                    "Starting economy available",
                    "Worker gather deposit completed",
                    "House construction completed",
                    "Population capacity increased"
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
                Require(economyStarted, checks[3]);
                Require(economyCompleted, $"{checks[4]} within {EconomyTimeoutSeconds} seconds");
                Require(houseCompleted, $"{checks[5]} within {ConstructionTimeoutSeconds} seconds");
                Require(economy.PopulationCapacity == 20, checks[6]);
                if (graphical)
                {
                    Require(Screen.width == 1920 && Screen.height == 1080, checks[7]);
                    Require(HasContent(screenshotPath), $"{checks[8]} within {ScreenshotTimeoutSeconds} seconds");
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

        private static bool HasContent(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
