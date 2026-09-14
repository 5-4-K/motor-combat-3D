using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>Shared by rams and weapon pushes. Mass is ignored everywhere a push is applied.</summary>
    public static class PushMath
    {
        /// <summary>
        /// Yaw rate (rad/s) a velocity change applied at a contact point gives a solid
        /// box of that footprint: spinScale × cross(r, Δv).y / k², with r the flat
        /// offset from the centre and k² = (width² + length²) / 12.
        /// </summary>
        public static float SpinDelta(Vector3 contactPoint, Vector3 centre, Vector3 pushDelta, float width, float length, float spinScale)
        {
            Vector3 offset = contactPoint - centre;
            offset.y = 0f;

            float radiusOfGyrationSquared = (width * width + length * length) / 12f;
            if (radiusOfGyrationSquared <= 0f) return 0f;

            return spinScale * Vector3.Cross(offset, pushDelta).y / radiusOfGyrationSquared;
        }
    }
}
