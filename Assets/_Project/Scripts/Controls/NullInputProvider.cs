using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Controls
{
    /// <summary>Always idle. Used by the stationary dummy car.</summary>
    public class NullInputProvider : MonoBehaviour, IInputProvider
    {
        public CarInput Sample() => CarInput.None;
    }
}
