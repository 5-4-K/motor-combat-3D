using System;
using System.Collections.Generic;

namespace MotorCombat.Core
{
    /// <summary>
    /// Something that takes effects. Implemented in the Effects assembly;
    /// declared here so ramming, weapons and the HUD use it without referencing
    /// Effects — the same arrangement as <see cref="IDamageable"/>.
    /// </summary>
    public interface IEffectReceiver
    {
        EffectOutcome Apply(in EffectRequest request);

        bool Has(EffectType type);

        /// <summary>Seconds left, or 0 when the effect is not active.</summary>
        float Remaining(EffectType type);

        /// <summary>
        /// Ends the effect early, but only the copy <paramref name="source"/> applied —
        /// effects don't stack, so the active copy may belong to someone else, whose
        /// effect must run its full time. Matching is by reference; a null source
        /// matches only a copy applied with a null source. True if it ended.
        /// </summary>
        bool End(EffectType type, CarController source);

        /// <summary>Clears the list, then fills it with every active effect in EffectType order.</summary>
        void GetActive(List<ActiveEffect> into);

        /// <summary>Outcome Applied or Restarted.</summary>
        event Action<EffectReport> Applied;

        /// <summary>An effect ended: expiry, Overhauled, death or respawn.</summary>
        event Action<EffectType> Ended;
    }
}
