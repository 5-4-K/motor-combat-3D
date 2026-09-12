using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Ramming;
using MotorCombat.Weapons;

namespace MotorCombat.Cars
{
    /// <summary>
    /// Builds a car from a definition. A scaled box today; swap the visual for a
    /// real model later without touching any module.
    /// </summary>
    public static class CarFactory
    {
        public const string DriverAnchorName = "DriverAnchor";

        public static CarController Spawn(
            CarDefinition definition,
            Vector3 position,
            Quaternion rotation,
            IInputProvider provider,
            Color colour)
        {
            var car = new GameObject(definition.name);
            car.transform.SetPositionAndRotation(position, rotation);

            // Body: a box of the configured dimensions. Z is length (forward).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(car.transform, false);
            body.transform.localScale = new Vector3(definition.width, definition.height, definition.length);
            Paint(body, colour);

            // The primitive brings its own collider on the child. Collision must
            // live on the root next to the Rigidbody, so drop the child's.
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(definition.width, definition.height, definition.length);

            // Nose marker, so facing is readable on a featureless box.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(car.transform, false);
            nose.transform.localScale = new Vector3(
                definition.width * 0.25f, definition.height * 0.25f, definition.length * 0.1f);
            nose.transform.localPosition = new Vector3(
                0f, definition.height * 0.4f, definition.length * 0.5f);
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            Paint(nose, Color.white);

            var anchor = new GameObject(DriverAnchorName);
            anchor.transform.SetParent(car.transform, false);
            anchor.transform.localPosition = definition.driverAnchorOffset;

            var rigidbody = car.AddComponent<Rigidbody>();
            rigidbody.mass = definition.mass;

            var controller = car.AddComponent<CarController>();

            car.AddComponent<DrivingModule>().config = definition.driveConfig;
            car.AddComponent<AimModule>().config = definition.aimConfig;
            car.AddComponent<RammingModule>();
            car.AddComponent<WeaponModule>();

            controller.Bind(provider);
            return controller;
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
