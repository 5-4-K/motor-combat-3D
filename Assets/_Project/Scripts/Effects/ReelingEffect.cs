using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// No throttle, steer, grip or yaw hold — the car slides and spins freely,
    /// as after a ram. Driving does not write yaw while YawHold is blocked, so
    /// the spin winds down here.
    /// </summary>
    public sealed class ReelingEffect : BlockEffect
    {
        public ReelingEffect() : base(EffectType.Reeling) { }

        public override void OnStep(EffectHost host, in EffectSet.Entry entry, float dt)
        {
            Rigidbody body = host.Body;
            if (body == null || host.Config == null) return;

            body.angularVelocity = Vector3.up * EffectRules.DecaySpin(body.angularVelocity.y, host.Config.reelingSpinDecayRate, dt);
        }
    }
}
