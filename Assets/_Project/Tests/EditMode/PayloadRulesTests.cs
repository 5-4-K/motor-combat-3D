using NUnit.Framework;
using UnityEngine;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class PayloadRulesTests
    {
        static PushSpec Push(float speed, PushDirection direction) => new PushSpec { speed = speed, direction = direction, spinScale = 1f, reelSeconds = 1f };

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void AlongTravel_PushesTheWayTheShotFlew_Flattened()
        {
            Vector3 dv = PayloadRules.PushDelta(Push(8f, PushDirection.AlongTravel), new Vector3(0f, 1f, 1f), Vector3.zero, new Vector3(5f, 0f, 0f));
            AssertVector(new Vector3(0f, 0f, 8f), dv);
        }

        [Test]
        public void AwayFromCentre_PushesFromTheHitboxCentreToTheCar_Flattened()
        {
            Vector3 dv = PayloadRules.PushDelta(Push(8f, PushDirection.AwayFromCentre), Vector3.forward, new Vector3(0f, 2f, 0f), new Vector3(3f, 0f, 0f));
            AssertVector(new Vector3(8f, 0f, 0f), dv);
        }

        [Test]
        public void AwayFromCentre_CoincidentCentres_FallBackToTravel()
        {
            Vector3 dv = PayloadRules.PushDelta(Push(8f, PushDirection.AwayFromCentre), Vector3.left, new Vector3(1f, 5f, 1f), new Vector3(1f, 0f, 1f));
            AssertVector(new Vector3(-8f, 0f, 0f), dv);
        }

        [Test]
        public void ZeroOrNegativeSpeed_IsNoPush()
        {
            AssertVector(Vector3.zero, PayloadRules.PushDelta(Push(0f, PushDirection.AlongTravel), Vector3.forward, Vector3.zero, Vector3.one));
            AssertVector(Vector3.zero, PayloadRules.PushDelta(Push(-3f, PushDirection.AlongTravel), Vector3.forward, Vector3.zero, Vector3.one));
        }

        [Test]
        public void NoUsableDirection_IsNoPush()
        {
            AssertVector(Vector3.zero, PayloadRules.PushDelta(Push(8f, PushDirection.AlongTravel), Vector3.up, Vector3.zero, Vector3.one));
        }
    }
}
