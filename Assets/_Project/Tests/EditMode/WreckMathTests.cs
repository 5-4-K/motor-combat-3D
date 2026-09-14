using NUnit.Framework;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class WreckMathTests
    {
        [Test]
        public void RollAngle_StartsAtZero() => Assert.AreEqual(0f, WreckMath.RollAngle(0f, 0.8f, 180f), 1e-4f);

        [Test]
        public void RollAngle_IsFullAtAndAfterRollSeconds()
        {
            Assert.AreEqual(180f, WreckMath.RollAngle(0.8f, 0.8f, 180f), 1e-3f);
            Assert.AreEqual(180f, WreckMath.RollAngle(5f, 0.8f, 180f), 1e-3f);
        }

        [Test]
        public void RollAngle_IsHalfwayAtHalfTime() => Assert.AreEqual(90f, WreckMath.RollAngle(0.4f, 0.8f, 180f), 1e-3f);

        [Test]
        public void RollAngle_ZeroDurationIsFull() => Assert.AreEqual(180f, WreckMath.RollAngle(0f, 0f, 180f), 1e-4f);

        [Test]
        public void Lift_IsZeroWhenUpright() => Assert.AreEqual(0f, WreckMath.Lift(0f, 1.1f, 0.67f), 1e-4f);

        /// <summary>On its side the car stands on its half WIDTH instead of its half height.</summary>
        [Test]
        public void Lift_AtNinetyDegreesIsHalfWidthMinusHalfHeight() => Assert.AreEqual(0.43f, WreckMath.Lift(90f, 1.1f, 0.67f), 1e-3f);

        [Test]
        public void Lift_IsZeroWhenUpsideDown() => Assert.AreEqual(0f, WreckMath.Lift(180f, 1.1f, 0.67f), 1e-3f);

        [Test]
        public void Lift_IsNeverNegative() => Assert.AreEqual(0f, WreckMath.Lift(90f, 0.5f, 1f), 1e-4f);

        [Test]
        public void Alpha_FadesLinearly()
        {
            Assert.AreEqual(1f, WreckMath.Alpha(0f, 1.5f), 1e-4f);
            Assert.AreEqual(0.5f, WreckMath.Alpha(0.75f, 1.5f), 1e-4f);
            Assert.AreEqual(0f, WreckMath.Alpha(2f, 1.5f), 1e-4f);
        }

        [Test]
        public void Alpha_ZeroDurationIsInvisible() => Assert.AreEqual(0f, WreckMath.Alpha(0f, 0f), 1e-4f);
    }
}
