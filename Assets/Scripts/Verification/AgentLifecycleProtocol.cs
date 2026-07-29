using System;
using UnityEngine;

namespace AshesOfRum
{
    [Serializable]
    public sealed class AgentFrameManifest
    {
        public int schemaVersion;
        public string buildSha;
        public string checkpoint;
        public int sequence;
        public int elapsedMilliseconds;
        public int width;
        public int height;
        public string statePath;
        public string stateHash;
        public string stateSha256;
        public string screenshotPath;
        public string screenshotSha256;
        public AgentCameraState camera;
    }

    [Serializable]
    public sealed class AgentSessionResult
    {
        public int schemaVersion;
        public string buildSha;
        public bool passed;
        public string scenario;
        public int completedSteps;
        public string outputPath;
        public string[] checkpointManifests;
        public string matchSummaryPath;
        public string matchSummarySha256;
        public string matchEventLogPath;
        public string matchEventLogSha256;
        public string error;
    }

    public sealed partial class AgentStateProjector
    {
        public bool TryResolveCameraTarget(AgentScriptStep step, out Vector3 position)
        {
            SynchronizeIds();
            if (string.IsNullOrWhiteSpace(step.targetId))
            {
                position = new Vector3(step.x, 0f, step.z);
                return true;
            }
            if (step.targetId == "hisar" && economy.FriendlyHisar != null)
            {
                position = economy.FriendlyHisar.transform.position;
                return true;
            }
            if (TryResolveWorker(step.targetId, out var worker))
            {
                position = worker.transform.position;
                return true;
            }
            if (TryResolveFormation(step.targetId, out var formation))
            {
                position = formation.transform.position;
                return true;
            }
            if (TryResolveBuilding(step.targetId, out var building))
            {
                position = building.transform.position;
                return true;
            }
            if (TryResolveVisibleHostileFormation(step.targetId, out var hostileFormation))
            {
                position = hostileFormation.transform.position;
                return true;
            }
            if (TryResolveVisibleHostileWorker(step.targetId, out var hostileWorker))
            {
                position = hostileWorker.transform.position;
                return true;
            }
            if (TryResolveVisibleHostileStructure(step.targetId, out var hostileStructure))
            {
                position = hostileStructure.TargetComponent.transform.position;
                return true;
            }
            if (hostileStructureMemory.TryGetValue(step.targetId, out var remembered))
            {
                position = new Vector3(remembered.state.position.x, remembered.state.position.y,
                    remembered.state.position.z);
                return true;
            }
            position = default;
            return false;
        }
    }

    public sealed partial class AgentCommandExecutor
    {
        public bool TryAuthorizeResultAction(out string rejectionCode)
        {
            if (economy.Outcome == MatchOutcome.InProgress)
            {
                rejectionCode = "result_required";
                return false;
            }
            rejectionCode = null;
            return true;
        }

        public void RestartMatch() => economy.RestartMatch();

        public void QuitMatch() => economy.RequestQuit();

        private bool ExecuteCenterCamera(AgentScriptStep step, out string rejectionCode)
        {
            if (string.IsNullOrWhiteSpace(step.targetId) &&
                (!float.IsFinite(step.x) || !float.IsFinite(step.z) || step.x < FogOfWarSystem.MinX ||
                 step.x > FogOfWarSystem.MaxX || step.z < FogOfWarSystem.MinZ || step.z > FogOfWarSystem.MaxZ))
            {
                rejectionCode = "invalid_position";
                return false;
            }
            if (!projector.TryResolveCameraTarget(step, out var position))
            {
                rejectionCode = "unknown_target";
                return false;
            }
            var cameraController = Camera.main != null ? Camera.main.GetComponent<RtsCameraController>() : null;
            if (cameraController == null)
            {
                rejectionCode = "camera_unavailable";
                return false;
            }
            cameraController.CenterOn(position);
            rejectionCode = null;
            return true;
        }
    }
}
