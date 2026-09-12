using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// Everything a future damage model needs about one impact. Raised today,
    /// consumed by nobody.
    /// </summary>
    public struct CarCollisionEvent
    {
        /// <summary>The other car, or null when the impact was with the wall or ground.</summary>
        public CarController other;

        public Vector3 point;
        public Vector3 normal;

        /// <summary>Closing speed along the contact normal, in m/s. Always positive.</summary>
        public float relativeSpeed;
    }
}
