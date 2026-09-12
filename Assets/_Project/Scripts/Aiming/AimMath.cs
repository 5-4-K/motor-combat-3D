using UnityEngine;

namespace MotorCombat.Aiming
{
    /// <summary>
    /// Pure aim maths. No Unity objects, no state — unit-testable without a scene.
    /// </summary>
    public static class AimMath
    {
        /// <summary>
        /// Advance a car-relative aim yaw by one frame of mouse movement and clamp
        /// it into the aiming cone.
        /// </summary>
        /// <param name="currentYaw">Current aim yaw in degrees, relative to the car's forward.</param>
        /// <param name="deltaPixels">Raw horizontal mouse displacement this frame. NOT scaled by dt.</param>
        /// <param name="sensitivity">Degrees of aim per pixel of mouse movement.</param>
        /// <param name="coneAngleDegrees">Total cone width; the yaw is clamped to half of it either side.</param>
        public static float Accumulate(float currentYaw, float deltaPixels, float sensitivity, float coneAngleDegrees)
        {
            float halfCone = coneAngleDegrees * 0.5f;
            float next = currentYaw + deltaPixels * sensitivity;
            return Mathf.Clamp(next, -halfCone, halfCone);
        }
    }
}
