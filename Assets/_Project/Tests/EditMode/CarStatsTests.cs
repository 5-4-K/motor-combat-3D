using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarStatsTests
    {
        static readonly object Corroded = new object();
        static readonly object Fortified = new object();

        [Test]
        public void NoModifiers_EffectiveEqualsBase()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            Assert.AreEqual(100f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void TopSpeed_DefaultsToOne()
        {
            Assert.AreEqual(1f, new CarStats().Effective(CarStat.TopSpeed), 1e-6f);
        }

        [Test]
        public void PercentagesFromDifferentSources_Add()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -30f);
            stats.Add(Fortified, CarStat.Defense, 20f);

            Assert.AreEqual(90f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void SameSourceOnTheSameStat_Replaces()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -30f);
            stats.Add(Corroded, CarStat.Defense, -10f);

            Assert.AreEqual(90f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void SameSourceOnDifferentStats_Coexist()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Attack, 100f);
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Attack, -20f);
            stats.Add(Corroded, CarStat.Defense, 50f);

            Assert.AreEqual(80f, stats.Effective(CarStat.Attack), 1e-4f);
            Assert.AreEqual(150f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void Remove_RemovesEveryModifierOfThatSource()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Attack, 100f);
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Attack, -20f);
            stats.Add(Corroded, CarStat.Defense, -20f);
            stats.Add(Fortified, CarStat.Defense, 10f);
            stats.Remove(Corroded);

            Assert.AreEqual(100f, stats.Effective(CarStat.Attack), 1e-4f);
            Assert.AreEqual(110f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void RemoveAll_RestoresEveryBase()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -30f);
            stats.Add(Fortified, CarStat.TopSpeed, -50f);
            stats.RemoveAll();

            Assert.AreEqual(100f, stats.Effective(CarStat.Defense), 1e-4f);
            Assert.AreEqual(1f, stats.Effective(CarStat.TopSpeed), 1e-6f);
        }

        [Test]
        public void Effective_NeverGoesBelowZero()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -150f);

            Assert.AreEqual(0f, stats.Effective(CarStat.Defense));
        }

        [Test]
        public void Modifiers_OnlyAffectTheirOwnStat()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Attack, 100f);
            stats.SetBase(CarStat.Strength, 1f);
            stats.Add(Corroded, CarStat.Attack, -50f);

            Assert.AreEqual(1f, stats.Effective(CarStat.Strength), 1e-6f);
        }

        [Test]
        public void Has_ReportsWhetherASourceHasModifiers()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);

            Assert.IsFalse(stats.Has(Corroded));

            stats.Add(Corroded, CarStat.Defense, -30f);

            Assert.IsTrue(stats.Has(Corroded));
            Assert.IsFalse(stats.Has(Fortified));
        }
    }
}
