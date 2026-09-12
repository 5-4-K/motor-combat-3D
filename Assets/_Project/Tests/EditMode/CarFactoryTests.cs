using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Cars;
using MotorCombat.Driving;
using MotorCombat.Aiming;

namespace MotorCombat.Tests
{
    /// <summary>
    /// Pins the car assembly contract that CameraRig and GameBootstrap both rely on.
    /// EditMode only, so Awake() never runs — assert only on what CarFactory sets directly,
    /// never on fields CarController.Awake would populate.
    /// </summary>
    public class CarFactoryTests
    {
        CarDefinition _definition;
        CarController _car;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<CarDefinition>();
            _definition.name = "TestCar";
            _definition.length = 4.5f;
            _definition.width = 2f;
            _definition.height = 1.2f;
            _definition.mass = 1200f;
            _definition.driverAnchorOffset = new Vector3(0f, 1f, 0.4f);
            _definition.driveConfig = ScriptableObject.CreateInstance<DriveConfig>();
            _definition.aimConfig = ScriptableObject.CreateInstance<AimConfig>();

            _car = CarFactory.Spawn(
                _definition, Vector3.zero, Quaternion.identity, null, Color.white);
        }

        [TearDown]
        public void TearDown()
        {
            if (_car != null) Object.DestroyImmediate(_car.gameObject);
            Object.DestroyImmediate(_definition.driveConfig);
            Object.DestroyImmediate(_definition.aimConfig);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void Spawn_ReturnsControllerOnTheRoot()
        {
            Assert.IsNotNull(_car);
            Assert.AreSame(_car.gameObject, _car.transform.root.gameObject);
        }

        [Test]
        public void Spawn_PutsExactlyOneColliderOnTheRoot_MatchingCarDimensions()
        {
            var colliders = _car.GetComponents<BoxCollider>();
            Assert.AreEqual(1, colliders.Length, "collision must live on the root, once");
            Assert.AreEqual(new Vector3(2f, 1.2f, 4.5f), colliders[0].size);
        }

        [Test]
        public void Spawn_LeavesRootScaleUnchangedSoColliderMatchesVisual()
        {
            Assert.AreEqual(Vector3.one, _car.transform.localScale);
        }

        [Test]
        public void Spawn_SetsRigidbodyMassFromDefinition()
        {
            var body = _car.GetComponent<Rigidbody>();
            Assert.IsNotNull(body);
            Assert.AreEqual(1200f, body.mass, 1e-4f);
        }

        [Test]
        public void Spawn_CreatesDriverAnchorAtTheConfiguredOffset()
        {
            var anchor = _car.transform.Find(CarController.DriverAnchorName);
            Assert.IsNotNull(anchor, "CameraRig's first-person mode looks this child up by name");
            Assert.AreEqual(new Vector3(0f, 1f, 0.4f), anchor.localPosition);
        }

        [Test]
        public void Spawn_AttachesAllFourModules()
        {
            var modules = _car.GetComponents<ICarModule>();
            Assert.AreEqual(4, modules.Length, "driving, aiming, ramming, weapons");
        }

        [Test]
        public void Spawn_WiresModuleConfigsFromTheDefinition()
        {
            Assert.AreSame(_definition.driveConfig, _car.GetComponent<DrivingModule>().config);
            Assert.AreSame(_definition.aimConfig, _car.GetComponent<AimModule>().config);
        }
    }
}
