using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Cars;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Ramming;
using MotorCombat.Combat;
using MotorCombat.Effects;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    /// <summary>
    /// A spent shot (hit something, or ran out of range) must not sweep again if
    /// Advance runs a second time before its deferred Destroy() takes effect; and
    /// the hit path against real hurtboxes built by CarFactory, without a scene.
    /// </summary>
    public class ShotTests
    {
        Shot _shot;

        [TearDown]
        public void TearDown()
        {
            // In EditMode, Perish() already destroys the GameObject immediately
            // (Destroy() is illegal outside play mode), so this is a no-op on the
            // happy path — it only matters if a test left the shot alive.
            if (_shot != null) Object.DestroyImmediate(_shot.gameObject);
            DestroyCars();
        }

        [Test]
        public void SpentShot_DoesNotAdvanceOrHitAgainOnASecondCall()
        {
            var launch = new ShotLaunch
            {
                source = null,
                sourceTag = "test",
                origin = new Vector3(0f, 1000f, 0f),   // far from any collider
                direction = Vector3.forward,
                settings = new ShotSettings { speed = 10f, range = 0.5f, radius = 0.1f },
                payload = new Payload { damageKind = DamageKind.Flat, damageAmount = 0f, effects = new EffectSpec[0] },
                attack = 100f
            };

            _shot = Shot.Launch(launch);

            // dt = 1s at speed 10 would ask for a 10m step, but StepDistance caps
            // it at the remaining range (0.5m), so this single Advance both moves
            // the shot the full 0.5m and exhausts its range in the same call.
            _shot.Advance(1f);

            Assert.IsTrue(_shot.Spent, "range ran out, so the shot must be marked spent");

            // EditMode has no deferred-destroy frame boundary: Perish() calls
            // DestroyImmediate synchronously, so the GameObject is already gone
            // by the time Advance returns. Reading its position is therefore not
            // meaningful here — Unity's overloaded == is how a destroyed
            // MonoBehaviour reports itself as gone.
            Assert.IsTrue(_shot == null, "a spent shot's GameObject is destroyed immediately in EditMode");

            // The regression this guards against: a second Advance on an
            // already-spent shot must be a pure no-op — no exception, and (since
            // _spent short-circuits before any Unity API call) no attempt to
            // touch the now-destroyed GameObject.
            Assert.DoesNotThrow(() => _shot.Advance(1f));

            _shot = null; // already destroyed; nothing left for TearDown to clean up
        }

        // --- Hits against real hurtboxes ------------------------------------------

        static readonly Vector3 SourcePosition = new Vector3(500f, 50f, 0f);
        static readonly Vector3 TargetPosition = new Vector3(500f, 50f, 5f);
        const float CarLength = 4.5f;

        readonly List<CarDefinition> _definitions = new List<CarDefinition>();
        readonly List<CarController> _cars = new List<CarController>();

        void DestroyCars()
        {
            foreach (CarController car in _cars) if (car != null) Object.DestroyImmediate(car.gameObject);
            _cars.Clear();

            foreach (CarDefinition definition in _definitions)
            {
                Object.DestroyImmediate(definition.driveConfig);
                Object.DestroyImmediate(definition.aimConfig);
                Object.DestroyImmediate(definition.ramConfig);
                Object.DestroyImmediate(definition.wreckConfig);
                Object.DestroyImmediate(definition.effectsConfig);
                Object.DestroyImmediate(definition.weaponsConfig);
                Object.DestroyImmediate(definition);
            }

            _definitions.Clear();
        }

        CarController SpawnCar(string name, Vector3 position)
        {
            var definition = ScriptableObject.CreateInstance<CarDefinition>();
            definition.name = name;
            definition.length = CarLength;
            definition.width = 2f;
            definition.height = 1.2f;
            definition.mass = 1200f;
            definition.maxHealth = 750f;
            definition.driveConfig = ScriptableObject.CreateInstance<DriveConfig>();
            definition.aimConfig = ScriptableObject.CreateInstance<AimConfig>();
            definition.ramConfig = ScriptableObject.CreateInstance<RamConfig>();
            definition.wreckConfig = ScriptableObject.CreateInstance<WreckConfig>();
            definition.effectsConfig = ScriptableObject.CreateInstance<EffectsConfig>();
            definition.weaponsConfig = ScriptableObject.CreateInstance<WeaponsConfig>();
            definition.hurtboxes = new[] { new HurtboxBox { centre = Vector3.zero, size = new Vector3(2f, 1.2f, CarLength) } };
            definition.loadout = new WeaponConfig[3];
            _definitions.Add(definition);

            CarController car = CarFactory.Spawn(definition, position, Quaternion.identity, null, Color.white);
            _cars.Add(car);
            return car;
        }

        static ShotLaunch Flat50(CarController source, Vector3 origin, Vector3 direction)
        {
            return new ShotLaunch
            {
                source = source,
                sourceTag = "test",
                origin = origin,
                direction = direction,
                settings = new ShotSettings { speed = 20f, range = 40f, radius = 0.1f },
                payload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0] },
                attack = 100f
            };
        }

        [Test]
        public void Shot_HitsAnEnemyHurtbox_LandsThePayloadOnce_AndIsSpent()
        {
            CarController source = SpawnCar("Source", SourcePosition);
            CarController target = SpawnCar("Target", TargetPosition);
            Physics.SyncTransforms();

            var health = target.GetComponent<Health>();
            float before = health.Current;

            Vector3 nose = SourcePosition + Vector3.forward * (CarLength / 2f + 0.15f);
            _shot = Shot.Launch(Flat50(source, nose, Vector3.forward));
            Shot shot = _shot;

            // 20 m/s × 0.5 s = a 10 m sweep; the target's rear face is 0.35 m ahead.
            shot.Advance(0.5f);

            Assert.AreEqual(before - 50f, health.Current, 1e-3f, "Flat 50 at attack 100 against defense 0");
            Assert.IsTrue(shot.Spent, "hitting an enemy spends the shot");

            Assert.DoesNotThrow(() => shot.Advance(0.5f));
            Assert.AreEqual(before - 50f, health.Current, 1e-3f, "a spent shot never lands its payload twice");

            _shot = null;
        }

        [Test]
        public void Shot_StartingInsideItsOwnCar_PassesThrough()
        {
            CarController source = SpawnCar("Source", SourcePosition);
            Physics.SyncTransforms();

            var health = source.GetComponent<Health>();
            float before = health.Current;

            // From the car's centre, heading away into open space.
            _shot = Shot.Launch(Flat50(source, SourcePosition, Vector3.back));
            _shot.Advance(0.5f);

            Assert.AreEqual(before, health.Current, 1e-3f, "the own car is passed straight through");
            Assert.IsFalse(_shot.Spent, "nothing stopped it and it still has range left");
            Assert.AreEqual(SourcePosition.z - 10f, _shot.transform.position.z, 1e-3f);
        }
    }
}
