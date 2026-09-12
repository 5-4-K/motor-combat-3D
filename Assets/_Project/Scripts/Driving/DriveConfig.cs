using UnityEngine;

namespace MotorCombat.Driving
{
    [CreateAssetMenu(menuName = "Motor Combat/Drive Config", fileName = "DriveConfig")]
    public class DriveConfig : ScriptableObject
    {
        [Header("Thrust")]
        [Tooltip("Newtons of forward force at full throttle.")]
        public float enginePower = 30000f;

        [Tooltip("Velocity decay RATE in 1/s. Terminal speed = enginePower / (mass * linearDrag).")]
        public float linearDrag = 1f;

        [Header("Braking and reverse")]
        public float brakeForce = 40000f;
        public float reversePower = 12000f;

        [Tooltip("Forward speed (m/s) below which S reverses instead of braking.")]
        public float reverseEpsilon = 0.5f;

        [Header("Turning")]
        [Tooltip("Degrees per second at full steer. Not gated on speed — this is what allows turning on the spot.")]
        public float turnRate = 90f;

        [Header("Grip")]
        [Tooltip("Sideways velocity decay RATE in 1/s. Lower drifts more. 0 is a hockey puck.")]
        public float lateralGripStrength = 6f;
    }
}
