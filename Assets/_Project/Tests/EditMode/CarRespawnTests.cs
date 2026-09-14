using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    /// <summary>
    /// CarRespawn's reset-before-reactivate ordering (the Respawnables loop runs
    /// before root.SetActive(true)) is verified by inspection, not by an
    /// executable test: EditMode cannot observe activeSelf from inside a
    /// component's own reset method without a MonoBehaviour test double, and a
    /// MonoBehaviour defined in this test assembly cannot be attached (Unity
    /// refuses AddComponent for types compiled into a test assembly). Instead,
    /// Respawn_ResetsTheCarsRespawnables_RevivingADestroyedCar below exercises a
    /// real production IRespawnable (Health) end to end.
    /// </summary>
    public class CarRespawnTests
    {
        GameObject _object;
        CarController _car;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Car", typeof(CarController));
            _car = _object.GetComponent<CarController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void Respawn_ResetsTheCarsRespawnables_RevivingADestroyedCar()
        {
            var health = _object.AddComponent<Health>();
            health.maxHealth = 1000f;
            health.Apply(new DamageRequest { sourceTag = "test", kind = DamageKind.Flat, amount = 5000f });
            _object.SetActive(false);

            CarRespawn.Respawn(_car, Vector3.zero, Quaternion.identity);

            Assert.IsFalse(health.IsDestroyed);
            Assert.AreEqual(1000f, health.Current);
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Targetable | CarAbility.Throttle));
        }

        [Test]
        public void Respawn_ReactivatesTheCarAtThePose()
        {
            _object.SetActive(false);
            var position = new Vector3(3f, 0.7f, -8f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            CarRespawn.Respawn(_car, position, rotation);

            Assert.IsTrue(_object.activeSelf);
            Assert.AreEqual(position, _object.transform.position);
            Assert.Less(Quaternion.Angle(rotation, _object.transform.rotation), 0.01f);
        }

        [Test]
        public void Respawn_PutsTheRootBackOnTheCarLayer()
        {
            _object.layer = PhysicsLayers.Wreck;

            CarRespawn.Respawn(_car, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(PhysicsLayers.Car, _object.layer);
        }

        [Test]
        public void Respawn_ZeroesAimAndVelocity()
        {
            var body = _object.GetComponent<Rigidbody>();
            _car.AimYaw = 25f;
            body.linearVelocity = new Vector3(4f, 0f, 2f);
            body.angularVelocity = new Vector3(0f, 3f, 0f);

            CarRespawn.Respawn(_car, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(0f, _car.AimYaw);
            Assert.AreEqual(Vector3.zero, body.linearVelocity);
            Assert.AreEqual(Vector3.zero, body.angularVelocity);
        }
    }
}
