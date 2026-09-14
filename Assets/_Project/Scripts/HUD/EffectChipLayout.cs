using System.Globalization;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>What an effect chip says, its colour, and where it sits in a row. Placeholder art: text chips until icons exist.</summary>
    public static class EffectChipLayout
    {
        public static string Label(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return "STUN";
                case EffectType.Suppressed: return "SUPP";
                case EffectType.Overheated: return "HEAT";
                case EffectType.Corroded: return "CORR";
                case EffectType.Reeling: return "REEL";
                case EffectType.Spiked: return "SPIK";
                case EffectType.Fortified: return "FORT";
                case EffectType.Armored: return "ARMR";
                case EffectType.Overhauled: return "OVHL";
                case EffectType.Exhausted: return "EXHS";
                default: return "????";
            }
        }

        public static Color Colour(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return new Color(1.00f, 0.85f, 0.20f, 0.9f);
                case EffectType.Suppressed: return new Color(0.60f, 0.60f, 0.70f, 0.9f);
                case EffectType.Overheated: return new Color(1.00f, 0.45f, 0.15f, 0.9f);
                case EffectType.Corroded: return new Color(0.55f, 0.80f, 0.25f, 0.9f);
                case EffectType.Reeling: return new Color(0.75f, 0.45f, 0.95f, 0.9f);
                case EffectType.Spiked: return new Color(0.90f, 0.30f, 0.35f, 0.9f);
                case EffectType.Fortified: return new Color(0.30f, 0.60f, 1.00f, 0.9f);
                case EffectType.Armored: return new Color(0.85f, 0.85f, 0.90f, 0.9f);
                case EffectType.Overhauled: return new Color(0.35f, 0.90f, 0.75f, 0.9f);
                case EffectType.Exhausted: return new Color(0.60f, 0.45f, 0.30f, 0.9f);
                default: return new Color(1f, 1f, 1f, 0.9f);
            }
        }

        /// <summary>DisplayedTenths' result for an untimed (infinite) effect — no digit to show.</summary>
        public const int Untimed = int.MinValue;

        /// <summary>
        /// Remaining time rounded UP to whole tenths of a second (1.2s → 12), so a chip
        /// never reads 0.0s while the effect is still on. As an int rather than a
        /// formatted string so a caller (EffectChipRow) can compare it every step
        /// without allocating. <see cref="Untimed"/> for an infinite duration.
        /// </summary>
        public static int DisplayedTenths(float remaining)
        {
            if (float.IsInfinity(remaining)) return Untimed;

            // The small offset stops float noise (1.2 × 10 = 12.0000005) rounding up a whole tenth.
            float tenths = Mathf.Ceil(remaining * 10f - 1e-3f);
            if (!(tenths > 0f)) tenths = 0f;   // also turns −0 and NaN into 0
            return (int)tenths;
        }

        /// <summary>
        /// "STUN 1.2s", invariant culture so it never reads "1,2s". An untimed effect
        /// shows only its label.
        /// </summary>
        public static string Text(EffectType type, float remaining)
        {
            int tenths = DisplayedTenths(remaining);
            if (tenths == Untimed) return Label(type);

            return Label(type) + " " + (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>X offset of chip <paramref name="index"/> in a row of <paramref name="count"/> centred on 0.</summary>
        public static float RowX(int index, int count, float chipWidth, float gap)
        {
            return (index - (count - 1) * 0.5f) * (chipWidth + gap);
        }
    }
}
