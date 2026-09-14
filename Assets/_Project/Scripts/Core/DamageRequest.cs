namespace MotorCombat.Core
{
    /// <summary>
    /// One instance of damage. Every source — rams now, weapons and effects later —
    /// sends this same request, so the rules that apply to damage live in exactly
    /// one place. Tick damage is not a kind: a source sends ordinary requests on
    /// a <see cref="TickSchedule"/>.
    /// </summary>
    public struct DamageRequest
    {
        /// <summary>The car that dealt it; null for the environment.</summary>
        public CarController source;

        /// <summary>"ram", a weapon id, an effect id — for attribution and logs.</summary>
        public string sourceTag;

        public DamageKind kind;

        /// <summary>Hit points for Flat; percent of max health for MaxHealthPercent.</summary>
        public float amount;

        /// <summary>False (default): only enemies take it. True: self and allies too, e.g. a self-inflicted debuff.</summary>
        public bool allowNonEnemy;
    }
}
