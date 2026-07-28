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
        private Renderer memberRenderer;
        private Color restingColor;
        private Vector3 restingScale;
        private Coroutine hitRoutine;

        public bool IsShowingHitFeedback { get; private set; }
        public FlankDirection LastHitFlank { get; private set; }

        public void Initialize(Renderer targetRenderer)
        {
            memberRenderer = targetRenderer;
            restingColor = targetRenderer.sharedMaterial.color;
            restingScale = transform.localScale;
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
            memberRenderer.sharedMaterial.color = Color.Lerp(restingColor, flashColor, 0.85f);
            transform.localScale = restingScale * scale;
            yield return new WaitForSeconds(HitSeconds);
            memberRenderer.sharedMaterial.color = restingColor;
            transform.localScale = restingScale;
            IsShowingHitFeedback = false;
            hitRoutine = null;
        }
    }
}
