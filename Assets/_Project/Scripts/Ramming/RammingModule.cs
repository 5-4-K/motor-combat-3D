using System;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// SEAM ONLY. Detects impacts and raises them. Applies no damage, no
    /// knockback and no status — Unity's own collision response is the entire
    /// behaviour for now.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RammingModule : MonoBehaviour, ICarModule
    {
        [Tooltip("Log impacts to the console. Useful while tuning collision feel.")]
        public bool logImpacts;

        public event Action<CarCollisionEvent> Collided;

        void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;

            ContactPoint contact = collision.GetContact(0);

            var impact = new CarCollisionEvent
            {
                other = collision.collider.GetComponentInParent<CarController>(),
                point = contact.point,
                normal = contact.normal,
                relativeSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal))
            };

            if (logImpacts)
            {
                string target = impact.other != null ? impact.other.name : "environment";
                Debug.Log($"[Ram] {name} hit {target} at {impact.relativeSpeed:F1} m/s");
            }

            Collided?.Invoke(impact);
        }

        public void Tick(in CarInput input, float dt) { }
        public void FrameTick(in CarInput input, float dt) { }
    }
}
