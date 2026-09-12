using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// Draws the crosshair by projecting the aim ray through the camera, NOT by
    /// lerping across screen X. Screen position is a tangent function of angle,
    /// so a linear mapping would visibly drift from where shots actually go near
    /// the screen edges.
    /// </summary>
    public class CrosshairHUD : MonoBehaviour
    {
        public CarController target;
        public Camera view;

        [Tooltip("How far along the aim ray the crosshair is projected, in metres.")]
        public float projectionDistance = 40f;

        [Tooltip("Crosshair arm length in pixels.")]
        public float size = 10f;

        [Tooltip("Pixels of margin when the aim leaves the viewport.")]
        public float edgeMargin = 24f;

        Texture2D _pixel;

        void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        void OnGUI()
        {
            if (target == null || view == null) return;

            Vector3 worldPoint = target.transform.position + target.AimDirection * projectionDistance;
            Vector3 screenPoint = view.WorldToScreenPoint(worldPoint);

            bool behindCamera = screenPoint.z < 0f;
            if (behindCamera)
            {
                // WorldToScreenPoint mirrors through the screen centre on BOTH axes
                // when the point is behind the camera, because both terms divide by a
                // negative view-space z. Un-mirror both, then let the clamp below push
                // the crosshair to the correct edge.
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            float clampedX = Mathf.Clamp(screenPoint.x, edgeMargin, Screen.width - edgeMargin);
            float clampedY = Mathf.Clamp(screenPoint.y, edgeMargin, Screen.height - edgeMargin);

            bool clamped = behindCamera
                           || !Mathf.Approximately(clampedX, screenPoint.x)
                           || !Mathf.Approximately(clampedY, screenPoint.y);

            // GUI space has y growing downward; screen space has it growing up.
            float x = clampedX;
            float y = Screen.height - clampedY;

            GUI.color = clamped ? new Color(1f, 0.6f, 0.2f) : Color.white;

            // Horizontal arm
            GUI.DrawTexture(new Rect(x - size, y - 1f, size * 2f, 2f), _pixel);
            // Vertical arm
            GUI.DrawTexture(new Rect(x - 1f, y - size, 2f, size * 2f), _pixel);

            GUI.color = Color.white;
        }
    }
}
