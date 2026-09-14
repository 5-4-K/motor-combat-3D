using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>
    /// A car's three weapon slots. Thin adapter: WeaponRules validates,
    /// WeaponTiming decides, MuzzleRules places, Shot flies.
    ///
    /// A press is a per-frame event: FrameTick collects it, the next physics step
    /// accepts or drops it. Nothing is buffered.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class WeaponModule : MonoBehaviour, ICarModule, IRespawnable, IWeaponSlots
    {
        public const int Slots = 3;

        public WeaponsConfig config;

        [Tooltip("Slot 1 (LMB), slot 2 (RMB), slot 3 (Space).")]
        public WeaponConfig[] loadout = new WeaponConfig[Slots];

        [Tooltip("Logs presses, drops, releases and cancelled wind-ups on this car.")]
        public bool logWeapons;

        /// <summary>Raised once per shot, just before it is launched.</summary>
        public event Action<ShotLaunch> Fired;

        readonly SlotTiming[] _slots = { SlotTiming.Ready, SlotTiming.Ready, SlotTiming.Ready };
        readonly WeaponConfig[] _valid = new WeaponConfig[Slots];
        readonly List<string> _errors = new List<string>();
        LockTiming _lock = LockTiming.None;
        int _pending;
        bool _ready;

        CarController _car;
        IDamageable _health;
        IEffectReceiver _effects;
        BoxCollider _box;

        CarController Car => _car != null ? _car : (_car = GetComponent<CarController>());

        public int SlotCount => Slots;

        void Start()
        {
            EnsureReady();
        }

        void OnDestroy()
        {
            if (_health != null) _health.Destroyed -= OnDestroyedByDamage;
        }

        /// <summary>
        /// Validates once, on first use rather than in Awake: CarFactory assigns
        /// the fields after AddComponent, and EditMode never runs Start.
        /// </summary>
        void EnsureReady()
        {
            if (_ready) return;
            _ready = true;

            _health = GetComponent<IDamageable>();
            _effects = GetComponent<IEffectReceiver>();
            _box = GetComponent<BoxCollider>();
            if (_health != null) _health.Destroyed += OnDestroyedByDamage;

            if (config == null)
            {
                Debug.LogError($"[MotorCombat] WeaponModule on '{name}' has no WeaponsConfig assigned; every weapon slot is disabled.", this);
                return;
            }

            if (loadout != null && loadout.Length > Slots)
            {
                Debug.LogWarning($"[MotorCombat] WeaponModule on '{name}' has {loadout.Length} loadout entries; only the first {Slots} are used.", this);
            }

            for (int i = 0; i < Slots; i++)
            {
                WeaponConfig weapon = loadout != null && i < loadout.Length ? loadout[i] : null;
                if (weapon == null) continue;

                _errors.Clear();
                if (WeaponRules.Validate(weapon, config.fireHeight, _errors))
                {
                    _valid[i] = weapon;
                    continue;
                }

                for (int e = 0; e < _errors.Count; e++)
                {
                    Debug.LogError($"[MotorCombat] Weapon '{weapon.name}' in slot {i + 1} of '{name}' is disabled: {_errors[e]}", this);
                }
            }
        }

        // --- ICarModule -----------------------------------------------------------

        public void FrameTick(in CarInput input, float dt)
        {
            _pending |= input.firePressed;
        }

        public void Tick(in CarInput input, float dt)
        {
            Step(Time.fixedTime);
        }

        /// <summary>Queues a press as if the slot's key went down this frame.</summary>
        public void Press(int slot)
        {
            if (slot >= 0 && slot < Slots) _pending |= 1 << slot;
        }

        /// <summary>
        /// One physics step. Public so tests can drive time. FireAllowed is
        /// re-read for every slot rather than cached once: Release can apply
        /// self-effects that stun or kill the own car (or a wind-up release
        /// earlier in this same step can), and a later slot in this same step
        /// must see that fresh state, not a value captured before it happened.
        /// </summary>
        public void Step(float now)
        {
            EnsureReady();

            for (int i = 0; i < Slots; i++)
            {
                if (!_slots[i].windingUp) continue;

                if (!FireAllowed())
                {
                    WeaponTiming.Cancel(ref _slots[i]);
                    Log(i, "wind-up cancelled (Fire blocked)");
                }
                else if (WeaponTiming.ShouldRelease(_slots[i], now))
                {
                    _slots[i].windingUp = false;
                    Release(i);
                }
            }

            int pending = _pending;
            _pending = 0;

            for (int i = 0; i < Slots; i++)
            {
                if ((pending & (1 << i)) == 0) continue;

                WeaponConfig weapon = _valid[i];
                if (!WeaponTiming.CanPress(_slots[i], weapon != null, FireAllowed(), _lock, now))
                {
                    Log(i, "press dropped");
                    continue;
                }

                bool releaseNow = WeaponTiming.Press(
                    ref _slots[i], ref _lock, i, now,
                    weapon.cooldownSeconds, weapon.windUpSeconds, weapon.recoverySeconds,
                    Car.Stats.Effective(CarStat.Attack));

                Log(i, releaseNow ? "pressed" : "pressed, winding up");
                if (releaseNow) Release(i);
            }
        }

        // --- IWeaponSlots -----------------------------------------------------------

        public WeaponSlotStatus GetStatus(int slot)
        {
            return GetStatus(slot, Time.fixedTime);
        }

        public WeaponSlotStatus GetStatus(int slot, float now)
        {
            EnsureReady();
            if (slot < 0 || slot >= Slots || _valid[slot] == null) return default;

            return new WeaponSlotStatus
            {
                assigned = true,
                cooldownRemaining = WeaponTiming.CooldownRemaining(_slots[slot], now),
                cooldownDuration = _valid[slot].cooldownSeconds,
                blocked = WeaponTiming.IsBlocked(true, FireAllowed(), _lock, slot, now)
            };
        }

        // --- IRespawnable -----------------------------------------------------------

        /// <summary>Clears the lock, wind-ups and queued presses. Cooldowns keep running.</summary>
        public void ResetForRespawn()
        {
            WeaponTiming.ResetForRespawn(_slots, ref _lock);
            _pending = 0;
        }

        // --- Internals ------------------------------------------------------------

        bool FireAllowed()
        {
            return Car.Abilities.Has(CarAbility.Fire) && (_health == null || !_health.IsDestroyed);
        }

        void OnDestroyedByDamage(DamageReport report)
        {
            for (int i = 0; i < Slots; i++) WeaponTiming.Cancel(ref _slots[i]);
        }

        void Release(int slot)
        {
            WeaponConfig weapon = _valid[slot];
            CarController car = Car;
            float attack = _slots[slot].attack;

            if (_effects != null && weapon.selfEffects != null)
            {
                for (int i = 0; i < weapon.selfEffects.Length; i++)
                {
                    EffectSpec spec = weapon.selfEffects[i];
                    _effects.Apply(new EffectRequest
                    {
                        source = car,
                        sourceTag = weapon.name,
                        type = spec.type,
                        magnitude = spec.magnitude,
                        duration = spec.duration,
                        allowNonEnemy = true,
                        attack = attack
                    });
                }
            }

            Transform root = car.transform;
            Vector3 boxSize = _box != null ? _box.size : Vector3.one;

            if (weapon.muzzle == MuzzleKind.Turret)
            {
                Launch(weapon, MuzzleRules.Turret(root.position, root.rotation, boxSize, config.fireHeight, car.AimYaw), attack);
            }
            else
            {
                for (int i = 0; i < MuzzleRules.FixedOrder.Length; i++)
                {
                    FixedMuzzles which = MuzzleRules.FixedOrder[i];
                    if ((weapon.fixedMuzzles & which) == 0) continue;
                    Launch(weapon, MuzzleRules.Fixed(which, root.position, root.rotation, boxSize, config.fireHeight), attack);
                }
            }

            Log(slot, "fired");
        }

        void Launch(WeaponConfig weapon, in Muzzle muzzle, float attack)
        {
            var launch = new ShotLaunch
            {
                source = Car,
                sourceTag = weapon.name,
                origin = muzzle.position,
                direction = muzzle.direction,
                settings = weapon.shot,
                payload = weapon.hitPayload,
                attack = attack
            };

            Fired?.Invoke(launch);
            Shot.Launch(launch);
        }

        void Log(int slot, string message)
        {
            if (logWeapons) Debug.Log($"[Weapons] {name} slot {slot + 1}: {message}", this);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: fire slot 1")] void DebugFireSlot1() => DebugFire(0);
        [ContextMenu("Debug: fire slot 2")] void DebugFireSlot2() => DebugFire(1);
        [ContextMenu("Debug: fire slot 3")] void DebugFireSlot3() => DebugFire(2);

        /// <summary>Queues a press; the next physics step applies the normal rules.</summary>
        void DebugFire(int slot)
        {
            Press(slot);
            Debug.Log($"[Weapons] {name}: debug press queued for slot {slot + 1}", this);
        }
#endif
    }
}
