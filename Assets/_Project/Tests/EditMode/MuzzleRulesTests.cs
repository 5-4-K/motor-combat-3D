using NUnit.Framework;
using UnityEngine;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class MuzzleRulesTests
    {
        static readonly Vector3 Box = new Vector3(2f, 1.2f, 4f);    // width, height, length
        static readonly Vector3 Root = new Vector3(10f, 0.6f, 20f); // grounded: floor at y = 0

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void Fixed_Front_IsTheFrontFaceCentre_AtFireHeight()
        {
            Muzzle m = MuzzleRules.Fixed(FixedMuzzles.Front, Root, Quaternion.identity, Box, 0.5f);
            AssertVector(new Vector3(10f, 0.5f, 22f), m.position);
            AssertVector(Vector3.forward, m.direction);
        }

        [Test]
        public void Fixed_RearLeftRight_PointStraightOutOfTheirFaces()
        {
            Muzzle rear = MuzzleRules.Fixed(FixedMuzzles.Rear, Root, Quaternion.identity, Box, 0.5f);
            Muzzle left = MuzzleRules.Fixed(FixedMuzzles.Left, Root, Quaternion.identity, Box, 0.5f);
            Muzzle right = MuzzleRules.Fixed(FixedMuzzles.Right, Root, Quaternion.identity, Box, 0.5f);

            AssertVector(new Vector3(10f, 0.5f, 18f), rear.position);
            AssertVector(Vector3.back, rear.direction);
            AssertVector(new Vector3(9f, 0.5f, 20f), left.position);
            AssertVector(Vector3.left, left.direction);
            AssertVector(new Vector3(11f, 0.5f, 20f), right.position);
            AssertVector(Vector3.right, right.direction);
        }

        [Test]
        public void Fixed_RotatesWithTheCarsYaw()
        {
            Muzzle m = MuzzleRules.Fixed(FixedMuzzles.Front, Root, Quaternion.Euler(0f, 90f, 0f), Box, 0.5f);
            AssertVector(new Vector3(12f, 0.5f, 20f), m.position);
            AssertVector(Vector3.right, m.direction);
        }

        [Test]
        public void Directions_AreFlat_WhenTheCarIsPitched()
        {
            Muzzle m = MuzzleRules.Fixed(FixedMuzzles.Front, Root, Quaternion.Euler(-10f, 0f, 0f), Box, 0.5f);
            Assert.AreEqual(0f, m.direction.y, 1e-5f);
            Assert.AreEqual(1f, m.direction.magnitude, 1e-4f);
        }

        [Test]
        public void Turret_SitsAtTheFront_AndFollowsTheAim()
        {
            Muzzle m = MuzzleRules.Turret(Root, Quaternion.identity, Box, 0.5f, 90f);
            AssertVector(new Vector3(10f, 0.5f, 22f), m.position);
            AssertVector(Vector3.right, m.direction);
        }

        [Test]
        public void FixedOrder_IsFrontRearLeftRight()
        {
            CollectionAssert.AreEqual(
                new[] { FixedMuzzles.Front, FixedMuzzles.Rear, FixedMuzzles.Left, FixedMuzzles.Right },
                MuzzleRules.FixedOrder);
        }
    }
}
