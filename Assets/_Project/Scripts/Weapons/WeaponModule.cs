using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>
    /// SEAM ONLY. Holds weapons and knows where the car is aiming. Fires nothing,
    /// because no IWeapon exists yet.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class WeaponModule : MonoBehaviour, ICarModule
    {
        readonly List<IWeapon> _weapons = new List<IWeapon>();

        CarController _car;

        void Awake()
        {
            _car = GetComponent<CarController>();
        }

        public void Equip(IWeapon weapon)
        {
            if (weapon != null) _weapons.Add(weapon);
        }

        /// <summary>Fires every equipped weapon along the car's current aim.</summary>
        public void Fire()
        {
            Vector3 origin = transform.position;
            Vector3 direction = _car.AimDirection;

            for (int i = 0; i < _weapons.Count; i++)
            {
                _weapons[i].Fire(origin, direction);
            }
        }

        public void Tick(in CarInput input, float dt) { }
        public void FrameTick(in CarInput input, float dt) { }
    }
}
