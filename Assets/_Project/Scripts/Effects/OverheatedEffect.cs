using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Damage on a fixed rhythm: the first tick on apply, then every
    /// overheatedTickSeconds. Each tick uses the attack captured at apply, so
    /// weakening the attacker later does not weaken a burn already running;
    /// defense is read live. A stacking re-apply keeps the rhythm, so it can
    /// never tick early.
    /// </summary>
    public sealed class OverheatedEffect : EffectBehaviour
    {
        public const string DamageTag = "overheated";

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            TryTick(host, entry);
        }

        public override void OnStep(EffectHost host, in EffectSet.Entry entry, float dt)
        {
            TryTick(host, entry);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Ticks.Forget(this, host.Car);
        }

        void TryTick(EffectHost host, in EffectSet.Entry entry)
        {
            if (host.Health == null || host.Config == null) return;
            if (!host.Ticks.TryTick(this, host.Car, host.Now, host.Config.overheatedTickSeconds)) return;

            host.Health.Apply(new DamageRequest
            {
                source = entry.source,
                sourceTag = DamageTag,
                kind = host.Config.overheatedDamageKind,
                amount = entry.magnitude,
                allowNonEnemy = entry.allowNonEnemy,
                attack = entry.attack
            });
        }
    }
}
