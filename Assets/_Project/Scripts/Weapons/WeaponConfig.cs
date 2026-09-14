using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>One weapon. Every request it sends is tagged with this asset's name.</summary>
    [CreateAssetMenu(menuName = "Motor Combat/Weapon", fileName = "Weapon")]
    public class WeaponConfig : ScriptableObject
    {
        public string displayName;
        public WeaponDelivery delivery = WeaponDelivery.Shot;

        [Header("Muzzle")]
        [Tooltip("Turret: fires from the front along the crosshair. Fixed: fires straight out of each selected face.")]
        public MuzzleKind muzzle = MuzzleKind.Turret;

        [Tooltip("Used only when muzzle is Fixed. One or more.")]
        public FixedMuzzles fixedMuzzles = FixedMuzzles.Front;

        [Header("Timing (seconds, all start on the press)")]
        [Tooltip("Before this weapon can be pressed again.")]
        public float cooldownSeconds = 1f;

        [Tooltip("Delay from the press until the shot leaves.")]
        public float windUpSeconds;

        [Tooltip("No weapon, this one included, can be pressed until it ends. Must not exceed the cooldown.")]
        public float recoverySeconds = 0.2f;

        [Header("Shot")]
        public ShotSettings shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.3f };

        [Header("Payload")]
        public Payload hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0], push = new PushSpec { spinScale = 1f } };

        [Tooltip("Applied to the firing car when the shot leaves.")]
        public EffectSpec[] selfEffects = new EffectSpec[0];

        static readonly List<string> Errors = new List<string>();

        void OnValidate()
        {
            Errors.Clear();
            if (WeaponRules.Validate(this, float.PositiveInfinity, Errors)) return;

            for (int i = 0; i < Errors.Count; i++)
            {
                Debug.LogWarning($"[MotorCombat] Weapon '{name}': {Errors[i]}", this);
            }
        }
    }
}
