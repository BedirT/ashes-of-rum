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
            Returning
        }

        private NavMeshAgent agent;
        private EconomyTuning tuning;
        private EconomyWallet wallet;
        private Hisar hisar;
        private ResourceCache targetCache;
        private GameObject selectionRing;
        private GameObject carriedBundle;
        private float gatherReadyAt;
        private int gatherSlot;

        public Activity CurrentActivity { get; private set; }
        public bool IsSelected { get; private set; }
        public int CarriedSupplies { get; private set; }

        public void Initialize(EconomyTuning economyTuning, EconomyWallet economyWallet, Hisar home, int slot)
        {
            tuning = economyTuning;
            wallet = economyWallet;
            hisar = home;
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
            targetCache = null;
            SetCarrying(0);
            CurrentActivity = Activity.Moving;
            agent.stoppingDistance = 0.25f;
            agent.SetDestination(destination);
        }

        public void IssueGather(ResourceCache cache)
        {
            targetCache = cache;
            if (targetCache == null || targetCache.Remaining <= 0)
            {
                CurrentActivity = Activity.Idle;
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
                        CurrentActivity = Activity.Idle;
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
                    if (HasArrived()) DepositAndReturn();
                    break;
            }
        }

        private void FinishGathering()
        {
            if (targetCache == null)
            {
                CurrentActivity = Activity.Idle;
                return;
            }

            SetCarrying(targetCache.TakeBatch(tuning.gatherBatch));
            if (CarriedSupplies == 0)
            {
                CurrentActivity = Activity.Idle;
                return;
            }

            CurrentActivity = Activity.Returning;
            agent.stoppingDistance = 0.5f;
            agent.SetDestination(hisar.DropOffPoint);
        }

        private void DepositAndReturn()
        {
            wallet.Deposit(CarriedSupplies);
            SetCarrying(0);
            if (targetCache != null && targetCache.Remaining > 0)
            {
                CurrentActivity = Activity.GoingToCache;
                agent.stoppingDistance = 0.2f;
                agent.SetDestination(targetCache.GetGatherPoint(gatherSlot));
            }
            else
            {
                CurrentActivity = Activity.Idle;
            }
        }

        private bool HasArrived() => agent.isOnNavMesh && agent.remainingDistance <= agent.stoppingDistance + 0.05f;

        private void SetCarrying(int amount)
        {
            CarriedSupplies = amount;
            if (carriedBundle != null) carriedBundle.SetActive(amount > 0);
        }
    }
}
