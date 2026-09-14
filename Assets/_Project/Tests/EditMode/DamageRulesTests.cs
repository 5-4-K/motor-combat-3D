using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class DamageRulesTests
    {
        [TestCase(100f, 0f, 100f)]
        [TestCase(100f, 100f, 50f)]
        [TestCase(150f, 100f, 75f)]
        [TestCase(100f, 50f, 66.6667f)]
        [TestCase(100f, 150f, 40f)]
        [TestCase(100f, 300f, 25f)]
        public void Mitigate_MatchesTheSpecTable(float attack, float defense, float expected)
        {
            Assert.AreEqual(expected, DamageRules.Mitigate(100f, attack, defense), 1e-3f);
        }

        [Test]
        public void Mitigate_NegativeDefenseIsTreatedAsZero()
        {
            Assert.AreEqual(100f, DamageRules.Mitigate(100f, 100f, -50f), 1e-4f);
        }

        [Test]
        public void RawAmount_FlatIsTheAmount()
        {
            Assert.AreEqual(40f, DamageRules.RawAmount(DamageKind.Flat, 40f, 1000f), 1e-4f);
        }

        [Test]
        public void RawAmount_MaxHealthPercentIsAPercentOfMaxHealth()
        {
            Assert.AreEqual(250f, DamageRules.RawAmount(DamageKind.MaxHealthPercent, 25f, 1000f), 1e-4f);
        }
    }
}
