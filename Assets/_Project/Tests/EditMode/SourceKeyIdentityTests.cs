using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    /// <summary>
    /// CarStats, CarAbilities and HealthState's gates all key state off a
    /// caller-supplied "source" object. This builds two distinct string
    /// instances with equal content — never through a literal, so CLR
    /// interning (which would make two "source" literals the same
    /// reference) can't hide a bug — and checks that only the exact
    /// instance which registered a block, modifier or gate can remove it.
    /// </summary>
    public class SourceKeyIdentityTests
    {
        [Test]
        public void SourceKeys_AreComparedByIdentityAcrossAbilitiesStatsAndGates()
        {
            string keyA = new string(new[] { 's', 'o', 'u', 'r', 'c', 'e' });
            string keyB = new string(new[] { 's', 'o', 'u', 'r', 'c', 'e' });
            Assert.AreNotSame(keyA, keyB);
            Assert.AreEqual(keyA, keyB);

            var abilities = new CarAbilities();
            abilities.Block(keyA, CarAbility.Throttle, float.PositiveInfinity, BlockRefresh.KeepLonger);

            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(keyA, CarStat.Defense, -50f);

            var state = new HealthState(1000f);
            state.AddGate(keyA, _ => true);
            var request = new DamageRequest { sourceTag = "test", kind = DamageKind.Flat, amount = 10f };

            // A different instance with equal content must remove nothing.
            abilities.Unblock(keyB);
            stats.Remove(keyB);
            state.RemoveGate(keyB);

            Assert.IsFalse(abilities.Has(CarAbility.Throttle), "keyA's block should still be active");
            Assert.IsTrue(stats.Has(keyA), "keyA's modifier should still be registered");
            Assert.AreEqual(50f, stats.Effective(CarStat.Defense), 1e-4f);
            DamageResult stillGated = state.Apply(request, targetable: true, hostile: true, sourceAttack: 100f, targetDefense: 0f);
            Assert.AreEqual(DamageOutcome.Blocked, stillGated.outcome, "keyA's gate should still be active");

            // The exact same instance must remove everything.
            abilities.Unblock(keyA);
            stats.Remove(keyA);
            state.RemoveGate(keyA);

            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(stats.Has(keyA));
            Assert.AreEqual(100f, stats.Effective(CarStat.Defense), 1e-4f);
            DamageResult ungated = state.Apply(request, targetable: true, hostile: true, sourceAttack: 100f, targetDefense: 0f);
            Assert.AreEqual(DamageOutcome.Applied, ungated.outcome);
        }
    }
}
