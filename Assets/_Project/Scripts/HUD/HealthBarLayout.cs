using UnityEngine;

namespace MotorCombat.HUD
{
    /// <summary>Pure placement and text rules for health bars.</summary>
    public static class HealthBarLayout
    {
        /// <summary>In front of the camera and within the screen plus a margin.</summary>
        public static bool IsVisible(Vector3 screenPoint, float screenWidth, float screenHeight, float margin)
        {
            return screenPoint.z > 0f
                   && screenPoint.x >= -margin && screenPoint.x <= screenWidth + margin
                   && screenPoint.y >= -margin && screenPoint.y <= screenHeight + margin;
        }

        /// <summary>Projected car width in screen pixels → bar width in reference pixels, clamped.</summary>
        public static float BarWidth(float projectedWidthPixels, float canvasScale, float minWidth, float maxWidth)
        {
            float reference = projectedWidthPixels / Mathf.Max(0.0001f, canvasScale);
            return Mathf.Clamp(reference, minWidth, maxWidth);
        }

        public static float Fill(float current, float max)
        {
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        /// <summary>Rounded UP, so a living car never reads 0.</summary>
        public static string SelfText(float current, float max)
        {
            return $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
