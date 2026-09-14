using NUnit.Framework;
using UnityEngine;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class HealthBarLayoutTests
    {
        [Test]
        public void BehindTheCamera_IsHidden()
        {
            Assert.IsFalse(HealthBarLayout.IsVisible(new Vector3(960f, 540f, -5f), 1920f, 1080f, 100f));
        }

        [Test]
        public void OnScreen_IsVisible()
        {
            Assert.IsTrue(HealthBarLayout.IsVisible(new Vector3(960f, 540f, 20f), 1920f, 1080f, 100f));
        }

        [Test]
        public void FarOffScreen_IsHidden()
        {
            Assert.IsFalse(HealthBarLayout.IsVisible(new Vector3(-500f, 540f, 20f), 1920f, 1080f, 100f));
        }

        /// <summary>Widths come in as screen pixels and leave as reference pixels, clamped.</summary>
        [Test]
        public void BarWidth_ConvertsToReferencePixelsAndClamps()
        {
            Assert.AreEqual(100f, HealthBarLayout.BarWidth(200f, 2f, 60f, 160f), 1e-4f);
            Assert.AreEqual(60f, HealthBarLayout.BarWidth(10f, 1f, 60f, 160f), 1e-4f);
            Assert.AreEqual(160f, HealthBarLayout.BarWidth(900f, 1f, 60f, 160f), 1e-4f);
        }

        [Test]
        public void Fill_IsTheHealthFraction_Clamped()
        {
            Assert.AreEqual(0.35f, HealthBarLayout.Fill(350f, 1000f), 1e-5f);
            Assert.AreEqual(0f, HealthBarLayout.Fill(10f, 0f));
            Assert.AreEqual(1f, HealthBarLayout.Fill(1200f, 1000f));
        }

        [Test]
        public void SelfText_RoundsUp()
        {
            Assert.AreEqual("667 / 1000", HealthBarLayout.SelfText(666.67f, 1000f));
        }

        [Test]
        public void SelfText_NeverShowsZeroWhileAlive()
        {
            Assert.AreEqual("1 / 1000", HealthBarLayout.SelfText(0.01f, 1000f));
        }
    }
}
