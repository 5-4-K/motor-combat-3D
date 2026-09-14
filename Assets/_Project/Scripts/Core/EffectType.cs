namespace MotorCombat.Core
{
    /// <summary>
    /// Every effect in the game. Values are explicit: configs, HUD order and
    /// arrays are indexed by them.
    /// </summary>
    public enum EffectType
    {
        /// <summary>Complete stop once; no throttle, steer, weapons or ramming.</summary>
        Stunned = 0,

        /// <summary>Weapons disabled.</summary>
        Suppressed = 1,

        /// <summary>Takes damage every tick interval.</summary>
        Overheated = 2,

        /// <summary>Defense reduced by a percentage.</summary>
        Corroded = 3,

        /// <summary>No throttle, steer or grip; spins freely, as after a ram.</summary>
        Reeling = 4,

        /// <summary>Top speed reduced by a percentage.</summary>
        Spiked = 5,

        /// <summary>Defense boosted by a percentage.</summary>
        Fortified = 6,

        /// <summary>Immune to damage, not to effects.</summary>
        Armored = 7,

        /// <summary>Instantly removes every effect. Never itself active.</summary>
        Overhauled = 8,

        /// <summary>Attack reduced by a percentage.</summary>
        Exhausted = 9
    }
}
