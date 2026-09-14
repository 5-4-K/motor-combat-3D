using NUnit.Framework;
using UnityEngine;
using MotorCombat.Bootstrap;

namespace MotorCombat.Tests
{
    public class RespawnRulesTests
    {
        [Test]
        public void IsDue_FalseWhileTheWreckIsStillActive()
        {
            Assert.IsFalse(RespawnRules.IsDue(10f, 0f, 3f, carActive: true));
        }

        [Test]
        public void IsDue_FalseBeforeTheDelay()
        {
            Assert.IsFalse(RespawnRules.IsDue(2.9f, 0f, 3f, carActive: false));
        }

        [Test]
        public void IsDue_TrueAtTheDelayOnceInactive()
        {
            // 150 x 0.02 accumulates to just under 3 in float steps.
            float now = 0f;
            for (int i = 0; i < 150; i++) now += 0.02f;

            Assert.IsTrue(RespawnRules.IsDue(now, 0f, 3f, carActive: false));
        }

        [Test]
        public void IsDue_TreatsANegativeDelayAsZero()
        {
            Assert.IsTrue(RespawnRules.IsDue(0f, 0f, -5f, carActive: false));
        }

        [Test]
        public void SpawnBox_RotatesTheColliderCentreAndAddsTheMargin()
        {
            RespawnRules.SpawnBox(
                new Vector3(1f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(2f, 1f, 4f),
                0.1f,
                out Vector3 center,
                out Vector3 halfExtents);

            Assert.AreEqual(2f, center.x, 1e-4f);
            Assert.AreEqual(0f, center.y, 1e-4f);
            Assert.AreEqual(0f, center.z, 1e-4f);
            Assert.AreEqual(new Vector3(1.1f, 0.6f, 2.1f), halfExtents);
        }
    }
}
