using NUnit.Framework;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class HudShapesTests
    {
        [Test]
        public void CircleAlpha_SolidInside_ClearOutside_SoftAtTheEdge()
        {
            Assert.AreEqual(1f, HudShapes.CircleAlpha(10f, 64f), 1e-5f);
            Assert.AreEqual(0f, HudShapes.CircleAlpha(70f, 64f), 1e-5f);
            Assert.AreEqual(0.5f, HudShapes.CircleAlpha(64f, 64f), 1e-5f);
        }

        [Test]
        public void RingAlpha_IsSolidOnlyInTheBand()
        {
            Assert.AreEqual(0f, HudShapes.RingAlpha(10f, 64f, 8f), 1e-5f, "centre");
            Assert.AreEqual(1f, HudShapes.RingAlpha(60f, 64f, 8f), 1e-5f, "band");
            Assert.AreEqual(0f, HudShapes.RingAlpha(70f, 64f, 8f), 1e-5f, "outside");
        }

        [Test]
        public void Circle_IsASquareSprite_Cached()
        {
            Assert.IsNotNull(HudShapes.Circle);
            Assert.AreEqual(HudShapes.Circle.rect.width, HudShapes.Circle.rect.height);
            Assert.AreSame(HudShapes.Circle, HudShapes.Circle);
        }
    }
}
