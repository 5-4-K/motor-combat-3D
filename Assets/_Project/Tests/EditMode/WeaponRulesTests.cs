using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class WeaponRulesTests
    {
        WeaponConfig _weapon;
        readonly List<string> _errors = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _weapon = ScriptableObject.CreateInstance<WeaponConfig>();
            _weapon.muzzle = MuzzleKind.Turret;
            _weapon.cooldownSeconds = 1f;
            _weapon.windUpSeconds = 0f;
            _weapon.recoverySeconds = 0.5f;
            _weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.3f };
            _weapon.hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0] };
            _weapon.selfEffects = new EffectSpec[0];
            _errors.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_weapon);
        }

        bool Valid() => WeaponRules.Validate(_weapon, 0.6f, _errors);

        [Test]
        public void Validate_AGoodWeapon_HasNoErrors()
        {
            Assert.IsTrue(Valid(), string.Join("; ", _errors));
            Assert.AreEqual(0, _errors.Count);
        }

        [Test]
        public void Validate_CooldownShorterThanRecovery_IsAnError()
        {
            _weapon.cooldownSeconds = 0.4f;
            Assert.IsFalse(Valid());
            StringAssert.Contains("recoverySeconds", _errors[0]);
        }

        [Test]
        public void Validate_CooldownEqualToRecovery_IsFine()
        {
            _weapon.cooldownSeconds = 0.5f;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_NegativeOrNonFiniteTiming_IsAnError()
        {
            _weapon.windUpSeconds = -1f;
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.windUpSeconds = float.NaN;
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_FixedWithNoMuzzle_IsAnError()
        {
            _weapon.muzzle = MuzzleKind.Fixed;
            _weapon.fixedMuzzles = FixedMuzzles.None;
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.fixedMuzzles = FixedMuzzles.Front | FixedMuzzles.Rear;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_ShotNumbersMustBePositive()
        {
            _weapon.shot = new ShotSettings { speed = 0f, range = 40f, radius = 0.3f };
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.shot = new ShotSettings { speed = 40f, range = -1f, radius = 0.3f };
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0f };
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_RadiusAtOrAboveFireHeight_IsAnError_ButInfiniteHeightSkipsIt()
        {
            _weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.6f };
            Assert.IsFalse(Valid());

            _errors.Clear();
            Assert.IsTrue(WeaponRules.Validate(_weapon, float.PositiveInfinity, _errors));
        }

        [Test]
        public void Validate_NegativeDamage_IsAnError()
        {
            var payload = _weapon.hitPayload;
            payload.damageAmount = -5f;
            _weapon.hitPayload = payload;
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_TimedEffectWithoutDuration_IsAnError_OverhauledIsExempt()
        {
            var payload = _weapon.hitPayload;
            payload.effects = new[] { new EffectSpec { type = EffectType.Corroded, magnitude = 30f, duration = 0f } };
            _weapon.hitPayload = payload;
            Assert.IsFalse(Valid());

            _errors.Clear();
            payload.effects = new[] { new EffectSpec { type = EffectType.Overhauled, magnitude = 0f, duration = 0f } };
            _weapon.hitPayload = payload;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_SelfEffectsAreCheckedToo()
        {
            _weapon.selfEffects = new[] { new EffectSpec { type = EffectType.Spiked, magnitude = -20f, duration = 2f } };
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_PushWithoutReel_IsAnError()
        {
            var payload = _weapon.hitPayload;
            payload.push = new PushSpec { speed = 8f, direction = PushDirection.AlongTravel, spinScale = 1f, reelSeconds = 0f };
            _weapon.hitPayload = payload;
            Assert.IsFalse(Valid());
            StringAssert.Contains("reelSeconds", _errors[0]);

            _errors.Clear();
            payload.push.reelSeconds = 1f;
            _weapon.hitPayload = payload;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_NullWeapon_IsAnError()
        {
            Assert.IsFalse(WeaponRules.Validate(null, 0.6f, _errors));
            Assert.AreEqual(1, _errors.Count);
        }
    }
}
