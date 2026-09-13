using System;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// Thin adapter for rams. On a new car-vs-car contact it reduces both cars to
    /// plain values, asks RamRules what happened, and writes velocities and
    /// status. All decisions live in RamRules.
    ///
    /// PhysX has already applied its own collision response by the time
    /// OnCollisionEnter runs. A ram overwrites both cars' horizontal velocity
    /// from the pre-step snapshot, so that response never shows — the attacker
    /// feels no opposing impulse. Vertical velocity is never touched.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RammingModule : MonoBehaviour, ICarModule
    {
        public RamConfig config;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float attack = 1f;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float defense = 1f;

        [Tooltip("Log impacts and rams to the console. Useful while tuning.")]
        public bool logImpacts;

        /// <summary>Every impact, including walls and plain bumps.</summary>
        public event Action<CarCollisionEvent> Collided;

        /// <summary>Resolved rams only.</summary>
        public event Action<RamReport> Rammed;

        CarController _car;
        BoxCollider _box;

        // Both cars receive OnCollisionEnter for the same contact in the same
        // step. Whichever runs first resolves the pair and marks both modules.
        RammingModule _resolvedPartner;
        float _resolvedAt = -1f;

        // Looked up lazily: collision callbacks are not guaranteed to wait for Start.
        CarController Car => _car != null ? _car : (_car = GetComponent<CarController>());
        BoxCollider Box => _box != null ? _box : (_box = GetComponent<BoxCollider>());

        void Start()
        {
            // Checked in Start, not Awake — CarFactory assigns config after AddComponent.
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] RammingModule on '{name}' has no RamConfig assigned — this car will neither ram nor be rammed.", this);
            }
        }

        public void Tick(in CarInput input, float dt)
        {
            if (config == null || !Car.Status.IsReeling) return;

            // DrivingModule does not write yaw while reeling; the spin winds down here.
            Rigidbody body = Car.Body;
            body.angularVelocity = Vector3.up * RamRules.DecaySpin(body.angularVelocity.y, config.spinDecayRate, dt);
        }

        public void FrameTick(in CarInput input, float dt) { }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;

            ContactPoint first = collision.GetContact(0);
            var other = collision.collider.GetComponentInParent<CarController>();

            Collided?.Invoke(new CarCollisionEvent
            {
                other = other,
                point = first.point,
                normal = first.normal,
                relativeSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, first.normal))
            });

            if (other == null) return;

            var partner = other.GetComponent<RammingModule>();
            if (partner == null || config == null || partner.config == null) return;

            float now = Time.fixedTime;
            if (_resolvedPartner == partner && _resolvedAt == now) return;
            MarkResolved(partner, now);
            partner.MarkResolved(this, now);

            Vector3 contact = MeanContact(collision);
            RamParticipant self = Participant(contact, config.cornerBandMetres);
            RamParticipant them = partner.Participant(contact, config.cornerBandMetres);

            RamOutcome outcome = RamRules.Resolve(self, them, config.minRamSpeed, config.headOnAngleDegrees);

            switch (outcome.type)
            {
                case RamType.None:
                    if (logImpacts) Debug.Log($"[Ram] {name} bumped {other.name} (no ram)");
                    break;
                case RamType.HeadOn:
                    ApplyHeadOn(partner, self, them);
                    break;
                default:
                    if (outcome.attacker == 0) ApplyRam(this, partner, self, contact, outcome.type);
                    else ApplyRam(partner, this, them, contact, outcome.type);
                    break;
            }
        }

        void MarkResolved(RammingModule partner, float time)
        {
            _resolvedPartner = partner;
            _resolvedAt = time;
        }

        static Vector3 MeanContact(Collision collision)
        {
            Vector3 sum = Vector3.zero;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                sum += collision.GetContact(i).point;
            }
            return sum / count;
        }

        RamParticipant Participant(Vector3 worldContact, float cornerBand)
        {
            Vector3 forward = RamRules.FlatForward(transform.forward);
            Vector3 local = transform.InverseTransformPoint(worldContact) - Box.center;

            return new RamParticipant
            {
                region = RamRules.Region(local, Box.size.x, Box.size.z, cornerBand),
                flatForward = forward,
                forwardSpeed = RamRules.ForwardSpeed(Car.PreStepVelocity, forward),
                canAttack = Car.Status.CanAttack
            };
        }

        void ApplyRam(RammingModule attacker, RammingModule victim, in RamParticipant attackerSide, Vector3 contact, RamType type)
        {
            float scale = RamRules.ScaleFor(type, config.headOnScale, config.flankScale, config.rearScale);
            Vector3 shove = RamRules.ShoveDelta(attackerSide.flatForward, attacker.attack, victim.defense, attackerSide.forwardSpeed, scale);
            float spin = RamRules.SpinDelta(contact, victim.transform.position, shove, victim.Box.size.x, victim.Box.size.z, config.spinScale);

            attacker.SetHorizontalVelocity(Vector3.zero, 0f);
            attacker.Car.Status.Lock(config.attackerLockSeconds);

            Vector3 preVelocity = victim.Car.PreStepVelocity;
            victim.SetHorizontalVelocity(preVelocity + shove, victim.Car.PreStepAngularVelocity.y + spin);
            victim.Car.Status.Reel(config.reelSeconds);

            Report(type, attacker, victim, shove.magnitude, spin);
        }

        void ApplyHeadOn(RammingModule partner, in RamParticipant self, in RamParticipant them)
        {
            // Each car is shoved by the OTHER car's attack, speed and heading, and
            // resists with its own defense. Through the centre: no spin.
            Vector3 toSelf = RamRules.ShoveDelta(them.flatForward, partner.attack, defense, them.forwardSpeed, config.headOnScale);
            Vector3 toPartner = RamRules.ShoveDelta(self.flatForward, attack, partner.defense, self.forwardSpeed, config.headOnScale);

            SetHorizontalVelocity(toSelf, 0f);
            partner.SetHorizontalVelocity(toPartner, 0f);

            Car.Status.Lock(config.attackerLockSeconds);
            partner.Car.Status.Lock(config.attackerLockSeconds);

            Report(RamType.HeadOn, this, partner, toPartner.magnitude, 0f);
        }

        /// <summary>Writes horizontal velocity and yaw rate; keeps vertical velocity.</summary>
        void SetHorizontalVelocity(Vector3 horizontal, float yawRate)
        {
            Rigidbody body = Car.Body;
            body.linearVelocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);
            body.angularVelocity = Vector3.up * yawRate;
        }

        void Report(RamType type, RammingModule attacker, RammingModule victim, float shoveSpeed, float spin)
        {
            if (logImpacts)
            {
                Debug.Log($"[Ram] {type}: {attacker.name} → {victim.name}, shove {shoveSpeed:F1} m/s, spin {spin:F2} rad/s");
            }

            Rammed?.Invoke(new RamReport
            {
                type = type,
                attacker = attacker.Car,
                victim = victim.Car,
                shoveSpeed = shoveSpeed,
                spin = spin
            });
        }

        /// <summary>
        /// Illustrates the regions on the collider's top face: front red, rear
        /// blue, sides yellow, corner bands magenta. Region membership is decided
        /// by nearest face, so the strips show boundaries, not exact volumes.
        /// </summary>
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null || config == null) return;

            float band = config.cornerBandMetres;
            float halfWidth = box.size.x * 0.5f;
            float halfLength = box.size.z * 0.5f;
            float y = box.center.y + box.size.y * 0.5f + 0.02f;
            const float thin = 0.02f;

            Gizmos.matrix = transform.localToWorldMatrix;

            DrawZone(Color.red, new Vector3(0f, y, halfLength - band * 0.5f), new Vector3(box.size.x - 2f * band, thin, band));
            DrawZone(Color.blue, new Vector3(0f, y, -halfLength + band * 0.5f), new Vector3(box.size.x - 2f * band, thin, band));
            DrawZone(Color.yellow, new Vector3(halfWidth - band * 0.5f, y, 0f), new Vector3(band, thin, box.size.z - 2f * band));
            DrawZone(Color.yellow, new Vector3(-halfWidth + band * 0.5f, y, 0f), new Vector3(band, thin, box.size.z - 2f * band));

            Color corner = Color.magenta;
            DrawZone(corner, new Vector3(halfWidth - band * 0.5f, y, halfLength - band * 0.5f), new Vector3(band, thin, band));
            DrawZone(corner, new Vector3(-halfWidth + band * 0.5f, y, halfLength - band * 0.5f), new Vector3(band, thin, band));
            DrawZone(corner, new Vector3(halfWidth - band * 0.5f, y, -halfLength + band * 0.5f), new Vector3(band, thin, band));
            DrawZone(corner, new Vector3(-halfWidth + band * 0.5f, y, -halfLength + band * 0.5f), new Vector3(band, thin, band));
        }

        static void DrawZone(Color colour, Vector3 centre, Vector3 size)
        {
            Gizmos.color = colour;
            Gizmos.DrawCube(centre, size);
        }
    }
}
