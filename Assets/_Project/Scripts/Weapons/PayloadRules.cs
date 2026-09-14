using UnityEngine;

namespace MotorCombat.Weapons
{
    public static class PayloadRules
    {
        const float MinDirectionSqr = 1e-6f;

        /// <summary>The velocity change a push gives the hit car: flat, of length push.speed, or zero.</summary>
        public static Vector3 PushDelta(in PushSpec push, Vector3 travelDirection, Vector3 hitboxCentre, Vector3 targetCentre)
        {
            if (!(push.speed > 0f)) return Vector3.zero;

            Vector3 direction = push.direction == PushDirection.AwayFromCentre
                ? Flat(targetCentre - hitboxCentre)
                : Flat(travelDirection);

            if (push.direction == PushDirection.AwayFromCentre && direction.sqrMagnitude < MinDirectionSqr)
            {
                direction = Flat(travelDirection);
            }

            if (direction.sqrMagnitude < MinDirectionSqr) return Vector3.zero;
            return direction.normalized * push.speed;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
