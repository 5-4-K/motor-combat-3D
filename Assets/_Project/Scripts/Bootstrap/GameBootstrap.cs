using UnityEngine;
using MotorCombat.Controls;
using MotorCombat.Cars;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.HUD;
using MotorCombat.Core;

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
        public RespawnConfig respawnConfig;

        [Header("Scene references")]
        public CameraRig cameraRig;

        [Header("Dummy car")]
        [Tooltip("How far ahead of the player the stationary dummy spawns, in car lengths.")]
        public float dummyDistanceInCarLengths = 4f;

        void Start()
        {
            if (!Validate()) return;

            PhysicsLayers.ConfigureCollisions();

            ArenaBuilder.Build(arenaConfig, carDefinition.length);

            float halfHeight = carDefinition.height * 0.5f;

            var playerPosition = new Vector3(0f, halfHeight, 0f);
            var playerInput = gameObject.AddComponent<LocalInputProvider>();
            var player = CarFactory.Spawn(
                carDefinition,
                playerPosition,
                Quaternion.identity,
                playerInput,
                new Color(0.20f, 0.55f, 0.90f));
            player.name = "PlayerCar";

            var dummyPosition = new Vector3(0f, halfHeight, dummyDistanceInCarLengths * carDefinition.length);
            var dummyInput = gameObject.AddComponent<NullInputProvider>();
            var dummy = CarFactory.Spawn(
                carDefinition,
                dummyPosition,
                Quaternion.identity,
                dummyInput,
                new Color(0.85f, 0.35f, 0.25f));
            dummy.name = "DummyCar";

            var respawn = gameObject.AddComponent<RespawnRule>();
            respawn.config = respawnConfig;
            respawn.Track(player, playerPosition, Quaternion.identity);
            respawn.Track(dummy, dummyPosition, Quaternion.identity);

            cameraRig.Follow(player);

            var crosshair = cameraRig.gameObject.GetComponent<CrosshairHUD>();
            if (crosshair == null)
            {
                crosshair = cameraRig.gameObject.AddComponent<CrosshairHUD>();
            }
            crosshair.target = player;
            crosshair.view = cameraRig.GetComponent<Camera>();

            var hud = HudRoot.Create();

            var selfHealth = hud.gameObject.AddComponent<SelfHealthWidget>();
            selfHealth.viewer = player;
            selfHealth.Build(hud.Rect);

            var enemyBars = hud.gameObject.AddComponent<EnemyHealthBars>();
            enemyBars.viewer = player;
            enemyBars.view = cameraRig.GetComponent<Camera>();
            enemyBars.Build(hud.Rect);
        }

        bool Validate()
        {
            if (arenaConfig == null) { Debug.LogError("[MotorCombat] GameBootstrap.arenaConfig is not assigned."); return false; }
            if (carDefinition == null) { Debug.LogError("[MotorCombat] GameBootstrap.carDefinition is not assigned."); return false; }
            if (respawnConfig == null) { Debug.LogError("[MotorCombat] GameBootstrap.respawnConfig is not assigned."); return false; }
            if (carDefinition.driveConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.driveConfig is not assigned."); return false; }
            if (carDefinition.aimConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.aimConfig is not assigned."); return false; }
            if (carDefinition.ramConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.ramConfig is not assigned."); return false; }
            if (carDefinition.wreckConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.wreckConfig is not assigned."); return false; }
            if (cameraRig == null) { Debug.LogError("[MotorCombat] GameBootstrap.cameraRig is not assigned."); return false; }
            if (cameraRig.config == null) { Debug.LogError("[MotorCombat] CameraRig.config is not assigned."); return false; }
            return true;
        }
    }
}
