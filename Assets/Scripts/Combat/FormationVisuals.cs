using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


namespace AshesOfRum
{
    public sealed class FormationSelectionRing : MonoBehaviour { }

    public sealed class FormationFrontIndicator : MonoBehaviour { }

    public sealed class FormationMemberVisual : MonoBehaviour
    {
        private const float HitSeconds = 0.16f;
        private const float AttackSeconds = 0.7f;
        private const float DeathSeconds = 2.2f;
        private Renderer memberRenderer;
        private Color restingColor;
        private Vector3 restingScale;
        private Coroutine hitRoutine;
        private Coroutine attackRoutine;
        private ArcherMemberPresentation archerPresentation;
        private Renderer[] feedbackRenderers = Array.Empty<Renderer>();
        private Color[] feedbackColors = Array.Empty<Color>();
        private bool moving;

        public bool IsShowingHitFeedback { get; private set; }
        public FlankDirection LastHitFlank { get; private set; }
        public bool HasAuthoredPresentation => archerPresentation != null;
        public string CurrentAnimationState => archerPresentation == null ? string.Empty :
            archerPresentation.CurrentState;

        public void Initialize(Renderer targetRenderer, ArcherMemberPresentation authoredPresentation = null)
        {
            memberRenderer = targetRenderer;
            restingColor = targetRenderer == null ? Color.white : targetRenderer.sharedMaterial.color;
            restingScale = transform.localScale;
            archerPresentation = authoredPresentation;
            if (archerPresentation == null) return;
            feedbackRenderers = archerPresentation.FeedbackRenderers;
            feedbackColors = new Color[feedbackRenderers.Length];
            for (var index = 0; index < feedbackRenderers.Length; index++)
                feedbackColors[index] = feedbackRenderers[index] == null
                    ? Color.white
                    : feedbackRenderers[index].material.color;
        }

        public void SetMoving(bool value)
        {
            moving = value;
            if (archerPresentation == null || IsShowingHitFeedback || attackRoutine != null) return;
            var state = moving ? ArcherMemberPresentation.MoveState : ArcherMemberPresentation.IdleState;
            if (archerPresentation.CurrentState != state) archerPresentation.Play(state);
        }

        public void ShowAttack()
        {
            if (archerPresentation == null) return;
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = StartCoroutine(Attack());
        }

        public void ShowHit(FlankDirection flank = FlankDirection.Front)
        {
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            LastHitFlank = flank;
            hitRoutine = StartCoroutine(Flash(flank));
        }

        private IEnumerator Flash(FlankDirection flank)
        {
            IsShowingHitFeedback = true;
            var flashColor = flank switch
            {
                FlankDirection.Side => new Color(1f, 0.75f, 0.2f),
                FlankDirection.Rear => new Color(1f, 0.25f, 0.08f),
                _ => Color.white
            };
            var scale = flank switch
            {
                FlankDirection.Side => 1.2f,
                FlankDirection.Rear => 1.3f,
                _ => 1.12f
            };
            if (memberRenderer != null)
                memberRenderer.sharedMaterial.color = Color.Lerp(restingColor, flashColor, 0.85f);
            for (var index = 0; index < feedbackRenderers.Length; index++)
            {
                if (feedbackRenderers[index] != null)
                    feedbackRenderers[index].material.color =
                        Color.Lerp(feedbackColors[index], flashColor, 0.7f);
            }
            transform.localScale = restingScale * scale;
            archerPresentation?.Play(ArcherMemberPresentation.HitState, 0.04f);
            yield return new WaitForSeconds(HitSeconds);
            if (memberRenderer != null) memberRenderer.sharedMaterial.color = restingColor;
            RestoreFeedbackColors();
            transform.localScale = restingScale;
            IsShowingHitFeedback = false;
            hitRoutine = null;
            ResumeLocomotion();
        }

        public bool ShowDeath()
        {
            if (archerPresentation == null) return false;
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            IsShowingHitFeedback = false;
            RestoreFeedbackColors();
            transform.localScale = restingScale;
            archerPresentation.Play(ArcherMemberPresentation.DeathState, 0.04f);
            Destroy(gameObject, DeathSeconds);
            return true;
        }

        private IEnumerator Attack()
        {
            archerPresentation.Play(ArcherMemberPresentation.AttackState, 0.04f);
            yield return new WaitForSeconds(AttackSeconds);
            attackRoutine = null;
            if (!IsShowingHitFeedback) ResumeLocomotion();
        }

        private void ResumeLocomotion()
        {
            archerPresentation?.Play(moving
                ? ArcherMemberPresentation.MoveState
                : ArcherMemberPresentation.IdleState);
        }

        private void RestoreFeedbackColors()
        {
            for (var index = 0; index < feedbackRenderers.Length; index++)
            {
                if (feedbackRenderers[index] != null)
                    feedbackRenderers[index].material.color = feedbackColors[index];
            }
        }
    }
}
