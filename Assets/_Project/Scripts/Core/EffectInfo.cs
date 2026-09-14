namespace MotorCombat.Core
{
    /// <summary>Fixed facts about each effect type, shared by the effect system and the HUD.</summary>
    public static class EffectInfo
    {
        /// <summary>Number of effect types; values run 0 to Count − 1.</summary>
        public const int Count = 10;

        public static bool IsKnown(EffectType type)
        {
            return (int)type >= 0 && (int)type < Count;
        }

        public static bool IsBuff(EffectType type)
        {
            return type == EffectType.Fortified || type == EffectType.Armored || type == EffectType.Overhauled;
        }

        /// <summary>Everything but Overhauled, which is instant.</summary>
        public static bool IsTimed(EffectType type)
        {
            return IsKnown(type) && type != EffectType.Overhauled;
        }

        /// <summary>Effects whose request magnitude means something.</summary>
        public static bool UsesMagnitude(EffectType type)
        {
            return type == EffectType.Overheated || type == EffectType.Corroded || type == EffectType.Spiked
                   || type == EffectType.Fortified || type == EffectType.Exhausted;
        }
    }
}
