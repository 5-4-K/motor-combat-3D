using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// A centred row of effect chips. Chips are pooled: created the first time
    /// they are needed, then only shown, hidden and relabelled, so a flickering
    /// effect never allocates UI. The owner positions <see cref="Root"/>.
    /// </summary>
    public sealed class EffectChipRow
    {
        static readonly Color LabelColour = new Color(0.05f, 0.05f, 0.05f);

        class Chip
        {
            public RectTransform root;
            public Image background;
            public Text label;
            public string text;
        }

        readonly List<Chip> _chips = new List<Chip>();
        readonly List<ActiveEffect> _active = new List<ActiveEffect>();
        readonly float _chipWidth;
        readonly float _chipHeight;
        readonly float _gap;
        readonly int _fontSize;

        public EffectChipRow(RectTransform parent, string name, float chipWidth, float chipHeight, float gap, int fontSize)
        {
            _chipWidth = chipWidth;
            _chipHeight = chipHeight;
            _gap = gap;
            _fontSize = fontSize;

            Root = HudElements.Rect(name, parent);
            Root.sizeDelta = new Vector2(0f, chipHeight);
        }

        public RectTransform Root { get; }

        public int VisibleCount { get; private set; }

        public string TextAt(int index)
        {
            return _chips[index].text;
        }

        /// <summary>Shows the receiver's active effects in EffectType order; null shows nothing.</summary>
        public void Show(IEffectReceiver receiver)
        {
            if (receiver != null) receiver.GetActive(_active);
            else _active.Clear();

            int count = _active.Count;
            while (_chips.Count < count) _chips.Add(CreateChip());

            for (int i = 0; i < _chips.Count; i++)
            {
                Chip chip = _chips[i];
                bool show = i < count;
                if (chip.root.gameObject.activeSelf != show) chip.root.gameObject.SetActive(show);
                if (!show) continue;

                ActiveEffect effect = _active[i];
                chip.root.anchoredPosition = new Vector2(EffectChipLayout.RowX(i, count, _chipWidth, _gap), 0f);
                chip.background.color = EffectChipLayout.Colour(effect.type);

                string text = EffectChipLayout.Text(effect.type, effect.remaining);
                if (text != chip.text)
                {
                    chip.text = text;
                    chip.label.text = text;
                }
            }

            VisibleCount = count;
        }

        Chip CreateChip()
        {
            RectTransform root = HudElements.Rect("Chip", Root);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(_chipWidth, _chipHeight);

            Image background = HudElements.Image("Background", root, Color.white);
            HudElements.Fill(background.rectTransform);

            Text label = HudElements.Text("Label", root, _fontSize, TextAnchor.MiddleCenter);
            HudElements.Fill(label.rectTransform);
            label.color = LabelColour;

            return new Chip { root = root, background = background, label = label };
        }
    }
}
