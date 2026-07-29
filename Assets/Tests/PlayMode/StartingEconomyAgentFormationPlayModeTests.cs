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
        public IEnumerator AgentFormationCommands_SelectMoveArriveAndProjectStableState()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var friendly = economy.DeployFriendlyForAutomation(FormationType.Spearmen,
                new Vector3(-5f, 0f, -1f));
            var hostile = economy.DeployEnemyForAutomation(FormationType.Archers,
                new Vector3(5f, 0f, 1f));
            var projector = new AgentStateProjector(economy);
            var executor = new AgentCommandExecutor(economy, projector);

            Assert.That(projector.TryResolveFormation("formation-1", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(friendly));
            Assert.That(projector.TryResolveFormation("formation-2", out _), Is.False,
                "Hostile formations must never enter player-addressable state.");
            Assert.That(economy.TryIssueFormationMoveCommand(Vector3.zero, out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("no_selection"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "worker-1", "formation-1" }
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("mixed_actor_types"));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select",
                actorIds = new[] { "formation-1" }
            }, out rejection), Is.True, rejection);
            Assert.That(friendly.IsSelected, Is.True);
            Assert.That(economy.Workers.All(worker => !worker.IsSelected), Is.True);

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = FogOfWarSystem.MaxX + 1f,
                z = 0f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("invalid_position"));
            Assert.That(friendly.CurrentOrder, Is.EqualTo(FormationOrder.Idle));

            var destination = new Vector3(4f, 0f, 5f);
            var blocker = CreateRouteBlocker("Agent formation destination blocker",
                destination + Vector3.up, new Vector3(1.5f, 2f, 1.5f));
            yield return new WaitForSeconds(1f);
            Assert.That(friendly.CanReach(destination), Is.False);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = destination.x,
                z = destination.z
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unreachable"));
            Assert.That(friendly.CurrentOrder, Is.EqualTo(FormationOrder.Idle));
            Object.Destroy(blocker);
            yield return new WaitForSeconds(1f);

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "move",
                x = destination.x,
                z = destination.z
            }, out rejection), Is.True, rejection);
            var moving = projector.Project(1).formations.Single();
            Assert.That(moving.id, Is.EqualTo("formation-1"));
            Assert.That(moving.selected, Is.True);
            Assert.That(moving.order, Is.EqualTo("Move"));
            Assert.That(moving.hasDestination, Is.True);
            Assert.That(moving.destination.x, Is.EqualTo(destination.x));
            Assert.That(moving.destination.z, Is.EqualTo(destination.z));

            yield return WaitUntil(() => friendly.CurrentOrder == FormationOrder.Idle &&
                                         !friendly.HasDestination);
            var arrived = projector.Project(2).formations.Single();
            Assert.That(arrived.id, Is.EqualTo(moving.id));
            Assert.That(arrived.selected, Is.True);
            Assert.That(arrived.order, Is.EqualTo("Idle"));
            Assert.That(arrived.hasDestination, Is.False);
            Assert.That(new Vector2(arrived.position.x - destination.x, arrived.position.z - destination.z)
                .sqrMagnitude, Is.LessThanOrEqualTo(0.25f));

            Object.Destroy(hostile.gameObject);
        }
    }
}
