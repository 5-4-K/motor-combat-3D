using UnityEngine;

namespace MotorCombat.Cameras
{
    public enum CameraMode
    {
        ThirdPerson = 0,
        FirstPerson = 1
    }

    [CreateAssetMenu(menuName = "Motor Combat/Camera Config", fileName = "CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [Tooltip("Switch this in the Inspector to compare the two views.")]
        public CameraMode mode = CameraMode.ThirdPerson;

        [Header("Third person")]
        public float thirdPersonDistance = 8f;
        public float thirdPersonHeight = 3.5f;
        public float thirdPersonPitch = 12f;
        public float thirdPersonFov = 60f;

        [Header("First person")]
        public float firstPersonFov = 70f;
    }
}
