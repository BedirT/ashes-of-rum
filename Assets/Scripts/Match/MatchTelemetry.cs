using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AshesOfRum
{
    [Serializable]
    public sealed class MatchSummary
    {
        public string matchId;
        public string outcome;
        public float elapsedSeconds;
        public int friendlySuppliesGathered;
        public int hostileSuppliesGathered;
        public int friendlyEntitiesProduced;
        public int hostileEntitiesProduced;
        public int friendlyEntitiesLost;
        public int hostileEntitiesLost;
        public int friendlyBuildingsConstructed;
        public int hostileBuildingsConstructed;
        public int friendlyBuildingsDestroyed;
        public int hostileBuildingsDestroyed;
        public float firstContactSeconds = -1f;
        public float probeAttackSeconds = -1f;
        public float pressureAttackSeconds = -1f;
        public float finalAssaultSeconds = -1f;
        public string destroyedHisar;
    }

    [Serializable]
    public sealed class MatchEvent
    {
        public float elapsedSeconds;
        public string type;
        public string side;
        public string detail;
    }

    [Serializable]
    public sealed class MatchEventLog
    {
        public string matchId;
        public List<MatchEvent> events = new();
    }

    public sealed class MatchTelemetry
    {
        private readonly MatchSummary summary;
        private readonly MatchEventLog eventLog;

        public MatchTelemetry(string matchId = null)
        {
            var id = string.IsNullOrWhiteSpace(matchId)
                ? $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}"
                : matchId;
            summary = new MatchSummary { matchId = id, outcome = MatchOutcome.InProgress.ToString() };
            eventLog = new MatchEventLog { matchId = id };
            Record(0f, "match_started", "system", "The Sundered Road");
        }

        public MatchSummary Summary => summary;
        public MatchEventLog EventLog => eventLog;
        public string SummaryPath { get; private set; }
        public string EventLogPath { get; private set; }

        public void RecordSupplies(bool friendly, int amount, float elapsedSeconds)
        {
            if (amount <= 0) return;
            if (friendly) summary.friendlySuppliesGathered += amount;
            else summary.hostileSuppliesGathered += amount;
            Record(elapsedSeconds, "supplies_deposited", Side(friendly), amount.ToString());
        }

        public void RecordEntityProduced(bool friendly, string detail, float elapsedSeconds)
        {
            if (friendly) summary.friendlyEntitiesProduced++;
            else summary.hostileEntitiesProduced++;
            Record(elapsedSeconds, "entity_produced", Side(friendly), detail);
        }

        public void RecordEntityLost(bool friendly, string detail, float elapsedSeconds)
        {
            if (friendly) summary.friendlyEntitiesLost++;
            else summary.hostileEntitiesLost++;
            Record(elapsedSeconds, "entity_lost", Side(friendly), detail);
        }

        public void RecordBuildingConstructed(bool friendly, string detail, float elapsedSeconds)
        {
            if (friendly) summary.friendlyBuildingsConstructed++;
            else summary.hostileBuildingsConstructed++;
            Record(elapsedSeconds, "building_constructed", Side(friendly), detail);
        }

        public void RecordBuildingDestroyed(bool friendly, string detail, float elapsedSeconds)
        {
            if (friendly) summary.friendlyBuildingsDestroyed++;
            else summary.hostileBuildingsDestroyed++;
            Record(elapsedSeconds, "building_destroyed", Side(friendly), detail);
        }

        public void RecordFirstContact(float elapsedSeconds)
        {
            if (summary.firstContactSeconds >= 0f) return;
            summary.firstContactSeconds = elapsedSeconds;
            Record(elapsedSeconds, "first_contact", "system", "hostile_revealed");
        }

        public void RecordAiAttack(AiPhase phase, float elapsedSeconds)
        {
            switch (phase)
            {
                case AiPhase.Probe:
                    if (summary.probeAttackSeconds >= 0f) return;
                    summary.probeAttackSeconds = elapsedSeconds;
                    break;
                case AiPhase.Pressure:
                    if (summary.pressureAttackSeconds >= 0f) return;
                    summary.pressureAttackSeconds = elapsedSeconds;
                    break;
                case AiPhase.FinalAssault:
                    if (summary.finalAssaultSeconds >= 0f) return;
                    summary.finalAssaultSeconds = elapsedSeconds;
                    break;
                default:
                    return;
            }
            Record(elapsedSeconds, "ai_attack", "Alazhan", phase.ToString());
        }

        public void Complete(MatchOutcome outcome, float elapsedSeconds, string destroyedHisar)
        {
            if (outcome == MatchOutcome.InProgress) throw new ArgumentException("A completed match needs an outcome.");
            summary.outcome = outcome.ToString();
            summary.elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            summary.destroyedHisar = destroyedHisar;
            Record(summary.elapsedSeconds, "hisar_destroyed", "system", destroyedHisar);
            Record(summary.elapsedSeconds, "match_completed", "system", outcome.ToString());
        }

        public void Write(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A telemetry directory is required.");
            Directory.CreateDirectory(directory);
            SummaryPath = Path.Combine(directory, $"{summary.matchId}-summary.json");
            EventLogPath = Path.Combine(directory, $"{summary.matchId}-events.json");
            File.WriteAllText(SummaryPath, JsonUtility.ToJson(summary, true));
            File.WriteAllText(EventLogPath, JsonUtility.ToJson(eventLog, true));
        }

        private void Record(float elapsedSeconds, string type, string side, string detail)
        {
            eventLog.events.Add(new MatchEvent
            {
                elapsedSeconds = Mathf.Max(0f, elapsedSeconds),
                type = type,
                side = side,
                detail = detail
            });
        }

        private static string Side(bool friendly) => friendly ? "Karasungur" : "Alazhan";
    }
}
