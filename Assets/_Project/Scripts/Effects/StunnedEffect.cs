using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// A complete stop, once, when the stun lands (and again if a stacking stun
    /// lands). Afterwards grip and drag stay on, so a ram or weapon impulse can
    /// still shove a stunned car.
    /// </summary>
    public sealed class StunnedEffect : BlockEffect
    {
        public StunnedEffect() : base(EffectType.Stunned) { }

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            base.OnStart(host, entry);
            Stop(host);
        }

        public override void OnRefresh(EffectHost host, in EffectSet.Entry entry)
        {
            Stop(host);
        }

        static void Stop(EffectHost host)
        {
            Rigidbody body = host.Body;
            if (body == null) return;

            // Vertical velocity is kept: gravity and the floor own it.
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
            body.angularVelocity = Vector3.zero;
        }
    }
}
