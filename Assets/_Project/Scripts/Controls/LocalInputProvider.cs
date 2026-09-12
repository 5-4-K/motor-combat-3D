using UnityEngine;
using UnityEngine.InputSystem;
using MotorCombat.Core;

namespace MotorCombat.Controls
{
    /// <summary>
    /// Keyboard and mouse. Reads devices directly rather than through an action
    /// asset — the control set is three axes and will be replaced by a network
    /// provider for real play, so an .inputactions asset would be ceremony.
    /// </summary>
    public class LocalInputProvider : MonoBehaviour, IInputProvider
    {
        [Tooltip("Hide and lock the cursor while playing.")]
        public bool lockCursor = true;

        void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public CarInput Sample()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            var input = new CarInput();

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.throttle += 1f;
                if (keyboard.sKey.isPressed) input.throttle -= 1f;
                if (keyboard.dKey.isPressed) input.steer += 1f;
                if (keyboard.aKey.isPressed) input.steer -= 1f;
            }

            if (mouse != null)
            {
                // Already a per-frame displacement in pixels. Do not scale by dt.
                input.aimDeltaX = mouse.delta.ReadValue().x;
            }

            return input;
        }
    }
}
