using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>What effect behaviours act on. One per car, built by CarEffects.</summary>
    public sealed class EffectHost
    {
        public EffectHost(CarController car, Rigidbody body, IDamageable health, TickSchedule ticks)
        {
            Car = car;
            Body = body;
            Health = health;
            Ticks = ticks;
        }

        public CarController Car { get; }
        public Rigidbody Body { get; }

        /// <summary>May be null on a car without health; effects that deal or gate damage then do nothing.</summary>
        public IDamageable Health { get; }

        /// <summary>This car's own schedule, used by Overheated.</summary>
        public TickSchedule Ticks { get; }

        public EffectsConfig Config { get; set; }

        /// <summary>Physics time of the current apply or step.</summary>
        public float Now { get; set; }
    }
}
