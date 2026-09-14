using System;
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarAbilitiesTests
    {
        static readonly object KeyA = new object();
        static readonly object KeyB = new object();

        const CarAbility All = CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip
                               | CarAbility.Fire | CarAbility.Ram | CarAbility.Targetable;

        [Test]
        public void New_HasEveryAbility()
        {
            var abilities = new CarAbilities();
            Assert.IsTrue(abilities.Has(All));
        }

        [Test]
        public void Block_RemovesOnlyTheBlockedAbilities()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle | CarAbility.Steer, 1f, BlockRefresh.Restart);

            Assert.IsFalse(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.Has(CarAbility.Steer));
            Assert.IsTrue(abilities.Has(CarAbility.Grip));
            Assert.IsFalse(abilities.Has(CarAbility.Throttle | CarAbility.Grip), "a multi-flag query fails if any flag is blocked");
        }

        [Test]
        public void TimedBlock_ExpiresAfterItsDuration()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 0.5f, BlockRefresh.KeepLonger);

            abilities.Advance(0.3f);
            Assert.IsFalse(abilities.Has(CarAbility.Throttle));

            abilities.Advance(0.3f);
            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.IsBlockedBy(KeyA));
        }

        [Test]
        public void Restart_ResetsTheRemainingTime()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Grip, 1f, BlockRefresh.Restart);
            abilities.Advance(0.8f);
            abilities.Block(KeyA, CarAbility.Grip, 1f, BlockRefresh.Restart);
            abilities.Advance(0.5f);

            Assert.IsFalse(abilities.Has(CarAbility.Grip));
            Assert.AreEqual(0.5f, abilities.Remaining(KeyA), 1e-5f);
        }

        [Test]
        public void KeepLonger_NeverShortensALongerBlock()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Steer, 1f, BlockRefresh.KeepLonger);
            abilities.Block(KeyA, CarAbility.Steer, 0.2f, BlockRefresh.KeepLonger);
            abilities.Advance(0.5f);

            Assert.IsFalse(abilities.Has(CarAbility.Steer));
            Assert.AreEqual(0.5f, abilities.Remaining(KeyA), 1e-5f);
        }

        [Test]
        public void KeepLonger_ExtendsAShorterBlock()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Steer, 0.2f, BlockRefresh.KeepLonger);
            abilities.Block(KeyA, CarAbility.Steer, 1f, BlockRefresh.KeepLonger);

            Assert.AreEqual(1f, abilities.Remaining(KeyA), 1e-5f);
        }

        [Test]
        public void IgnoreIfActive_DoesNothingWhileTheBlockIsActive()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Fire, 1f, BlockRefresh.IgnoreIfActive);
            abilities.Advance(0.8f);
            abilities.Block(KeyA, CarAbility.Fire, 1f, BlockRefresh.IgnoreIfActive);
            abilities.Advance(0.5f);

            Assert.IsTrue(abilities.Has(CarAbility.Fire), "the re-application was ignored, so the first block expired");
        }

        [Test]
        public void IgnoreIfActive_AppliesAgainAfterExpiry()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Fire, 0.1f, BlockRefresh.IgnoreIfActive);
            abilities.Advance(0.2f);
            abilities.Block(KeyA, CarAbility.Fire, 1f, BlockRefresh.IgnoreIfActive);

            Assert.IsFalse(abilities.Has(CarAbility.Fire));
        }

        [Test]
        public void Reapplying_ReplacesTheAbilitySet()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 1f, BlockRefresh.Restart);
            abilities.Block(KeyA, CarAbility.Steer, 1f, BlockRefresh.Restart);

            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.Has(CarAbility.Steer));
        }

        [Test]
        public void Sources_AreIndependent()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 0.5f, BlockRefresh.KeepLonger);
            abilities.Block(KeyB, CarAbility.Throttle, 1f, BlockRefresh.Restart);

            abilities.Advance(0.6f);
            Assert.IsFalse(abilities.Has(CarAbility.Throttle), "KeyB still blocks");

            abilities.Unblock(KeyB);
            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
        }

        [Test]
        public void UntimedBlock_NeverExpires()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Targetable, float.PositiveInfinity, BlockRefresh.KeepLonger);
            abilities.Advance(1000f);

            Assert.IsFalse(abilities.Has(CarAbility.Targetable));

            abilities.Unblock(KeyA);
            Assert.IsTrue(abilities.Has(CarAbility.Targetable));
        }

        [Test]
        public void UnblockAll_ClearsEverySource()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 1f, BlockRefresh.Restart);
            abilities.Block(KeyB, CarAbility.Grip, float.PositiveInfinity, BlockRefresh.Restart);
            abilities.UnblockAll();

            Assert.IsTrue(abilities.Has(All));
        }

        [Test]
        public void Remaining_IsZeroForAnUnknownSource()
        {
            var abilities = new CarAbilities();
            Assert.AreEqual(0f, abilities.Remaining(KeyA));
            Assert.IsFalse(abilities.IsBlockedBy(KeyA));
        }

        [Test]
        public void NewBlockWithZeroSeconds_IsIgnored()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 0f, BlockRefresh.Restart);

            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.IsBlockedBy(KeyA));
        }

        [Test]
        public void NullSource_Throws()
        {
            var abilities = new CarAbilities();
            Assert.Throws<ArgumentNullException>(() => abilities.Block(null, CarAbility.Throttle, 1f, BlockRefresh.Restart));
        }
    }
}
