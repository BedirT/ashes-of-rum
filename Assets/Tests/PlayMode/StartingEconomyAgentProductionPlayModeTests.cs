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
        public IEnumerator AgentEconomyCommands_ShareBuildQueueRallyCancellationAndDemolitionRules()
        {
            yield return LoadEconomy();
            var economy = Object.FindAnyObjectByType<StartingEconomyController>();
            var projector = new AgentStateProjector(economy);
            var executor = new AgentCommandExecutor(economy, projector);
            var initial = projector.Project(1);

            Assert.That(initial.hisar.id, Is.EqualTo("hisar"));
            Assert.That(initial.hisar.health, Is.EqualTo(economy.FriendlyHisar.Health));
            Assert.That(initial.hisar.maxHealth, Is.EqualTo(economy.FriendlyHisar.MaxHealth));
            Assert.That(initial.hisar.selected, Is.False);
            Assert.That(initial.hisar.hasRally, Is.False);
            Assert.That(initial.production.count, Is.Zero);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "set_rally", x = -2f, z = -1f
            }, out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("no_selection"));
            Assert.That(economy.HisarRallyPoint, Is.Null);

            var supplies = economy.Supplies;
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "build", buildingType = "Barracks", x = 8f, z = -1f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unsupported_building"));
            Assert.That(economy.Supplies, Is.EqualTo(supplies));
            Assert.That(economy.Workers.All(worker => !worker.IsSelected), Is.True);
            Assert.That(projector.Project(2).buildings, Is.Empty);

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select", actorIds = new[] { "worker-1" }
            }, out rejection), Is.True, rejection);
            economy.CreditSuppliesForAutomation(500);
            foreach (var type in new[] { "House", "Watchtower" })
            {
                var suppliesBeforeBuild = economy.Supplies;
                Assert.That(executor.Execute(new AgentScriptStep
                {
                    action = "build", buildingType = type, x = 8f, z = -1f
                }, out rejection), Is.True, rejection);
                var unfinished = projector.Project(3).buildings.Single();
                Assert.That(executor.Execute(new AgentScriptStep
                {
                    action = "cancel_construction", targetId = unfinished.id
                }, out rejection), Is.True, rejection);
                yield return null;
                Assert.That(projector.Project(4).buildings, Is.Empty);
                Assert.That(economy.Supplies, Is.EqualTo(suppliesBeforeBuild));
            }

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "build", buildingType = "Storehouse", x = 8f, z = -1f
            }, out rejection), Is.True, rejection);
            var storehouseId = projector.Project(5).buildings.Single().id;
            yield return WaitUntil(() => economy.Storehouses.Count == 1 && economy.Storehouses[0].IsComplete);
            var completed = projector.Project(6).buildings.Single();
            Assert.That(completed.id, Is.EqualTo(storehouseId));
            Assert.That(completed.type, Is.EqualTo("Storehouse"));
            Assert.That(completed.complete, Is.True);

            Assert.That(executor.Execute(new AgentScriptStep { action = "select_hisar" }, out rejection), Is.True,
                rejection);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "set_rally", targetId = "cache-2"
            }, out rejection), Is.True, rejection);
            var cacheRally = projector.Project(7).hisar;
            Assert.That(cacheRally.selected, Is.True);
            Assert.That(cacheRally.hasRally, Is.True);
            Assert.That(cacheRally.rallyCacheId, Is.EqualTo("cache-2"));
            Assert.That(cacheRally.rallyPosition.x, Is.EqualTo(economy.Caches[1].transform.position.x));

            var priorRally = economy.HisarRallyPoint;
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "set_rally", targetId = "cache-3"
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unknown_target"));
            Assert.That(economy.HisarRallyPoint, Is.EqualTo(priorRally));
            economy.Caches[1].TakeBatch(economy.Caches[1].Remaining);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "set_rally", targetId = "cache-2"
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("cache_exhausted"));
            Assert.That(economy.HisarRallyPoint, Is.EqualTo(priorRally));
            Assert.That(projector.Project(8).hisar.rallyCacheId, Is.Null,
                "An exhausted rally cache must not remain exposed as a valid cache target.");

            var suppliesBeforeWrongSelection = economy.Supplies;
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "select", actorIds = new[] { "worker-1" }
            }, out rejection), Is.True, rejection);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "train", formationType = "Worker"
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("no_selection"));
            Assert.That(economy.Supplies, Is.EqualTo(suppliesBeforeWrongSelection));
            Assert.That(economy.ProductionQueueCount, Is.Zero);
            Assert.That(executor.Execute(new AgentScriptStep { action = "select_hisar" }, out rejection), Is.True,
                rejection);

            foreach (var type in new[] { "Worker", "Spearmen", "Archers", "Cavalry" })
            {
                economy.CreditSuppliesForAutomation(400);
                var suppliesBeforeTraining = economy.Supplies;
                Assert.That(executor.Execute(new AgentScriptStep
                {
                    action = "train", formationType = type
                }, out rejection), Is.True, rejection);
                var queued = projector.Project(9).production;
                Assert.That(queued.count, Is.EqualTo(1));
                Assert.That(queued.activeItem, Is.EqualTo(type));
                if (type == "Worker")
                {
                    var suppliesAfterQueue = economy.Supplies;
                    Assert.That(executor.Execute(new AgentScriptStep
                    {
                        action = "select", actorIds = new[] { "worker-1" }
                    }, out rejection), Is.True, rejection);
                    Assert.That(executor.Execute(new AgentScriptStep { action = "cancel_production" },
                        out rejection), Is.False);
                    Assert.That(rejection, Is.EqualTo("no_selection"));
                    Assert.That(economy.Supplies, Is.EqualTo(suppliesAfterQueue));
                    Assert.That(economy.ProductionQueueCount, Is.EqualTo(1));
                    Assert.That(executor.Execute(new AgentScriptStep { action = "select_hisar" }, out rejection),
                        Is.True, rejection);
                }
                Assert.That(executor.Execute(new AgentScriptStep { action = "cancel_production" }, out rejection),
                    Is.True, rejection);
                Assert.That(projector.Project(10).production.count, Is.Zero);
                Assert.That(economy.Supplies, Is.EqualTo(suppliesBeforeTraining));
            }
            Assert.That(executor.Execute(new AgentScriptStep { action = "cancel_production" }, out rejection),
                Is.False);
            Assert.That(rejection, Is.EqualTo("queue_empty"));
            var suppliesBeforeUnknown = economy.Supplies;
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "train", formationType = "Swordsmen"
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("unsupported_production"));
            Assert.That(economy.Supplies, Is.EqualTo(suppliesBeforeUnknown));
            Assert.That(economy.ProductionQueueCount, Is.Zero);

            foreach (var worker in economy.Workers.ToArray()) worker.ApplyFixedDamage(worker.MaxHealth);
            yield return null;
            Assert.That(economy.Workers, Is.Empty);
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "set_rally", x = -2f, z = -1f
            }, out rejection), Is.True, rejection);
            var terrainRally = projector.Project(11).hisar;
            Assert.That(terrainRally.rallyCacheId, Is.Null);
            Assert.That(terrainRally.rallyPosition.x, Is.EqualTo(-2f));
            Assert.That(terrainRally.rallyPosition.z, Is.EqualTo(-1f));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "set_rally", x = FogOfWarSystem.MaxX + 1f, z = 0f
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("invalid_position"));
            Assert.That(economy.HisarRallyPoint.Value.x, Is.EqualTo(-2f));

            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "confirm_demolition", targetId = storehouseId
            }, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo("confirmation_required"));
            Assert.That(economy.Storehouses, Has.Count.EqualTo(1));
            var suppliesBeforeDemolition = economy.Supplies;
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "request_demolition", targetId = storehouseId
            }, out rejection), Is.True, rejection);
            Assert.That(projector.Project(12).buildings.Single().id, Is.EqualTo(storehouseId));
            Assert.That(executor.Execute(new AgentScriptStep
            {
                action = "confirm_demolition", targetId = storehouseId
            }, out rejection), Is.True, rejection);
            yield return WaitUntil(() => economy.Storehouses.Count == 0);
            Assert.That(economy.Supplies, Is.EqualTo(suppliesBeforeDemolition));
            Assert.That(projector.Project(13).buildings, Is.Empty);
        }
    }
}
