using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class EffectChipLayoutTests
    {
        [Test]
        public void Label_IsFourLettersAndUniquePerEffect()
        {
            var seen = new HashSet<string>();
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                string label = EffectChipLayout.Label(type);
                Assert.AreEqual(4, label.Length, type.ToString());
                Assert.IsTrue(seen.Add(label), "duplicate label " + label);
            }
        }

        [Test]
        public void Text_RoundsUpToTenths_SoAChipNeverReadsZeroWhileOn()
        {
            Assert.AreEqual("STUN 1.2s", EffectChipLayout.Text(EffectType.Stunned, 1.2f));
            Assert.AreEqual("STUN 1.3s", EffectChipLayout.Text(EffectType.Stunned, 1.21f));
            Assert.AreEqual("STUN 0.1s", EffectChipLayout.Text(EffectType.Stunned, 0.01f));
            Assert.AreEqual("STUN 0.0s", EffectChipLayout.Text(EffectType.Stunned, 0f));
        }

        [Test]
        public void Text_UsesInvariantCulture_AndDropsTheTimeWhenUntimed()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("CORR 1.5s", EffectChipLayout.Text(EffectType.Corroded, 1.5f));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.AreEqual("FORT", EffectChipLayout.Text(EffectType.Fortified, float.PositiveInfinity));
        }

        [Test]
        public void RowX_CentresTheRow()
        {
            Assert.AreEqual(0f, EffectChipLayout.RowX(0, 1, 40f, 4f), 1e-4f);
            Assert.AreEqual(-22f, EffectChipLayout.RowX(0, 2, 40f, 4f), 1e-4f);
            Assert.AreEqual(22f, EffectChipLayout.RowX(1, 2, 40f, 4f), 1e-4f);
            Assert.AreEqual(-44f, EffectChipLayout.RowX(0, 3, 40f, 4f), 1e-4f);
            Assert.AreEqual(0f, EffectChipLayout.RowX(1, 3, 40f, 4f), 1e-4f);
        }
    }
}
