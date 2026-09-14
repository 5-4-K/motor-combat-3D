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
using MotorCombat.Bootstrap;
using MotorCombat.Effects;
using MotorCombat.Core;
using MotorCombat.Weapons;

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
            GetOrCreate<RespawnConfig>("RespawnConfig");
            var effects = GetOrCreate<EffectsConfig>("EffectsConfig");

            Directory.CreateDirectory(ConfigDir + "/Weapons");
            AssetDatabase.Refresh();

            var weapons = GetOrCreate<WeaponsConfig>("WeaponsConfig");

            var turret = GetOrCreate<WeaponConfig>("Weapons/TestTurret", w =>
            {
                w.displayName = "Test Turret";
                w.muzzle = MuzzleKind.Turret;
                w.cooldownSeconds = 0.5f;
                w.windUpSeconds = 0f;
                w.recoverySeconds = 0.2f;
                w.shot = new ShotSettings { speed = 60f, range = 60f, radius = 0.25f };
                w.hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0] };
                w.selfEffects = new EffectSpec[0];
            });

            var frontRear = GetOrCreate<WeaponConfig>("Weapons/TestFrontRear", w =>
            {
                w.displayName = "Test Front+Rear";
                w.muzzle = MuzzleKind.Fixed;
                w.fixedMuzzles = FixedMuzzles.Front | FixedMuzzles.Rear;
                w.cooldownSeconds = 3f;
                w.windUpSeconds = 0.5f;
                w.recoverySeconds = 1f;
                w.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.35f };
                w.hitPayload = new Payload
                {
                    damageKind = DamageKind.Flat,
                    damageAmount = 80f,
                    effects = new[] { new EffectSpec { type = EffectType.Corroded, magnitude = 30f, duration = 4f } }
                };
                w.selfEffects = new EffectSpec[0];
            });

            var sides = GetOrCreate<WeaponConfig>("Weapons/TestSides", w =>
            {
                w.displayName = "Test Sides";
                w.muzzle = MuzzleKind.Fixed;
                w.fixedMuzzles = FixedMuzzles.Left | FixedMuzzles.Right;
                w.cooldownSeconds = 4f;
                w.windUpSeconds = 0f;
                w.recoverySeconds = 0.5f;
                w.shot = new ShotSettings { speed = 50f, range = 30f, radius = 0.4f };
                w.hitPayload = new Payload
                {
                    damageKind = DamageKind.Flat,
                    damageAmount = 40f,
                    effects = new EffectSpec[0],
                    push = new PushSpec { speed = 8f, direction = PushDirection.AlongTravel, spinScale = 1f, reelSeconds = 1f }
                };
                w.selfEffects = new[] { new EffectSpec { type = EffectType.Spiked, magnitude = 20f, duration = 2f } };
            });

            var car = GetOrCreate<CarDefinition>("CarDefinition");
            if (car.driveConfig == null) car.driveConfig = drive;
            if (car.aimConfig == null) car.aimConfig = aim;
            if (car.ramConfig == null) car.ramConfig = ram;
            if (car.wreckConfig == null) car.wreckConfig = wreck;
            if (car.effectsConfig == null) car.effectsConfig = effects;
            if (car.weaponsConfig == null) car.weaponsConfig = weapons;
            if (car.loadout == null || car.loadout.Length == 0 || System.Array.TrueForAll(car.loadout, w => w == null))
            {
                car.loadout = new[] { turret, frontRear, sides };
            }
            if (car.hurtboxes == null || car.hurtboxes.Length == 0)
            {
                car.hurtboxes = new[] { new HurtboxBox { centre = Vector3.zero, size = new Vector3(car.width, car.height, car.length) } };
            }
            EditorUtility.SetDirty(car);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MotorCombat] Default configs ready in " + ConfigDir);
        }

        static T GetOrCreate<T>(string assetName) where T : ScriptableObject
        {
            return GetOrCreate<T>(assetName, null);
        }

        /// <summary><paramref name="initialise"/> runs only when the asset is newly created, so re-running never clobbers tuning.</summary>
        static T GetOrCreate<T>(string assetName, System.Action<T> initialise) where T : ScriptableObject
        {
            string path = $"{ConfigDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            initialise?.Invoke(created);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
