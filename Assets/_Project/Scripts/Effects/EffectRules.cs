using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>Every effect decision that needs no scene.</summary>
    public static class EffectRules
    {
        /// <summary>A timer at or below this many seconds has expired. Physics time accumulates in float steps.</summary>
        public const float ExpiryTolerance = 1e-4f;

        /// <summary>Attack when there is neither a snapshot nor a source. Equals DamageRules.NeutralAttack, which lives in Combat.</summary>
        public const float NeutralAttack = 100f;

        const CarAbility StunnedMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram;
        const CarAbility SuppressedMask = CarAbility.Fire;
        const CarAbility ReelingMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram;

        /// <summary>
        /// targetable → hostility → valid → Overhauled → already active (stacks or not) → new.
        /// Overhauled is always Applied once it passes the first three checks: it is never itself active.
        /// </summary>
        public static EffectOutcome Decide(in EffectRequest request, bool targetable, bool hostile, bool alreadyActive, bool stacks)
        {
            if (!targetable) return EffectOutcome.NotTargetable;
            if (!request.allowNonEnemy && !hostile) return EffectOutcome.NotHostile;
            if (!IsValid(request)) return EffectOutcome.Invalid;
            if (request.type == EffectType.Overhauled) return EffectOutcome.Applied;
            if (alreadyActive) return stacks ? EffectOutcome.Restarted : EffectOutcome.AlreadyActive;
            return EffectOutcome.Applied;
        }

        /// <summary>
        /// Known type; a timed effect needs a positive duration (+∞ allowed);
        /// a sized effect needs a finite, non-negative magnitude. Written as
        /// !(x > 0) so NaN from a broken config is rejected too.
        /// </summary>
        public static bool IsValid(in EffectRequest request)
        {
            if (!EffectInfo.IsKnown(request.type)) return false;
            if (EffectInfo.IsTimed(request.type) && !(request.duration > 0f)) return false;
            if (EffectInfo.UsesMagnitude(request.type) && !(request.magnitude >= 0f && !float.IsInfinity(request.magnitude))) return false;
            return true;
        }

        /// <summary>The attack an effect's damage uses for its whole life, fixed at apply.</summary>
        public static float AttackSnapshot(float? requested, bool hasSource, float sourceAttack)
        {
            return requested ?? (hasSource ? sourceAttack : NeutralAttack);
        }

        public static CarAbility BlockMask(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return StunnedMask;
                case EffectType.Suppressed: return SuppressedMask;
                case EffectType.Reeling: return ReelingMask;
                default: return CarAbility.None;
            }
        }

        /// <summary>The stat and signed percentage a stat effect of this magnitude applies. False for non-stat effects.</summary>
        public static bool StatChange(EffectType type, float magnitude, out CarStat stat, out float percent)
        {
            switch (type)
            {
                case EffectType.Corroded: stat = CarStat.Defense; percent = -magnitude; return true;
                case EffectType.Fortified: stat = CarStat.Defense; percent = magnitude; return true;
                case EffectType.Spiked: stat = CarStat.TopSpeed; percent = -magnitude; return true;
                case EffectType.Exhausted: stat = CarStat.Attack; percent = -magnitude; return true;
                default: stat = default; percent = 0f; return false;
            }
        }

        /// <summary>Exponential spin decay. <paramref name="rate"/> is in 1/s.</summary>
        public static float DecaySpin(float yawRate, float rate, float dt)
        {
            return yawRate * Mathf.Exp(-rate * dt);
        }
    }
}
