using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Tests
{
    public sealed class HarnessContractTests
    {
        [Test]
        public void HasRequiredObjects_RequiresRootAndCamera()
        {
            Assert.That(HarnessContract.HasRequiredObjects(name => name == HarnessContract.RootObjectName), Is.False);
            Assert.That(HarnessContract.HasRequiredObjects(
                name => name == HarnessContract.RootObjectName || name == HarnessContract.CameraObjectName), Is.True);
        }

        [Test]
        public void PlayerWindow_MatchesLockedResolution()
        {
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1920));
            Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(1080));
            Assert.That(PlayerSettings.fullScreenMode, Is.EqualTo(FullScreenMode.Windowed));
        }

        [Test]
        public void AgentScript_UsesSupportedVersionAndUniqueSteps()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures",
                "agent-starting-economy.json"));

            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.schemaVersion, Is.EqualTo(AgentProtocol.SchemaVersion));
            Assert.That(script.scenario, Is.EqualTo("starting-economy-gather-deposit"));
            Assert.That(script.steps, Has.Length.EqualTo(8));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "capture"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "end_session"));
        }

        [Test]
        public void HouseAgentScript_UsesRealBuildAndCompletionSteps()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures",
                "agent-house-construction.json"));

            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.schemaVersion, Is.EqualTo(AgentProtocol.SchemaVersion));
            Assert.That(script.scenario, Is.EqualTo("house-construction-completion"));
            Assert.That(script.steps, Has.Length.EqualTo(6));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "build" && step.buildingType == "House"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "wait" && step.condition == "building_complete"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "capture"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "end_session"));
        }

        [Test]
        public void TrainingAgentScript_UsesRealGatherQueueAndCompletionSteps()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures",
                "agent-formation-training.json"));

            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.schemaVersion, Is.EqualTo(AgentProtocol.SchemaVersion));
            Assert.That(script.scenario, Is.EqualTo("spearmen-training-completion"));
            Assert.That(script.steps, Has.Length.EqualTo(11));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "select_hisar"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "train" && step.formationType == "Spearmen"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "wait" && step.condition == "formation_ready" &&
                step.formationType == "Spearmen"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "capture"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "end_session"));
        }

        [Test]
        public void FormationMovementAgentScript_UsesStableSelectionMoveAndArrivalSteps()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures",
                "agent-formation-movement.json"));

            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.schemaVersion, Is.EqualTo(AgentProtocol.SchemaVersion));
            Assert.That(script.scenario, Is.EqualTo("spearmen-movement-arrival"));
            Assert.That(script.steps, Has.Length.EqualTo(14));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "select_hisar"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "select" && step.actorIds.Length == 1 && step.actorIds[0] == "formation-1"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "wait" && step.condition == "formation_arrived" &&
                step.targetId == "formation-1"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "capture"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "end_session"));
        }

        [Test]
        public void FormationCombatAgentScript_UsesScoutStopFocusAndDamageSteps()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures",
                "agent-formation-combat.json"));

            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.scenario, Is.EqualTo("spearmen-scout-and-focus-combat"));
            Assert.That(script.steps, Has.Length.EqualTo(21));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "select_hisar"));
            Assert.That(script.steps, Has.Exactly(2).Matches<AgentScriptStep>(step =>
                step.action == "attack_move"));
            Assert.That(script.steps, Has.Exactly(2).Matches<AgentScriptStep>(step => step.action == "stop"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "focus" && step.targetId == "hostile-worker-1"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "wait" && step.condition == "hostile_target_damaged" &&
                step.targetId == "hostile-worker-1"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "wait" && step.condition == "hostile_worker_in_focus_range" &&
                step.targetId == "hostile-worker-1"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "capture"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "end_session"));
        }

        [Test]
        public void EconomyProductionAgentScript_SelectsBeforeConfirmedDemolition()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures",
                "agent-economy-production.json"));

            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.scenario, Is.EqualTo("economy-production-cancellation-rally-demolition"));
            Assert.That(script.steps, Has.Length.EqualTo(30));
            var selectIndex = System.Array.FindIndex(script.steps, step => step.action == "select_building" &&
                step.targetId == "building-2");
            var requestIndex = System.Array.FindIndex(script.steps, step => step.action == "request_demolition" &&
                step.targetId == "building-2");
            var confirmIndex = System.Array.FindIndex(script.steps, step => step.action == "confirm_demolition" &&
                step.targetId == "building-2");
            Assert.That(selectIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(requestIndex, Is.EqualTo(selectIndex + 1));
            Assert.That(confirmIndex, Is.EqualTo(requestIndex + 1));
        }

        [Test]
        public void AgentHash_IsStableAndUsesSha256()
        {
            Assert.That(AgentProtocol.Sha256("Ashes of Rum"),
                Is.EqualTo("eb09395648365bc034d4726eb70302fe9305ce70fc83c0c73a16496706d7c544"));
            Assert.That(AgentProtocol.Sha256("Ashes of Rum"), Has.Length.EqualTo(64));
        }

        [Test]
        public void LiveMailbox_ValidatesIdentityOrderSizeAndImmutableResponses()
        {
            var root = Path.Combine(Path.GetTempPath(), "ashes-agent-live-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var mailbox = new AgentLiveMailbox(root, new string('a', 40));
                Assert.That(File.Exists(Path.Combine(root, "ready.json")), Is.False);
                mailbox.PublishReady();
                Assert.That(File.Exists(Path.Combine(root, "ready.json")), Is.True);
                var ready = JsonUtility.FromJson<AgentLiveReady>(File.ReadAllText(Path.Combine(root, "ready.json")));
                Assert.That(ready.result, Is.EqualTo(mailbox.ResultPath));
                Assert.That(ready.artifacts, Is.EqualTo(mailbox.Artifacts));
                var request = new AgentLiveRequest
                {
                    schemaVersion = AgentProtocol.SchemaVersion,
                    sessionId = mailbox.SessionId,
                    sequence = 1,
                    requestId = "observe-1",
                    command = new AgentScriptStep { action = "observe" }
                };
                File.WriteAllText(mailbox.RequestPath(1), JsonUtility.ToJson(request));
                Assert.That(mailbox.TryRead(1, out var parsed, out var rejection), Is.True);
                Assert.That(rejection, Is.Null);
                Assert.That(parsed.command.action, Is.EqualTo("observe"));

                request.sequence = 2;
                request.command.action = "move";
                File.WriteAllText(mailbox.RequestPath(2), JsonUtility.ToJson(request));
                Assert.That(mailbox.TryRead(2, out _, out rejection), Is.True);
                Assert.That(rejection, Is.EqualTo("request_id_conflict"));

                File.WriteAllBytes(mailbox.RequestPath(3), new byte[AgentLiveMailbox.MaximumRequestBytes + 1]);
                Assert.That(mailbox.TryRead(3, out _, out rejection), Is.True);
                Assert.That(rejection, Is.EqualTo("request_too_large"));

                request.sequence = 4;
                request.requestId = "wrong-session";
                request.sessionId = "another-session";
                File.WriteAllText(mailbox.RequestPath(4), JsonUtility.ToJson(request));
                Assert.That(mailbox.TryRead(4, out _, out rejection), Is.True);
                Assert.That(rejection, Is.EqualTo("wrong_session"));

                request.sequence = 99;
                request.requestId = "wrong-sequence";
                request.sessionId = mailbox.SessionId;
                File.WriteAllText(mailbox.RequestPath(5), JsonUtility.ToJson(request));
                Assert.That(mailbox.TryRead(5, out _, out rejection), Is.True);
                Assert.That(rejection, Is.EqualTo("wrong_sequence"));

                File.WriteAllText(mailbox.RequestPath(6), "{not-json");
                Assert.That(mailbox.TryRead(6, out _, out rejection), Is.True);
                Assert.That(rejection, Is.EqualTo("malformed_request"));

                mailbox.Publish(1, new AgentProtocolResponse { schemaVersion = 1, sequence = 1 });
                Assert.Throws<System.IO.IOException>(() => mailbox.Publish(1,
                    new AgentProtocolResponse { schemaVersion = 1, sequence = 1 }));

                mailbox.PublishResult(new AgentLiveResult
                {
                    schemaVersion = 1,
                    sessionId = mailbox.SessionId,
                    buildSha = new string('a', 40),
                    passed = true,
                    terminalAction = "end_session",
                    processedResponses = 1,
                    outboxPath = mailbox.Outbox,
                    artifactsPath = mailbox.Artifacts,
                    checkpointManifests = new[] { "checkpoint.json" }
                });
                var result = JsonUtility.FromJson<AgentLiveResult>(File.ReadAllText(mailbox.ResultPath));
                Assert.That(result.passed, Is.True);
                Assert.That(result.processedResponses, Is.EqualTo(1));
                Assert.That(result.checkpointManifests, Is.EqualTo(new[] { "checkpoint.json" }));
                Assert.Throws<System.IO.IOException>(() => mailbox.PublishResult(result));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [TestCase("agent-complete-match-restart.json", "complete-match-victory-restart", 28, "restart")]
        [TestCase("agent-complete-match-quit.json", "complete-match-victory-quit", 26, "quit")]
        public void CompleteMatchAgentScripts_UseOnlyRealEconomyAndPlayerCommands(string file, string scenario,
            int steps, string lifecycleAction)
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "scripts", "fixtures", file));
            var script = AgentProtocol.LoadScript(path);

            Assert.That(script.scenario, Is.EqualTo(scenario));
            Assert.That(script.steps, Has.Length.EqualTo(steps));
            Assert.That(script.steps.Count(step => step.action == "capture"), Is.GreaterThanOrEqualTo(2));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == "wait" && step.condition == "outcome_is" && step.targetId == "Victory"));
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step =>
                step.action == lifecycleAction));
            Assert.That(script.steps, Has.None.Matches<AgentScriptStep>(step => step.action is
                "credit_supplies" or "spawn" or "damage" or "advance_time" or "reveal" or "suspend_ai"));
        }
    }
}
