using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Global per-effect rules, the same for every source. How big and how long
    /// an effect is comes from whatever applies it (RamConfig, later weapon
    /// configs). Every value here is a placeholder — tuning belongs to the designer.
    /// </summary>
    [CreateAssetMenu(menuName = "Motor Combat/Effects Config", fileName = "EffectsConfig")]
    public class EffectsConfig : ScriptableObject
    {
        const string StackTip = "On: a new copy landing on a car that already has this effect restarts its timer, and the new size replaces the old. Off: the new copy does nothing.";

        [Header("Stacking")]
        [Tooltip(StackTip)] public bool stunnedStacks;
        [Tooltip(StackTip)] public bool suppressedStacks;
        [Tooltip(StackTip)] public bool overheatedStacks;
        [Tooltip(StackTip)] public bool corrodedStacks;
        [Tooltip(StackTip)] public bool reelingStacks = true;
        [Tooltip(StackTip)] public bool spikedStacks;
        [Tooltip(StackTip)] public bool fortifiedStacks;
        [Tooltip(StackTip)] public bool armoredStacks;
        [Tooltip(StackTip)] public bool exhaustedStacks;

        [Header("Overheated")]
        [Tooltip("Flat: magnitude is hit points per tick. MaxHealthPercent: magnitude is percent of the victim's max health per tick. Applies to every source of Overheated.")]
        public DamageKind overheatedDamageKind = DamageKind.Flat;

        [Tooltip("Seconds between ticks, for every source. The first tick lands when the effect does.")]
        [Min(0.02f)]
        public float overheatedTickSeconds = 1f;

        [Header("Reeling")]
        [Tooltip("Rate in 1/s at which a reeling car's spin decays, as exp(-rate × dt).")]
        public float reelingSpinDecayRate = 2f;

        [Header("Debug (Inspector context menus on CarEffects)")]
        [Tooltip("Seconds.")]
        [Min(0.01f)]
        public float debugDuration = 3f;

        [Tooltip("Percent, for Corroded, Spiked, Fortified and Exhausted.")]
        [Min(0f)]
        public float debugPercent = 30f;

        [Tooltip("Damage per tick for Overheated, in the damage kind above.")]
        [Min(0f)]
        public float debugOverheatAmount = 20f;

        public bool Stacks(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return stunnedStacks;
                case EffectType.Suppressed: return suppressedStacks;
                case EffectType.Overheated: return overheatedStacks;
                case EffectType.Corroded: return corrodedStacks;
                case EffectType.Reeling: return reelingStacks;
                case EffectType.Spiked: return spikedStacks;
                case EffectType.Fortified: return fortifiedStacks;
                case EffectType.Armored: return armoredStacks;
                case EffectType.Exhausted: return exhaustedStacks;
                default: return false;   // Overhauled is instant; unknown types never stack
            }
        }
    }
}
