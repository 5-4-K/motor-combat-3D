using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Ramming;
using MotorCombat.Weapons;
using MotorCombat.Effects;

namespace MotorCombat.Cars
{
    /// <summary>
    /// Builds a car from a definition.
    ///
    /// The visual is a CHILD object; the Rigidbody and BoxCollider live on the
    /// root. Swapping a box for a real model therefore touches no module, and
    /// physics never depends on what the car looks like.
    /// </summary>
    public static class CarFactory
    {
        /// <summary>Name of the visual child, whichever kind it is.</summary>
        public const string VisualName = "Body";

        /// <summary>Facing marker on the placeholder box. Absent on a real model.</summary>
        public const string NoseName = "Nose";

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static CarController Spawn(
            CarDefinition definition,
            Vector3 position,
            Quaternion rotation,
            IInputProvider provider,
            Color colour)
        {
            var car = new GameObject(definition.name);
            car.transform.SetPositionAndRotation(position, rotation);

            int carLayer = PhysicsLayers.Car;
            if (carLayer >= 0) car.layer = carLayer;

            if (definition.visualPrefab != null)
            {
                BuildModelVisual(car.transform, definition, colour);
            }
            else
            {
                BuildPlaceholderVisual(car.transform, definition, colour);
            }

            // Collision comes from the definition, never from the model. A mesh
            // collider taken off a car body is the obvious move and is wrong:
            // concave mesh colliders cannot be dynamic in PhysX at all, and a
            // convex hull of a car body rams less predictably than a box.
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(definition.width, definition.height, definition.length);
            collider.sharedMaterial = Frictionless();

            var anchor = new GameObject(CarController.DriverAnchorName);
            anchor.transform.SetParent(car.transform, false);
            anchor.transform.localPosition = definition.driverAnchorOffset;

            var rigidbody = car.AddComponent<Rigidbody>();
            rigidbody.mass = definition.mass;

            var controller = car.AddComponent<CarController>();

            car.AddComponent<DrivingModule>().config = definition.driveConfig;
            car.AddComponent<AimModule>().config = definition.aimConfig;
            controller.Stats.SetBase(CarStat.Attack, definition.attack);
            controller.Stats.SetBase(CarStat.Defense, definition.defense);
            controller.Stats.SetBase(CarStat.Strength, definition.strength);
            controller.Stats.SetBase(CarStat.Resistance, definition.resistance);

            car.AddComponent<RammingModule>().config = definition.ramConfig;
            car.AddComponent<WeaponModule>();

            car.AddComponent<Health>().maxHealth = definition.maxHealth;
            car.AddComponent<WreckSequence>().config = definition.wreckConfig;

            // Last, so it ticks after driving and ramming every physics step.
            car.AddComponent<CarEffects>().config = definition.effectsConfig;

            controller.Bind(provider);
            return controller;
        }

        // --- Visuals ----------------------------------------------------------

        static void BuildModelVisual(Transform parent, CarDefinition definition, Color colour)
        {
            var visual = Object.Instantiate(definition.visualPrefab, parent);
            visual.name = VisualName;
            visual.transform.localPosition = definition.visualOffset;
            visual.transform.localRotation = Quaternion.Euler(0f, definition.visualYawOffset, 0f);

            StripPhysics(visual);
            Tint(visual, colour, definition.tintedMaterials);

            // No nose marker: a real model shows its own facing.
        }

        static void BuildPlaceholderVisual(Transform parent, CarDefinition definition, Color colour)
        {
            // Body: a box of the configured dimensions. Z is length (forward).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = VisualName;
            body.transform.SetParent(parent, false);
            body.transform.localScale = new Vector3(definition.width, definition.height, definition.length);
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            Paint(body, colour);

            // Nose marker, so facing is readable on a featureless box.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = NoseName;
            nose.transform.SetParent(parent, false);
            nose.transform.localScale = new Vector3(
                definition.width * 0.25f, definition.height * 0.25f, definition.length * 0.1f);
            nose.transform.localPosition = new Vector3(
                0f, definition.height * 0.4f, definition.length * 0.5f);
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            Paint(nose, Color.white);
        }

        /// <summary>
        /// Removes anything on the model that would fight the root's physics.
        /// Asset-store car prefabs routinely ship mesh colliders; left in place
        /// they join the root Rigidbody as a compound collider, and a concave
        /// mesh collider on a dynamic body is illegal in PhysX. A nested
        /// Rigidbody would be worse still.
        /// </summary>
        static void StripPhysics(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(body);
            }
        }

        /// <summary>
        /// Applies the team colour through a MaterialPropertyBlock rather than by
        /// swapping materials. The model's materials are shared assets: writing to
        /// them would recolour every car at once and destroy the authored look.
        /// A property block is per-renderer, instantiates nothing, and leaks nothing.
        ///
        /// An empty <paramref name="targets"/> tints everything; listing materials
        /// keeps glass and interior out of it.
        /// </summary>
        static void Tint(GameObject visual, Color colour, Material[] targets)
        {
            bool tintAll = targets == null || targets.Length == 0;
            var block = new MaterialPropertyBlock();

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (!tintAll && System.Array.IndexOf(targets, materials[i]) < 0) continue;

                    renderer.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, colour);
                    block.SetColor(ColorId, colour);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }

        static PhysicsMaterial _frictionless;

        /// <summary>
        /// Zero-friction contact material, shared by every car.
        ///
        /// The design makes <see cref="Driving.DrivePhysics"/> the single source of
        /// resistance: drag decays speed, ApplyGrip decays sideways velocity, and
        /// Rigidbody damping is forced to zero so PhysX cannot stack a second decay.
        /// PhysX CONTACT FRICTION is the third force that rule missed — at the default
        /// 0.6 it subtracts a flat mu*m*g (about 7 kN on a 1200 kg car), which costs
        /// forward thrust ~24% and reverse thrust ~59%, and breaks the terminal-speed
        /// formula the configs are tuned against.
        ///
        /// Minimum combine means this wins against the ground's and wall's default
        /// material, so neither of those needs its own.
        /// </summary>
        static PhysicsMaterial Frictionless()
        {
            if (_frictionless == null)
            {
                _frictionless = new PhysicsMaterial("MotorCombatFrictionless")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            return _frictionless;
        }

        static void Paint(GameObject target, Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = colour;
            target.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
