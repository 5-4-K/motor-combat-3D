using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Physics layers and which of them collide. Configured in code, once at
    /// boot, so the rules are reviewable here rather than hidden in a
    /// ProjectSettings bit matrix. The names must exist in TagManager.
    ///
    /// Later sub-projects add their own layers beside these; `Hurtbox`
    /// (sub-project 3) touches nothing.
    /// </summary>
    public static class PhysicsLayers
    {
        public const string ArenaName = "Arena";
        public const string CarName = "Car";
        public const string WreckName = "Wreck";
        public const string HurtboxName = "Hurtbox";

        public static int Arena => LayerMask.NameToLayer(ArenaName);
        public static int Car => LayerMask.NameToLayer(CarName);
        public static int Wreck => LayerMask.NameToLayer(WreckName);
        public static int Hurtbox => LayerMask.NameToLayer(HurtboxName);

        /// <summary>A wreck touches only the arena (floor and wall). Cars touch cars and the arena.</summary>
        public static void ConfigureCollisions()
        {
            int arena = Arena;
            int car = Car;
            int wreck = Wreck;

            if (arena < 0 || car < 0 || wreck < 0)
            {
                Debug.LogError($"[MotorCombat] Physics layers '{ArenaName}', '{CarName}' and '{WreckName}' must exist in Project Settings > Tags and Layers.");
                return;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(wreck, layer, layer != arena);
            }

            Physics.IgnoreLayerCollision(car, car, false);
            Physics.IgnoreLayerCollision(car, arena, false);

            // Hurtboxes are reached only by weapon queries, never by contacts or
            // trigger events.
            int hurtbox = Hurtbox;
            if (hurtbox < 0)
            {
                Debug.LogError($"[MotorCombat] Physics layer '{HurtboxName}' must exist in Project Settings > Tags and Layers.");
                return;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(hurtbox, layer, true);
            }
        }
    }
}
