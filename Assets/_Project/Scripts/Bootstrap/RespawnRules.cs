using UnityEngine;

namespace MotorCombat.Bootstrap
{
    /// <summary>The respawn rule's decisions, without a scene.</summary>
    public static class RespawnRules
    {
        // Physics time accumulates in float steps; 150 x 0.02 lands just under 3.
        const float Tolerance = 1e-4f;

        /// <summary>
        /// True once the delay has passed AND the car is inactive — the wreck
        /// sequence deactivates it when the roll and fade are done, so respawn
        /// never cuts them short.
        /// </summary>
        public static bool IsDue(float now, float destroyedAt, float delaySeconds, bool carActive)
        {
            return !carActive && now >= destroyedAt + Mathf.Max(0f, delaySeconds) - Tolerance;
        }

        /// <summary>World centre and half extents of the car's box at a spawn pose, grown by a margin on every side.</summary>
        public static void SpawnBox(Vector3 position, Quaternion rotation, Vector3 boxCenter, Vector3 boxSize, float margin,
                                    out Vector3 center, out Vector3 halfExtents)
        {
            center = position + rotation * boxCenter;
            halfExtents = boxSize * 0.5f + Vector3.one * Mathf.Max(0f, margin);
        }
    }
}
