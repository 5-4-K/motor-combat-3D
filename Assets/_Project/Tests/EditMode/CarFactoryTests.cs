using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Cars;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Ramming;

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
            _definition.ramConfig = ScriptableObject.CreateInstance<RamConfig>();
            _definition.attack = 150f;
            _definition.defense = 40f;
            _definition.strength = 2f;
            _definition.resistance = 3f;

            _car = CarFactory.Spawn(
                _definition, Vector3.zero, Quaternion.identity, null, Color.white);
        }

        [TearDown]
        public void TearDown()
        {
            if (_car != null) Object.DestroyImmediate(_car.gameObject);
            if (_modelCar != null) Object.DestroyImmediate(_modelCar.gameObject);
            if (_model != null) Object.DestroyImmediate(_model);
            Object.DestroyImmediate(_definition.driveConfig);
            Object.DestroyImmediate(_definition.aimConfig);
            Object.DestroyImmediate(_definition.ramConfig);
            Object.DestroyImmediate(_definition);
        }

        // --- Model visual helpers ---------------------------------------------

        GameObject _model;
        CarController _modelCar;

        /// <summary>
        /// Stands in for an asset-store car prefab: a renderer, a collider it
        /// ships with, and a nested Rigidbody. All three are things CarFactory
        /// has to cope with.
        /// </summary>
        GameObject BuildFakeModel()
        {
            var model = new GameObject("FakeModel");

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);   // brings its own BoxCollider
            mesh.name = "FakeBody";
            mesh.transform.SetParent(model.transform, false);

            model.AddComponent<Rigidbody>();
            return model;
        }

        CarController SpawnWithModel()
        {
            _model = BuildFakeModel();
            _definition.visualPrefab = _model;
            _modelCar = CarFactory.Spawn(
                _definition, Vector3.zero, Quaternion.identity, null, Color.red);
            return _modelCar;
        }

        static Transform Visual(CarController car)
        {
            return car.transform.Find(CarFactory.VisualName);
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

        [Test]
        public void Spawn_WiresRamConfigAndStatBasesFromTheDefinition()
        {
            Assert.AreSame(_definition.ramConfig, _car.GetComponent<RammingModule>().config);
            Assert.AreEqual(150f, _car.Stats.Base(CarStat.Attack));
            Assert.AreEqual(40f, _car.Stats.Base(CarStat.Defense));
            Assert.AreEqual(2f, _car.Stats.Base(CarStat.Strength));
            Assert.AreEqual(3f, _car.Stats.Base(CarStat.Resistance));
            Assert.AreEqual(1f, _car.Stats.Base(CarStat.TopSpeed));
        }

        // --- Visual: placeholder box ------------------------------------------

        [Test]
        public void Spawn_UsesThePlaceholderBoxWhenNoModelIsAssigned()
        {
            Assert.IsNotNull(Visual(_car), "the box is still named " + CarFactory.VisualName);
            Assert.IsNotNull(_car.transform.Find(CarFactory.NoseName),
                "a featureless box needs the facing marker");
        }

        // --- Visual: real model -----------------------------------------------

        [Test]
        public void Spawn_InstantiatesTheModelAsTheVisualChild()
        {
            var car = SpawnWithModel();
            var visual = Visual(car);

            Assert.IsNotNull(visual, "the model becomes the visual child");
            Assert.Greater(visual.GetComponentsInChildren<Renderer>(true).Length, 0);
            Assert.AreNotSame(_model.transform, visual, "must be an instance, not the source object");
        }

        [Test]
        public void Spawn_SuppressesTheNoseMarkerWhenAModelIsAssigned()
        {
            var car = SpawnWithModel();
            Assert.IsNull(car.transform.Find(CarFactory.NoseName),
                "a real model shows its own facing; the marker is placeholder-only");
        }

        /// <summary>
        /// The one that matters. Asset-store car prefabs routinely ship mesh
        /// colliders. Left in place they join the root Rigidbody as a compound
        /// collider, and a concave mesh collider on a dynamic body is illegal in
        /// PhysX — so the car's collision silently stops being the box the
        /// driving model assumes.
        /// </summary>
        [Test]
        public void Spawn_StripsCollidersThatShipWithTheModel()
        {
            var car = SpawnWithModel();

            Assert.AreEqual(0, Visual(car).GetComponentsInChildren<Collider>(true).Length,
                "the model must contribute no collision");
            Assert.AreEqual(1, car.GetComponents<BoxCollider>().Length,
                "collision still lives on the root, exactly once");
        }

        [Test]
        public void Spawn_StripsNestedRigidbodiesFromTheModel()
        {
            var car = SpawnWithModel();

            Assert.AreEqual(0, Visual(car).GetComponentsInChildren<Rigidbody>(true).Length,
                "a nested Rigidbody would break the root's physics");
        }

        [Test]
        public void Spawn_SizesTheColliderFromTheDefinitionNotTheModel()
        {
            var car = SpawnWithModel();

            Assert.AreEqual(new Vector3(2f, 1.2f, 4.5f), car.GetComponents<BoxCollider>()[0].size,
                "the model is decoration inside the box, never the source of its size");
        }

        [Test]
        public void Spawn_AppliesTheConfiguredVisualOffsetAndYaw()
        {
            _definition.visualOffset = new Vector3(0f, -0.6f, 0f);
            _definition.visualYawOffset = 180f;

            var visual = Visual(SpawnWithModel());

            Assert.AreEqual(new Vector3(0f, -0.6f, 0f), visual.localPosition);
            Assert.AreEqual(180f, visual.localRotation.eulerAngles.y, 1e-3f);
        }

        /// <summary>
        /// Team colour goes through a MaterialPropertyBlock. The model's materials
        /// are shared assets — writing to them would recolour every car at once
        /// and permanently alter the asset on disk.
        /// </summary>
        [Test]
        public void Spawn_TintsWithoutTouchingTheSharedMaterial()
        {
            var car = SpawnWithModel();
            var renderer = Visual(car).GetComponentInChildren<Renderer>();

            Assert.IsTrue(renderer.HasPropertyBlock(), "tint must be per-instance");

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            Assert.AreEqual(Color.red, block.GetColor("_BaseColor"));
        }
    }
}
