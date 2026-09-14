namespace MotorCombat.Core
{
    /// <summary>
    /// One application of an effect. Every source — the ram now, weapons later —
    /// sends this through the target's <see cref="IEffectReceiver"/>. Size and
    /// duration come from the source's config; per-effect rules (stacking,
    /// Overheated's damage kind and interval) come from the effects config.
    /// </summary>
    public struct EffectRequest
    {
        /// <summary>The car that applied it; null for the environment or a debug menu.</summary>
        public CarController source;

        /// <summary>"ram", a weapon id, "debug" — for attribution and logs.</summary>
        public string sourceTag;

        public EffectType type;

        /// <summary>
        /// Always a positive size; the effect decides the sign. Percent for
        /// Corroded, Fortified, Spiked and Exhausted; damage per tick for
        /// Overheated (hit points or percent of max health, per the effects
        /// config). Ignored by the other effects.
        /// </summary>
        public float magnitude;

        /// <summary>Seconds. Ignored by Overhauled.</summary>
        public float duration;

        /// <summary>False (default): only enemies receive it. True: self and allies too — self-buffs and self-debuffs.</summary>
        public bool allowNonEnemy;

        /// <summary>Attack captured when the source fired. Null: the source's effective attack at apply time (100 for a null source).</summary>
        public float? attack;
    }
}
