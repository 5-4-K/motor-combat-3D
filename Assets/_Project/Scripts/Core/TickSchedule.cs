using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MotorCombat.Core
{
    /// <summary>
    /// When a tick of damage (or any periodic effect) is due. The first tick for a
    /// (source, target) pair is immediate; the next is due one interval after the
    /// last. The schedule survives contact breaks, so brushing in and out of a
    /// zone cannot tick faster than its interval.
    /// </summary>
    public class TickSchedule
    {
        // Physics time accumulates in float steps; 25 × 0.02 lands just under 0.5.
        const float Tolerance = 1e-4f;

        /// <summary>
        /// Both halves of the key compare by reference identity — see
        /// ReferenceKeyComparer. A tuple's default comparer instead defers to
        /// each element's own Equals, which for a string or boxed value
        /// compares content, letting an equal-content key tick a pair it was
        /// never given.
        /// </summary>
        sealed class PairComparer : IEqualityComparer<(object source, object target)>
        {
            public static readonly PairComparer Instance = new PairComparer();

            public bool Equals((object source, object target) x, (object source, object target) y)
                => ReferenceEquals(x.source, y.source) && ReferenceEquals(x.target, y.target);

            public int GetHashCode((object source, object target) pair)
            {
                int sourceHash = pair.source != null ? RuntimeHelpers.GetHashCode(pair.source) : 0;
                int targetHash = pair.target != null ? RuntimeHelpers.GetHashCode(pair.target) : 0;
                return (sourceHash * 397) ^ targetHash;
            }
        }

        readonly Dictionary<(object source, object target), float> _lastTick =
            new Dictionary<(object source, object target), float>(PairComparer.Instance);
        readonly List<(object source, object target)> _forget = new List<(object source, object target)>();

        /// <summary>Returns true, and records the tick, when one is due.</summary>
        public bool TryTick(object source, object target, float now, float interval)
        {
            var key = (source, target);
            if (_lastTick.TryGetValue(key, out float last) && now < last + interval - Tolerance)
            {
                return false;
            }

            _lastTick[key] = now;
            return true;
        }

        /// <summary>Clears every entry for a target, e.g. when it is removed.</summary>
        public void Forget(object target)
        {
            _forget.Clear();
            foreach (var key in _lastTick.Keys)
            {
                if (ReferenceEquals(key.target, target)) _forget.Add(key);
            }
            for (int i = 0; i < _forget.Count; i++) _lastTick.Remove(_forget[i]);
        }
    }
}
