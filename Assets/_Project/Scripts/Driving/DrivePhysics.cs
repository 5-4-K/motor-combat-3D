using UnityEngine;

namespace MotorCombat.Driving
{
    /// <summary>
    /// Pure arcade driving maths. No Rigidbody, no MonoBehaviour, no state —
    /// every function is a plain transformation, so the feel of the car can be
    /// pinned by unit tests without opening a scene.
    /// </summary>
    public static class DrivePhysics
    {
        /// <summary>
        /// Engine, brake and reverse resolved into a single world-space force.
        /// Braking and reversing are the same key: brake while actually moving
        /// forward, reverse once nearly stopped.
        /// </summary>
        public static Vector3 DriveForce(
            Vector3 forward,
            Vector3 velocity,
            float throttle,
            float enginePower,
            float brakeForce,
            float reversePower,
            float reverseEpsilon)
        {
            if (throttle > 0f)
            {
                return forward * (throttle * enginePower);
            }

            if (throttle < 0f)
            {
                float forwardSpeed = Vector3.Dot(velocity, forward);
                if (forwardSpeed > reverseEpsilon)
                {
                    return -forward * brakeForce;
                }

                // throttle is negative, so this pushes backwards.
                return forward * (throttle * reversePower);
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Exponential velocity decay. Exponential rather than a per-tick
        /// multiply so that changing Fixed Timestep does not change how the car
        /// handles. <paramref name="dragRate"/> is in 1/s.
        /// </summary>
        public static Vector3 ApplyDrag(Vector3 velocity, float dragRate, float dt)
        {
            return velocity * Mathf.Exp(-dragRate * dt);
        }

        /// <summary>
        /// Bleed the sideways component of velocity. The gap this leaves between
        /// where the nose points and where the car is actually travelling is the
        /// drift. <paramref name="gripStrength"/> is in 1/s; higher is grippier.
        /// </summary>
        public static Vector3 ApplyGrip(Vector3 velocity, Vector3 forward, float gripStrength, float dt)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float longitudinal = Vector3.Dot(velocity, forward);
            float lateral = Vector3.Dot(velocity, right);
            float vertical = Vector3.Dot(velocity, Vector3.up);

            lateral *= Mathf.Exp(-gripStrength * dt);

            return forward * longitudinal + right * lateral + Vector3.up * vertical;
        }

        /// <summary>
        /// Degrees per second of yaw for a given steer input. Deliberately has no
        /// speed parameter: that is what makes turning on the spot work.
        /// </summary>
        public static float YawRate(float steer, float turnRateDegPerSec)
        {
            return steer * turnRateDegPerSec;
        }

        /// <summary>
        /// The speed at which engine force and drag balance. Only holds while
        /// Rigidbody.linearDamping is zero and drag is applied by ApplyDrag.
        /// </summary>
        public static float TerminalSpeed(float enginePower, float mass, float dragRate)
        {
            return enginePower / (mass * dragRate);
        }
    }
}
