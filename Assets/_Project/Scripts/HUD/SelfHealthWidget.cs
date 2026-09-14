using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The viewer's own HP, bottom-centre, in every camera mode — in first person
    /// you never see your own car, so its health lives on the screen.
    /// </summary>
    public class SelfHealthWidget : MonoBehaviour
    {
        public CarController viewer;

        [Tooltip("Reference pixels.")] public float width = 520f;
        [Tooltip("Reference pixels.")] public float height = 16f;
        [Tooltip("Reference pixels from the bottom of the screen.")] public float bottomMargin = 40f;
        public int fontSize = 22;

        static readonly Color Track = new Color(0.08f, 0.12f, 0.08f, 0.85f);
        static readonly Color FillColour = new Color(0.32f, 0.76f, 0.35f);

        RectTransform _root;
        RectTransform _fill;
        Text _label;
        IDamageable _health;
        CarController _healthOwner;

        public void Build(RectTransform canvas)
        {
            _root = HudElements.Rect("SelfHealth", canvas);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, bottomMargin);
            _root.sizeDelta = new Vector2(width, height);

            var track = HudElements.Image("Track", _root, Track);
            HudElements.Fill(track.rectTransform);

            _fill = HudElements.Image("Fill", _root, FillColour).rectTransform;
            HudElements.SetFill(_fill, 1f);

            _label = HudElements.Text("Label", _root, fontSize, TextAnchor.LowerCenter);
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(0f, fontSize + 6f);
        }

        void LateUpdate()
        {
            if (_root == null) return;

            if (viewer != _healthOwner)
            {
                _healthOwner = viewer;
                _health = viewer != null ? viewer.GetComponent<IDamageable>() : null;
            }

            bool show = viewer != null && _health != null && !_health.IsDestroyed;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            HudElements.SetFill(_fill, HealthBarLayout.Fill(_health.Current, _health.Max));
            _label.text = HealthBarLayout.SelfText(_health.Current, _health.Max);
        }
    }
}
