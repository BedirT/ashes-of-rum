using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed partial class StartingEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator AgentLifecycleState_IsFogSafeResultGatedAndTerminalSafe()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var projector = new AgentStateProjector(economy);
            var executor = new AgentCommandExecutor(economy, projector);

            var initial = projector.Project(1);
            Assert.That(initial.hisar.health, Is.EqualTo(initial.hisar.maxHealth));
            Assert.That(initial.hisar.attackable, Is.True);
            Assert.That(initial.hisar.destroyed, Is.False);
            Assert.That(initial.hisar.resultActionsAvailable, Is.False);
            Assert.That(executor.TryAuthorizeResultAction(out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("result_required"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "center_camera",
                targetId = "enemy-hisar"
            }, out rejection), Is.False, "An unexplored hostile Hisar must not become a camera oracle.");
            Assert.That(rejection, Is.EqualTo("unknown_target"));
            economy.DeployEnemyForAutomation(FormationType.Cavalry, new Vector3(0f, 0f, 20f));
            projector.Project(2);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "center_camera",
                targetId = "hostile-formation-1"
            }, out rejection), Is.False, "A hidden mobile hostile must not become a camera oracle.");
            Assert.That(rejection, Is.EqualTo("unknown_target"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "center_camera",
                x = FogOfWarSystem.MaxX + 1f,
                z = 0f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("invalid_position"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "center_camera",
                targetId = "hisar"
            }, out rejection), Is.True, rejection);

            economy.DestroyHisarForAutomation(true);
            yield return null;
            var terminal = projector.Project(3);
            Assert.That(terminal.outcome, Is.EqualTo(MatchOutcome.Victory.ToString()));
            Assert.That(terminal.hisar.health, Is.EqualTo(terminal.hisar.maxHealth));
            Assert.That(terminal.hisar.attackable, Is.True);
            Assert.That(terminal.hisar.destroyed, Is.False);
            Assert.That(terminal.hisar.resultActionsAvailable, Is.True);
            Assert.That(executor.TryAuthorizeResultAction(out rejection), Is.True);
            Assert.That(rejection, Is.Null);
            Assert.That(File.Exists(economy.MatchSummaryPath), Is.True);
            Assert.That(File.Exists(economy.MatchEventLogPath), Is.True);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator AgentLifecycleState_ProjectsDestroyedFriendlyHisarAfterDefeat()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var projector = new AgentStateProjector(economy);

            economy.DestroyHisarForAutomation(false);
            yield return null;
            var terminal = projector.Project(1);
            Assert.That(terminal.outcome, Is.EqualTo(MatchOutcome.Defeat.ToString()));
            Assert.That(terminal.hisar.health, Is.Zero);
            Assert.That(terminal.hisar.maxHealth, Is.GreaterThan(0));
            Assert.That(terminal.hisar.attackable, Is.False);
            Assert.That(terminal.hisar.destroyed, Is.True);
            Assert.That(terminal.hisar.resultActionsAvailable, Is.True);
            Time.timeScale = 1f;
        }
    }
}
