using NUnit.Framework;
using MotorCombat.Cameras;

namespace MotorCombat.Tests
{
    /// <summary>
    /// Competitive fairness rule: every player sees the same HORIZONTAL angle,
    /// whatever their monitor shape. Unity's camera only takes a vertical FOV,
    /// so the vertical value has to be derived from the screen's aspect ratio.
    /// </summary>
    public class CameraFovTests
    {
        const float Aspect16x9 = 16f / 9f;

        /// <summary>
        /// The configured horizontal values were converted from the vertical FOVs
        /// tuned by eye (70 first person, 60 third person) as seen at 16:9. They
        /// must round-trip back to exactly those looks on a 16:9 screen.
        /// </summary>
        [Test]
        public void At16x9_ReproducesTheViewsTunedAsVerticalFov()
        {
            Assert.AreEqual(70f, CameraFov.VerticalFor(102.45f, Aspect16x9), 0.05f, "first person");
            Assert.AreEqual(60f, CameraFov.VerticalFor(91.49f, Aspect16x9), 0.05f, "third person");
        }

        [Test]
        public void SquareScreen_HasEqualVerticalAndHorizontalAngles()
        {
            Assert.AreEqual(90f, CameraFov.VerticalFor(90f, 1f), 0.01f);
        }

        /// <summary>
        /// The point of the rule: an ultrawide player does NOT get more side view,
        /// they get less top-and-bottom view instead.
        /// </summary>
        [Test]
        public void WiderScreen_GetsLessVerticalView_ForTheSameHorizontalAngle()
        {
            float at4x3 = CameraFov.VerticalFor(102f, 4f / 3f);
            float at16x9 = CameraFov.VerticalFor(102f, Aspect16x9);
            float at21x9 = CameraFov.VerticalFor(102f, 21f / 9f);

            Assert.Greater(at4x3, at16x9);
            Assert.Greater(at16x9, at21x9);
        }

        [Test]
        public void BadAspect_DoesNotProduceNaN()
        {
            float result = CameraFov.VerticalFor(102f, 0f);
            Assert.IsFalse(float.IsNaN(result));
            Assert.Greater(result, 0f);
            Assert.Less(result, 180f);
        }

        [Test]
        public void HorizontalAngleAtOrPast180_IsClamped()
        {
            float result = CameraFov.VerticalFor(200f, Aspect16x9);
            Assert.IsFalse(float.IsNaN(result));
            Assert.Greater(result, 0f);
            Assert.Less(result, 180f);
        }
    }
}
