using System.Collections.Generic;

namespace MotorCombat.Core
{
    /// <summary>
    /// Every active car. The HUD (and later weapons and bots) find cars here
    /// instead of referencing whoever spawned them. A deactivated wreck drops out.
    /// </summary>
    public static class CarRegistry
    {
        static readonly List<CarController> Cars = new List<CarController>();

        public static IReadOnlyList<CarController> All => Cars;

        public static void Register(CarController car)
        {
            if (car != null && !Cars.Contains(car)) Cars.Add(car);
        }

        public static void Unregister(CarController car)
        {
            Cars.Remove(car);
        }
    }
}
