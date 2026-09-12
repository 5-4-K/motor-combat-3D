using UnityEngine;

namespace MotorCombat.Cameras
{
    /// <summary>
    /// Competitive fairness: every player sees the same HORIZONTAL angle,
    /// whatever their monitor shape.
    ///
    /// Unity's camera takes a vertical FOV and derives the horizontal one from
    /// the aspect ratio, so by default a 21:9 player sees ~16 degrees more to the
    /// sides than a 16:9 player -- in a car brawler that is a car closing from
    /// the side, seen earlier. Holding horizontal fixed instead means wider
    /// screens lose some view top and bottom, which costs nothing in a game where
    /// every car is grounded and aim is horizontal only.
    /// </summary>
    public static class CameraFov
    {
        const float MinFov = 1f;
        const float MaxFov = 179f;
        const float MinAspect = 0.01f;

        /// <summary>
        /// Vertical FOV that shows exactly <paramref name="horizontalDegrees"/>
        /// across a screen of the given aspect ratio (width / height).
        /// Clamps input that would otherwise produce NaN or a degenerate camera.
        /// </summary>
        public static float VerticalFor(float horizontalDegrees, float aspect)
        {
            float horizontal = Mathf.Clamp(horizontalDegrees, MinFov, MaxFov);
            float safeAspect = Mathf.Max(MinAspect, aspect);

            float vertical = Camera.HorizontalToVerticalFieldOfView(horizontal, safeAspect);
            return Mathf.Clamp(vertical, MinFov, MaxFov);
        }
    }
}
