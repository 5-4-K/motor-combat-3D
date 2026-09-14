using System.IO;
using UnityEditor;
using UnityEngine;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.Cars;
using MotorCombat.Ramming;
using MotorCombat.Combat;

namespace MotorCombat.EditorTools
{
    /// <summary>
    /// Creates the default tuning assets. Idempotent: existing assets are left
    /// alone, so re-running never clobbers hand-tuned values.
    /// </summary>
    public static class ConfigAssetBootstrap
    {
        const string ConfigDir = "Assets/_Project/Configs";

        [MenuItem("Motor Combat/Create Default Configs")]
        public static void CreateDefaults()
        {
            Directory.CreateDirectory(ConfigDir);
            AssetDatabase.Refresh();

            var drive = GetOrCreate<DriveConfig>("DriveConfig");
            var aim = GetOrCreate<AimConfig>("AimConfig");
            GetOrCreate<ArenaConfig>("ArenaConfig");
            GetOrCreate<CameraConfig>("CameraConfig");
            var ram = GetOrCreate<RamConfig>("RamConfig");
            var wreck = GetOrCreate<WreckConfig>("WreckConfig");

            var car = GetOrCreate<CarDefinition>("CarDefinition");
            if (car.driveConfig == null) car.driveConfig = drive;
            if (car.aimConfig == null) car.aimConfig = aim;
            if (car.ramConfig == null) car.ramConfig = ram;
            if (car.wreckConfig == null) car.wreckConfig = wreck;
            EditorUtility.SetDirty(car);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MotorCombat] Default configs ready in " + ConfigDir);
        }

        static T GetOrCreate<T>(string assetName) where T : ScriptableObject
        {
            string path = $"{ConfigDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
