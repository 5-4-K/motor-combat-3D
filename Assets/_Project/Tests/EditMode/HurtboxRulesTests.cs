using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class HurtboxRulesTests
    {
        const float CarHeight = 1.2f;   // floor is at local y = -0.6

        static HurtboxBox Box(float centreY, float sizeY)
        {
            return new HurtboxBox { centre = new Vector3(0f, centreY, 0f), size = new Vector3(2f, sizeY, 4f) };
        }

        [Test]
        public void SpansHeight_FullCarBox_CoversTheFireHeight()
        {
            Assert.IsTrue(HurtboxRules.SpansHeight(new[] { Box(0f, 1.2f) }, CarHeight, 0.6f));
        }

        [Test]
        public void SpansHeight_BoxAboveTheFireHeight_DoesNot()
        {
            // Box from 0.8 to 1.2 above the floor.
            Assert.IsFalse(HurtboxRules.SpansHeight(new[] { Box(0.4f, 0.4f) }, CarHeight, 0.6f));
        }

        [Test]
        public void SpansHeight_AnyOneBoxIsEnough()
        {
            Assert.IsTrue(HurtboxRules.SpansHeight(new[] { Box(0.4f, 0.4f), Box(-0.3f, 0.6f) }, CarHeight, 0.6f));
        }

        [Test]
        public void SpansHeight_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(HurtboxRules.SpansHeight(null, CarHeight, 0.6f));
            Assert.IsFalse(HurtboxRules.SpansHeight(new HurtboxBox[0], CarHeight, 0.6f));
        }
    }
}
