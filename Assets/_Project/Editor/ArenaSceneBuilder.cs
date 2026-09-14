using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.Cars;
using MotorCombat.Bootstrap;

namespace MotorCombat.EditorTools
{
    /// <summary>
    /// Generates Arena.unity from code, so the scene is reproducible and its
    /// construction is reviewable in a diff rather than as opaque YAML.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/Arena.unity";
        const string ConfigDir = "Assets/_Project/Configs";

        [MenuItem("Motor Combat/Rebuild Arena Scene")]
        public static void BuildScene()
        {
            var cameraConfig = Load<CameraConfig>("CameraConfig");
            var arenaConfig = Load<ArenaConfig>("ArenaConfig");
            var carDefinition = Load<CarDefinition>("CarDefinition");
            var respawnConfig = Load<RespawnConfig>("RespawnConfig");

            if (cameraConfig == null || arenaConfig == null || carDefinition == null || respawnConfig == null)
            {
                Debug.LogError(
                    "[MotorCombat] Aborting scene build: one or more config assets are missing. " +
                    "Run Motor Combat > Create Default Configs first. " +
                    "The open scene and the build settings are unchanged.");
                return;
            }

            Directory.CreateDirectory(SceneDir);

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[MotorCombat] Scene rebuild cancelled.");
                return;
            }

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Light ---
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, 138f, 0f);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();

            var rig = cameraObject.AddComponent<CameraRig>();
            rig.config = cameraConfig;

            // --- Bootstrap ---
            var bootstrapObject = new GameObject("GameBootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.arenaConfig = arenaConfig;
            bootstrap.carDefinition = carDefinition;
            bootstrap.respawnConfig = respawnConfig;
            bootstrap.cameraRig = rig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log("[MotorCombat] Arena scene written to " + ScenePath);
        }

        static T Load<T>(string assetName) where T : ScriptableObject
        {
            string path = $"{ConfigDir}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"[MotorCombat] Missing config asset at {path}. " +
                               "Run Motor Combat > Create Default Configs first.");
            }
            return asset;
        }

        static void RegisterInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
