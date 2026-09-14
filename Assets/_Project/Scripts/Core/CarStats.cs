using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorCombat.Core
{
    public enum CarStat
    {
        /// <summary>Damage multiplier; 100 deals a weapon's listed damage.</summary>
        Attack,

        /// <summary>Damage reduction with diminishing returns.</summary>
        Defense,

        /// <summary>Ram shove dealt.</summary>
        Strength,

        /// <summary>Ram shove received.</summary>
        Resistance,

        /// <summary>Multiplier on engine force, and so on terminal speed. Base 1.</summary>
        TopSpeed
    }

    /// <summary>
    /// Base stats plus percentage modifiers. Lives in Core so the systems that
    /// modify stats (effects) and the systems that read them (damage, ramming,
    /// driving) never reference each other.
    ///
    /// effective = max(0, base × (1 + Σ percent / 100)). Percentages from
    /// different sources ADD, so the order effects were applied never matters.
    /// </summary>
    public class CarStats
    {
        struct Modifier
        {
            public object source;
            public CarStat stat;
            public float percent;
        }

        static readonly int StatCount = Enum.GetValues(typeof(CarStat)).Length;

        readonly float[] _base = new float[StatCount];
        readonly List<Modifier> _modifiers = new List<Modifier>();

        public CarStats()
        {
            _base[(int)CarStat.TopSpeed] = 1f;
        }

        public void SetBase(CarStat stat, float value)
        {
            _base[(int)stat] = value;
        }

        public float Base(CarStat stat)
        {
            return _base[(int)stat];
        }

        public float Effective(CarStat stat)
        {
            float percent = 0f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].stat == stat) percent += _modifiers[i].percent;
            }
            return Mathf.Max(0f, _base[(int)stat] * (1f + percent / 100f));
        }

        /// <summary>Adds a percentage modifier. The same source on the same stat replaces its previous one.</summary>
        public void Add(object source, CarStat stat, float percent)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (ReferenceEquals(_modifiers[i].source, source) && _modifiers[i].stat == stat)
                {
                    _modifiers[i] = new Modifier { source = source, stat = stat, percent = percent };
                    return;
                }
            }

            _modifiers.Add(new Modifier { source = source, stat = stat, percent = percent });
        }

        /// <summary>Removes every modifier registered by this source, on every stat.</summary>
        public void Remove(object source)
        {
            _modifiers.RemoveAll(m => ReferenceEquals(m.source, source));
        }

        public void RemoveAll()
        {
            _modifiers.Clear();
        }

        /// <summary>True when this source has registered a modifier on any stat, by reference identity.</summary>
        public bool Has(object source)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (ReferenceEquals(_modifiers[i].source, source)) return true;
            }
            return false;
        }
    }
}
