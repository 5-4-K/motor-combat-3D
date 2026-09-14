using System.Collections.Generic;
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    public class EffectSetTests
    {
        static EffectSet.Entry Entry(EffectType type, float duration)
        {
            return new EffectSet.Entry { type = type, remaining = duration, duration = duration, magnitude = 10f };
        }

        [Test]
        public void Set_MakesTheTypeActive()
        {
            var set = new EffectSet();
            set.Set(Entry(EffectType.Corroded, 3f));

            Assert.IsTrue(set.IsActive(EffectType.Corroded));
            Assert.AreEqual(3f, set.Remaining(EffectType.Corroded));
            Assert.IsTrue(set.TryGet(EffectType.Corroded, out EffectSet.Entry entry));
            Assert.AreEqual(10f, entry.magnitude);
            Assert.IsFalse(set.IsActive(EffectType.Stunned));
            Assert.AreEqual(0f, set.Remaining(EffectType.Stunned));
        }

        [Test]
        public void Advance_ExpiresAtTheDuration_DespiteFloatSteps()
        {
            var set = new EffectSet();
            var expired = new List<EffectType>();
            set.Set(Entry(EffectType.Corroded, 3f));

            for (int i = 0; i < 149; i++) set.Advance(0.02f, expired);
            Assert.IsTrue(set.IsActive(EffectType.Corroded), "one step still to go");
            Assert.AreEqual(0, expired.Count);

            set.Advance(0.02f, expired);
            Assert.IsFalse(set.IsActive(EffectType.Corroded));
            CollectionAssert.AreEqual(new[] { EffectType.Corroded }, expired);
        }

        [Test]
        public void Advance_ReportsExpiredTypesInTypeOrder()
        {
            var set = new EffectSet();
            var expired = new List<EffectType>();
            set.Set(Entry(EffectType.Exhausted, 1f));
            set.Set(Entry(EffectType.Stunned, 1f));

            set.Advance(1f, expired);

            CollectionAssert.AreEqual(new[] { EffectType.Stunned, EffectType.Exhausted }, expired);
        }

        [Test]
        public void Advance_NeverExpiresAnUntimedEffect()
        {
            var set = new EffectSet();
            var expired = new List<EffectType>();
            set.Set(Entry(EffectType.Fortified, float.PositiveInfinity));

            set.Advance(1000f, expired);

            Assert.IsTrue(set.IsActive(EffectType.Fortified));
        }

        [Test]
        public void Remove_ReportsWhetherItWasActive()
        {
            var set = new EffectSet();
            set.Set(Entry(EffectType.Armored, 2f));

            Assert.IsTrue(set.Remove(EffectType.Armored));
            Assert.IsFalse(set.Remove(EffectType.Armored));
            Assert.IsFalse(set.IsActive(EffectType.Armored));
        }

        [Test]
        public void ActiveTypes_ListsInTypeOrder()
        {
            var set = new EffectSet();
            var types = new List<EffectType> { EffectType.Armored };
            set.Set(Entry(EffectType.Reeling, 2f));
            set.Set(Entry(EffectType.Stunned, 2f));

            set.ActiveTypes(types);

            CollectionAssert.AreEqual(new[] { EffectType.Stunned, EffectType.Reeling }, types, "cleared first, then filled");
        }
    }
}
