using System.IO;
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
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "quit"));
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
            Assert.That(script.steps, Has.Exactly(1).Matches<AgentScriptStep>(step => step.action == "quit"));
        }

        [Test]
        public void AgentHash_IsStableAndUsesSha256()
        {
            Assert.That(AgentProtocol.Sha256("Ashes of Rum"),
                Is.EqualTo("eb09395648365bc034d4726eb70302fe9305ce70fc83c0c73a16496706d7c544"));
            Assert.That(AgentProtocol.Sha256("Ashes of Rum"), Has.Length.EqualTo(64));
        }
    }
}
