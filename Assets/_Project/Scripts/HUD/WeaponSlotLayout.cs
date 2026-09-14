using UnityEngine;

namespace MotorCombat.HUD
{
    /// <summary>Where each weapon circle sits and what it shows. Placeholder art: key labels until icons exist.</summary>
    public static class WeaponSlotLayout
    {
        /// <summary>X of circle <paramref name="index"/>'s centre in a row of <paramref name="count"/>, centred on 0.</summary>
        public static float CentreX(int index, int count, float diameter, float gap)
        {
            float total = count * diameter + Mathf.Max(0, count - 1) * gap;
            return -total * 0.5f + diameter * 0.5f + index * (diameter + gap);
        }

        /// <summary>How much of the circle the dark overlay covers: remaining / duration, clamped to 0–1.</summary>
        public static float CooldownFill(float remaining, float duration)
        {
            if (!(duration > 0f) || !(remaining > 0f)) return 0f;
            return Mathf.Clamp01(remaining / duration);
        }

        public static string KeyLabel(int index)
        {
            switch (index)
            {
                case 0: return "LMB";
                case 1: return "RMB";
                case 2: return "SPC";
                default: return "";
            }
        }
    }
}
