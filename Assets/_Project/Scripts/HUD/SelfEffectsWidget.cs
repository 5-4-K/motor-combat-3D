using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The viewer's own active effects, as chips centred above the self health
    /// bar — in first person you never see your own car, so its state lives on
    /// the screen.
    /// </summary>
    public class SelfEffectsWidget : MonoBehaviour
    {
        public CarController viewer;

        [Tooltip("Reference pixels.")] public float chipWidth = 72f;
        [Tooltip("Reference pixels.")] public float chipHeight = 24f;
        [Tooltip("Reference pixels.")] public float gap = 6f;
        public int fontSize = 15;
        [Tooltip("Reference pixels from the bottom of the screen; clears the self health bar and its label.")] public float bottomMargin = 96f;

        EffectChipRow _row;
        IEffectReceiver _receiver;
        CarController _receiverOwner;

        public void Build(RectTransform canvas)
        {
            _row = new EffectChipRow(canvas, "SelfEffects", chipWidth, chipHeight, gap, fontSize);
            RectTransform root = _row.Root;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, bottomMargin);
        }

        void LateUpdate()
        {
            if (_row == null) return;

            if (viewer != _receiverOwner)
            {
                _receiverOwner = viewer;
                _receiver = viewer != null ? viewer.GetComponent<IEffectReceiver>() : null;
            }

            _row.Show(viewer != null ? _receiver : null);
        }
    }
}
