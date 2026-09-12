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

        [Tooltip("HORIZONTAL field of view in degrees, identical for every monitor shape. " +
                 "Wider screens see less top and bottom instead of more to the sides -- " +
                 "a competitive fairness rule. 92 matches a 60 vertical FOV at 16:9.")]
        [Range(30f, 170f)]
        public float thirdPersonHorizontalFov = 92f;

        [Header("First person")]
        [Tooltip("HORIZONTAL field of view in degrees, identical for every monitor shape. " +
                 "102 matches a 70 vertical FOV at 16:9.")]
        [Range(30f, 170f)]
        public float firstPersonHorizontalFov = 102f;
    }
}
