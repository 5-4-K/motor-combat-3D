using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>Rules shared by every weapon on every car.</summary>
    [CreateAssetMenu(menuName = "Motor Combat/Weapons Config", fileName = "WeaponsConfig")]
    public class WeaponsConfig : ScriptableObject
    {
        [Tooltip("Height above the car's floor, in metres, at which every muzzle on every car fires. Every car's hurtbox must span it.")]
        public float fireHeight = 0.6f;
    }
}
