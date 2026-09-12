using UnityEngine;

namespace MotorCombat.Aiming
{
    [CreateAssetMenu(menuName = "Motor Combat/Aim Config", fileName = "AimConfig")]
    public class AimConfig : ScriptableObject
    {
        [Tooltip("Total width of the aiming cone. Aim is clamped to half of this either side of the car's forward.")]
        [Range(10f, 180f)]
        public float coneAngleDegrees = 90f;

        [Tooltip("Degrees of aim per pixel of mouse movement.")]
        public float mouseSensitivity = 0.12f;
    }
}
