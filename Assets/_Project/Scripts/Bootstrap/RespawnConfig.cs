using UnityEngine;

namespace MotorCombat.Bootstrap
{
    /// <summary>
    /// Tuning for the placeholder respawn rule. A future game mode brings its own
    /// rule and config. Every value here is a placeholder — tuning belongs to the designer.
    /// </summary>
    [CreateAssetMenu(menuName = "Motor Combat/Respawn Config", fileName = "RespawnConfig")]
    public class RespawnConfig : ScriptableObject
    {
        [Tooltip("Seconds from destruction before the car may respawn. The wreck's roll and fade always finish first.")]
        [Min(0f)]
        public float respawnDelaySeconds = 3f;

        [Tooltip("Metres added around the car's box on every side when checking that the spawn point is clear.")]
        [Min(0f)]
        public float clearanceMargin = 0.1f;
    }
}
