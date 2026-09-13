using UnityEngine;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Ramming;

namespace MotorCombat.Cars
{
    [CreateAssetMenu(menuName = "Motor Combat/Car Definition", fileName = "CarDefinition")]
    public class CarDefinition : ScriptableObject
    {
        [Header("Dimensions (metres)")]
        [Tooltip("Physical length in metres. Arena size is independent of this -- see ArenaConfig.radiusMetres.")]
        public float length = 4.5f;
        public float width = 2f;
        public float height = 1.2f;

        public float mass = 1200f;

        [Header("Driver eye position, relative to the car's centre")]
        public Vector3 driverAnchorOffset = new Vector3(0f, 1f, 0.4f);

        [Header("Visual")]
        [Tooltip("Model to use as the car's visual. Leave empty for the placeholder box. " +
                 "The collider is ALWAYS a box sized from the dimensions above -- the model is " +
                 "decoration sitting inside it, and any colliders it ships with are stripped.")]
        public GameObject visualPrefab;

        [Tooltip("Position correction for the model, relative to the car's centre. Models are " +
                 "commonly authored with their origin at the wheels' contact patch rather than " +
                 "at the centre of the body; this lifts or drops the model to match.")]
        public Vector3 visualOffset;

        [Tooltip("Yaw correction in degrees. Unity's forward is +Z; set 180 if the model was " +
                 "authored nose-first along -Z, or the car will appear to drive backwards.")]
        public float visualYawOffset;

        [Tooltip("Which of the model's materials take the team colour. Leave empty to tint every " +
                 "renderer. List the body and paintwork materials to keep glass and interior out " +
                 "of it. Tinting is applied per-instance, so the shared material assets are never " +
                 "modified and the two cars can differ.")]
        public Material[] tintedMaterials;

        [Header("Ramming")]
        [Tooltip("Multiplies the shove this car deals when it rams. Balanced against the victim's defense as a ratio.")]
        public float attack = 1f;

        [Tooltip("Divides the shove this car receives when rammed. Must be above zero.")]
        [Min(0.01f)]
        public float defense = 1f;

        [Header("Behaviour")]
        public DriveConfig driveConfig;
        public AimConfig aimConfig;
        public RamConfig ramConfig;
    }
}
