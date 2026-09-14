using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Bootstrap
{
    /// <summary>
    /// Placeholder until game modes exist: a destroyed car comes back at its
    /// original spawn pose once the delay has passed, the wreck sequence has
    /// finished, and nothing is parked on the spot. Waiting for a clear spot,
    /// rather than spawning into an overlap, avoids PhysX launching both cars
    /// apart. Replace this component, not CarRespawn, to change the rule.
    /// </summary>
    public class RespawnRule : MonoBehaviour
    {
        public RespawnConfig config;

        class Entry
        {
            public CarController car;
            public IDamageable health;
            public Vector3 position;
            public Quaternion rotation;
            public bool pending;
            public float destroyedAt;
            public Action<DamageReport> onDestroyed;
        }

        readonly List<Entry> _entries = new List<Entry>();

        void Start()
        {
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] RespawnRule on '{name}' has no RespawnConfig assigned — destroyed cars will not respawn.", this);
            }
        }

        public void Track(CarController car, Vector3 position, Quaternion rotation)
        {
            if (car == null) return;

            var health = car.GetComponent<IDamageable>();
            if (health == null)
            {
                Debug.LogError($"[MotorCombat] RespawnRule cannot track '{car.name}': it has no IDamageable.", this);
                return;
            }

            var entry = new Entry { car = car, health = health, position = position, rotation = rotation };
            entry.onDestroyed = report =>
            {
                entry.pending = true;
                entry.destroyedAt = Time.fixedTime;
            };
            health.Destroyed += entry.onDestroyed;
            _entries.Add(entry);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].health != null) _entries[i].health.Destroyed -= _entries[i].onDestroyed;
            }
            _entries.Clear();
        }

        void FixedUpdate()
        {
            if (config == null) return;

            float now = Time.fixedTime;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!entry.pending || entry.car == null) continue;
                if (!RespawnRules.IsDue(now, entry.destroyedAt, config.respawnDelaySeconds, entry.car.gameObject.activeSelf)) continue;
                if (!IsClear(entry)) continue;

                entry.pending = false;
                CarRespawn.Respawn(entry.car, entry.position, entry.rotation);
            }
        }

        bool IsClear(Entry entry)
        {
            var box = entry.car.GetComponent<BoxCollider>();
            Vector3 size = box != null ? box.size : Vector3.one;
            Vector3 offset = box != null ? box.center : Vector3.zero;

            RespawnRules.SpawnBox(entry.position, entry.rotation, offset, size, config.clearanceMargin,
                out Vector3 center, out Vector3 halfExtents);

            // The dead car is inactive, so it never blocks its own spot. Wrecks
            // sit on the Wreck layer and phase through cars anyway.
            int carLayer = PhysicsLayers.Car;
            int mask = carLayer >= 0 ? 1 << carLayer : Physics.DefaultRaycastLayers;
            return !Physics.CheckBox(center, halfExtents, entry.rotation, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
