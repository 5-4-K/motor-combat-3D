using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The viewer's weapon slots: three circles top-centre. A dark overlay drains
    /// top-first over the cooldown; a red border and slash mark a slot that can't
    /// fire for any other reason. Both can show at once. Stays visible while the
    /// car is a wreck — its slots then read as blocked.
    /// </summary>
    public class WeaponSlotsWidget : MonoBehaviour
    {
        public const int SlotCount = 3;

        public static readonly Color BackgroundColour = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color OverlayColour = new Color(0.25f, 0.25f, 0.25f, 0.85f);
        public static readonly Color RingColour = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color BlockedColour = new Color(0.9f, 0.15f, 0.15f, 1f);
        public static readonly Color EmptyRingColour = new Color(1f, 1f, 1f, 0.25f);

        public CarController viewer;

        [Tooltip("Reference pixels.")] public float diameter = 72f;
        [Tooltip("Reference pixels.")] public float gap = 20f;
        [Tooltip("Reference pixels from the top of the screen.")] public float topMargin = 24f;
        [Tooltip("Reference pixels.")] public float borderThickness = 4f;
        [Tooltip("Reference pixels.")] public float slashThickness = 6f;
        public int fontSize = 18;

        class SlotView
        {
            public Image background;
            public Image overlay;
            public Image border;
            public Image slash;
            public Text label;
        }

        readonly SlotView[] _views = new SlotView[SlotCount];
        Sprite _ring;
        IWeaponSlots _slots;
        CarController _slotsOwner;

        public RectTransform Root { get; private set; }

        public int SlotViews => Root == null ? 0 : SlotCount;
        public bool SlashShown(int index) => _views[index].slash.enabled;
        public float OverlayFill(int index) => _views[index].overlay.enabled ? _views[index].overlay.fillAmount : 0f;
        public bool LabelShown(int index) => _views[index].label.enabled;
        public Color BorderColour(int index) => _views[index].border.color;

        public void Build(RectTransform canvas)
        {
            Root = HudElements.Rect("WeaponSlots", canvas);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 1f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.anchoredPosition = new Vector2(0f, -topMargin);
            Root.sizeDelta = new Vector2(0f, diameter);

            _ring = HudShapes.CreateRing(borderThickness / diameter);

            for (int i = 0; i < SlotCount; i++)
            {
                _views[i] = CreateView(i);
            }

            Show(null);
        }

        void OnDestroy()
        {
            if (_ring != null)
            {
                DestroyImmediate(_ring.texture);
                DestroyImmediate(_ring);
            }
        }

        void LateUpdate()
        {
            if (Root == null) return;

            if (viewer != _slotsOwner)
            {
                _slotsOwner = viewer;
                _slots = viewer != null ? viewer.GetComponent<IWeaponSlots>() : null;
            }

            Show(viewer != null ? _slots : null);
        }

        /// <summary>Draws every slot from <paramref name="slots"/>; null draws three empty slots.</summary>
        public void Show(IWeaponSlots slots)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponSlotStatus status = slots != null && i < slots.SlotCount ? slots.GetStatus(i) : default;
                Apply(_views[i], status);
            }
        }

        static void Apply(SlotView view, in WeaponSlotStatus status)
        {
            if (!status.assigned)
            {
                view.background.enabled = false;
                view.overlay.enabled = false;
                view.label.enabled = false;
                view.slash.enabled = false;
                view.border.color = EmptyRingColour;
                return;
            }

            float fill = WeaponSlotLayout.CooldownFill(status.cooldownRemaining, status.cooldownDuration);

            view.background.enabled = true;
            view.label.enabled = true;
            view.overlay.enabled = fill > 0f;
            view.overlay.fillAmount = fill;
            view.slash.enabled = status.blocked;
            view.border.color = status.blocked ? BlockedColour : RingColour;
        }

        SlotView CreateView(int index)
        {
            RectTransform root = HudElements.Rect("Slot" + (index + 1), Root);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(diameter, diameter);
            root.anchoredPosition = new Vector2(WeaponSlotLayout.CentreX(index, SlotCount, diameter, gap), 0f);

            var view = new SlotView();

            view.background = HudElements.Image("Background", root, BackgroundColour);
            view.background.sprite = HudShapes.Circle;
            HudElements.Fill(view.background.rectTransform);

            view.label = HudElements.Text("Key", root, fontSize, TextAnchor.MiddleCenter);
            view.label.text = WeaponSlotLayout.KeyLabel(index);
            HudElements.Fill(view.label.rectTransform);

            view.overlay = HudElements.Image("Cooldown", root, OverlayColour);
            view.overlay.sprite = HudShapes.Circle;
            view.overlay.type = Image.Type.Filled;
            view.overlay.fillMethod = Image.FillMethod.Vertical;
            view.overlay.fillOrigin = (int)Image.OriginVertical.Bottom;   // shrinks toward the bottom: the top clears first
            HudElements.Fill(view.overlay.rectTransform);

            view.border = HudElements.Image("Border", root, RingColour);
            view.border.sprite = _ring;
            HudElements.Fill(view.border.rectTransform);

            view.slash = HudElements.Image("Slash", root, BlockedColour);
            RectTransform slash = view.slash.rectTransform;
            slash.anchorMin = slash.anchorMax = new Vector2(0.5f, 0.5f);
            slash.pivot = new Vector2(0.5f, 0.5f);
            slash.sizeDelta = new Vector2(diameter - borderThickness, slashThickness);
            slash.localRotation = Quaternion.Euler(0f, 0f, -45f);   // top-left to bottom-right

            return view;
        }
    }
}
