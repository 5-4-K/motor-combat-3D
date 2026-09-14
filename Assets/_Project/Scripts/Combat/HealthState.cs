using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// The damage path as a plain class: targetable → hostility → gates → raw
    /// amount → mitigation → apply. The caller supplies the facts that need a
    /// scene (targetable, hostile, attack, defense), so every rule is testable
    /// without one.
    /// </summary>
    public class HealthState
    {
        readonly List<KeyValuePair<object, Func<DamageRequest, bool>>> _gates =
            new List<KeyValuePair<object, Func<DamageRequest, bool>>>();

        public float Max { get; }
        public float Current { get; private set; }
        public bool IsDestroyed { get; private set; }

        public HealthState(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public void AddGate(object source, Func<DamageRequest, bool> blocks)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));

            RemoveGate(source);
            _gates.Add(new KeyValuePair<object, Func<DamageRequest, bool>>(source, blocks));
        }

        public void RemoveGate(object source)
        {
            for (int i = _gates.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_gates[i].Key, source)) _gates.RemoveAt(i);
            }
        }

        /// <summary>
        /// Back to full health and alive. Gates stay: each belongs to its
        /// source (for example Armored), which removes it itself.
        /// </summary>
        public void Revive()
        {
            Current = Max;
            IsDestroyed = false;
        }

        public DamageResult Apply(in DamageRequest request, bool targetable, bool hostile, float sourceAttack, float targetDefense)
        {
            if (!targetable || IsDestroyed) return DamageResult.Of(DamageOutcome.NotTargetable);
            if (!request.allowNonEnemy && !hostile) return DamageResult.Of(DamageOutcome.NotHostile);

            for (int i = 0; i < _gates.Count; i++)
            {
                if (_gates[i].Value(request)) return DamageResult.Of(DamageOutcome.Blocked);
            }

            float raw = DamageRules.RawAmount(request.kind, request.amount, Max);
            float mitigated = DamageRules.Mitigate(raw, sourceAttack, targetDefense);

            // Written as !(x > 0) so a NaN from a broken config is rejected too.
            if (!(mitigated > 0f)) return DamageResult.Of(DamageOutcome.ZeroAmount);

            float dealt = Mathf.Min(mitigated, Current);
            Current -= dealt;

            bool killed = false;
            if (Current <= 0f)
            {
                Current = 0f;
                IsDestroyed = true;
                killed = true;
            }

            return new DamageResult { outcome = DamageOutcome.Applied, dealt = dealt, killed = killed };
        }
    }
}
