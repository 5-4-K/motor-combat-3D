using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// A bar and name over every enemy car, always on. Drawn in screen space from
    /// a point above the roof, so it faces the viewer from any angle. Width follows
    /// the car's on-screen width, clamped. Nearer bars draw on top.
    ///
    /// Runs after the camera has moved this frame, or bars lag one frame behind.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EnemyHealthBars : MonoBehaviour
    {
        public CarController viewer;
        public Camera view;

        [Tooltip("Metres above the car's roof.")] public float anchorMarginMetres = 0.4f;
        [Tooltip("Reference pixels.")] public float minWidth = 60f;
        [Tooltip("Reference pixels.")] public float maxWidth = 160f;
        [Tooltip("Reference pixels.")] public float barHeight = 8f;
        public int fontSize = 14;
        [Tooltip("Reference pixels beyond the edge before a bar is hidden.")] public float screenMargin = 100f;

        static readonly Color Track = new Color(0.12f, 0.02f, 0.02f, 0.85f);
        static readonly Color FillColour = new Color(0.88f, 0.25f, 0.23f);

        class Bar
        {
            public RectTransform root;
            public RectTransform fill;
            public Text name;
            public float depth;
        }

        readonly Dictionary<CarController, Bar> _bars = new Dictionary<CarController, Bar>();
        readonly List<Bar> _visible = new List<Bar>();
        readonly List<CarController> _stale = new List<CarController>();

        RectTransform _layer;
        Canvas _canvas;

        public void Build(RectTransform canvas)
        {
            _canvas = canvas.GetComponent<Canvas>();
            _layer = HudElements.Rect("EnemyBars", canvas);
            HudElements.Fill(_layer);
        }

        void LateUpdate()
        {
            if (_layer == null) return;

            foreach (Bar bar in _bars.Values) bar.root.gameObject.SetActive(false);
            _visible.Clear();

            if (viewer != null && view != null)
            {
                float scale = _canvas != null ? _canvas.scaleFactor : 1f;
                IReadOnlyList<CarController> cars = CarRegistry.All;

                for (int i = 0; i < cars.Count; i++)
                {
                    CarController car = cars[i];
                    if (car == null || car == viewer || !Hostility.AreEnemies(viewer, car)) continue;

                    var health = car.GetComponent<IDamageable>();
                    if (health == null || health.IsDestroyed) continue;

                    var box = car.GetComponent<BoxCollider>();
                    float carHeight = box != null ? box.size.y : 1f;
                    float carWidth = box != null ? box.size.x : 2f;

                    Vector3 anchor = car.transform.position + Vector3.up * (carHeight * 0.5f + anchorMarginMetres);
                    Vector3 screen = view.WorldToScreenPoint(anchor);
                    // screenMargin is reference pixels, so convert to screen pixels before
                    // comparing — otherwise the pop-out point depends on the display's resolution.
                    if (!HealthBarLayout.IsVisible(screen, Screen.width, Screen.height, screenMargin * scale)) continue;

                    Vector3 halfSpan = view.transform.right * (carWidth * 0.5f);
                    Vector3 left = view.WorldToScreenPoint(anchor - halfSpan);
                    Vector3 right = view.WorldToScreenPoint(anchor + halfSpan);
                    float projected = Vector2.Distance(left, right);

                    Bar bar = GetOrCreate(car);
                    bar.root.gameObject.SetActive(true);
                    bar.root.anchoredPosition = new Vector2(screen.x, screen.y) / scale;
                    bar.root.sizeDelta = new Vector2(HealthBarLayout.BarWidth(projected, scale, minWidth, maxWidth), barHeight);
                    HudElements.SetFill(bar.fill, HealthBarLayout.Fill(health.Current, health.Max));
                    if (bar.name.text != car.name) bar.name.text = car.name;

                    bar.depth = screen.z;
                    _visible.Add(bar);
                }
            }

            // Farthest first, so nearer bars end up on top.
            _visible.Sort((a, b) => b.depth.CompareTo(a.depth));
            for (int i = 0; i < _visible.Count; i++) _visible[i].root.SetSiblingIndex(i);

            RemoveDeletedCars();
        }

        Bar GetOrCreate(CarController car)
        {
            if (_bars.TryGetValue(car, out Bar existing)) return existing;

            var root = HudElements.Rect("Bar", _layer);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0f);

            var track = HudElements.Image("Track", root, Track);
            HudElements.Fill(track.rectTransform);

            var fill = HudElements.Image("Fill", root, FillColour).rectTransform;
            HudElements.SetFill(fill, 1f);

            var label = HudElements.Text("Name", root, fontSize, TextAnchor.LowerCenter);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            labelRect.sizeDelta = new Vector2(0f, fontSize + 4f);

            var bar = new Bar { root = root, fill = fill, name = label };
            _bars[car] = bar;
            return bar;
        }

        void RemoveDeletedCars()
        {
            _stale.Clear();
            foreach (var pair in _bars)
            {
                if (pair.Key == null) _stale.Add(pair.Key);
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Bar bar = _bars[_stale[i]];
                if (bar.root != null) Destroy(bar.root.gameObject);
                _bars.Remove(_stale[i]);
            }
        }
    }
}
