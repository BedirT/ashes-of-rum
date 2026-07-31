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
        public const string TurnLeftState = "TurnLeft90";
        public const string TurnRightState = "TurnRight90";
        public const string PreviewWalkForwardState = "PreviewWalkForward";
        public const string PreviewAimWalkForwardState = "PreviewAimWalkForward";
        public const string PreviewWalkLeftState = "PreviewWalkLeft";
        public const string PreviewWalkRightState = "PreviewWalkRight";
        public const string PreviewWalkBackwardState = "PreviewWalkBackward";
        public const float NockSeconds = 0.12f;
        private const float NockedArrowHalfLength = 0.375f;

        [SerializeField] private Animator animator;
        [SerializeField] private Renderer[] factionRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] feedbackRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform bow;
        [SerializeField] private Transform nockedArrow;
        [SerializeField] private LineRenderer bowString;
        [SerializeField] private Transform upperStringAnchor;
        [SerializeField] private Transform lowerStringAnchor;
        private Transform drawHand;
        private bool arrowNocked;
        private float desiredGroundY;
        private int groundingFramesRemaining;

        public Animator Animator => animator;
        public Renderer[] FeedbackRenderers => feedbackRenderers;
        public float LastBlendSeconds { get; private set; }
        public string CurrentState { get; private set; } = IdleState;
        public bool IsNockedArrowVisible => nockedArrow != null && nockedArrow.gameObject.activeSelf;
        public bool IsBowStringDrawn { get; private set; }
        public float WorldBottomY => factionRenderers.Length == 0
            ? transform.position.y
            : factionRenderers.Where(itemRenderer => itemRenderer != null)
                .Min(itemRenderer => itemRenderer.bounds.min.y);

        public bool IsFactionRenderer(Renderer candidate) => Array.IndexOf(factionRenderers, candidate) >= 0;

        public void Configure(Animator targetAnimator, Renderer[] tintedRenderers, Renderer[] flashedRenderers,
            Transform bowTransform, Transform nockedArrowTransform, LineRenderer stringRenderer,
            Transform upperAnchor, Transform lowerAnchor)
        {
            animator = targetAnimator;
            factionRenderers = tintedRenderers ?? Array.Empty<Renderer>();
            feedbackRenderers = flashedRenderers ?? Array.Empty<Renderer>();
            bow = bowTransform;
            nockedArrow = nockedArrowTransform;
            bowString = stringRenderer;
            upperStringAnchor = upperAnchor;
            lowerStringAnchor = lowerAnchor;
        }

        public void Initialize(float groundY)
        {
            if (animator == null)
                throw new InvalidOperationException("Archer presentation requires an Animator.");
            if (bow == null)
                throw new InvalidOperationException("Archer presentation requires a bow attachment.");
            if (nockedArrow == null || bowString == null || upperStringAnchor == null || lowerStringAnchor == null)
                throw new InvalidOperationException("Archer presentation requires nocked-arrow and bow-string props.");

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
            bowString.material.color = Color.white;
            drawHand = animator.GetBoneTransform(HumanBodyBones.RightHand)
                ?? throw new InvalidOperationException("Archer Avatar has no mapped right draw hand.");
            ReleaseNockedArrow();
            desiredGroundY = groundY;
            groundingFramesRemaining = 12;
            PlayImmediate(IdleState);
        }

        public void PlayLoop(string state) => TransitionTo(state);

        public void Play(string state) => TransitionTo(state);

        public void PlayImmediate(string state)
        {
            if (animator == null || CurrentState == DeathState && state != DeathState) return;
            CurrentState = state;
            LastBlendSeconds = 0f;
            animator.speed = 1f;
            animator.Play(state, 0, 0f);
            SetAttackPresentation(state == AttackState);
        }

        public static float BlendSeconds(string fromState, string toState)
        {
            if (toState == DeathState) return 0.04f;
            if (toState == HitState) return 0.035f;
            if (toState == AttackState) return 0.06f;
            if (IsTurn(toState)) return 0.08f;
            if (IsTurn(fromState)) return 0.12f;
            if (fromState == MoveState && toState == IdleState) return 0.18f;
            if (fromState == IdleState && toState == MoveState) return 0.14f;
            if (fromState == AttackState) return 0.16f;
            if (fromState == HitState) return 0.1f;
            return 0.1f;
        }

        private void TransitionTo(string state)
        {
            if (animator == null || CurrentState == DeathState && state != DeathState) return;
            var previousState = CurrentState;
            CurrentState = state;
            LastBlendSeconds = BlendSeconds(previousState, state);
            animator.speed = 1f;
            animator.CrossFadeInFixedTime(state, LastBlendSeconds, 0, 0f);
            SetAttackPresentation(state == AttackState);
        }

        private static bool IsTurn(string state) => state == TurnLeftState || state == TurnRightState;

        private void LateUpdate()
        {
            if (groundingFramesRemaining > 0 && factionRenderers.Length > 0)
            {
                groundingFramesRemaining--;
                transform.position += Vector3.up * (desiredGroundY - WorldBottomY);
            }
            UpdateDrawEquipment();
        }

        public void ReleaseNockedArrow()
        {
            arrowNocked = false;
            IsBowStringDrawn = false;
            if (nockedArrow != null) nockedArrow.gameObject.SetActive(false);
            UpdateDrawEquipment();
        }

        private void SetAttackPresentation(bool attacking)
        {
            if (!attacking)
            {
                ReleaseNockedArrow();
                return;
            }
            arrowNocked = true;
            IsBowStringDrawn = true;
            if (nockedArrow != null) nockedArrow.gameObject.SetActive(true);
            UpdateDrawEquipment();
        }

        private void UpdateDrawEquipment()
        {
            if (bowString == null || upperStringAnchor == null || lowerStringAnchor == null) return;
            var upper = upperStringAnchor.position;
            var lower = lowerStringAnchor.position;
            var restingNock = Vector3.Lerp(upper, lower, 0.5f);
            var drawPoint = arrowNocked && drawHand != null ? drawHand.position : restingNock;
            bowString.positionCount = 3;
            bowString.SetPosition(0, upper);
            bowString.SetPosition(1, drawPoint);
            bowString.SetPosition(2, lower);
            if (!arrowNocked || nockedArrow == null || drawHand == null) return;

            var direction = restingNock - drawHand.position;
            if (direction.sqrMagnitude <= 0.0001f) direction = animator.transform.forward;
            direction.Normalize();
            nockedArrow.SetPositionAndRotation(drawHand.position + direction * NockedArrowHalfLength,
                Quaternion.LookRotation(direction, animator.transform.up));
        }
    }
}
