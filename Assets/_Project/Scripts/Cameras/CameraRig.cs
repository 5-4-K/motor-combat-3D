using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Cameras
{
    /// <summary>
    /// First or third person, chosen by config. Camera YAW is rigid to the
    /// chassis in both modes and is never smoothed: lagging it would drag the
    /// crosshair across the screen during turns, which is exactly the thing the
    /// aiming design guarantees will not happen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public CameraConfig config;

        CarController _target;
        Transform _driverAnchor;
        Camera _camera;

        void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void Follow(CarController target)
        {
            _target = target;
            _driverAnchor = target != null
                ? target.transform.Find(CarController.DriverAnchorName)
                : null;
        }

        void LateUpdate()
        {
            if (_target == null || config == null) return;

            if (config.mode == CameraMode.FirstPerson)
            {
                ApplyFirstPerson();
            }
            else
            {
                ApplyThirdPerson();
            }
        }

        void ApplyFirstPerson()
        {
            Transform anchor = _driverAnchor != null ? _driverAnchor : _target.transform;

            _camera.fieldOfView = config.firstPersonFov;
            transform.position = anchor.position;
            transform.rotation = Quaternion.Euler(0f, ChassisYaw(), 0f);
        }

        void ApplyThirdPerson()
        {
            float yaw = ChassisYaw();
            Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 offset = flatRotation * new Vector3(0f, 0f, -config.thirdPersonDistance)
                             + Vector3.up * config.thirdPersonHeight;

            _camera.fieldOfView = config.thirdPersonFov;
            transform.position = _target.transform.position + offset;
            transform.rotation = Quaternion.Euler(config.thirdPersonPitch, yaw, 0f);
        }

        float ChassisYaw()
        {
            return _target.transform.eulerAngles.y;
        }
    }
}
