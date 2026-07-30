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
        private float desiredGroundY;
        private int groundingFramesRemaining;

        public Animator Animator => animator;
        public Renderer[] FeedbackRenderers => feedbackRenderers;
        public string CurrentState { get; private set; } = IdleState;
        public Color FactionTint { get; private set; } = Color.white;
        public float WorldBottomY => factionRenderers.Length == 0
            ? transform.position.y
            : factionRenderers.Where(itemRenderer => itemRenderer != null)
                .Min(itemRenderer => itemRenderer.bounds.min.y);

        public bool IsFactionRenderer(Renderer candidate) => Array.IndexOf(factionRenderers, candidate) >= 0;

        public void Configure(Animator targetAnimator, Renderer[] tintedRenderers, Renderer[] flashedRenderers)
        {
            animator = targetAnimator;
            factionRenderers = tintedRenderers ?? Array.Empty<Renderer>();
            feedbackRenderers = flashedRenderers ?? Array.Empty<Renderer>();
        }

        public void Initialize(Color factionColor, float groundY)
        {
            if (animator == null)
                throw new InvalidOperationException("Archer presentation requires an Animator.");

            animator.applyRootMotion = false;
            FactionTint = Color.Lerp(Color.white, factionColor, 0.6f);
            foreach (var itemRenderer in factionRenderers)
            {
                if (itemRenderer == null) continue;
                itemRenderer.material.color = FactionTint;
            }
            foreach (var itemRenderer in feedbackRenderers)
            {
                if (itemRenderer == null || IsFactionRenderer(itemRenderer)) continue;
                itemRenderer.material.color = Color.white;
            }
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
            if (groundingFramesRemaining <= 0 || factionRenderers.Length == 0) return;
            groundingFramesRemaining--;
            transform.position += Vector3.up * (desiredGroundY - WorldBottomY);
        }
    }
}
