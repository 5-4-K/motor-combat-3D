using System;

namespace MotorCombat.Core
{
    /// <summary>
    /// Something that takes damage. Implemented in the Combat assembly; declared
    /// here so ramming, weapons, effects and the HUD use it without referencing
    /// Combat.
    /// </summary>
    public interface IDamageable
    {
        float Current { get; }
        float Max { get; }
        bool IsDestroyed { get; }

        DamageResult Apply(in DamageRequest request);

        /// <summary>A gate returning true blocks the request (e.g. Armored). Keyed by source; re-adding replaces.</summary>
        void AddGate(object source, Func<DamageRequest, bool> blocks);

        void RemoveGate(object source);

        event Action<DamageReport> Damaged;
        event Action<DamageReport> Destroyed;
    }
}
