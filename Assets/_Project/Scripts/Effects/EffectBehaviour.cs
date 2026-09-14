namespace MotorCombat.Effects
{
    /// <summary>
    /// What one effect does. One instance per effect per car, and that instance
    /// is the source key for every block, modifier, gate and tick it registers,
    /// so its OnEnd removes exactly what its OnStart added. Timing lives in
    /// EffectSet, never here.
    /// </summary>
    public abstract class EffectBehaviour
    {
        /// <summary>The effect has just landed.</summary>
        public virtual void OnStart(EffectHost host, in EffectSet.Entry entry) { }

        /// <summary>A stacking copy landed while active; the entry already holds the new size and timer.</summary>
        public virtual void OnRefresh(EffectHost host, in EffectSet.Entry entry) { }

        /// <summary>Every physics step while active, after expiry has been handled.</summary>
        public virtual void OnStep(EffectHost host, in EffectSet.Entry entry, float dt) { }

        /// <summary>Expired, cleansed, or the car died or respawned.</summary>
        public virtual void OnEnd(EffectHost host) { }
    }
}
