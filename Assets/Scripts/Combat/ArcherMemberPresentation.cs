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

        [SerializeField] private Animator animator;
        [SerializeField] private Renderer[] factionRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] feedbackRenderers = Array.Empty<Renderer>();
        [SerializeField] private Transform bow;
        private float desiredGroundY;
        private int groundingFramesRemaining;

        public Animator Animator => animator;
        public Renderer[] FeedbackRenderers => feedbackRenderers;
        public float LastBlendSeconds { get; private set; }
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
        }

        private static bool IsTurn(string state) => state == TurnLeftState || state == TurnRightState;

        private void LateUpdate()
        {
            if (groundingFramesRemaining > 0 && factionRenderers.Length > 0)
            {
                groundingFramesRemaining--;
                transform.position += Vector3.up * (desiredGroundY - WorldBottomY);
            }
        }

    }
}
