using NUnit.Framework;
using MotorCombat.Aiming;

namespace MotorCombat.Tests
{
    public class AimMathTests
    {
        const float Cone = 90f;          // +/- 45 degrees
        const float Sensitivity = 0.12f; // degrees per pixel

        [Test]
        public void ZeroDelta_LeavesYawUnchanged()
        {
            float result = AimMath.Accumulate(12.5f, 0f, Sensitivity, Cone);
            Assert.AreEqual(12.5f, result, 1e-5f);
        }

        [Test]
        public void SmallDelta_AccumulatesAtSensitivity()
        {
            float result = AimMath.Accumulate(0f, 100f, Sensitivity, Cone);
            Assert.AreEqual(12f, result, 1e-5f);
        }

        [Test]
        public void LargePositiveDelta_ClampsAtHalfCone()
        {
            float result = AimMath.Accumulate(0f, 10000f, Sensitivity, Cone);
            Assert.AreEqual(45f, result, 1e-5f);
        }

        [Test]
        public void LargeNegativeDelta_ClampsAtNegativeHalfCone()
        {
            float result = AimMath.Accumulate(0f, -10000f, Sensitivity, Cone);
            Assert.AreEqual(-45f, result, 1e-5f);
        }

        [Test]
        public void PushingPastTheEdge_DoesNotWrapAround()
        {
            float atEdge = AimMath.Accumulate(0f, 10000f, Sensitivity, Cone);
            float stillAtEdge = AimMath.Accumulate(atEdge, 10000f, Sensitivity, Cone);
            Assert.AreEqual(45f, stillAtEdge, 1e-5f);
        }

        [Test]
        public void ReversingFromTheEdge_MovesBackImmediately()
        {
            float atEdge = AimMath.Accumulate(0f, 10000f, Sensitivity, Cone);
            float backOff = AimMath.Accumulate(atEdge, -100f, Sensitivity, Cone);
            Assert.AreEqual(33f, backOff, 1e-5f);
        }
    }
}
