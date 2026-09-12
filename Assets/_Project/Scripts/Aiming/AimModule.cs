using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Aiming
{
    /// <summary>
    /// Turns per-frame mouse movement into the car-relative aim yaw. Runs on the
    /// frame tick because the mouse delta is a per-frame quantity.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class AimModule : MonoBehaviour, ICarModule
    {
        public AimConfig config;

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
                Debug.LogError($"[MotorCombat] AimModule on '{name}' has no AimConfig assigned — this car will not aim.", this);
            }
        }

        public void FrameTick(in CarInput input, float dt)
        {
            if (config == null) return;

            _car.AimYaw = AimMath.Accumulate(
                _car.AimYaw,
                input.aimDeltaX,
                config.mouseSensitivity,
                config.coneAngleDegrees);
        }

        public void Tick(in CarInput input, float dt)
        {
            // Aiming is frame-rate driven, not fixed-step.
        }
    }
}
