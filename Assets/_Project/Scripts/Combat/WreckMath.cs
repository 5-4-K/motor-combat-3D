using UnityEngine;

namespace MotorCombat.Combat
{
    /// <summary>Pure timing and geometry for the wreck roll and fade.</summary>
    public static class WreckMath
    {
        /// <summary>
        /// Fade alpha below which the wreck stops casting a shadow. The car
        /// fades out; a solid shadow under an invisible car reads as a bug.
        /// Cutting the shadow mid-fade, rather than waiting for full
        /// transparency, avoids a visible pop at the moment of death.
        /// </summary>
        public const float ShadowCutoffAlpha = 0.5f;

        /// <summary>Eased roll angle in degrees at <paramref name="elapsed"/> seconds.</summary>
        public static float RollAngle(float elapsed, float rollSeconds, float rollDegrees)
        {
            float t = rollSeconds > 0f ? Mathf.Clamp01(elapsed / rollSeconds) : 1f;
            return Mathf.SmoothStep(0f, rollDegrees, t);
        }

        /// <summary>
        /// How far the model must rise so its lowest corner stays on the floor
        /// while rolled. A car is wider than it is tall, so on its side it would
        /// otherwise sink by halfWidth − halfHeight.
        /// </summary>
        public static float Lift(float angleDegrees, float halfWidth, float halfHeight)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float reach = halfWidth * Mathf.Abs(Mathf.Sin(radians)) + halfHeight * Mathf.Abs(Mathf.Cos(radians));
            return Mathf.Max(0f, reach - halfHeight);
        }

        /// <summary>Linear fade from 1 to 0.</summary>
        public static float Alpha(float elapsed, float fadeSeconds)
        {
            return fadeSeconds > 0f ? 1f - Mathf.Clamp01(elapsed / fadeSeconds) : 0f;
        }

        /// <summary>Whether the wreck should still cast a shadow at this fade alpha.</summary>
        public static bool CastsShadow(float alpha) => alpha >= ShadowCutoffAlpha;
    }
}
