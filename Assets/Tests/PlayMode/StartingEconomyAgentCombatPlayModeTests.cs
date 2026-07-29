using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesOfRum.Tests
{
    public sealed partial class StartingEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator AgentCombatCommands_ObserveFogSafeTargetsAndShareFormationOrders()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var friendly = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(0f, 0f, 0f));
            var hostile = economy.DeployEnemyForAutomation(FormationType.Cavalry,
                new Vector3(0f, 0f, 12f));
            var projector = new AgentStateProjector(economy);
            var executor = new AgentCommandExecutor(economy, projector);

            var hidden = projector.Project(1);
            Assert.That(hidden.visibleHostileFormations, Is.Empty);
            Assert.That(hidden.visibleHostileWorkers, Is.Empty);
            Assert.That(hidden.visibleHostileStructures, Is.Empty);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "focus",
                targetId = "hostile-formation-1"
            }, out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unknown_target"));
            Assert.That(economy.TryIssueFocusCommand(hostile, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("no_selection"));

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "formation-1" }
            }, out rejection), Is.True, rejection);
            Assert.That(economy.TryIssueFocusCommand(hostile, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("target_not_visible"));
            Assert.That(friendly.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "attack_move",
                x = 0f,
                z = 4f
            }, out rejection), Is.True, rejection);
            Assert.That(friendly.CurrentOrder, Is.EqualTo(FormationOrder.AttackMove));
            Assert.That(executor.Execute(new AgentScriptStep { action = "stop" }, out rejection), Is.True,
                rejection);
            Assert.That(friendly.CurrentOrder, Is.EqualTo(FormationOrder.Idle));

            var scout = new GameObject("Agent combat visibility scout");
            scout.transform.position = hostile.transform.position;
            economy.FogOfWar.RegisterFriendly(scout.transform);
            economy.FogOfWar.RefreshNow();
            var visible = projector.Project(2);
            Assert.That(visible.visibleHostileFormations, Has.Length.EqualTo(1));
            Assert.That(visible.visibleHostileFormations[0].id, Is.EqualTo("hostile-formation-1"));
            Assert.That(visible.visibleHostileFormations[0].type, Is.EqualTo("Cavalry"));
            Assert.That(projector.TryResolveVisibleHostileFormation("hostile-formation-1", out var resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(hostile));

            hostile.transform.position = new Vector3(0f, 0f, 4f);
            scout.transform.position = hostile.transform.position;
            economy.FogOfWar.RefreshNow();
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "focus",
                targetId = "hostile-formation-1"
            }, out rejection), Is.True, rejection);
            Assert.That(friendly.CurrentOrder, Is.EqualTo(FormationOrder.Focus));
            Assert.That(friendly.Target, Is.SameAs(hostile));
            yield return WaitUntil(() => hostile.TotalMemberHealth < hostile.MaximumMemberHealth);
            var engaged = projector.Project(3);
            Assert.That(engaged.formations.Single().targetId, Is.EqualTo("hostile-formation-1"));
            Assert.That(engaged.visibleHostileFormations.Single().totalHealth,
                Is.LessThan(engaged.visibleHostileFormations.Single().maxHealth));

            scout.transform.position = economy.EnemyHisar.transform.position;
            economy.FogOfWar.RefreshNow();
            var structures = projector.Project(4);
            Assert.That(structures.visibleHostileStructures.Any(item => item.id == "enemy-hisar"), Is.True);
            Assert.That(structures.visibleHostileWorkers, Is.Not.Empty);
            Assert.That(structures.visibleHostileWorkers[0].id, Is.EqualTo("hostile-worker-1"));
            Assert.That(projector.TryResolveVisibleHostileStructure("enemy-hisar", out var enemyHisar), Is.True);
            Assert.That(enemyHisar, Is.SameAs(economy.EnemyHisar));

            hostile.IssueStop();
            hostile.transform.position = new Vector3(0f, 0f, 20f);
            scout.transform.position = economy.FriendlyHisar.transform.position;
            economy.FogOfWar.RefreshNow();
            Assert.That(projector.Project(5).visibleHostileFormations, Is.Empty,
                "A hostile mobile formation must disappear immediately outside current vision.");
            Assert.That(projector.TryResolveVisibleHostileFormation("hostile-formation-1", out _), Is.False);

            Object.Destroy(scout);
            Object.Destroy(hostile.gameObject);
        }
    }
}
