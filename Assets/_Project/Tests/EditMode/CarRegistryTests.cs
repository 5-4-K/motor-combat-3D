using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    /// <summary>EditMode never calls OnEnable, so these register explicitly.</summary>
    public class CarRegistryTests
    {
        GameObject _object;
        CarController _car;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Registered", typeof(CarController));
            _car = _object.GetComponent<CarController>();
        }

        [TearDown]
        public void TearDown()
        {
            CarRegistry.Unregister(_car);
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void Register_AddsTheCar()
        {
            CarRegistry.Register(_car);
            CollectionAssert.Contains(CarRegistry.All, _car);
        }

        [Test]
        public void Register_Twice_KeepsOneEntry()
        {
            CarRegistry.Register(_car);
            CarRegistry.Register(_car);

            int count = 0;
            foreach (var car in CarRegistry.All) if (car == _car) count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Unregister_RemovesTheCar()
        {
            CarRegistry.Register(_car);
            CarRegistry.Unregister(_car);
            CollectionAssert.DoesNotContain(CarRegistry.All, _car);
        }
    }
}
