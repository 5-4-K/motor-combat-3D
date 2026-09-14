using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class HostilityTests
    {
        GameObject _a;
        GameObject _b;

        [SetUp]
        public void SetUp()
        {
            _a = new GameObject("A", typeof(CarController));
            _b = new GameObject("B", typeof(CarController));
        }

        [TearDown]
        public void TearDown()
        {
            Hostility.ResetRule();
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        CarController A => _a.GetComponent<CarController>();
        CarController B => _b.GetComponent<CarController>();

        [Test]
        public void DifferentCars_AreEnemies()
        {
            Assert.IsTrue(Hostility.AreEnemies(A, B));
        }

        [Test]
        public void ACar_IsNotItsOwnEnemy()
        {
            Assert.IsFalse(Hostility.AreEnemies(A, A));
        }

        [Test]
        public void NullSource_IsAnEnemyOfEveryone()
        {
            Assert.IsTrue(Hostility.AreEnemies(null, A));
        }

        [Test]
        public void ReplacedRule_IsUsed_AndResetRestoresTheDefault()
        {
            Hostility.SetRule((x, y) => false);
            Assert.IsFalse(Hostility.AreEnemies(A, B));

            Hostility.ResetRule();
            Assert.IsTrue(Hostility.AreEnemies(A, B));
        }
    }
}
