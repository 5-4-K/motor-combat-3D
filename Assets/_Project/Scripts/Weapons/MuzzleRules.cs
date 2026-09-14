using System;
using UnityEngine;

namespace MotorCombat.Weapons
{
    public struct Muzzle
    {
        public Vector3 position;

        /// <summary>Flat and normalised.</summary>
        public Vector3 direction;
    }

    /// <summary>
    /// Where each muzzle is, from the car's collision box. Height is always the
    /// car's floor plus the global fire height; the root is the box centre.
    /// </summary>
    public static class MuzzleRules
    {
        /// <summary>The order a multi-muzzle weapon fires in.</summary>
        public static readonly FixedMuzzles[] FixedOrder = { FixedMuzzles.Front, FixedMuzzles.Rear, FixedMuzzles.Left, FixedMuzzles.Right };

        public static Muzzle Fixed(FixedMuzzles which, Vector3 rootPosition, Quaternion rootRotation, Vector3 boxSize, float fireHeight)
        {
            Vector3 forward = FlatForward(rootRotation);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = boxSize.x * 0.5f;
            float halfLength = boxSize.z * 0.5f;

            switch (which)
            {
                case FixedMuzzles.Front: return At(rootPosition + forward * halfLength, forward, rootPosition.y, boxSize.y, fireHeight);
                case FixedMuzzles.Rear: return At(rootPosition - forward * halfLength, -forward, rootPosition.y, boxSize.y, fireHeight);
                case FixedMuzzles.Left: return At(rootPosition - right * halfWidth, -right, rootPosition.y, boxSize.y, fireHeight);
                case FixedMuzzles.Right: return At(rootPosition + right * halfWidth, right, rootPosition.y, boxSize.y, fireHeight);
                default: throw new ArgumentException($"expected exactly one fixed muzzle, got {which}", nameof(which));
            }
        }

        /// <summary>At the front muzzle, pointing along the car's aim.</summary>
        public static Muzzle Turret(Vector3 rootPosition, Quaternion rootRotation, Vector3 boxSize, float fireHeight, float aimYaw)
        {
            Muzzle front = Fixed(FixedMuzzles.Front, rootPosition, rootRotation, boxSize, fireHeight);
            front.direction = Quaternion.AngleAxis(aimYaw, Vector3.up) * front.direction;
            return front;
        }

        static Muzzle At(Vector3 position, Vector3 direction, float rootY, float boxHeight, float fireHeight)
        {
            position.y = rootY - boxHeight * 0.5f + fireHeight;
            return new Muzzle { position = position, direction = direction };
        }

        static Vector3 FlatForward(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }
    }
}
