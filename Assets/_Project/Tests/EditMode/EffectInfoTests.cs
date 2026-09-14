using System;
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class EffectInfoTests
    {
        [Test]
        public void IsKnown_CoversExactlyTheDeclaredTypes()
        {
            Assert.AreEqual(Enum.GetValues(typeof(EffectType)).Length, EffectInfo.Count);
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                Assert.IsTrue(EffectInfo.IsKnown(type), type.ToString());
            }
            Assert.IsFalse(EffectInfo.IsKnown((EffectType)(-1)));
            Assert.IsFalse(EffectInfo.IsKnown((EffectType)EffectInfo.Count));
        }

        [Test]
        public void IsBuff_OnlyFortifiedArmoredAndOverhauled()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                bool expected = type == EffectType.Fortified || type == EffectType.Armored || type == EffectType.Overhauled;
                Assert.AreEqual(expected, EffectInfo.IsBuff(type), type.ToString());
            }
        }

        [Test]
        public void IsTimed_EverythingButOverhauled()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                Assert.AreEqual(type != EffectType.Overhauled, EffectInfo.IsTimed(type), type.ToString());
            }
        }

        [Test]
        public void UsesMagnitude_OnlyOverheatedAndTheStatEffects()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                bool expected = type == EffectType.Overheated || type == EffectType.Corroded || type == EffectType.Spiked
                                || type == EffectType.Fortified || type == EffectType.Exhausted;
                Assert.AreEqual(expected, EffectInfo.UsesMagnitude(type), type.ToString());
            }
        }
    }
}
