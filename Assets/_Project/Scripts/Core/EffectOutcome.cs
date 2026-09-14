namespace MotorCombat.Core
{
    public enum EffectOutcome
    {
        /// <summary>The effect started (or, for Overhauled, every effect ended).</summary>
        Applied,

        /// <summary>Already active and allowed to stack: its timer restarted and the new size replaced the old.</summary>
        Restarted,

        /// <summary>Already active and not allowed to stack: nothing changed.</summary>
        AlreadyActive,

        /// <summary>The target is a wreck or otherwise not targetable.</summary>
        NotTargetable,

        /// <summary>The source is not the target's enemy and allowNonEnemy was not set.</summary>
        NotHostile,

        /// <summary>Unknown type, a bad duration or magnitude, or no effects config.</summary>
        Invalid
    }
}
