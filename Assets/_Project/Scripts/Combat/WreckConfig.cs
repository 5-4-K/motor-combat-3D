using UnityEngine;

namespace MotorCombat.Combat
{
    /// <summary>How a destroyed car leaves. Placeholders — tuning belongs to the designer.</summary>
    [CreateAssetMenu(menuName = "Motor Combat/Wreck Config", fileName = "WreckConfig")]
    public class WreckConfig : ScriptableObject
    {
        [Tooltip("Barrel roll about the car's length axis, in degrees. Visual only; the physics box stays flat.")]
        public float rollDegrees = 180f;

        [Tooltip("Seconds the roll takes, eased.")]
        [Min(0f)]
        public float rollSeconds = 0.8f;

        [Tooltip("Seconds to fade from opaque to invisible.")]
        [Min(0f)]
        public float fadeSeconds = 1.5f;

        [Tooltip("Seconds after destruction when the car is deactivated (not deleted, so game modes can respawn it).")]
        [Min(0f)]
        public float removeAfterSeconds = 1.5f;
    }
}
