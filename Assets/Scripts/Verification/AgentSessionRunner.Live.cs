using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public sealed partial class AgentSessionRunner
    {
        private const float DefaultLiveIdleTimeoutSeconds = 120f;
        private const float MaximumLiveIdleTimeoutSeconds = 600f;
        private const float MaximumLiveWaitTimeoutSeconds = 120f;
        private const int MaximumCapturePathCharacters = 900;

        private IEnumerator RunLive()
        {
            AgentLiveMailbox mailbox = null;
            var buildSha = GetArgumentValue("--agent-build-sha");
            var screenshotsEnabled = GetArgumentValue("--agent-screenshot") != null;
            var simulationSpeed = AgentVerificationSpeed.Default;
            float idleTimeout;
            try
            {
                if (!Debug.isDebugBuild) throw new InvalidOperationException("Agent mode requires a Development build.");
                if (GetArgumentValue("--agent-script") != null)
                    throw new InvalidOperationException("--agent-script and --agent-live-dir are mutually exclusive.");
                if (!IsSha(buildSha)) throw new InvalidOperationException("--agent-build-sha must be a full Git SHA.");
                if (!AgentVerificationSpeed.TryRead(Environment.GetCommandLineArgs(), out simulationSpeed,
                        out var speedError))
                    throw new InvalidOperationException(speedError);
                mailbox = new AgentLiveMailbox(GetArgumentValue("--agent-live-dir"), buildSha);
                idleTimeout = ParseLiveIdleTimeout();
                if (screenshotsEnabled) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            }
            catch (Exception exception)
            {
                if (mailbox != null)
                    PublishLiveResult(mailbox, false, "initialization_failure", exception.Message, 0,
                        Array.Empty<string>(), string.Empty, string.Empty, string.Empty, string.Empty);
                Debug.LogError($"AGENT_LIVE:FAIL:{exception.Message}");
                Application.Quit(1);
                yield break;
            }

            StartingEconomyController economy = null;
            var startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while (economy == null && Time.realtimeSinceStartup < startupDeadline)
            {
                economy = FindAnyObjectByType<StartingEconomyController>();
                yield return null;
            }
            if (economy == null)
            {
                PublishLiveResult(mailbox, false, "startup_failure", "starting_economy_unavailable", 0,
                    Array.Empty<string>(), string.Empty, string.Empty, string.Empty, string.Empty);
                Debug.LogError("AGENT_LIVE:FAIL:Starting economy unavailable.");
                Application.Quit(1);
                yield break;
            }
            AgentVerificationSpeed.Apply(simulationSpeed);

            var projector = new AgentStateProjector(economy, buildSha);
            var executor = new AgentCommandExecutor(economy, projector);
            var sequence = 1;
            var manifests = new List<string>();
            var matchSummaryPath = string.Empty;
            var matchSummarySha256 = string.Empty;
            var matchEventLogPath = string.Empty;
            var matchEventLogSha256 = string.Empty;
            mailbox.PublishReady();
            var lastRequestTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (!mailbox.TryRead(sequence, out var request, out var validationRejection))
                {
                    if (Time.realtimeSinceStartup - lastRequestTime >= idleTimeout)
                    {
                        PublishLiveResult(mailbox, false, "idle_timeout", "idle_timeout", sequence - 1,
                            manifests, matchSummaryPath, matchSummarySha256, matchEventLogPath,
                            matchEventLogSha256);
                        Debug.LogError($"AGENT_LIVE:FAIL:idle_timeout:{sequence}");
                        Application.Quit(1);
                        yield break;
                    }
                    yield return null;
                    continue;
                }

                lastRequestTime = Time.realtimeSinceStartup;
                var step = request?.command;
                var response = new AgentProtocolResponse
                {
                    schemaVersion = AgentProtocol.SchemaVersion,
                    sessionId = mailbox.SessionId,
                    sequence = sequence,
                    requestId = request?.requestId,
                    action = step?.action,
                    accepted = false,
                    rejectionCode = validationRejection
                };
                var endSession = false;
                var shippedQuit = false;
                if (validationRejection == null)
                {
                    var commandRejection = ValidateLiveCommand(step, mailbox.Artifacts, screenshotsEnabled);
                    if (commandRejection != null)
                    {
                        response.rejectionCode = commandRejection;
                    }
                    else if (step.action == "wait")
                    {
                        var passed = false;
                        yield return WaitForCondition(step, economy, projector, value => passed = value);
                        response.accepted = passed;
                        response.rejectionCode = passed ? null : "condition_timeout";
                    }
                    else if (step.action == "capture")
                    {
                        var captured = false;
                        yield return CaptureCheckpoint(step, sequence, projector, mailbox.Artifacts,
                            screenshotsEnabled ? mailbox.Root : null, response, manifests,
                            value => captured = value);
                        response.accepted = captured;
                        response.rejectionCode = captured ? null : "capture_failed";
                    }
                    else if (step.action == "end_session")
                    {
                        response.accepted = true;
                        endSession = true;
                    }
                    else if (step.action == "restart")
                    {
                        if (!executor.TryAuthorizeResultAction(out var rejection))
                            response.rejectionCode = rejection;
                        else
                        {
                            CaptureTelemetry(economy, ref matchSummaryPath, ref matchSummarySha256,
                                ref matchEventLogPath, ref matchEventLogSha256);
                            var previous = economy;
                            executor.RestartMatch();
                            yield return RebindAfterRestart(previous, value => economy = value);
                            response.accepted = economy != null && economy.Outcome == MatchOutcome.InProgress;
                            response.rejectionCode = response.accepted ? null : "restart_failed";
                            if (response.accepted)
                            {
                                AgentVerificationSpeed.Apply(simulationSpeed);
                                projector = new AgentStateProjector(economy, buildSha);
                                executor = new AgentCommandExecutor(economy, projector);
                            }
                        }
                    }
                    else if (step.action == "quit")
                    {
                        response.accepted = executor.TryAuthorizeResultAction(out var rejection);
                        response.rejectionCode = rejection;
                        shippedQuit = response.accepted;
                        if (response.accepted)
                            CaptureTelemetry(economy, ref matchSummaryPath, ref matchSummarySha256,
                                ref matchEventLogPath, ref matchEventLogSha256);
                    }
                    else
                    {
                        response.accepted = executor.Execute(step, out var rejection);
                        response.rejectionCode = rejection;
                    }
                }

                response.state ??= projector.Project(sequence);
                mailbox.Publish(sequence, response);
                Debug.Log($"AGENT_LIVE:RESPONSE:{sequence}:{response.accepted}");
                sequence++;
                if (shippedQuit)
                {
                    PublishLiveResult(mailbox, true, "quit", null, sequence - 1, manifests,
                        matchSummaryPath, matchSummarySha256, matchEventLogPath, matchEventLogSha256);
                    executor.QuitMatch();
                    yield break;
                }
                if (endSession)
                {
                    PublishLiveResult(mailbox, true, "end_session", null, sequence - 1, manifests,
                        matchSummaryPath, matchSummarySha256, matchEventLogPath, matchEventLogSha256);
                    Debug.Log("AGENT_LIVE:PASS:end_session");
                    Application.Quit(0);
                    yield break;
                }
                yield return null;
            }
        }

        private static void PublishLiveResult(AgentLiveMailbox mailbox, bool passed, string terminalAction,
            string error, int processedResponses, ICollection<string> manifests, string summaryPath,
            string summaryHash, string eventPath, string eventHash)
        {
            mailbox.PublishResult(new AgentLiveResult
            {
                schemaVersion = AgentProtocol.SchemaVersion,
                sessionId = mailbox.SessionId,
                buildSha = mailbox.BuildSha,
                passed = passed,
                terminalAction = terminalAction,
                error = error,
                processedResponses = processedResponses,
                outboxPath = mailbox.Outbox,
                artifactsPath = mailbox.Artifacts,
                checkpointManifests = manifests.ToArray(),
                matchSummaryPath = summaryPath,
                matchSummarySha256 = summaryHash,
                matchEventLogPath = eventPath,
                matchEventLogSha256 = eventHash
            });
        }

        private static float ParseLiveIdleTimeout()
        {
            var value = GetArgumentValue("--agent-live-idle-timeout");
            if (string.IsNullOrWhiteSpace(value)) return DefaultLiveIdleTimeoutSeconds;
            if (!float.TryParse(value, out var parsed) || !float.IsFinite(parsed) || parsed <= 0f)
                throw new InvalidOperationException("--agent-live-idle-timeout must be positive.");
            return Mathf.Min(parsed, MaximumLiveIdleTimeoutSeconds);
        }

        private static string ValidateLiveCommand(AgentScriptStep step, string artifactDirectory,
            bool screenshotsEnabled)
        {
            if (step.action == "wait" && (!float.IsFinite(step.timeoutSeconds) || step.timeoutSeconds < 0f ||
                                           step.timeoutSeconds > MaximumLiveWaitTimeoutSeconds))
                return "invalid_timeout";
            if (step.action != "capture") return null;
            if (!IsSafeCheckpoint(step.checkpoint)) return "invalid_checkpoint";
            try
            {
                var paths = new[]
                {
                    Path.Combine(artifactDirectory, $"{step.checkpoint}-state.json"),
                    Path.Combine(artifactDirectory, $"{step.checkpoint}-frame.json"),
                    screenshotsEnabled ? Path.Combine(artifactDirectory, $"{step.checkpoint}.png") : string.Empty
                };
                return paths.Where(path => !string.IsNullOrEmpty(path))
                    .Any(path => Path.GetFullPath(path).Length > MaximumCapturePathCharacters)
                    ? "invalid_checkpoint"
                    : null;
            }
            catch (Exception)
            {
                return "invalid_checkpoint";
            }
        }
    }
}
