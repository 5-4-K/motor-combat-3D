using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Brings a car back to life at a pose — or teleports a live one. The
    /// mechanism only: WHEN to respawn belongs to a game mode (today the
    /// placeholder RespawnRule).
    /// </summary>
    public static class CarRespawn
    {
        static readonly List<IRespawnable> Respawnables = new List<IRespawnable>();

        public static void Respawn(CarController car, Vector3 position, Quaternion rotation)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));

            GameObject root = car.gameObject;

            // Reset first, while a wreck is still inactive, so nothing runs a
            // frame against the old life's state.
            Respawnables.Clear();
            root.GetComponents(Respawnables);
            for (int i = 0; i < Respawnables.Count; i++)
            {
                Respawnables[i].ResetForRespawn();
            }
            Respawnables.Clear();

            int carLayer = PhysicsLayers.Car;
            if (carLayer >= 0) root.layer = carLayer;

            car.transform.SetPositionAndRotation(position, rotation);
            car.AimYaw = 0f;

            // A ram in the first step reads the pre-step snapshot; without this
            // it would read the wreck's last slide.
            car.ClearMotionSnapshot();

            root.SetActive(true);

            var body = root.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
