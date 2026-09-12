using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Driving
{
    /// <summary>
    /// Thin adapter: reads config, calls DrivePhysics, writes the result to the
    /// Rigidbody. All the maths worth testing lives in DrivePhysics.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class DrivingModule : MonoBehaviour, ICarModule
    {
        public DriveConfig config;

        CarController _car;

        void Awake()
        {
            _car = GetComponent<CarController>();

            if (config == null)
            {
                Debug.LogError($"[MotorCombat] DrivingModule on '{name}' has no DriveConfig assigned — this car will not drive.", this);
            }
        }

        public void Tick(in CarInput input, float dt)
        {
            if (config == null) return;

            Rigidbody body = _car.Body;
            Vector3 forward = FlatForward();

            // 1. Thrust, brake and reverse.
            Vector3 force = DrivePhysics.DriveForce(
                forward,
                body.linearVelocity,
                input.throttle,
                config.enginePower,
                config.brakeForce,
                config.reversePower,
                config.reverseEpsilon);

            if (force != Vector3.zero)
            {
                body.AddForce(force, ForceMode.Force);
            }

            // 2. Drag, applied manually so terminal speed matches the formula.
            body.linearVelocity = DrivePhysics.ApplyDrag(body.linearVelocity, config.linearDrag, dt);

            // 3. Yaw, set directly. No speed gate, so turning on the spot works.
            float yawRate = DrivePhysics.YawRate(input.steer, config.turnRate);
            body.angularVelocity = Vector3.up * (yawRate * Mathf.Deg2Rad);

            // 4. Grip. Whatever sideways velocity survives is the drift.
            body.linearVelocity = DrivePhysics.ApplyGrip(
                body.linearVelocity, forward, config.lateralGripStrength, dt);
        }

        public void FrameTick(in CarInput input, float dt)
        {
            // Driving is fixed-step only.
        }

        /// <summary>
        /// The car's forward flattened onto the ground plane, so a slight pitch
        /// from a collision never leaks into steering or grip.
        /// </summary>
        Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }
    }
}
