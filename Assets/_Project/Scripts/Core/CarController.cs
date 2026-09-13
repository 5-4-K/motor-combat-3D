using System.Collections.Generic;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Deliberately dumb. Owns the Rigidbody and the aim yaw, samples input once
    /// per frame, and forwards it to whatever modules are attached. Contains no
    /// physics and no game rules, so any module can be swapped without touching
    /// this file.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        /// <summary>Name of the child transform marking the first-person eye position.</summary>
        public const string DriverAnchorName = "DriverAnchor";

        public Rigidbody Body { get; private set; }

        /// <summary>Aim angle in degrees, relative to the car's forward. Written by the aim module.</summary>
        public float AimYaw { get; set; }

        /// <summary>World-space direction the weapons point.</summary>
        public Vector3 AimDirection => Quaternion.AngleAxis(AimYaw, Vector3.up) * transform.forward;

        /// <summary>Lock and reel timers. Written by ramming, obeyed by driving.</summary>
        public CarStatus Status { get; } = new CarStatus();

        /// <summary>
        /// Velocity going INTO the last physics step, recorded after every module
        /// ticked. Collision callbacks run after the step, by which time PhysX has
        /// already applied its own response — this is the pre-contact value.
        /// </summary>
        public Vector3 PreStepVelocity { get; private set; }

        /// <summary>Angular counterpart of <see cref="PreStepVelocity"/>.</summary>
        public Vector3 PreStepAngularVelocity { get; private set; }

        IInputProvider _input;
        readonly List<ICarModule> _modules = new List<ICarModule>();

        // Sampled once per Update and reused by FixedUpdate. See Update's comment.
        CarInput _current;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();

            // Drag and angular damping are applied by the driving module using
            // exponential decay, so PhysX must not also apply its own.
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void Start()
        {
            // Awake runs the moment AddComponent is called, so modules attached
            // after the controller would be missed. Collect once the object is
            // fully assembled.
            _modules.Clear();
            GetComponents(_modules);
        }

        public void Bind(IInputProvider provider)
        {
            _input = provider;
        }

        void Update()
        {
            if (_input == null) return;

            // Sampled EXACTLY once per frame. aimDeltaX is a per-frame
            // accumulation that must be consumed once; sampling again in
            // FixedUpdate would double-consume or drop it depending on how many
            // physics steps landed in this frame.
            _current = _input.Sample();

            float dt = Time.deltaTime;
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].FrameTick(in _current, dt);
            }
        }

        void FixedUpdate()
        {
            // Reuses the cached struct; only the level fields (throttle, steer)
            // are meaningful here.
            float dt = Time.fixedDeltaTime;

            Status.Advance(dt);

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Tick(in _current, dt);
            }

            // Last, so it reflects every module's writes. Forces added with
            // AddForce this step are integrated inside the physics step and are
            // absent here — at most enginePower / mass × dt, about 0.5 m/s.
            PreStepVelocity = Body.linearVelocity;
            PreStepAngularVelocity = Body.angularVelocity;
        }
    }
}
