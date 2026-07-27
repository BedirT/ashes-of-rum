using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AshesOfRum
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class WorkerAgent : MonoBehaviour
    {
        public enum Activity
        {
            Idle,
            Moving,
            GoingToCache,
            Gathering,
            Returning,
            GoingToConstruction,
            Constructing
        }

        private enum DeferredOrder
        {
            None,
            Move,
            Gather,
            Construct
        }

        private NavMeshAgent agent;
        private EconomyTuning tuning;
        private EconomyWallet wallet;
        private Hisar hisar;
        private Func<Vector3, Vector3> resolveDropOff;
        private Func<Vector3, bool> isCurrentlyVisible;
        private IReadOnlyList<ResourceCache> knownCaches;
        private Action<string> notifyEconomyState;
        private ResourceCache targetCache;
        private GameObject selectionRing;
        private GameObject carriedBundle;
        private float gatherReadyAt;
        private int gatherSlot;
        private DeferredOrder deferredOrder;
        private Vector3 deferredDestination;
        private ResourceCache deferredCache;
        private ConstructibleBuilding deferredBuilding;
        private ConstructibleBuilding construction;
        private ResourceCache resumeCache;
        private Action<ConstructibleBuilding> constructionCompleted;
        private Action<int> suppliesDeposited;
        private Action<WorkerAgent> destroyedCallback;
        private Action<Vector3> damagedCallback;
        private Action<WorkerAgent> gatheringRouteFailed;

        public Activity CurrentActivity { get; private set; }
        public bool IsFriendly { get; private set; }
        public bool IsAlive { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public bool IsSelected { get; private set; }
        public int CarriedSupplies { get; private set; }
        public ConstructibleBuilding CurrentConstruction => construction ?? deferredBuilding;
        public Vector3 LastDropOffPoint { get; private set; }
        public ResourceCache TargetCache => targetCache;

        public void Initialize(EconomyTuning economyTuning, EconomyWallet economyWallet, Hisar home,
            IReadOnlyList<ResourceCache> caches, int slot, Action<string> economyStateNotification,
            Func<Vector3, Vector3> dropOffResolver = null, Func<Vector3, bool> visibilityResolver = null,
            bool friendly = true, Action<int> onSuppliesDeposited = null, Action<WorkerAgent> onDestroyed = null,
            Action<Vector3> onDamaged = null, Action<WorkerAgent> onGatheringRouteFailed = null)
        {
            tuning = economyTuning;
            wallet = economyWallet;
            hisar = home;
            resolveDropOff = dropOffResolver;
            isCurrentlyVisible = visibilityResolver;
            knownCaches = caches;
            notifyEconomyState = economyStateNotification;
            suppliesDeposited = onSuppliesDeposited;
            destroyedCallback = onDestroyed;
            damagedCallback = onDamaged;
            gatheringRouteFailed = onGatheringRouteFailed;
            IsFriendly = friendly;
            IsAlive = true;
            MaxHealth = Mathf.Max(1, tuning.memberHealth);
            Health = MaxHealth;
            gatherSlot = slot;
            agent = GetComponent<NavMeshAgent>();
            agent.speed = tuning.workerSpeed;
            agent.angularSpeed = 720f;
            agent.acceleration = 18f;
            agent.stoppingDistance = 0.25f;
            selectionRing = transform.Find("Selection Ring")?.gameObject;
            carriedBundle = transform.Find("Carried Supplies")?.gameObject;
            SetSelected(false);
            SetCarrying(0);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionRing != null) selectionRing.SetActive(selected);
        }

        public void IssueMove(Vector3 destination)
        {
            if (CurrentConstruction != null) return;
            if (CarriedSupplies > 0)
            {
                deferredOrder = DeferredOrder.Move;
                deferredDestination = destination;
                ReturnHome();
                return;
            }

            BeginMove(destination);
        }

        public void IssueGather(ResourceCache cache)
        {
            if (CurrentConstruction != null) return;
            if (CarriedSupplies > 0)
            {
                deferredOrder = DeferredOrder.Gather;
                deferredCache = cache;
                ReturnHome();
                return;
            }

            BeginGather(cache);
        }

        public void IssueConstruct(ConstructibleBuilding building, Action<ConstructibleBuilding> completed)
        {
            if (building == null) return;
            resumeCache = targetCache;
            constructionCompleted = completed;
            if (CarriedSupplies > 0)
            {
                deferredOrder = DeferredOrder.Construct;
                deferredBuilding = building;
                ReturnHome();
                return;
            }

            BeginConstruction(building);
        }

        public bool CancelConstruction()
        {
            var cancelled = CurrentConstruction;
            if (cancelled == null || cancelled.IsComplete) return false;
            deferredBuilding = null;
            construction = null;
            constructionCompleted = null;
            if (deferredOrder == DeferredOrder.Construct) deferredOrder = DeferredOrder.None;
            if (CurrentActivity is Activity.GoingToConstruction or Activity.Constructing)
                ResumeAfterConstruction();
            return true;
        }

        public bool CanReach(Vector3 destination)
        {
            if (agent == null || !agent.isOnNavMesh) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path) &&
                   path.status == NavMeshPathStatus.PathComplete;
        }

        public void ApplyFixedDamage(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            damagedCallback?.Invoke(transform.position);
            Health = Mathf.Max(0, Health - amount);
            if (Health > 0) return;
            IsAlive = false;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            CurrentActivity = Activity.Idle;
            destroyedCallback?.Invoke(this);
            Destroy(gameObject, 0.25f);
        }

        public void Suspend()
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            enabled = false;
        }

        private void BeginMove(Vector3 destination)
        {
            targetCache = null;
            CurrentActivity = Activity.Moving;
            agent.stoppingDistance = 0.25f;
            agent.SetDestination(destination);
        }

        private void BeginGather(ResourceCache cache)
        {
            targetCache = cache;
            if (targetCache == null || targetCache.Remaining <= 0)
            {
                RetargetOrBecomeIdle(targetCache != null ? targetCache.transform.position : transform.position);
                return;
            }

            CurrentActivity = Activity.GoingToCache;
            agent.stoppingDistance = 0.2f;
            agent.SetDestination(targetCache.GetGatherPoint(gatherSlot));
        }

        private void Update()
        {
            if (agent == null || agent.pathPending) return;

            switch (CurrentActivity)
            {
                case Activity.Moving:
                    if (HasArrived()) CurrentActivity = Activity.Idle;
                    break;
                case Activity.GoingToCache:
                    if (targetCache == null || targetCache.Remaining <= 0)
                    {
                        RetargetOrBecomeIdle(targetCache != null ? targetCache.transform.position : transform.position);
                    }
                    else if (HasArrived())
                    {
                        CurrentActivity = Activity.Gathering;
                        gatherReadyAt = Time.time + tuning.gatherSeconds;
                    }
                    break;
                case Activity.Gathering:
                    if (Time.time >= gatherReadyAt) FinishGathering();
                    break;
                case Activity.Returning:
                    RefreshDropOff();
                    if (HasArrived()) DepositAndReturn();
                    break;
                case Activity.GoingToConstruction:
                    if (construction == null)
                    {
                        ResumeAfterConstruction();
                    }
                    else if (HasArrived())
                    {
                        CurrentActivity = Activity.Constructing;
                        agent.ResetPath();
                    }
                    break;
                case Activity.Constructing:
                    if (construction != null && construction.Advance(Time.deltaTime))
                        FinishConstruction();
                    break;
            }
        }

        private void FinishGathering()
        {
            if (targetCache == null)
            {
                RetargetOrBecomeIdle(transform.position);
                return;
            }

            SetCarrying(targetCache.TakeBatch(tuning.gatherBatch));
            if (CarriedSupplies == 0)
            {
                RetargetOrBecomeIdle(targetCache.transform.position);
                return;
            }

            ReturnHome();
        }

        private void DepositAndReturn()
        {
            var deposited = CarriedSupplies;
            wallet.Deposit(deposited);
            suppliesDeposited?.Invoke(deposited);
            SetCarrying(0);
            if (deferredOrder != DeferredOrder.None)
            {
                ExecuteDeferredOrder();
                return;
            }

            if (targetCache != null && targetCache.Remaining > 0)
            {
                BeginGather(targetCache);
            }
            else
            {
                RetargetOrBecomeIdle(targetCache != null ? targetCache.transform.position : transform.position);
            }
        }

        private void ReturnHome()
        {
            CurrentActivity = Activity.Returning;
            agent.stoppingDistance = 0.5f;
            RefreshDropOff();
        }

        private void RefreshDropOff()
        {
            var currentDropOff = resolveDropOff?.Invoke(transform.position) ?? hisar.DropOffPoint;
            if ((currentDropOff - LastDropOffPoint).sqrMagnitude < 0.01f && agent.hasPath) return;
            LastDropOffPoint = currentDropOff;
            var slotOffset = Vector3.right * ((gatherSlot - 1.5f) * 0.7f);
            agent.SetDestination(LastDropOffPoint + slotOffset);
        }

        private void ExecuteDeferredOrder()
        {
            var order = deferredOrder;
            var destination = deferredDestination;
            var cache = deferredCache;
            deferredOrder = DeferredOrder.None;
            deferredCache = null;

            if (order == DeferredOrder.Move) BeginMove(destination);
            else if (order == DeferredOrder.Gather) BeginGather(cache);
            else
            {
                var building = deferredBuilding;
                deferredBuilding = null;
                BeginConstruction(building);
            }
        }

        private void BeginConstruction(ConstructibleBuilding building)
        {
            construction = building;
            if (construction == null)
            {
                ResumeAfterConstruction();
                return;
            }
            targetCache = null;
            CurrentActivity = Activity.GoingToConstruction;
            agent.stoppingDistance = 0.35f;
            agent.SetDestination(construction.BuildPoint);
        }

        private void FinishConstruction()
        {
            var completed = construction;
            construction = null;
            var completedCallback = constructionCompleted;
            constructionCompleted = null;
            ResumeAfterConstruction();
            completedCallback?.Invoke(completed);
        }

        private void ResumeAfterConstruction()
        {
            var previousCache = resumeCache;
            resumeCache = null;
            if (previousCache != null && previousCache.Remaining > 0) BeginGather(previousCache);
            else
            {
                CurrentActivity = Activity.Idle;
                if (agent.isOnNavMesh) agent.ResetPath();
            }
        }

        private void RetargetOrBecomeIdle(Vector3 searchOrigin)
        {
            ResourceCache fallback = null;
            var nearestSqrDistance = tuning.cacheFallbackRadius * tuning.cacheFallbackRadius;
            if (knownCaches != null)
            {
                foreach (var cache in knownCaches)
                {
                    if (cache == null || cache.Remaining <= 0 ||
                        isCurrentlyVisible?.Invoke(cache.transform.position) == false) continue;
                    var sqrDistance = Vector3.SqrMagnitude(cache.transform.position - searchOrigin);
                    if (sqrDistance > nearestSqrDistance) continue;
                    fallback = cache;
                    nearestSqrDistance = sqrDistance;
                }
            }

            if (fallback != null)
            {
                BeginGather(fallback);
                return;
            }

            targetCache = null;
            CurrentActivity = Activity.Idle;
            if (agent.isOnNavMesh) agent.ResetPath();
            notifyEconomyState?.Invoke($"{name} idle - no Supplies cache nearby");
            gatheringRouteFailed?.Invoke(this);
        }

        private bool HasArrived() => agent.isOnNavMesh && agent.remainingDistance <= agent.stoppingDistance + 0.05f;

        private void SetCarrying(int amount)
        {
            CarriedSupplies = amount;
            if (carriedBundle != null) carriedBundle.SetActive(amount > 0);
        }
    }
}
