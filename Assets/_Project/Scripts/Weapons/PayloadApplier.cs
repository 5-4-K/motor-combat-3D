using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>Where and how a hitbox touched a car.</summary>
    public struct PayloadHit
    {
        public CarController source;
        public string sourceTag;

        /// <summary>Attack snapshotted when the weapon was pressed.</summary>
        public float attack;

        public CarController target;
        public Vector3 point;
        public Vector3 travelDirection;
        public Vector3 hitboxCentre;
    }

    /// <summary>
    /// Lands a payload on a live enemy. Order: effects → (stop if they killed it)
    /// → push + Reeling → damage. Effects go first so a Stunned in the list stops
    /// the car before the push, instead of cancelling it.
    /// </summary>
    public static class PayloadApplier
    {
        public static void Apply(in Payload payload, in PayloadHit hit)
        {
            CarController target = hit.target;
            if (target == null) return;
            if (!target.Abilities.Has(CarAbility.Targetable)) return;
            if (!Hostility.AreEnemies(hit.source, target)) return;

            var receiver = target.GetComponent<IEffectReceiver>();
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsDestroyed) return;

            if (receiver != null && payload.effects != null)
            {
                for (int i = 0; i < payload.effects.Length; i++)
                {
                    EffectSpec spec = payload.effects[i];
                    receiver.Apply(new EffectRequest
                    {
                        source = hit.source,
                        sourceTag = hit.sourceTag,
                        type = spec.type,
                        magnitude = spec.magnitude,
                        duration = spec.duration,
                        attack = hit.attack
                    });
                }
            }

            if (damageable != null && damageable.IsDestroyed) return;

            Push(payload.push, hit, receiver);

            if (damageable != null && payload.damageAmount > 0f)
            {
                damageable.Apply(new DamageRequest
                {
                    source = hit.source,
                    sourceTag = hit.sourceTag,
                    kind = payload.damageKind,
                    amount = payload.damageAmount,
                    attack = hit.attack
                });
            }
        }

        static void Push(in PushSpec push, in PayloadHit hit, IEffectReceiver receiver)
        {
            if (!(push.speed > 0f)) return;

            CarController target = hit.target;
            Vector3 centre = target.transform.position;
            Vector3 delta = PayloadRules.PushDelta(push, hit.travelDirection, hit.hitboxCentre, centre);
            if (delta == Vector3.zero) return;

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity += delta;

                BoxCollider box = target.GetComponent<BoxCollider>();
                if (box != null)
                {
                    Vector3 angular = body.angularVelocity;
                    angular.y += PushMath.SpinDelta(hit.point, centre, delta, box.size.x, box.size.z, push.spinScale);
                    body.angularVelocity = angular;
                }
            }

            if (receiver != null)
            {
                receiver.Apply(new EffectRequest
                {
                    source = hit.source,
                    sourceTag = hit.sourceTag,
                    type = EffectType.Reeling,
                    duration = push.reelSeconds,
                    attack = hit.attack
                });
            }
        }
    }
}
