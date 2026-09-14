using System;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>One box of a car's hurtbox, in the car's local space relative to its centre. No rotation.</summary>
    [Serializable]
    public struct HurtboxBox
    {
        public Vector3 centre;
        public Vector3 size;
    }
}
