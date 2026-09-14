using NUnit.Framework;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class WeaponTimingTests
    {
        SlotTiming _slot;
        LockTiming _lock;

        [SetUp]
        public void SetUp()
        {
            _slot = SlotTiming.Ready;
            _lock = LockTiming.None;
        }

        [Test]
        public void CanPress_FreshSlot_WhenValidAndFireAllowed()
        {
            Assert.IsTrue(WeaponTiming.CanPress(_slot, true, true, _lock, 10f));
            Assert.IsFalse(WeaponTiming.CanPress(_slot, false, true, _lock, 10f), "invalid weapon");
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, false, _lock, 10f), "Fire blocked");
        }

        [Test]
        public void Press_StartsCooldownAndLockOnThePress()
        {
            bool releaseNow = WeaponTiming.Press(ref _slot, ref _lock, 1, 10f, 2f, 0f, 0.5f, 120f);

            Assert.IsTrue(releaseNow, "no wind-up fires in the same step");
            Assert.AreEqual(12f, _slot.cooldownEndsAt, 1e-5f);
            Assert.AreEqual(10.5f, _lock.endsAt, 1e-5f);
            Assert.AreEqual(1, _lock.owner);
            Assert.AreEqual(120f, _slot.attack, "attack snapshot taken on the press");
            Assert.AreEqual(2f, WeaponTiming.CooldownRemaining(_slot, 10f), 1e-5f);
        }

        [Test]
        public void Cooldown_BlocksTheSameSlotUntilItEnds()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 2f, 0f, 0f, 100f);
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, true, _lock, 11.9f));
            Assert.IsTrue(WeaponTiming.CanPress(_slot, true, true, _lock, 12f));
        }

        [Test]
        public void Lock_BlocksEverySlot_IncludingItsOwner()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 0.5f, 0f, 1f, 100f);   // cooldown < recovery: an invalid config, but the rule must still hold
            var other = SlotTiming.Ready;

            Assert.IsFalse(WeaponTiming.CanPress(other, true, true, _lock, 10.9f), "another slot");
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, true, _lock, 10.9f), "the owner");
            Assert.IsTrue(WeaponTiming.CanPress(other, true, true, _lock, 11f));
        }

        [Test]
        public void ZeroRecovery_LeavesNoLock_ForTheSameStep()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 1f, 0f, 0f, 100f);
            Assert.IsTrue(WeaponTiming.CanPress(SlotTiming.Ready, true, true, _lock, 10f));
        }

        [Test]
        public void WindUp_ReleasesAfterTheDelay()
        {
            bool releaseNow = WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 3f, 0.5f, 1f, 100f);

            Assert.IsFalse(releaseNow);
            Assert.IsTrue(_slot.windingUp);
            Assert.IsFalse(WeaponTiming.ShouldRelease(_slot, 10.4f));
            Assert.IsTrue(WeaponTiming.ShouldRelease(_slot, 10.5f));
        }

        [Test]
        public void Cancel_StopsTheWindUp_AndKeepsCooldownAndLock()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 3f, 0.5f, 1f, 100f);
            WeaponTiming.Cancel(ref _slot);

            Assert.IsFalse(_slot.windingUp);
            Assert.IsFalse(WeaponTiming.ShouldRelease(_slot, 20f));
            Assert.AreEqual(13f, _slot.cooldownEndsAt, 1e-5f);
            Assert.AreEqual(11f, _lock.endsAt, 1e-5f);
        }

        [Test]
        public void CanPress_IsFalseWhileTheSlotIsWindingUp()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 0.2f, 1f, 0f, 100f);   // wind-up longer than cooldown
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, true, _lock, 10.5f));
        }

        [Test]
        public void IsBlocked_ByFire_OrByAnotherSlotsLock_NotByOwnLockOrCooldown()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 3f, 0f, 1f, 100f);

            Assert.IsFalse(WeaponTiming.IsBlocked(true, true, _lock, 0, 10.5f), "own lock shows as cooldown only");
            Assert.IsTrue(WeaponTiming.IsBlocked(true, true, _lock, 1, 10.5f), "another slot's recovery");
            Assert.IsFalse(WeaponTiming.IsBlocked(true, true, _lock, 1, 11f), "lock over");
            Assert.IsTrue(WeaponTiming.IsBlocked(true, false, _lock, 0, 20f), "Fire blocked");
            Assert.IsFalse(WeaponTiming.IsBlocked(false, false, _lock, 0, 20f), "an empty slot is never blocked");
        }

        [Test]
        public void ResetForRespawn_ClearsWindUpsAndLock_KeepsCooldowns()
        {
            var slots = new[] { SlotTiming.Ready, SlotTiming.Ready };
            WeaponTiming.Press(ref slots[0], ref _lock, 0, 10f, 5f, 1f, 2f, 100f);

            WeaponTiming.ResetForRespawn(slots, ref _lock);

            Assert.IsFalse(slots[0].windingUp);
            Assert.AreEqual(15f, slots[0].cooldownEndsAt, 1e-5f);
            Assert.AreEqual(-1, _lock.owner);
            Assert.IsTrue(WeaponTiming.CanPress(slots[1], true, true, _lock, 10.1f));
        }
    }
}
