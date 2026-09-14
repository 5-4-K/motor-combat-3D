using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Sits on a car's hurtbox child, beside its trigger boxes, and maps any of
    /// them back to the car. Weapons reach it only through queries: the Hurtbox
    /// layer collides with nothing.
    /// </summary>
    public class Hurtbox : MonoBehaviour
    {
        public const string ObjectName = "Hurtbox";

        public CarController Car { get; set; }
    }
}
