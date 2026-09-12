using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>
    /// SEAM ONLY. Nothing implements this yet.
    /// </summary>
    public interface IWeapon
    {
        string DisplayName { get; }

        /// <summary>Fire along a world-space direction from a world-space origin.</summary>
        void Fire(Vector3 origin, Vector3 direction);
    }
}
