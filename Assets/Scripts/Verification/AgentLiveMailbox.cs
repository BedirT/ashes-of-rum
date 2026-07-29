using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AshesOfRum
{
    [Serializable]
    public sealed class AgentLiveReady
    {
        public int schemaVersion;
        public string sessionId;
        public string buildSha;
        public int nextSequence;
        public string inbox;
        public string outbox;
        public string artifacts;
        public string result;
    }

    [Serializable]
    public sealed class AgentLiveRequest
    {
        public int schemaVersion;
        public string sessionId;
        public int sequence;
        public string requestId;
        public AgentScriptStep command;
    }

    [Serializable]
    public sealed class AgentLiveResult
    {
        public int schemaVersion;
        public string sessionId;
        public string buildSha;
        public bool passed;
        public string terminalAction;
        public string error;
        public int processedResponses;
        public string outboxPath;
        public string artifactsPath;
        public string[] checkpointManifests;
        public string matchSummaryPath;
        public string matchSummarySha256;
        public string matchEventLogPath;
        public string matchEventLogSha256;
    }

    public sealed class AgentLiveMailbox
    {
        public const int MaximumRequestBytes = 64 * 1024;
        private readonly Dictionary<string, string> requestHashes = new(StringComparer.Ordinal);

        public AgentLiveMailbox(string root, string buildSha)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("--agent-live-dir is required.");
            Root = Path.GetFullPath(root);
            if (Directory.Exists(Root) || File.Exists(Root))
                throw new InvalidOperationException("Agent live session directory must not already exist.");
            SessionId = Guid.NewGuid().ToString("N");
            BuildSha = buildSha;
            Inbox = Path.Combine(Root, "inbox");
            Outbox = Path.Combine(Root, "outbox");
            Artifacts = Path.Combine(Root, "artifacts");
            Directory.CreateDirectory(Inbox);
            Directory.CreateDirectory(Outbox);
            Directory.CreateDirectory(Artifacts);
        }

        public string BuildSha { get; }
        public string ResultPath => Path.Combine(Root, "result.json");

        public void PublishReady()
        {
            AtomicWrite(Path.Combine(Root, "ready.json"), JsonUtility.ToJson(new AgentLiveReady
            {
                schemaVersion = AgentProtocol.SchemaVersion,
                sessionId = SessionId,
                buildSha = BuildSha,
                nextSequence = 1,
                inbox = Inbox,
                outbox = Outbox,
                artifacts = Artifacts,
                result = ResultPath
            }, true));
        }

        public string Root { get; }
        public string SessionId { get; }
        public string Inbox { get; }
        public string Outbox { get; }
        public string Artifacts { get; }

        public string RequestPath(int sequence) => Path.Combine(Inbox, SequenceName(sequence));
        public string ResponsePath(int sequence) => Path.Combine(Outbox, SequenceName(sequence));

        public bool TryRead(int sequence, out AgentLiveRequest request, out string rejectionCode)
        {
            request = null;
            rejectionCode = null;
            var path = RequestPath(sequence);
            if (!File.Exists(path)) return false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length > MaximumRequestBytes)
                {
                    rejectionCode = "request_too_large";
                    return true;
                }
                var json = File.ReadAllText(path, Encoding.UTF8);
                request = JsonUtility.FromJson<AgentLiveRequest>(json);
                if (request == null || request.command == null)
                    rejectionCode = "malformed_request";
                else if (request.schemaVersion != AgentProtocol.SchemaVersion)
                    rejectionCode = "unsupported_schema";
                else if (!string.Equals(request.sessionId, SessionId, StringComparison.Ordinal))
                    rejectionCode = "wrong_session";
                else if (request.sequence != sequence)
                    rejectionCode = "wrong_sequence";
                else if (string.IsNullOrWhiteSpace(request.requestId) || request.requestId.Length > 128)
                    rejectionCode = "invalid_request_id";
                else if (string.IsNullOrWhiteSpace(request.command.action))
                    rejectionCode = "invalid_command";
                else
                {
                    var hash = AgentProtocol.Sha256(json);
                    if (requestHashes.TryGetValue(request.requestId, out var previous))
                        rejectionCode = previous == hash ? "duplicate_request" : "request_id_conflict";
                    else
                        requestHashes.Add(request.requestId, hash);
                }
            }
            catch (Exception)
            {
                rejectionCode = "malformed_request";
            }
            return true;
        }

        public void Publish(int sequence, AgentProtocolResponse response)
        {
            var path = ResponsePath(sequence);
            if (File.Exists(path)) throw new IOException($"Response {sequence} already exists.");
            AtomicWrite(path, JsonUtility.ToJson(response, true));
        }

        public void PublishResult(AgentLiveResult result)
        {
            if (File.Exists(ResultPath)) throw new IOException("Live session result already exists.");
            AtomicWrite(ResultPath, JsonUtility.ToJson(result, true));
        }

        public static void AtomicWrite(string path, string value)
        {
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, value, new UTF8Encoding(false));
            File.Move(temporary, path);
        }

        private static string SequenceName(int sequence) =>
            sequence.ToString("D6", CultureInfo.InvariantCulture) + ".json";
    }
}
