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

        [Tooltip("Crosshair arm length in pixels AT THE REFERENCE WIDTH, measured from the " +
                 "centre outwards. Scales with screen width so the cross covers the same " +
                 "angle of the world on every monitor.")]
        public float size = 30f;

        [Tooltip("Margin in pixels AT THE REFERENCE WIDTH when the aim leaves the viewport.")]
        public float edgeMargin = 24f;

        [Tooltip("Screen width the pixel values above are authored for. The camera holds a " +
                 "fixed HORIZONTAL field of view, so a pixel covers the same angle for every " +
                 "player only when measured against width -- never height.")]
        public float referenceWidth = 1920f;

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

            float scale = Screen.width / Mathf.Max(1f, referenceWidth);
            float margin = edgeMargin * scale;
            float arm = size * scale;
            float thickness = Mathf.Max(1f, 2f * scale);

            float clampedX = Mathf.Clamp(screenPoint.x, margin, Screen.width - margin);
            float clampedY = Mathf.Clamp(screenPoint.y, margin, Screen.height - margin);

            bool clamped = behindCamera
                           || !Mathf.Approximately(clampedX, screenPoint.x)
                           || !Mathf.Approximately(clampedY, screenPoint.y);

            // GUI space has y growing downward; screen space has it growing up.
            float x = clampedX;
            float y = Screen.height - clampedY;

            GUI.color = clamped ? new Color(1f, 0.6f, 0.2f) : Color.white;

            // Horizontal arm
            GUI.DrawTexture(new Rect(x - arm, y - thickness * 0.5f, arm * 2f, thickness), _pixel);
            // Vertical arm
            GUI.DrawTexture(new Rect(x - thickness * 0.5f, y - arm, thickness, arm * 2f), _pixel);

            GUI.color = Color.white;
        }
    }
}
