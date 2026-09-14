using System;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>Immune to all damage, self-inflicted included. Effects still land.</summary>
    public sealed class ArmoredEffect : EffectBehaviour
    {
        static readonly Func<DamageRequest, bool> BlockEverything = request => true;

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            host.Health?.AddGate(this, BlockEverything);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Health?.RemoveGate(this);
        }
    }
}
