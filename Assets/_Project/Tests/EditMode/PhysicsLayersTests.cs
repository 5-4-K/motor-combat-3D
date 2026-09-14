using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    /// <summary>
    /// ConfigureCollisions changes the project-wide layer matrix, so every test
    /// restores the rows it touched.
    /// </summary>
    public class PhysicsLayersTests
    {
        bool[] _carRow;
        bool[] _wreckRow;

        [SetUp]
        public void SetUp()
        {
            _carRow = Capture(PhysicsLayers.Car);
            _wreckRow = Capture(PhysicsLayers.Wreck);
        }

        [TearDown]
        public void TearDown()
        {
            Restore(PhysicsLayers.Car, _carRow);
            Restore(PhysicsLayers.Wreck, _wreckRow);
        }

        static bool[] Capture(int layer)
        {
            var row = new bool[32];
            if (layer < 0) return row;
            for (int i = 0; i < 32; i++) row[i] = Physics.GetIgnoreLayerCollision(layer, i);
            return row;
        }

        static void Restore(int layer, bool[] row)
        {
            if (layer < 0) return;
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, row[i]);
        }

        [Test]
        public void LayerNames_ResolveToTheReservedIndices()
        {
            Assert.AreEqual(8, PhysicsLayers.Arena);
            Assert.AreEqual(9, PhysicsLayers.Car);
            Assert.AreEqual(10, PhysicsLayers.Wreck);
        }

        [Test]
        public void ConfigureCollisions_WreckCollidesOnlyWithTheArena()
        {
            PhysicsLayers.ConfigureCollisions();

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Arena));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Car));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Wreck));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, 0), "Default");
        }

        [Test]
        public void ConfigureCollisions_CarsCollideWithCarsAndTheArena()
        {
            PhysicsLayers.ConfigureCollisions();

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Car, PhysicsLayers.Car));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Car, PhysicsLayers.Arena));
        }
    }
}
