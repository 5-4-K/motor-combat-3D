using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// One resolved ram. Raised for future consumers (damage, HUD, audio) and
    /// logged while tuning. For a head-on, attacker is the car whose callback
    /// resolved the pair and victim is the other; both were locked.
    /// </summary>
    public struct RamReport
    {
        public RamType type;
        public CarController attacker;
        public CarController victim;

        /// <summary>Speed change given to the victim, in m/s.</summary>
        public float shoveSpeed;

        /// <summary>Yaw-rate change given to the victim, in rad/s. Zero for head-ons.</summary>
        public float spin;
    }
}
