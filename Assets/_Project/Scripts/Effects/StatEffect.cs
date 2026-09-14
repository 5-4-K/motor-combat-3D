using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>Corroded, Fortified, Spiked and Exhausted: one percentage modifier for as long as the effect lasts.</summary>
    public sealed class StatEffect : EffectBehaviour
    {
        readonly EffectType _type;

        public StatEffect(EffectType type)
        {
            _type = type;
        }

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            AddModifier(host, entry.magnitude);
        }

        /// <summary>The same source replaces its modifier, so a stacking copy's size takes over.</summary>
        public override void OnRefresh(EffectHost host, in EffectSet.Entry entry)
        {
            AddModifier(host, entry.magnitude);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Car.Stats.Remove(this);
        }

        void AddModifier(EffectHost host, float magnitude)
        {
            if (EffectRules.StatChange(_type, magnitude, out CarStat stat, out float percent))
            {
                host.Car.Stats.Add(this, stat, percent);
            }
        }
    }
}
