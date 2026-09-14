using System;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>How a weapon delivers its payload. Later sub-projects append values.</summary>
    public enum WeaponDelivery { Shot }

    /// <summary>A weapon fires from the turret or from fixed muzzles — never both.</summary>
    public enum MuzzleKind { Turret, Fixed }

    [Flags]
    public enum FixedMuzzles
    {
        None = 0,
        Front = 1,
        Rear = 2,
        Left = 4,
        Right = 8
    }

    public enum PushDirection
    {
        /// <summary>The way the hitbox was moving.</summary>
        AlongTravel,

        /// <summary>From the hitbox's centre out to the hit car's centre.</summary>
        AwayFromCentre
    }

    [Serializable]
    public struct ShotSettings
    {
        public float speed;    // m/s
        public float range;    // m
        public float radius;   // m
    }

    [Serializable]
    public struct EffectSpec
    {
        public EffectType type;
        public float magnitude;
        public float duration;
    }

    /// <summary>A push always reels: speed > 0 needs reelSeconds > 0.</summary>
    [Serializable]
    public struct PushSpec
    {
        /// <summary>Velocity change in m/s. Mass, strength and resistance are ignored. 0 = no push.</summary>
        public float speed;
        public PushDirection direction;

        /// <summary>0 never spins; 1 is the physically correct spin for an off-centre hit.</summary>
        public float spinScale;

        public float reelSeconds;
    }

    /// <summary>What a hitbox does to a car it hits. Reusable: shots now; explosions, fields and auras later.</summary>
    [Serializable]
    public struct Payload
    {
        public DamageKind damageKind;

        /// <summary>0 = no damage.</summary>
        public float damageAmount;

        public EffectSpec[] effects;
        public PushSpec push;
    }
}
