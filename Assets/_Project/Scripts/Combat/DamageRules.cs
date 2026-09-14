using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// The damage formula. MOBA pattern: attack scales the hit as a percentage,
    /// defense removes a share with diminishing returns and never reaches zero.
    /// </summary>
    public static class DamageRules
    {
        /// <summary>Attack used when there is no source car (the environment).</summary>
        public const float NeutralAttack = 100f;

        public static float RawAmount(DamageKind kind, float amount, float maxHealth)
        {
            return kind == DamageKind.MaxHealthPercent ? amount / 100f * maxHealth : amount;
        }

        /// <summary>raw × attack/100 × 100/(100 + defense). Negative defense counts as 0.</summary>
        public static float Mitigate(float raw, float attack, float defense)
        {
            return raw * (attack / 100f) * (100f / (100f + Mathf.Max(0f, defense)));
        }
    }
}
