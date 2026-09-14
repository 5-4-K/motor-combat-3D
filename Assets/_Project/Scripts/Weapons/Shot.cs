using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    public struct ShotLaunch
    {
        public CarController source;
        public string sourceTag;
        public Vector3 origin;

        /// <summary>Flat and normalised.</summary>
        public Vector3 direction;

        public ShotSettings settings;
        public Payload payload;
        public float attack;
    }

    /// <summary>
    /// A straight shot. No collider and no Rigidbody: each physics step it sweeps a
    /// sphere from here to where it will be, against hurtboxes and the arena, so a
    /// fast shot can't tunnel and every hit has a point. ShotRules decides what
    /// stops it.
    /// </summary>
    public class Shot : MonoBehaviour
    {
        const int MaxHits = 16;

        static readonly RaycastHit[] Hits = new RaycastHit[MaxHits];
        static readonly ShotCandidate[] Candidates = new ShotCandidate[MaxHits];
        static readonly CarController[] HitCars = new CarController[MaxHits];
        static Material _material;

        ShotLaunch _launch;
        float _remaining;
        int _mask;
        int _arenaLayer;

        public float Remaining => _remaining;

        public static Shot Launch(in ShotLaunch launch)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Shot ({launch.sourceTag})";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = launch.origin;
            go.transform.localScale = Vector3.one * (launch.settings.radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = SharedMaterial();

            var shot = go.AddComponent<Shot>();
            shot._launch = launch;
            shot._remaining = launch.settings.range;
            shot._mask = LayerMask.GetMask(PhysicsLayers.HurtboxName, PhysicsLayers.ArenaName);
            shot._arenaLayer = PhysicsLayers.Arena;
            return shot;
        }

        void FixedUpdate()
        {
            Advance(Time.fixedDeltaTime);
        }

        public void Advance(float dt)
        {
            Vector3 position = transform.position;
            Vector3 direction = _launch.direction;
            float step = ShotRules.StepDistance(_launch.settings.speed, dt, _remaining);

            int count = Physics.SphereCastNonAlloc(position, _launch.settings.radius, direction, Hits, step, _mask, QueryTriggerInteraction.Collide);
            if (count > MaxHits) count = MaxHits;

            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                bool wall = collider.gameObject.layer == _arenaLayer;

                CarController car = null;
                if (!wall)
                {
                    var hurtbox = collider.GetComponent<Hurtbox>();
                    if (hurtbox != null) car = hurtbox.Car;
                }

                HitCars[i] = car;
                Candidates[i] = new ShotCandidate
                {
                    distance = Hits[i].distance,
                    wall = wall,
                    ownCar = car != null && car == _launch.source,
                    targetable = car != null && IsTargetable(car),
                    enemy = car != null && Hostility.AreEnemies(_launch.source, car)
                };
            }

            int stop = ShotRules.Resolve(Candidates, count);
            if (stop >= 0)
            {
                if (!Candidates[stop].wall)
                {
                    RaycastHit hit = Hits[stop];

                    // A sweep that starts inside a collider reports distance 0 and point zero.
                    Vector3 point = hit.distance <= 0f ? hit.collider.ClosestPoint(position) : hit.point;

                    PayloadApplier.Apply(_launch.payload, new PayloadHit
                    {
                        source = _launch.source,
                        sourceTag = _launch.sourceTag,
                        attack = _launch.attack,
                        target = HitCars[stop],
                        point = point,
                        travelDirection = direction,
                        hitboxCentre = position + direction * hit.distance
                    });
                }

                ClearBuffers(count);
                Destroy(gameObject);
                return;
            }

            ClearBuffers(count);
            transform.position = position + direction * step;
            _remaining -= step;
            if (_remaining <= ShotRules.RangeTolerance) Destroy(gameObject);
        }

        static bool IsTargetable(CarController car)
        {
            if (!car.Abilities.Has(CarAbility.Targetable)) return false;
            var damageable = car.GetComponent<IDamageable>();
            return damageable == null || !damageable.IsDestroyed;
        }

        /// <summary>Drop references so a static buffer never keeps a destroyed car alive.</summary>
        static void ClearBuffers(int count)
        {
            for (int i = 0; i < count; i++) HitCars[i] = null;
        }

        static Material SharedMaterial()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                _material = new Material(shader) { name = "MotorCombatShot", color = new Color(1f, 0.85f, 0.3f) };
            }

            return _material;
        }
    }
}
