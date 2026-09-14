using System;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Who is an enemy of whom. There are no teams yet, so every other car is an
    /// enemy. Team modes replace the rule; no caller changes.
    /// </summary>
    public static class Hostility
    {
        static readonly Func<CarController, CarController, bool> DefaultRule = (a, b) => a != b;

        static Func<CarController, CarController, bool> _rule = DefaultRule;

        // Domain reload is disabled for this project, so a static field set by
        // a previous play session (e.g. a test's SetRule) would otherwise
        // still be sitting there the next time play mode starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => ResetRule();

        /// <summary>True when either car is null (the environment hurts everyone), otherwise the current rule.</summary>
        public static bool AreEnemies(CarController a, CarController b)
        {
            if (a == null || b == null) return true;
            return _rule(a, b);
        }

        public static void SetRule(Func<CarController, CarController, bool> rule)
        {
            _rule = rule ?? DefaultRule;
        }

        public static void ResetRule()
        {
            _rule = DefaultRule;
        }
    }
}
