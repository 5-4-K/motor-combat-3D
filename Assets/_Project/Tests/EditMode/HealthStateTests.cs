using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class HealthStateTests
    {
        static DamageRequest Flat(float amount, string tag = "test")
        {
            return new DamageRequest { kind = DamageKind.Flat, amount = amount, sourceTag = tag };
        }

        static DamageResult Hit(HealthState state, DamageRequest request, bool targetable = true, bool hostile = true, float attack = 100f, float defense = 0f)
        {
            return state.Apply(request, targetable, hostile, attack, defense);
        }

        [Test]
        public void New_StartsAtMax()
        {
            var state = new HealthState(1000f);
            Assert.AreEqual(1000f, state.Current);
            Assert.AreEqual(1000f, state.Max);
            Assert.IsFalse(state.IsDestroyed);
        }

        [Test]
        public void NotTargetable_ChangesNothing()
        {
            var state = new HealthState(1000f);
            var result = Hit(state, Flat(100f), targetable: false);

            Assert.AreEqual(DamageOutcome.NotTargetable, result.outcome);
            Assert.AreEqual(1000f, state.Current);
        }

        [Test]
        public void NotTargetable_IsCheckedBeforeHostility()
        {
            var state = new HealthState(1000f);
            Assert.AreEqual(DamageOutcome.NotTargetable, Hit(state, Flat(100f), targetable: false, hostile: false).outcome);
        }

        [Test]
        public void NotHostile_ChangesNothing()
        {
            var state = new HealthState(1000f);
            var result = Hit(state, Flat(100f), hostile: false);

            Assert.AreEqual(DamageOutcome.NotHostile, result.outcome);
            Assert.AreEqual(1000f, state.Current);
        }

        [Test]
        public void AllowNonEnemy_BypassesHostility()
        {
            var state = new HealthState(1000f);
            var request = Flat(100f);
            request.allowNonEnemy = true;

            Assert.AreEqual(DamageOutcome.Applied, Hit(state, request, hostile: false).outcome);
            Assert.AreEqual(900f, state.Current, 1e-4f);
        }

        [Test]
        public void Gate_BlocksAndReceivesTheRequest()
        {
            var state = new HealthState(1000f);
            object armored = new object();
            state.AddGate(armored, r => r.sourceTag == "blocked");

            Assert.AreEqual(DamageOutcome.Blocked, Hit(state, Flat(100f, "blocked")).outcome);
            Assert.AreEqual(DamageOutcome.Applied, Hit(state, Flat(100f, "other")).outcome);
        }

        [Test]
        public void RemoveGate_StopsBlocking()
        {
            var state = new HealthState(1000f);
            object armored = new object();
            state.AddGate(armored, r => true);
            state.RemoveGate(armored);

            Assert.AreEqual(DamageOutcome.Applied, Hit(state, Flat(100f)).outcome);
        }

        [Test]
        public void ZeroAttack_GivesZeroAmount()
        {
            var state = new HealthState(1000f);
            Assert.AreEqual(DamageOutcome.ZeroAmount, Hit(state, Flat(100f), attack: 0f).outcome);
            Assert.AreEqual(1000f, state.Current);
        }

        [Test]
        public void Flat_IsMitigatedByAttackAndDefense()
        {
            var state = new HealthState(1000f);
            var result = Hit(state, Flat(100f), attack: 100f, defense: 100f);

            Assert.AreEqual(50f, result.dealt, 1e-4f);
            Assert.AreEqual(950f, state.Current, 1e-4f);
        }

        [Test]
        public void MaxHealthPercent_UsesMaxHealth()
        {
            var state = new HealthState(1000f);
            Hit(state, Flat(500f));
            var request = new DamageRequest { kind = DamageKind.MaxHealthPercent, amount = 10f };
            var result = Hit(state, request);

            Assert.AreEqual(100f, result.dealt, 1e-4f, "10% of MAX (1000), not of current (500)");
        }

        [Test]
        public void Overkill_ClampsAtZero_AndReportsTheHpActuallyRemoved()
        {
            var state = new HealthState(100f);
            var result = Hit(state, Flat(150f));

            Assert.AreEqual(0f, state.Current);
            Assert.AreEqual(100f, result.dealt, 1e-4f);
            Assert.IsTrue(result.killed);
            Assert.IsTrue(state.IsDestroyed);
        }

        [Test]
        public void Killed_IsReportedOnce()
        {
            var state = new HealthState(100f);
            Hit(state, Flat(150f));
            var second = Hit(state, Flat(150f));

            Assert.AreEqual(DamageOutcome.NotTargetable, second.outcome);
            Assert.IsFalse(second.killed);
        }

        [Test]
        public void NonLethalHit_IsNotAKill()
        {
            var state = new HealthState(100f);
            var result = Hit(state, Flat(99f));

            Assert.IsFalse(result.killed);
            Assert.IsFalse(state.IsDestroyed);
        }
    }
}
