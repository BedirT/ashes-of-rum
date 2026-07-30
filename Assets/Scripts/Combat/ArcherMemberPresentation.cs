using System;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class ArcherMemberPresentation : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string MoveState = "Move";
        public const string AttackState = "Attack";
        public const string HitState = "Hit";
        public const string DeathState = "Death";

        [SerializeField] private Animator animator;
        [SerializeField] private Renderer[] factionRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] feedbackRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform bow;
        private Transform leftHand;
        private Transform leftIndex;
        private Transform leftRing;
        private float desiredGroundY;
        private int groundingFramesRemaining;

        public Animator Animator => animator;
        public Renderer[] FeedbackRenderers => feedbackRenderers;
        public string CurrentState { get; private set; } = IdleState;
        public float WorldBottomY => factionRenderers.Length == 0
            ? transform.position.y
            : factionRenderers.Where(itemRenderer => itemRenderer != null)
                .Min(itemRenderer => itemRenderer.bounds.min.y);

        public bool IsFactionRenderer(Renderer candidate) => Array.IndexOf(factionRenderers, candidate) >= 0;

        public void Configure(Animator targetAnimator, Renderer[] tintedRenderers, Renderer[] flashedRenderers,
            Transform bowTransform)
        {
            animator = targetAnimator;
            factionRenderers = tintedRenderers ?? Array.Empty<Renderer>();
            feedbackRenderers = flashedRenderers ?? Array.Empty<Renderer>();
            bow = bowTransform;
        }

        public void Initialize(float groundY)
        {
            if (animator == null)
                throw new InvalidOperationException("Archer presentation requires an Animator.");
            if (bow == null)
                throw new InvalidOperationException("Archer presentation requires a bow attachment.");

            animator.applyRootMotion = false;
            foreach (var itemRenderer in factionRenderers)
            {
                if (itemRenderer == null) continue;
                itemRenderer.material.color = Color.white;
            }
            foreach (var itemRenderer in feedbackRenderers)
            {
                if (itemRenderer == null || IsFactionRenderer(itemRenderer)) continue;
                itemRenderer.material.color = Color.white;
            }
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand)
                ?? throw new InvalidOperationException("Archer Avatar has no mapped left hand.");
            leftIndex = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            leftRing = animator.GetBoneTransform(HumanBodyBones.LeftRingProximal);
            desiredGroundY = groundY;
            groundingFramesRemaining = 12;
            Play(IdleState, 0f);
        }

        public void Play(string state, float transitionSeconds = 0.08f)
        {
            if (animator == null || CurrentState == DeathState && state != DeathState) return;
            CurrentState = state;
            if (transitionSeconds <= 0f) animator.Play(state, 0, 0f);
            else animator.CrossFadeInFixedTime(state, transitionSeconds, 0, 0f);
        }

        private void LateUpdate()
        {
            AlignBowToHand();
            if (groundingFramesRemaining > 0 && factionRenderers.Length > 0)
            {
                groundingFramesRemaining--;
                transform.position += Vector3.up * (desiredGroundY - WorldBottomY);
            }
        }

        private void AlignBowToHand()
        {
            var fingerBase = leftIndex != null && leftRing != null
                ? Vector3.Lerp(leftIndex.position, leftRing.position, 0.5f)
                : leftHand.position;
            var palmCenter = Vector3.Lerp(leftHand.position, fingerBase, 0.55f);
            bow.SetPositionAndRotation(palmCenter, animator.transform.rotation);
            var meshFilter = bow.GetComponentInChildren<MeshFilter>()
                ?? throw new InvalidOperationException("Archer bow requires a mesh.");
            var size = meshFilter.sharedMesh.bounds.size;
            var longAxis = size.x > size.y && size.x > size.z
                ? Vector3.right
                : size.y > size.z ? Vector3.up : Vector3.forward;
            var thinAxis = size.x < size.y && size.x < size.z
                ? Vector3.right
                : size.y < size.z ? Vector3.up : Vector3.forward;
            var breadthAxis = Vector3.one - longAxis - thinAxis;

            var up = animator.transform.up;
            var longDirection = meshFilter.transform.TransformDirection(longAxis);
            bow.rotation = Quaternion.FromToRotation(longDirection, up) * bow.rotation;

            var normal = Vector3.ProjectOnPlane(meshFilter.transform.TransformDirection(thinAxis), up).normalized;
            var facing = Vector3.ProjectOnPlane(animator.transform.forward, up).normalized;
            bow.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(normal, facing, up), up) * bow.rotation;

            var meshBounds = meshFilter.sharedMesh.bounds;
            var localGrip = meshBounds.center + Vector3.Scale(meshBounds.extents, breadthAxis) * 0.9f;
            bow.position += palmCenter - meshFilter.transform.TransformPoint(localGrip);
            if (bow.parent != leftHand) bow.SetParent(leftHand, true);
        }
    }
}
