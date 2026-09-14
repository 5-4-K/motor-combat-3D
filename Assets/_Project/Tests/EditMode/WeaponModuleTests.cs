using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Effects;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    /// <summary>
    /// WeaponModule on a real car without a scene. Times are relative to
    /// Time.fixedTime captured in SetUp (it is not 0 in EditMode). Fired shots are
    /// real GameObjects; TearDown destroys them.
    /// </summary>
    public class WeaponModuleTests
    {
        static readonly object TestBlock = new object();

        GameObject _carObject;
        CarController _car;
        Health _health;
        CarEffects _effects;
        WeaponModule _weapons;
        WeaponsConfig _weaponsConfig;
        EffectsConfig _effectsConfig;
        readonly List<WeaponConfig> _configs = new List<WeaponConfig>();
        readonly List<ShotLaunch> _fired = new List<ShotLaunch>();
        float _t0;

        [SetUp]
        public void SetUp()
        {
            _t0 = Time.fixedTime;
            _weaponsConfig = ScriptableObject.CreateInstance<WeaponsConfig>();
            _effectsConfig = ScriptableObject.CreateInstance<EffectsConfig>();

            _carObject = new GameObject("Car", typeof(CarController));
            _carObject.transform.position = new Vector3(0f, 0.6f, 0f);
            _car = _carObject.GetComponent<CarController>();
            _car.Stats.SetBase(CarStat.Attack, 100f);
            _carObject.AddComponent<BoxCollider>().size = new Vector3(2f, 1.2f, 4f);
            _health = _carObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
            _effects = _carObject.AddComponent<CarEffects>();
            _effects.config = _effectsConfig;
            _weapons = _carObject.AddComponent<WeaponModule>();
            _weapons.config = _weaponsConfig;
            _weapons.Fired += launch => _fired.Add(launch);
            _fired.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Shot shot in Object.FindObjectsByType<Shot>(FindObjectsSortMode.None)) Object.DestroyImmediate(shot.gameObject);
            Object.DestroyImmediate(_carObject);
            foreach (WeaponConfig config in _configs) Object.DestroyImmediate(config);
            _configs.Clear();
            Object.DestroyImmediate(_weaponsConfig);
            Object.DestroyImmediate(_effectsConfig);
        }

        WeaponConfig Weapon(float cooldown = 1f, float windUp = 0f, float recovery = 0.5f, MuzzleKind muzzle = MuzzleKind.Turret, FixedMuzzles fixedMuzzles = FixedMuzzles.Front)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
            weapon.name = "TestWeapon" + _configs.Count;
            weapon.muzzle = muzzle;
            weapon.fixedMuzzles = fixedMuzzles;
            weapon.cooldownSeconds = cooldown;
            weapon.windUpSeconds = windUp;
            weapon.recoverySeconds = recovery;
            weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.3f };
            weapon.hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 10f, effects = new EffectSpec[0] };
            weapon.selfEffects = new EffectSpec[0];
            _configs.Add(weapon);
            return weapon;
        }

        void Load(params WeaponConfig[] loadout) => _weapons.loadout = loadout;

        void PressAt(int slot, float elapsed)
        {
            _weapons.Press(slot);
            _weapons.Step(_t0 + elapsed);
        }

        WeaponSlotStatus Status(int slot, float elapsed) => _weapons.GetStatus(slot, _t0 + elapsed);

        [Test]
        public void Press_FiresAndStartsTheCooldown()
        {
            Load(Weapon(cooldown: 2f));
            PressAt(0, 0f);

            Assert.AreEqual(1, _fired.Count);
            WeaponSlotStatus status = Status(0, 0.5f);
            Assert.IsTrue(status.assigned);
            Assert.AreEqual(1.5f, status.cooldownRemaining, 1e-3f);
            Assert.AreEqual(2f, status.cooldownDuration, 1e-5f);
        }

        [Test]
        public void Press_DuringCooldown_IsDropped_NotRemembered()
        {
            Load(Weapon(cooldown: 1f, recovery: 0f));
            PressAt(0, 0f);
            PressAt(0, 0.5f);
            _weapons.Step(_t0 + 1.2f);

            Assert.AreEqual(1, _fired.Count);
        }

        [Test]
        public void RecoveryLock_BlocksTheOtherSlots_AndShowsThemBlocked()
        {
            Load(Weapon(cooldown: 3f, recovery: 1f), Weapon(cooldown: 1f, recovery: 0.2f));
            PressAt(0, 0f);
            PressAt(1, 0.5f);

            Assert.AreEqual(1, _fired.Count);
            Assert.IsTrue(Status(1, 0.5f).blocked);
            Assert.IsFalse(Status(0, 0.5f).blocked, "the lock's own slot shows its cooldown only");

            PressAt(1, 1f);
            Assert.AreEqual(2, _fired.Count);
        }

        [Test]
        public void SameStep_TheLowestSlotWins()
        {
            Load(Weapon(recovery: 0.5f), Weapon(recovery: 0.5f));
            _weapons.Press(1);
            _weapons.Press(0);
            _weapons.Step(_t0);

            Assert.AreEqual(1, _fired.Count);
            Assert.AreEqual("TestWeapon0", _fired[0].sourceTag);
        }

        [Test]
        public void WindUp_ReleasesAfterTheDelay()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);
            _weapons.Step(_t0 + 0.3f);
            Assert.AreEqual(0, _fired.Count);

            _weapons.Step(_t0 + 0.5f);
            Assert.AreEqual(1, _fired.Count);
        }

        [Test]
        public void FireBlockedDuringWindUp_CancelsTheShot_AndKeepsTheCooldown()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);

            _car.Abilities.Block(TestBlock, CarAbility.Fire, 10f, BlockRefresh.KeepLonger);
            _weapons.Step(_t0 + 0.2f);
            _car.Abilities.Unblock(TestBlock);
            _weapons.Step(_t0 + 0.6f);

            Assert.AreEqual(0, _fired.Count);
            Assert.AreEqual(2.4f, Status(0, 0.6f).cooldownRemaining, 1e-3f);
        }

        [Test]
        public void FireBlocked_DropsThePress_AndShowsBlocked()
        {
            Load(Weapon());
            _car.Abilities.Block(TestBlock, CarAbility.Fire, 10f, BlockRefresh.KeepLonger);
            PressAt(0, 0f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsTrue(Status(0, 0f).blocked);
            Assert.AreEqual(0f, Status(0, 0f).cooldownRemaining, "a dropped press starts nothing");
        }

        [Test]
        public void Destroyed_CancelsTheWindUp()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);

            _health.Apply(new DamageRequest { sourceTag = "test", kind = DamageKind.Flat, amount = 5000f });
            _weapons.Step(_t0 + 0.6f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsTrue(Status(0, 0.6f).blocked, "a wreck can't fire");
        }

        [Test]
        public void ResetForRespawn_ClearsTheLock_AndKeepsCooldowns()
        {
            Load(Weapon(cooldown: 5f, recovery: 2f), Weapon(cooldown: 1f, recovery: 0f));
            PressAt(0, 0f);

            _weapons.ResetForRespawn();
            PressAt(1, 0.1f);

            Assert.AreEqual(2, _fired.Count, "the lock is gone");
            Assert.AreEqual(4.9f, Status(0, 0.1f).cooldownRemaining, 1e-3f, "the cooldown carried over");
        }

        [Test]
        public void SelfEffects_LandWhenTheShotLeaves_NotOnThePress()
        {
            WeaponConfig weapon = Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f);
            weapon.selfEffects = new[] { new EffectSpec { type = EffectType.Spiked, magnitude = 20f, duration = 2f } };
            Load(weapon);

            PressAt(0, 0f);
            Assert.IsFalse(_effects.Has(EffectType.Spiked), "not on the press");

            _weapons.Step(_t0 + 0.5f);
            Assert.IsTrue(_effects.Has(EffectType.Spiked));
        }

        [Test]
        public void AttackSnapshot_IsTakenOnThePress()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);
            _car.Stats.SetBase(CarStat.Attack, 300f);
            _weapons.Step(_t0 + 0.5f);

            Assert.AreEqual(100f, _fired[0].attack, 1e-4f);
        }

        [Test]
        public void FixedMuzzles_FireOneShotEach_InOrder()
        {
            Load(Weapon(muzzle: MuzzleKind.Fixed, fixedMuzzles: FixedMuzzles.Rear | FixedMuzzles.Front));
            PressAt(0, 0f);

            Assert.AreEqual(2, _fired.Count);
            Assert.AreEqual(1f, _fired[0].direction.z, 1e-4f, "front first");
            Assert.AreEqual(-1f, _fired[1].direction.z, 1e-4f);
            Assert.AreEqual(0.6f, _fired[0].origin.y, 1e-4f, "floor 0 + fire height 0.6");
        }

        [Test]
        public void AnInvalidWeapon_DisablesItsSlot()
        {
            Load(Weapon(cooldown: 0.1f, recovery: 0.5f));
            LogAssert.Expect(LogType.Error, new Regex("slot 1.*recoverySeconds"));

            PressAt(0, 0f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsFalse(Status(0, 0f).assigned);
        }

        [Test]
        public void SelfStunOnRelease_BlocksAPressQueuedInTheSameStep()
        {
            WeaponConfig weapon0 = Weapon(cooldown: 3f, windUp: 0.5f, recovery: 0.2f);
            weapon0.selfEffects = new[] { new EffectSpec { type = EffectType.Stunned, magnitude = 0f, duration = 3f } };
            Load(weapon0, Weapon());

            PressAt(0, 0f);
            _weapons.Press(1);
            _weapons.Step(_t0 + 0.5f);

            Assert.AreEqual(1, _fired.Count, "slot 0's own release still launches");
            Assert.AreEqual("TestWeapon0", _fired[0].sourceTag);
        }

        [Test]
        public void NoWeaponsConfig_LogsAnError_AndDisablesEverySlot()
        {
            _weapons.config = null;
            Load(Weapon());
            LogAssert.Expect(LogType.Error, new Regex("no WeaponsConfig"));

            PressAt(0, 0f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsFalse(Status(0, 0f).assigned);
        }

        [Test]
        public void NonPositiveFireHeight_LogsAnError_AndDisablesEverySlot()
        {
            _weaponsConfig.fireHeight = 0f;
            Load(Weapon());
            LogAssert.Expect(LogType.Error, new Regex("fireHeight must be a finite number above 0"));

            PressAt(0, 0f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsFalse(Status(0, 0f).assigned);
        }

        [Test]
        public void LoadoutLongerThanThree_WarnsAndUsesTheFirstThree()
        {
            Load(Weapon(), Weapon(), Weapon(), Weapon());
            LogAssert.Expect(LogType.Warning, new Regex("only the first 3"));

            Assert.IsTrue(Status(2, 0f).assigned);
            Assert.IsFalse(_weapons.GetStatus(3, _t0).assigned);
        }

        [Test]
        public void TurretDirection_IsReadAtRelease()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f, muzzle: MuzzleKind.Turret));
            PressAt(0, 0f);
            _car.AimYaw = 90f;
            _weapons.Step(_t0 + 0.5f);

            Assert.AreEqual(1, _fired.Count);
            Assert.AreEqual(1f, _fired[0].direction.x, 1e-4f);
        }

        [Test]
        public void CancelledWindUp_KeepsTheLockRunning()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f), Weapon());
            PressAt(0, 0f);

            _car.Abilities.Block(TestBlock, CarAbility.Fire, 10f, BlockRefresh.KeepLonger);
            _weapons.Step(_t0 + 0.2f);
            _car.Abilities.Unblock(TestBlock);

            PressAt(1, 0.6f);
            Assert.AreEqual(0, _fired.Count, "slot 0's cancelled wind-up never fired, and slot 1 is still locked out");
            Assert.IsTrue(Status(1, 0.6f).blocked);

            PressAt(1, 1f);
            Assert.AreEqual(1, _fired.Count);
        }
    }
}
