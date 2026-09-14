using System.Collections.Generic;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Which effects a car has, and for how long. The ONLY timer an effect has:
    /// its blocks, modifiers and gates are untimed and removed when this says it
    /// ended, so they can never outlive or undershoot it. At most one entry per
    /// type — the no-stacking rule makes a second copy a restart, never a second entry.
    /// </summary>
    public class EffectSet
    {
        public struct Entry
        {
            public EffectType type;
            public float remaining;
            public float duration;
            public float magnitude;
            public CarController source;
            public string sourceTag;

            /// <summary>Attack fixed at apply; used by Overheated's ticks.</summary>
            public float attack;

            public bool allowNonEnemy;
        }

        readonly Entry[] _entries = new Entry[EffectInfo.Count];
        readonly bool[] _active = new bool[EffectInfo.Count];

        public bool IsActive(EffectType type)
        {
            return EffectInfo.IsKnown(type) && _active[(int)type];
        }

        public bool TryGet(EffectType type, out Entry entry)
        {
            if (!IsActive(type))
            {
                entry = default;
                return false;
            }

            entry = _entries[(int)type];
            return true;
        }

        public float Remaining(EffectType type)
        {
            return IsActive(type) ? _entries[(int)type].remaining : 0f;
        }

        /// <summary>Adds or replaces the entry for its type.</summary>
        public void Set(in Entry entry)
        {
            _entries[(int)entry.type] = entry;
            _active[(int)entry.type] = true;
        }

        public bool Remove(EffectType type)
        {
            if (!IsActive(type)) return false;

            _active[(int)type] = false;
            _entries[(int)type] = default;
            return true;
        }

        /// <summary>Counts every timer down; clears <paramref name="expired"/>, then adds each type that ended, in type order, and removes it.</summary>
        public void Advance(float dt, List<EffectType> expired)
        {
            expired.Clear();

            for (int i = 0; i < _entries.Length; i++)
            {
                if (!_active[i]) continue;

                _entries[i].remaining -= dt;   // infinity minus dt stays infinity
                if (_entries[i].remaining <= EffectRules.ExpiryTolerance)
                {
                    expired.Add((EffectType)i);
                    _active[i] = false;
                    _entries[i] = default;
                }
            }
        }

        /// <summary>Clears <paramref name="into"/>, then adds every active type in type order.</summary>
        public void ActiveTypes(List<EffectType> into)
        {
            into.Clear();
            for (int i = 0; i < _active.Length; i++)
            {
                if (_active[i]) into.Add((EffectType)i);
            }
        }
    }
}
