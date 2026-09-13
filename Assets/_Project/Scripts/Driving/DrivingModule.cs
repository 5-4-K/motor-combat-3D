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
        }

        void Start()
        {
            // Checked in Start, not Awake. Awake fires the instant AddComponent
            // returns, which is BEFORE CarFactory assigns config on the same
            // line — so an Awake check reports every factory-built car as
            // misconfigured. By Start the whole object is assembled.
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
            CarStatus status = _car.Status;

            // Locked or reeling: throttle and steer are ignored. Aim is untouched —
            // it runs in AimModule on the frame tick.
            float throttle = status.CanDrive ? input.throttle : 0f;
            float steer = status.CanDrive ? input.steer : 0f;

            // 1. Thrust, brake and reverse.
            Vector3 force = DrivePhysics.DriveForce(
                forward,
                body.linearVelocity,
                throttle,
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

            // A reeling car slides and spins freely: no yaw write (the ram's spin
            // survives and decays in RammingModule) and no grip (the shove survives).
            if (status.IsReeling) return;

            // 3. Yaw, set directly. Never gated on speed MAGNITUDE, so turning on
            //    the spot works; the SIGN of travel can invert the steering sense
            //    so reversing handles like a real car.
            float forwardSpeed = Vector3.Dot(body.linearVelocity, forward);
            float yawRate = DrivePhysics.YawRate(
                steer,
                config.turnRate,
                forwardSpeed,
                config.flipSteeringInReverse,
                config.reverseEpsilon);
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
