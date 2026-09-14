using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    /// <summary>
    /// CarEffects end to end on a real Health, without a scene. Apply stamps the
    /// live Time.fixedTime, which EditMode does not reset to 0, so Step is
    /// driven relative to the time captured in SetUp.
    /// </summary>
    public class CarEffectsTests
    {
        GameObject _carObject;
        GameObject _sourceObject;
        CarController _car;
        CarController _source;
        Rigidbody _body;
        Health _health;
        CarEffects _effects;
        EffectsConfig _config;
        float _t0;

        [SetUp]
        public void SetUp()
        {
            _t0 = Time.fixedTime;
            _config = ScriptableObject.CreateInstance<EffectsConfig>();

            _sourceObject = new GameObject("Source", typeof(CarController));
            _source = _sourceObject.GetComponent<CarController>();
            _source.Stats.SetBase(CarStat.Attack, 100f);

            _carObject = new GameObject("Car", typeof(CarController));
            _car = _carObject.GetComponent<CarController>();
            _body = _carObject.GetComponent<Rigidbody>();
            _car.Stats.SetBase(CarStat.Attack, 100f);
            _health = _carObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
            _effects = _carObject.AddComponent<CarEffects>();
            _effects.config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_carObject);
            Object.DestroyImmediate(_sourceObject);
            Object.DestroyImmediate(_config);
        }

        EffectOutcome Apply(EffectType type, float magnitude = 30f, float duration = 3f, bool fromSelf = false)
        {
            return _effects.Apply(new EffectRequest
            {
                source = fromSelf ? _car : _source,
                sourceTag = "test",
                type = type,
                magnitude = magnitude,
                duration = duration,
                allowNonEnemy = fromSelf
            });
        }

        static DamageRequest Flat(float amount, CarController source)
        {
            return new DamageRequest { source = source, sourceTag = "test", kind = DamageKind.Flat, amount = amount };
        }

        /// <summary>Steps at <paramref name="elapsed"/> seconds after apply. Apply stamps the live Time.fixedTime, which is not 0 in the EditMode harness.</summary>
        void StepTo(float dt, float elapsed)
        {
            _effects.Step(dt, _t0 + elapsed);
        }

        // --- Each effect ---------------------------------------------------------

        [Test]
        public void Stunned_BlocksDrivingWeaponsAndRamming_AndStopsTheCarOnce()
        {
            _body.linearVelocity = new Vector3(5f, 1f, 3f);
            _body.angularVelocity = new Vector3(0f, 2f, 0f);

            Assert.AreEqual(EffectOutcome.Applied, Apply(EffectType.Stunned));

            Assert.IsFalse(_car.Abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Steer));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Fire));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Ram));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Grip | CarAbility.YawHold), "stop once, then pushable: grip and yaw stay");
            Assert.AreEqual(new Vector3(0f, 1f, 0f), _body.linearVelocity, "horizontal zeroed, vertical kept");
            Assert.AreEqual(Vector3.zero, _body.angularVelocity);

            _body.linearVelocity = new Vector3(4f, 0f, 0f);
            StepTo(0.5f, 0.5f);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), _body.linearVelocity, "a shove after the stun landed is not undone");
        }

        [Test]
        public void Stunned_EndsAfterItsDuration_AndGivesTheAbilitiesBack()
        {
            Apply(EffectType.Stunned, duration: 1f);

            StepTo(0.5f, 0.5f);
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Throttle));

            StepTo(0.5f, 1f);
            Assert.IsFalse(_effects.Has(EffectType.Stunned));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram));
        }

        [Test]
        public void Suppressed_BlocksOnlyFire()
        {
            Apply(EffectType.Suppressed);

            Assert.IsFalse(_car.Abilities.Has(CarAbility.Fire));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram | CarAbility.Targetable));
        }

        [Test]
        public void Corroded_LowersDefense_UntilItEnds()
        {
            _car.Stats.SetBase(CarStat.Defense, 100f);

            Apply(EffectType.Corroded, magnitude: 30f, duration: 2f);
            Assert.AreEqual(70f, _car.Stats.Effective(CarStat.Defense), 1e-3f);

            StepTo(2f, 2f);
            Assert.AreEqual(100f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
        }

        [Test]
        public void Spiked_LowersTopSpeed()
        {
            Apply(EffectType.Spiked, magnitude: 30f);
            Assert.AreEqual(0.7f, _car.Stats.Effective(CarStat.TopSpeed), 1e-4f);
        }

        [Test]
        public void Exhausted_LowersAttack()
        {
            Apply(EffectType.Exhausted, magnitude: 30f);
            Assert.AreEqual(70f, _car.Stats.Effective(CarStat.Attack), 1e-3f);
        }

        [Test]
        public void DifferentEffectsOnTheSameStat_Add()
        {
            _car.Stats.SetBase(CarStat.Defense, 100f);

            Apply(EffectType.Corroded, magnitude: 30f);
            Apply(EffectType.Fortified, magnitude: 20f, fromSelf: true);

            Assert.AreEqual(90f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
        }

        [Test]
        public void Reeling_BlocksLikeARamReel_AndDecaysTheSpin()
        {
            _body.angularVelocity = new Vector3(0f, 3f, 0f);

            Apply(EffectType.Reeling, duration: 1f);

            Assert.IsFalse(_car.Abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Steer));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.YawHold));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Grip));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Ram));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Fire));

            StepTo(0.1f, 0.1f);
            Assert.AreEqual(3f * Mathf.Exp(-2f * 0.1f), _body.angularVelocity.y, 1e-4f);
        }

        [Test]
        public void Armored_BlocksAllDamage_IncludingSelfInflicted_UntilItEnds()
        {
            Apply(EffectType.Armored, duration: 1f, fromSelf: true);

            Assert.AreEqual(DamageOutcome.Blocked, _health.Apply(Flat(100f, _source)).outcome);
            var self = Flat(100f, _car);
            self.allowNonEnemy = true;
            Assert.AreEqual(DamageOutcome.Blocked, _health.Apply(self).outcome);

            StepTo(1f, 1f);
            Assert.AreEqual(DamageOutcome.Applied, _health.Apply(Flat(100f, _source)).outcome);
        }

        [Test]
        public void Overheated_TicksOnApply_ThenEveryInterval_ButNotAtExpiry()
        {
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);
            Assert.AreEqual(990f, _health.Current, 1e-3f, "first tick lands on apply");

            StepTo(0.5f, 0.5f);
            Assert.AreEqual(990f, _health.Current, 1e-3f);

            StepTo(0.5f, 1f);
            Assert.AreEqual(980f, _health.Current, 1e-3f);

            StepTo(1f, 2f);
            Assert.AreEqual(970f, _health.Current, 1e-3f);

            StepTo(1f, 3f);
            Assert.AreEqual(970f, _health.Current, 1e-3f, "expired before it could tick at 3 s");
            Assert.IsFalse(_effects.Has(EffectType.Overheated));
        }

        [Test]
        public void Overheated_KeepsTheAttackFromApplyTime()
        {
            _source.Stats.SetBase(CarStat.Attack, 200f);
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);
            Assert.AreEqual(980f, _health.Current, 1e-3f);

            _source.Stats.SetBase(CarStat.Attack, 50f);
            StepTo(1f, 1f);
            Assert.AreEqual(960f, _health.Current, 1e-3f, "the later drop in the source's attack does not weaken the burn");
        }

        [Test]
        public void Overheated_UsesTheConfiguredDamageKind()
        {
            _config.overheatedDamageKind = DamageKind.MaxHealthPercent;

            Apply(EffectType.Overheated, magnitude: 1f, duration: 3f);

            Assert.AreEqual(990f, _health.Current, 1e-3f, "1% of 1000 max health");
        }

        [Test]
        public void Overheated_TicksAreBlockedByArmored()
        {
            Apply(EffectType.Armored, duration: 5f, fromSelf: true);
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);
            StepTo(1f, 1f);

            Assert.AreEqual(1000f, _health.Current, 1e-3f);
        }

        // --- Stacking, cleanse, rejections --------------------------------------

        [Test]
        public void Reapply_WhenTheEffectDoesNotStack_ChangesNothing()
        {
            _car.Stats.SetBase(CarStat.Defense, 100f);
            Apply(EffectType.Corroded, magnitude: 30f, duration: 4f);
            StepTo(3f, 3f);

            Assert.AreEqual(EffectOutcome.AlreadyActive, Apply(EffectType.Corroded, magnitude: 50f, duration: 4f));

            Assert.AreEqual(70f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
            Assert.AreEqual(1f, _effects.Remaining(EffectType.Corroded), 1e-3f);
        }

        [Test]
        public void Reapply_WhenTheEffectStacks_RestartsTheTimerAndReplacesTheSize()
        {
            _config.corrodedStacks = true;
            _car.Stats.SetBase(CarStat.Defense, 100f);
            Apply(EffectType.Corroded, magnitude: 30f, duration: 4f);
            StepTo(3f, 3f);

            Assert.AreEqual(EffectOutcome.Restarted, Apply(EffectType.Corroded, magnitude: 50f, duration: 4f));

            Assert.AreEqual(50f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
            Assert.AreEqual(4f, _effects.Remaining(EffectType.Corroded), 1e-3f);
        }

        [Test]
        public void Overhauled_EndsEveryEffect_BuffsIncluded()
        {
            var ended = new List<EffectType>();
            _effects.Ended += ended.Add;
            Apply(EffectType.Stunned);
            Apply(EffectType.Fortified, fromSelf: true);

            Assert.AreEqual(EffectOutcome.Applied, Apply(EffectType.Overhauled, magnitude: 0f, duration: 0f, fromSelf: true));

            Assert.IsFalse(_effects.Has(EffectType.Stunned));
            Assert.IsFalse(_effects.Has(EffectType.Fortified));
            Assert.IsFalse(_effects.Has(EffectType.Overhauled), "instant, never itself active");
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle));
            CollectionAssert.AreEquivalent(new[] { EffectType.Stunned, EffectType.Fortified }, ended);
        }

        [Test]
        public void NonEnemySource_IsRejectedUnlessAllowed()
        {
            var request = new EffectRequest { source = _car, sourceTag = "test", type = EffectType.Corroded, magnitude = 30f, duration = 3f };
            Assert.AreEqual(EffectOutcome.NotHostile, _effects.Apply(request));

            request.allowNonEnemy = true;
            Assert.AreEqual(EffectOutcome.Applied, _effects.Apply(request));
        }

        [Test]
        public void Wreck_TakesNoEffects()
        {
            _health.Apply(Flat(5000f, _source));

            Assert.AreEqual(EffectOutcome.NotTargetable, Apply(EffectType.Stunned));
        }

        [Test]
        public void Death_EndsEveryEffect()
        {
            Apply(EffectType.Corroded);
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);

            _health.Apply(Flat(5000f, _source));

            Assert.IsFalse(_effects.Has(EffectType.Corroded));
            Assert.IsFalse(_effects.Has(EffectType.Overheated));
            StepTo(1f, 1f);
            Assert.AreEqual(0f, _health.Current);
        }

        [Test]
        public void ResetForRespawn_EndsEveryEffect()
        {
            Apply(EffectType.Suppressed);
            Apply(EffectType.Corroded);

            _effects.ResetForRespawn();

            Assert.IsFalse(_effects.Has(EffectType.Suppressed));
            Assert.IsFalse(_effects.Has(EffectType.Corroded));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Fire));
        }

        [Test]
        public void GetActive_ListsEffectsInTypeOrder_WithTheirTimeLeft()
        {
            Apply(EffectType.Exhausted, duration: 2f);
            Apply(EffectType.Stunned, duration: 3f);
            StepTo(0.5f, 0.5f);

            var active = new List<ActiveEffect> { new ActiveEffect() };
            _effects.GetActive(active);

            Assert.AreEqual(2, active.Count, "cleared first, then filled");
            Assert.AreEqual(EffectType.Stunned, active[0].type);
            Assert.AreEqual(2.5f, active[0].remaining, 1e-3f);
            Assert.AreEqual(EffectType.Exhausted, active[1].type);
            Assert.AreEqual(1.5f, active[1].remaining, 1e-3f);
        }

        [Test]
        public void MissingConfig_IsInvalid()
        {
            _effects.config = null;

            Assert.AreEqual(EffectOutcome.Invalid, Apply(EffectType.Stunned));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle));
        }

        [Test]
        public void Applied_IsRaisedForApplyAndRestart_NotForRejections()
        {
            var reports = new List<EffectReport>();
            _effects.Applied += reports.Add;

            Apply(EffectType.Reeling);      // Applied
            Apply(EffectType.Reeling);      // Restarted: Reeling stacks by default
            Apply(EffectType.Suppressed);   // Applied
            Apply(EffectType.Suppressed);   // AlreadyActive: no event

            Assert.AreEqual(3, reports.Count);
            Assert.AreEqual(EffectOutcome.Restarted, reports[1].outcome);
            Assert.AreSame(_car, reports[0].target);
            Assert.AreSame(_source, reports[0].source);
        }
    }
}
