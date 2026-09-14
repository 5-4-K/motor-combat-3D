using System;
using System.Collections.Generic;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>Configuration checks. Pure: no scene, no logging.</summary>
    public static class WeaponRules
    {
        const FixedMuzzles AllFixed = FixedMuzzles.Front | FixedMuzzles.Rear | FixedMuzzles.Left | FixedMuzzles.Right;

        /// <summary>
        /// Adds one message per broken rule. <paramref name="fireHeight"/> =
        /// +∞ skips the floor-clearance rule (the Inspector has no car).
        /// </summary>
        public static bool Validate(WeaponConfig weapon, float fireHeight, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            int before = errors.Count;
            if (weapon == null)
            {
                errors.Add("no weapon config");
                return false;
            }

            NonNegative(weapon.cooldownSeconds, "cooldownSeconds", errors);
            NonNegative(weapon.windUpSeconds, "windUpSeconds", errors);
            NonNegative(weapon.recoverySeconds, "recoverySeconds", errors);

            if (IsFinite(weapon.cooldownSeconds) && IsFinite(weapon.recoverySeconds) && weapon.cooldownSeconds < weapon.recoverySeconds)
            {
                errors.Add($"cooldownSeconds ({weapon.cooldownSeconds}) is shorter than recoverySeconds ({weapon.recoverySeconds}); the cooldown must be at least the recovery");
            }

            if (!Enum.IsDefined(typeof(WeaponDelivery), weapon.delivery))
            {
                errors.Add($"delivery ({(int)weapon.delivery}) is not a known WeaponDelivery");
            }
            else if (weapon.delivery == WeaponDelivery.Shot)
            {
                ValidateShotDelivery(weapon, fireHeight, errors);
            }

            ValidatePayload(weapon.hitPayload, "hitPayload", errors);
            ValidateEffects(weapon.selfEffects, "selfEffects", errors);

            return errors.Count == before;
        }

        /// <summary>Muzzle placement and the shot's own numbers — both belong to the Shot delivery.</summary>
        static void ValidateShotDelivery(WeaponConfig weapon, float fireHeight, List<string> errors)
        {
            if (weapon.muzzle == MuzzleKind.Fixed && (weapon.fixedMuzzles & AllFixed) == 0)
            {
                errors.Add("muzzle is Fixed but no fixed muzzle is selected");
            }

            Positive(weapon.shot.speed, "shot.speed", errors);
            Positive(weapon.shot.range, "shot.range", errors);
            Positive(weapon.shot.radius, "shot.radius", errors);

            if (IsFinite(weapon.shot.radius) && weapon.shot.radius >= fireHeight)
            {
                errors.Add($"shot.radius ({weapon.shot.radius}) must be below the fire height ({fireHeight}), or the shot hits the floor");
            }
        }

        /// <summary>Checks one payload. Public so later hitboxes (explosions, fields) validate theirs the same way.</summary>
        public static void ValidatePayload(in Payload payload, string label, List<string> errors)
        {
            NonNegative(payload.damageAmount, label + ".damageAmount", errors);
            ValidateEffects(payload.effects, label + ".effects", errors);

            NonNegative(payload.push.speed, label + ".push.speed", errors);
            if (!IsFinite(payload.push.spinScale)) errors.Add($"{label}.push.spinScale must be a finite number");

            if (payload.push.speed > 0f && !(payload.push.reelSeconds > 0f && IsFinite(payload.push.reelSeconds)))
            {
                errors.Add($"{label}.push has a speed but reelSeconds ({payload.push.reelSeconds}) is not above 0; a push always reels");
            }
        }

        static void ValidateEffects(EffectSpec[] effects, string label, List<string> errors)
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Length; i++)
            {
                EffectSpec spec = effects[i];
                string at = $"{label}[{i}] ({spec.type})";

                if (!EffectInfo.IsKnown(spec.type)) errors.Add($"{at} is not a known effect");
                NonNegative(spec.magnitude, at + ".magnitude", errors);

                if (EffectInfo.IsTimed(spec.type) && !(spec.duration > 0f))
                {
                    errors.Add($"{at}.duration ({spec.duration}) must be above 0");
                }
            }
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void NonNegative(float value, string field, List<string> errors)
        {
            if (!IsFinite(value) || value < 0f) errors.Add($"{field} must be a finite number ≥ 0 (is {value})");
        }

        static void Positive(float value, string field, List<string> errors)
        {
            if (!IsFinite(value) || value <= 0f) errors.Add($"{field} must be a finite number above 0 (is {value})");
        }
    }
}
