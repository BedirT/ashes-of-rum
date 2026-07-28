using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AshesOfRum
{
    public static class FormationFrontlineRules
    {
        private const float MinimumApproachDot = 0.25f;

        public static bool Blocks(Vector3 moverPosition, Vector3 moveDirection, Vector3 opponentPosition,
            float radius)
        {
            moveDirection.y = 0f;
            var offset = opponentPosition - moverPosition;
            offset.y = 0f;
            if (moveDirection.sqrMagnitude <= 0.01f || offset.sqrMagnitude <= 0.01f ||
                offset.sqrMagnitude > radius * radius) return false;
            return Vector3.Dot(moveDirection.normalized, offset.normalized) >= MinimumApproachDot;
        }
    }

    public static class FormationMemberRules
    {
        public static Vector3 Slot(int index) =>
            new((index % 4 - 1.5f) * 1.15f, 0.85f, -(index / 4) * 1.35f);
    }
}
