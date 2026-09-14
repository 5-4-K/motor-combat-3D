using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Ramming;

namespace MotorCombat.Tests
{
    public class PushMathTests
    {
        const float W = 2f;
        const float L = 4f;

        [Test]
        public void SpinDelta_ThroughTheCentre_IsZero()
        {
            Assert.AreEqual(0f, PushMath.SpinDelta(Vector3.zero, Vector3.zero, new Vector3(5f, 0f, 0f), W, L, 1f), 1e-5f);
        }

        /// <summary>k² = (2² + 4²) / 12 = 5/3; cross((0,0,−2), (1,0,0)).y = −2; −2 / (5/3) = −1.2 rad/s.</summary>
        [Test]
        public void SpinDelta_TailPushedRight_SpinsNoseLeft()
        {
            Assert.AreEqual(-1.2f, PushMath.SpinDelta(new Vector3(0f, 0f, -2f), Vector3.zero, new Vector3(1f, 0f, 0f), W, L, 1f), 1e-4f);
        }

        [Test]
        public void RamRulesSpinDelta_MatchesPushMath()
        {
            var contact = new Vector3(0.7f, 0.3f, -1.9f);
            var shove = new Vector3(3f, 0f, 1f);
            Assert.AreEqual(
                PushMath.SpinDelta(contact, Vector3.zero, shove, W, L, 0.8f),
                RamRules.SpinDelta(contact, Vector3.zero, shove, W, L, 0.8f),
                1e-6f);
        }
    }
}
