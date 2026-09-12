using UnityEngine;
using MotorCombat.Driving;
using MotorCombat.Aiming;

namespace MotorCombat.Cars
{
    [CreateAssetMenu(menuName = "Motor Combat/Car Definition", fileName = "CarDefinition")]
    public class CarDefinition : ScriptableObject
    {
        [Header("Dimensions (metres)")]
        [Tooltip("Length also sets the arena size, via ArenaConfig.radiusInCarLengths.")]
        public float length = 4.5f;
        public float width = 2f;
        public float height = 1.2f;

        public float mass = 1200f;

        [Header("Driver eye position, relative to the car's centre")]
        public Vector3 driverAnchorOffset = new Vector3(0f, 1f, 0.4f);

        [Header("Behaviour")]
        public DriveConfig driveConfig;
        public AimConfig aimConfig;
    }
}
