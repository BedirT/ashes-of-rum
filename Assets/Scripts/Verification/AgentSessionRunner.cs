using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class AgentSessionRunner : MonoBehaviour
    {
        private const float StartupTimeoutSeconds = 20f;
        private const float CaptureTimeoutSeconds = 15f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            if (GetArgumentValue("--agent-script") == null) return;
            var runner = new GameObject("Agent Session Runner");
            DontDestroyOnLoad(runner);
            runner.AddComponent<AgentSessionRunner>().StartCoroutine(runner.GetComponent<AgentSessionRunner>().Run());
        }

        private IEnumerator Run()
        {
            var scriptPath = GetArgumentValue("--agent-script");
            var outputPath = GetArgumentValue("--agent-output");
            var resultPath = GetArgumentValue("--agent-result");
            var artifactDirectory = GetArgumentValue("--agent-artifacts");
            var screenshotPath = GetArgumentValue("--agent-screenshot");
            var buildSha = GetArgumentValue("--agent-build-sha");
            AgentScript script = null;
            var completedSteps = 0;
            var manifests = new List<string>();
            string failure = null;

            try
            {
                if (!Debug.isDebugBuild) throw new InvalidOperationException("Agent mode requires a Development build.");
                RequirePath(outputPath, "--agent-output");
                RequirePath(resultPath, "--agent-result");
                RequirePath(artifactDirectory, "--agent-artifacts");
                if (!IsSha(buildSha)) throw new InvalidOperationException("--agent-build-sha must be a full Git SHA.");
                script = AgentProtocol.LoadScript(scriptPath);
                CreateParent(outputPath);
                CreateParent(resultPath);
                Directory.CreateDirectory(artifactDirectory);
                File.WriteAllText(outputPath, string.Empty);
                if (!string.IsNullOrWhiteSpace(screenshotPath))
                {
                    CreateParent(screenshotPath);
                    Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                }
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }

            StartingEconomyController economy = null;
            if (failure == null)
            {
                var startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
                while (economy == null && Time.realtimeSinceStartup < startupDeadline)
                {
                    economy = FindAnyObjectByType<StartingEconomyController>();
                    yield return null;
                }
                if (economy == null) failure = "Starting economy was not available before the startup timeout.";
            }

            if (failure == null)
            {
                var projector = new AgentStateProjector(economy, buildSha);
                var executor = new AgentCommandExecutor(economy, projector);
                for (var index = 0; index < script.steps.Length; index++)
                {
                    var step = script.steps[index];
                    var sequence = index + 1;
                    var response = new AgentProtocolResponse
                    {
                        schemaVersion = AgentProtocol.SchemaVersion,
                        sequence = sequence,
                        requestId = step.id,
                        action = step.action
                    };
                    if (step.action == "wait")
                    {
                        var conditionPassed = false;
                        yield return WaitForCondition(step, economy, projector, value => conditionPassed = value);
                        response.accepted = conditionPassed;
                        response.rejectionCode = conditionPassed ? null : "condition_timeout";
                    }
                    else if (step.action == "capture")
                    {
                        var captureSucceeded = false;
                        yield return CaptureCheckpoint(step, sequence, projector, artifactDirectory, screenshotPath,
                            response, manifests, value => captureSucceeded = value);
                        response.accepted = captureSucceeded;
                        if (!captureSucceeded) response.rejectionCode = "capture_failed";
                    }
                    else if (step.action == "quit")
                    {
                        response.accepted = true;
                    }
                    else
                    {
                        response.accepted = executor.Execute(step, out var rejectionCode);
                        response.rejectionCode = rejectionCode;
                    }

                    response.state ??= projector.Project(sequence);
                    File.AppendAllText(outputPath, JsonUtility.ToJson(response) + Environment.NewLine);
                    completedSteps++;
                    if (!response.accepted)
                    {
                        failure = $"Step {step.id} was rejected: {response.rejectionCode}.";
                        break;
                    }
                    if (step.action == "quit") break;
                    yield return null;
                }
            }

            var result = new AgentSessionResult
            {
                schemaVersion = AgentProtocol.SchemaVersion,
                buildSha = buildSha,
                passed = failure == null && script != null && completedSteps == script.steps.Length,
                scenario = script?.scenario,
                completedSteps = completedSteps,
                outputPath = outputPath,
                checkpointManifests = manifests.ToArray(),
                error = failure
            };
            if (!string.IsNullOrWhiteSpace(resultPath))
            {
                CreateParent(resultPath);
                File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            }
            Debug.Log($"AGENT_SESSION:{(result.passed ? "PASS" : "FAIL")}:{resultPath}");
            yield return null;
            Application.Quit(result.passed ? 0 : 1);
        }

        private static IEnumerator WaitForCondition(AgentScriptStep step, StartingEconomyController economy,
            AgentStateProjector projector, Action<bool> completed)
        {
            var timeout = step.timeoutSeconds > 0f ? step.timeoutSeconds : 15f;
            var deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ConditionMet(step, economy, projector))
                {
                    completed(true);
                    yield break;
                }
                yield return null;
            }
            completed(false);
        }

        private static bool ConditionMet(AgentScriptStep step, StartingEconomyController economy,
            AgentStateProjector projector)
        {
            switch (step.condition)
            {
                case "supplies_greater_than":
                    return economy.Supplies > step.value;
                case "worker_idle":
                    return projector.TryResolveWorker(step.targetId, out var worker) &&
                           worker.CurrentActivity == WorkerAgent.Activity.Idle;
                default:
                    return false;
            }
        }

        private static IEnumerator CaptureCheckpoint(AgentScriptStep step, int sequence,
            AgentStateProjector projector, string artifactDirectory, string screenshotPath,
            AgentProtocolResponse response, ICollection<string> manifests, Action<bool> completed)
        {
            if (!IsSafeCheckpoint(step.checkpoint))
            {
                completed(false);
                yield break;
            }
            var previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForEndOfFrame();
            var state = projector.Project(sequence);
            var statePath = Path.Combine(artifactDirectory, $"{step.checkpoint}-state.json");
            var manifestPath = Path.Combine(artifactDirectory, $"{step.checkpoint}-frame.json");
            File.WriteAllText(statePath, JsonUtility.ToJson(state, true));

            var screenshotSha = string.Empty;
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
                ScreenCapture.CaptureScreenshot(screenshotPath);
                var deadline = Time.realtimeSinceStartup + CaptureTimeoutSeconds;
                while ((!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0) &&
                       Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
                {
                    Time.timeScale = previousTimeScale;
                    completed(false);
                    yield break;
                }
                screenshotSha = AgentProtocol.Sha256File(screenshotPath);
            }

            var manifest = new AgentFrameManifest
            {
                schemaVersion = AgentProtocol.SchemaVersion,
                buildSha = state.buildSha,
                checkpoint = step.checkpoint,
                sequence = sequence,
                elapsedMilliseconds = state.elapsedMilliseconds,
                width = string.IsNullOrWhiteSpace(screenshotPath) ? 0 : Screen.width,
                height = string.IsNullOrWhiteSpace(screenshotPath) ? 0 : Screen.height,
                statePath = statePath,
                stateHash = state.stateHash,
                screenshotPath = screenshotPath,
                screenshotSha256 = screenshotSha,
                camera = state.camera
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            response.state = state;
            response.checkpointStatePath = statePath;
            response.frameManifestPath = manifestPath;
            response.screenshotPath = screenshotPath;
            manifests.Add(manifestPath);
            Time.timeScale = previousTimeScale;
            completed(true);
        }

        private static bool IsSafeCheckpoint(string value) => !string.IsNullOrWhiteSpace(value) &&
            value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

        private static bool IsSha(string value) => value?.Length == 40 &&
            value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static void RequirePath(string path, string argument)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException($"{argument} is required.");
        }

        private static void CreateParent(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        }

        private static string GetArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (arguments[index] == name) return arguments[index + 1];
            return null;
        }
    }
}
