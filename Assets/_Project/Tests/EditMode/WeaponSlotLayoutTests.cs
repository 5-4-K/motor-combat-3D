using NUnit.Framework;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class WeaponSlotLayoutTests
    {
        [Test]
        public void CentreX_ThreeCircles_AreCentredOnZero()
        {
            Assert.AreEqual(-92f, WeaponSlotLayout.CentreX(0, 3, 72f, 20f), 1e-4f);
            Assert.AreEqual(0f, WeaponSlotLayout.CentreX(1, 3, 72f, 20f), 1e-4f);
            Assert.AreEqual(92f, WeaponSlotLayout.CentreX(2, 3, 72f, 20f), 1e-4f);
        }

        [Test]
        public void CooldownFill_IsTheRemainingFraction_Clamped()
        {
            Assert.AreEqual(0.25f, WeaponSlotLayout.CooldownFill(1f, 4f), 1e-5f);
            Assert.AreEqual(1f, WeaponSlotLayout.CooldownFill(9f, 4f), 1e-5f);
            Assert.AreEqual(0f, WeaponSlotLayout.CooldownFill(0f, 4f), 1e-5f);
        }

        [Test]
        public void CooldownFill_ZeroDurationOrNaN_IsEmpty()
        {
            Assert.AreEqual(0f, WeaponSlotLayout.CooldownFill(1f, 0f), 1e-5f);
            Assert.AreEqual(0f, WeaponSlotLayout.CooldownFill(float.NaN, 4f), 1e-5f);
        }

        [Test]
        public void KeyLabel_MatchesTheFireKeys()
        {
            Assert.AreEqual("LMB", WeaponSlotLayout.KeyLabel(0));
            Assert.AreEqual("RMB", WeaponSlotLayout.KeyLabel(1));
            Assert.AreEqual("SPC", WeaponSlotLayout.KeyLabel(2));
            Assert.AreEqual("", WeaponSlotLayout.KeyLabel(3));
        }
    }
}
