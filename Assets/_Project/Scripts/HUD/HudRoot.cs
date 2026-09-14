using UnityEngine;
using UnityEngine.UI;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The HUD canvas, built in code like the arena. It scales with screen WIDTH
    /// against 1920 — the same fairness rule as the camera and crosshair: every
    /// monitor sees identical proportions. Widgets are separate components added
    /// under it; later sub-projects add their own.
    /// </summary>
    public class HudRoot : MonoBehaviour
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public Canvas Canvas { get; private set; }
        public RectTransform Rect { get; private set; }

        public static HudRoot Create()
        {
            var go = new GameObject("HUD", typeof(RectTransform));

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            var root = go.AddComponent<HudRoot>();
            root.Canvas = canvas;
            root.Rect = (RectTransform)go.transform;
            return root;
        }
    }
}
