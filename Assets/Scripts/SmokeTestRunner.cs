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
            yield return null;
            yield return new WaitForEndOfFrame();

            var checks = new[] { "Bootstrap scene loaded", "Required bootstrap objects available" };
            var result = new SmokeResult
            {
                scene = SceneManager.GetActiveScene().name,
                checks = checks
            };

            try
            {
                Require(result.scene == HarnessContract.SceneName, checks[0]);
                Require(HarnessContract.HasRequiredObjects(name => GameObject.Find(name) != null), checks[1]);
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
