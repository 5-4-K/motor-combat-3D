using UnityEngine;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// Global ram tuning. Per-car stats (strength, resistance) live on CarDefinition.
    /// Every value here is a placeholder — tuning belongs to the designer.
    /// </summary>
    [CreateAssetMenu(menuName = "Motor Combat/Ram Config", fileName = "RamConfig")]
    public class RamConfig : ScriptableObject
    {
        [Header("Shove scale per ram type")]
        [Tooltip("Head-on shove multiplier. Keep small: head-ons stop both cars and should not reward either.")]
        public float headOnScale = 0.2f;

        [Tooltip("Flank (side) shove multiplier.")]
        public float flankScale = 1.5f;

        [Tooltip("Rear shove multiplier.")]
        public float rearScale = 1.2f;

        [Header("States (seconds)")]
        [Tooltip("How long the attacker (and both cars in a head-on) ignore throttle and steer.")]
        public float attackerLockSeconds = 0.5f;

        [Tooltip("How long a flank or rear victim reels: no throttle, steer or grip; spins freely.")]
        public float reelSeconds = 1f;

        [Header("Thresholds")]
        [Tooltip("Minimum attacker forward speed in m/s. Slower front contacts are plain physics bumps.")]
        public float minRamSpeed = 3f;

        [Tooltip("Headings within this many degrees of opposite (front hit) are head-on, and of parallel (rear hit) are rear. Otherwise flank.")]
        [Range(0f, 90f)]
        public float headOnAngleDegrees = 45f;

        [Tooltip("Width in metres of the corner band where a front or rear face meets a side.")]
        public float cornerBandMetres = 0.3f;

        [Header("Spin")]
        [Tooltip("Multiplies the yaw a real impulse at the contact point would give. 1 = physical.")]
        public float spinScale = 1f;

        [Tooltip("Rate in 1/s at which a reeling car's spin decays, as exp(-rate × dt).")]
        public float spinDecayRate = 2f;
    }
}
