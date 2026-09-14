namespace MotorCombat.Core
{
    /// <summary>Returned immediately, so a caller (a projectile, a zone) knows at once what its hit did.</summary>
    public struct DamageResult
    {
        public DamageOutcome outcome;

        /// <summary>Hit points actually removed — never more than the target had left.</summary>
        public float dealt;

        /// <summary>True only on the hit that took health to zero.</summary>
        public bool killed;

        public static DamageResult Of(DamageOutcome outcome)
        {
            return new DamageResult { outcome = outcome };
        }
    }
}
