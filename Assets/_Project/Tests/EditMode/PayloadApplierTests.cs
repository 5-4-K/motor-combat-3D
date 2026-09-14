using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Effects;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    /// <summary>A payload landing on a real Health + CarEffects target, without a scene.</summary>
    public class PayloadApplierTests
    {
        GameObject _targetObject;
        GameObject _sourceObject;
        CarController _target;
        CarController _source;
        Rigidbody _body;
        Health _health;
        CarEffects _effects;
        EffectsConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EffectsConfig>();

            _sourceObject = new GameObject("Source", typeof(CarController));
            _source = _sourceObject.GetComponent<CarController>();
            _source.Stats.SetBase(CarStat.Attack, 100f);

            _targetObject = new GameObject("Target", typeof(CarController));
            _target = _targetObject.GetComponent<CarController>();
            _body = _targetObject.GetComponent<Rigidbody>();
            _targetObject.AddComponent<BoxCollider>().size = new Vector3(2f, 1f, 4f);
            _health = _targetObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
            _effects = _targetObject.AddComponent<CarEffects>();
            _effects.config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_targetObject);
            Object.DestroyImmediate(_sourceObject);
            Object.DestroyImmediate(_config);
        }

        PayloadHit Hit(Vector3 point, Vector3 travel, CarController source = null, float attack = 100f)
        {
            return new PayloadHit
            {
                source = source != null ? source : _source,
                sourceTag = "test",
                attack = attack,
                target = _target,
                point = point,
                travelDirection = travel,
                hitboxCentre = point
            };
        }

        static Payload Damage(float amount) => new Payload { damageKind = DamageKind.Flat, damageAmount = amount, effects = new EffectSpec[0] };

        static PushSpec Push(float speed) => new PushSpec { speed = speed, direction = PushDirection.AlongTravel, spinScale = 1f, reelSeconds = 1f };

        [Test]
        public void Damage_UsesTheAttackSnapshot()
        {
            PayloadApplier.Apply(Damage(50f), Hit(Vector3.zero, Vector3.forward, attack: 150f));
            Assert.AreEqual(925f, _health.Current, 1e-3f);
        }

        [Test]
        public void Effects_AreApplied()
        {
            Payload payload = Damage(0f);
            payload.effects = new[] { new EffectSpec { type = EffectType.Corroded, magnitude = 30f, duration = 4f } };

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.IsTrue(_effects.Has(EffectType.Corroded));
            Assert.AreEqual(1000f, _health.Current, "no damage when the amount is 0");
        }

        [Test]
        public void Push_AddsVelocityAlongTravel_AndReels()
        {
            _body.linearVelocity = new Vector3(0f, 0f, 2f);
            Payload payload = Damage(0f);
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.AreEqual(10f, _body.linearVelocity.z, 1e-3f, "added, not overwritten");
            Assert.IsTrue(_effects.Has(EffectType.Reeling));
            Assert.AreEqual(1f, _effects.Remaining(EffectType.Reeling), 1e-3f);
        }

        /// <summary>Hit on the tail, pushed toward +X: cross((0,0,−2), (8,0,0)).y = −16; k² = (4 + 16)/12 = 5/3 → −9.6 rad/s.</summary>
        [Test]
        public void Push_OffCentreHit_Spins()
        {
            Payload payload = Damage(0f);
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(new Vector3(0f, 0f, -2f), Vector3.right));

            Assert.AreEqual(-9.6f, _body.angularVelocity.y, 1e-3f);
        }

        [Test]
        public void NoPushSpeed_NoVelocityAndNoReel()
        {
            PayloadApplier.Apply(Damage(10f), Hit(Vector3.zero, Vector3.forward));

            Assert.AreEqual(Vector3.zero, _body.linearVelocity);
            Assert.IsFalse(_effects.Has(EffectType.Reeling));
        }

        [Test]
        public void AStunInTheEffectsList_DoesNotCancelThePush()
        {
            _body.linearVelocity = new Vector3(5f, 0f, 0f);
            Payload payload = Damage(0f);
            payload.effects = new[] { new EffectSpec { type = EffectType.Stunned, duration = 3f } };
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.AreEqual(0f, _body.linearVelocity.x, 1e-3f, "the stun stopped the car first");
            Assert.AreEqual(8f, _body.linearVelocity.z, 1e-3f, "then the push landed");
        }

        [Test]
        public void OwnCar_ReceivesNothing()
        {
            Payload payload = Damage(50f);
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward, source: _target));

            Assert.AreEqual(1000f, _health.Current);
            Assert.AreEqual(Vector3.zero, _body.linearVelocity);
        }

        [Test]
        public void AKillFromTheEffects_StopsBeforePushAndDamage()
        {
            Payload payload = Damage(50f);
            payload.effects = new[] { new EffectSpec { type = EffectType.Overheated, magnitude = 5000f, duration = 3f } };
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.IsTrue(_health.IsDestroyed);
            Assert.AreEqual(Vector3.zero, _body.linearVelocity);
        }
    }
}
