using UnityEngine;
using MotorCombat.Controls;
using MotorCombat.Cars;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.HUD;

namespace MotorCombat.Bootstrap
{
    /// <summary>
    /// The only thing authored into the Arena scene besides a light and a camera.
    /// Builds the arena from config, spawns the cars, and wires the camera.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Configs")]
        public ArenaConfig arenaConfig;
        public CarDefinition carDefinition;

        [Header("Scene references")]
        public CameraRig cameraRig;

        [Header("Dummy car")]
        [Tooltip("How far ahead of the player the stationary dummy spawns, in car lengths.")]
        public float dummyDistanceInCarLengths = 4f;

        void Start()
        {
            if (!Validate()) return;

            ArenaBuilder.Build(arenaConfig, carDefinition.length);

            float halfHeight = carDefinition.height * 0.5f;

            var playerInput = gameObject.AddComponent<LocalInputProvider>();
            var player = CarFactory.Spawn(
                carDefinition,
                new Vector3(0f, halfHeight, 0f),
                Quaternion.identity,
                playerInput,
                new Color(0.20f, 0.55f, 0.90f));
            player.name = "PlayerCar";

            var dummyInput = gameObject.AddComponent<NullInputProvider>();
            var dummy = CarFactory.Spawn(
                carDefinition,
                new Vector3(0f, halfHeight, dummyDistanceInCarLengths * carDefinition.length),
                Quaternion.identity,
                dummyInput,
                new Color(0.85f, 0.35f, 0.25f));
            dummy.name = "DummyCar";

            cameraRig.Follow(player);

            var crosshair = cameraRig.gameObject.GetComponent<CrosshairHUD>();
            if (crosshair == null)
            {
                crosshair = cameraRig.gameObject.AddComponent<CrosshairHUD>();
            }
            crosshair.target = player;
            crosshair.view = cameraRig.GetComponent<Camera>();
        }

        bool Validate()
        {
            if (arenaConfig == null) { Debug.LogError("[MotorCombat] GameBootstrap.arenaConfig is not assigned."); return false; }
            if (carDefinition == null) { Debug.LogError("[MotorCombat] GameBootstrap.carDefinition is not assigned."); return false; }
            if (carDefinition.driveConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.driveConfig is not assigned."); return false; }
            if (carDefinition.aimConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.aimConfig is not assigned."); return false; }
            if (cameraRig == null) { Debug.LogError("[MotorCombat] GameBootstrap.cameraRig is not assigned."); return false; }
            return true;
        }
    }
}
