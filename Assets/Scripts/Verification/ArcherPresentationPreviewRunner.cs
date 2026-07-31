using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesOfRum
{
    public sealed class ArcherPresentationPreviewRunner : MonoBehaviour
    {
        private ArcherMemberPresentation[] interactiveArchers = Array.Empty<ArcherMemberPresentation>();
        private FormationAgent interactiveFormation;
        private Coroutine interactiveTurn;
        private string interactiveState = ArcherMemberPresentation.IdleState;

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
            var preview = runner.AddComponent<ArcherPresentationPreviewRunner>();
            preview.StartCoroutine(preview.Run());
        }

        private IEnumerator Run()
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
                    target = new Vector3(inspectedArcher.x, 1.05f, inspectedArcher.z);
                    var cameraOffset = HasArgument("--archer-preview-side")
                        ? new Vector3(5.4f, 1.4f, 0f)
                        : HasArgument("--archer-preview-front")
                            ? new Vector3(0f, 1.4f, 5.4f)
                            : new Vector3(0f, 1.4f, -5.4f);
                    camera.transform.position = target + cameraOffset;
                    camera.transform.LookAt(target);
                    camera.fieldOfView = 30f;
                }
                else if (HasArgument("--archer-preview-formation"))
                {
                    hostile.gameObject.SetActive(false);
                    target = friendly.transform.position + new Vector3(0f, 0.9f, 0f);
                    camera.transform.position = target + new Vector3(0f, 7.4f, -8.2f);
                    camera.transform.LookAt(target);
                    camera.fieldOfView = 34f;
                }

                if (TryGetArgumentVector3("--archer-preview-bow-rotation", out var bowRotation))
                {
                    foreach (var attachment in friendly.GetComponentsInChildren<AuthoredEquipmentAttachment>(true)
                                 .Concat(hostile.GetComponentsInChildren<AuthoredEquipmentAttachment>(true)))
                        if (attachment.AttachmentId == "Bow") attachment.transform.localEulerAngles = bowRotation;
                }
            }
            catch (Exception exception)
            {
                result.error = exception.Message;
                Finish(result);
                yield break;
            }

            for (var frame = 0; frame < 14; frame++) yield return new WaitForEndOfFrame();

            if (HasArgument("--archer-preview-interactive"))
            {
                interactiveArchers = friendly.GetComponentsInChildren<ArcherMemberPresentation>()
                    .Where(presentation => presentation.gameObject.activeInHierarchy).ToArray();
                interactiveFormation = friendly;
                PlayInteractive(ArcherMemberPresentation.IdleState, true);
                yield break;
            }

            if (HasArgument("--archer-preview-attack"))
            {
                var inspectedArcher = friendly.GetComponentsInChildren<FormationMemberVisual>()
                    .First(presentation => presentation.gameObject.activeInHierarchy &&
                                           presentation.HasAuthoredPresentation);
                inspectedArcher.ShowAttack();
                var attackTime = GetArgumentFloat("--archer-preview-attack-time", 0.25f);
                yield return new WaitForSeconds(attackTime);
                yield return new WaitForEndOfFrame();
            }

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

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (interactiveArchers.Length == 0 || keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) PlayInteractive(ArcherMemberPresentation.IdleState);
            if (keyboard.digit2Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.PreviewWalkForwardState);
            if (keyboard.digit3Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.MoveState);
            if (keyboard.digit4Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.PreviewAimWalkForwardState);
            if (keyboard.digit5Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.TurnLeftState);
            if (keyboard.digit6Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.TurnRightState);
            if (keyboard.digit7Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.PreviewWalkLeftState);
            if (keyboard.digit8Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.PreviewWalkRightState);
            if (keyboard.digit9Key.wasPressedThisFrame)
                PlayInteractive(ArcherMemberPresentation.PreviewWalkBackwardState);
            if (keyboard.aKey.wasPressedThisFrame) PlayInteractive(ArcherMemberPresentation.AttackState);
            if (keyboard.hKey.wasPressedThisFrame) PlayInteractive(ArcherMemberPresentation.HitState);
            if (keyboard.dKey.wasPressedThisFrame) PlayInteractive(ArcherMemberPresentation.DeathState);
            if (keyboard.rKey.wasPressedThisFrame) PlayInteractive(interactiveState, true);
            if (keyboard.escapeKey.wasPressedThisFrame) Application.Quit();
        }

        private void PlayInteractive(string state, bool immediate = false)
        {
            interactiveState = state;
            var looping = state == ArcherMemberPresentation.IdleState ||
                          state == ArcherMemberPresentation.MoveState ||
                          state.StartsWith("PreviewWalk", StringComparison.Ordinal) ||
                          state == ArcherMemberPresentation.PreviewAimWalkForwardState;
            foreach (var archer in interactiveArchers)
            {
                if (immediate) archer.PlayImmediate(state);
                else if (looping) archer.PlayLoop(state);
                else archer.Play(state);
            }
            if (state == ArcherMemberPresentation.TurnLeftState ||
                state == ArcherMemberPresentation.TurnRightState)
            {
                if (interactiveTurn != null) StopCoroutine(interactiveTurn);
                interactiveTurn = StartCoroutine(PreviewFormationTurn(
                    state == ArcherMemberPresentation.TurnRightState ? 90f : -90f));
            }
        }

        private IEnumerator PreviewFormationTurn(float degrees)
        {
            var start = interactiveFormation.transform.rotation;
            var end = start * Quaternion.Euler(0f, degrees, 0f);
            var elapsed = 0f;
            const float duration = 0.45f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                interactiveFormation.transform.rotation = Quaternion.Slerp(start, end,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            interactiveFormation.transform.rotation = end;
            interactiveTurn = null;
        }

        private void OnGUI()
        {
            if (interactiveArchers.Length == 0) return;
            GUI.Box(new Rect(20f, 20f, 680f, 205f), "Archer Animation Preview");
            GUI.Label(new Rect(40f, 50f, 640f, 25f),
                "1 Idle   2 Old Sneaky Walk   3 New Formation March   4 AimWalkForward");
            GUI.Label(new Rect(40f, 80f, 640f, 25f),
                "5 TurnLeft90   6 TurnRight90   7 WalkLeft   8 WalkRight");
            GUI.Label(new Rect(40f, 110f, 640f, 25f),
                "9 WalkBackward   A AimRecoil   H HitFront   D DeathBackward");
            GUI.Label(new Rect(40f, 140f, 640f, 25f),
                "R Restart current animation   Esc Close viewer");
            GUI.Label(new Rect(40f, 170f, 640f, 25f),
                $"Current: {interactiveState}   Archers: {interactiveArchers.Length}");
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

        private static float GetArgumentFloat(string name, float fallback) =>
            float.TryParse(GetArgumentValue(name), out var value) ? Mathf.Max(0f, value) : fallback;

        private static bool TryGetArgumentVector3(string name, out Vector3 value)
        {
            var parts = (GetArgumentValue(name) ?? string.Empty).Split(',');
            if (parts.Length == 3 && float.TryParse(parts[0], out var x) &&
                float.TryParse(parts[1], out var y) && float.TryParse(parts[2], out var z))
            {
                value = new Vector3(x, y, z);
                return true;
            }

            value = default;
            return false;
        }
    }
}
