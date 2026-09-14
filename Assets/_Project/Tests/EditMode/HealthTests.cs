using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    /// <summary>
    /// The Health component end to end, without a scene. EditMode never runs
    /// Awake, and Health needs none: it reads CarController.Abilities/Stats,
    /// which are initialised with the object.
    /// </summary>
    public class HealthTests
    {
        GameObject _sourceObject;
        GameObject _targetObject;
        CarController _source;
        CarController _target;
        Health _health;

        [SetUp]
        public void SetUp()
        {
            _sourceObject = new GameObject("Source", typeof(CarController));
            _source = _sourceObject.GetComponent<CarController>();
            _source.Stats.SetBase(CarStat.Attack, 100f);

            _targetObject = new GameObject("Target", typeof(CarController));
            _target = _targetObject.GetComponent<CarController>();
            _health = _targetObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sourceObject);
            Object.DestroyImmediate(_targetObject);
        }

        DamageRequest Flat(float amount, CarController source)
        {
            return new DamageRequest { source = source, sourceTag = "test", kind = DamageKind.Flat, amount = amount };
        }

        [Test]
        public void Apply_RaisesDamagedWithAReport()
        {
            DamageReport? seen = null;
            _health.Damaged += r => seen = r;

            _health.Apply(Flat(100f, _source));

            Assert.IsTrue(seen.HasValue);
            Assert.AreSame(_source, seen.Value.source);
            Assert.AreSame(_target, seen.Value.target);
            Assert.AreEqual(100f, seen.Value.dealt, 1e-4f);
            Assert.AreEqual(900f, seen.Value.healthAfter, 1e-4f);
        }

        [Test]
        public void Apply_UsesSourceAttackAndTargetDefense()
        {
            _source.Stats.SetBase(CarStat.Attack, 150f);
            _target.Stats.SetBase(CarStat.Defense, 100f);

            var result = _health.Apply(Flat(100f, _source));

            Assert.AreEqual(75f, result.dealt, 1e-3f);
        }

        [Test]
        public void Apply_IgnoresDamageFromItself()
        {
            bool raised = false;
            _health.Damaged += r => raised = true;

            var result = _health.Apply(Flat(100f, _target));

            Assert.AreEqual(DamageOutcome.NotHostile, result.outcome);
            Assert.IsFalse(raised);
        }

        [Test]
        public void Kill_RaisesDestroyedOnce_AndBlocksTheWreckAbilities()
        {
            int destroyed = 0;
            _health.Destroyed += r => destroyed++;

            _health.Apply(Flat(5000f, _source));
            _health.Apply(Flat(5000f, _source));

            Assert.AreEqual(1, destroyed);
            Assert.IsTrue(_health.IsDestroyed);
            Assert.IsFalse(_target.Abilities.Has(CarAbility.Targetable));
            Assert.IsFalse(_target.Abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(_target.Abilities.Has(CarAbility.Fire));
            Assert.IsTrue(_target.Abilities.Has(CarAbility.Grip), "the wreck slides to a stop under grip");
            Assert.IsTrue(_target.Abilities.Has(CarAbility.YawHold), "the wreck does not spin");
        }

        [Test]
        public void Kill_ClearsEarlierBlocksAndModifiers()
        {
            object reel = new object();
            object corroded = new object();
            _target.Abilities.Block(reel, CarAbility.YawHold | CarAbility.Grip, 5f, BlockRefresh.Restart);
            _target.Stats.SetBase(CarStat.Defense, 100f);
            _target.Stats.Add(corroded, CarStat.Defense, -50f);

            _health.Apply(Flat(5000f, _source));

            Assert.IsTrue(_target.Abilities.Has(CarAbility.YawHold | CarAbility.Grip));
            Assert.AreEqual(100f, _target.Stats.Effective(CarStat.Defense), 1e-4f);
        }
    }
}
