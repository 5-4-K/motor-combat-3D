using System;
using System.Collections.Generic;

namespace MotorCombat.Core
{
    /// <summary>What a car is currently allowed to do. A block switches one or more of these off.</summary>
    [Flags]
    public enum CarAbility
    {
        None = 0,

        /// <summary>Throttle input is used.</summary>
        Throttle = 1,

        /// <summary>Steer input is used.</summary>
        Steer = 2,

        /// <summary>Driving writes yaw every step. Off = the car spins freely.</summary>
        YawHold = 4,

        /// <summary>Lateral grip is applied. Off = the car slides freely.</summary>
        Grip = 8,

        /// <summary>Weapons may fire.</summary>
        Fire = 16,

        /// <summary>The car may qualify as a ram attacker.</summary>
        Ram = 32,

        /// <summary>The car may take damage and be rammed.</summary>
        Targetable = 64
    }

    /// <summary>What happens when a source that already has an active block blocks again.</summary>
    public enum BlockRefresh
    {
        /// <summary>Keep whichever time is longer. Ram lock.</summary>
        KeepLonger,

        /// <summary>Start the full duration again. Ram reel.</summary>
        Restart,

        /// <summary>Do nothing while active. The non-stacking rule for effects.</summary>
        IgnoreIfActive
    }

    /// <summary>
    /// Ability switches. Any system switches abilities off by registering a block
    /// under its own source key; nothing here knows about rams, effects or
    /// wrecks. Lives in Core so the module that CAUSES a block and the module
    /// that OBEYS it never reference each other.
    ///
    /// Blocks from different sources never interact: an ability is available
    /// only when no active block covers it.
    /// </summary>
    public class CarAbilities
    {
        struct Entry
        {
            public CarAbility abilities;
            public float remaining;
        }

        readonly Dictionary<object, Entry> _blocks = new Dictionary<object, Entry>();
        readonly List<object> _keys = new List<object>();

        /// <param name="seconds">Duration; <see cref="float.PositiveInfinity"/> for an untimed block. A new block of zero or less is ignored.</param>
        public void Block(object source, CarAbility abilities, float seconds, BlockRefresh refresh)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (_blocks.TryGetValue(source, out Entry entry))
            {
                switch (refresh)
                {
                    case BlockRefresh.IgnoreIfActive:
                        return;
                    case BlockRefresh.KeepLonger:
                        entry.remaining = Math.Max(entry.remaining, seconds);
                        break;
                    default:
                        entry.remaining = seconds;
                        break;
                }

                entry.abilities = abilities;
                _blocks[source] = entry;
                return;
            }

            if (seconds <= 0f) return;

            _blocks[source] = new Entry { abilities = abilities, remaining = seconds };
        }

        public void Unblock(object source)
        {
            if (source != null) _blocks.Remove(source);
        }

        public void UnblockAll()
        {
            _blocks.Clear();
        }

        /// <summary>True only if no active block covers any of the requested flags.</summary>
        public bool Has(CarAbility ability)
        {
            foreach (Entry entry in _blocks.Values)
            {
                if ((entry.abilities & ability) != 0) return false;
            }
            return true;
        }

        public bool IsBlockedBy(object source)
        {
            return source != null && _blocks.ContainsKey(source);
        }

        /// <summary>Seconds left on a source's block, or 0 when it has none.</summary>
        public float Remaining(object source)
        {
            return source != null && _blocks.TryGetValue(source, out Entry entry) ? entry.remaining : 0f;
        }

        /// <summary>Counts every timed block down and removes the expired ones.</summary>
        public void Advance(float dt)
        {
            _keys.Clear();
            _keys.AddRange(_blocks.Keys);

            for (int i = 0; i < _keys.Count; i++)
            {
                Entry entry = _blocks[_keys[i]];
                entry.remaining -= dt;   // infinity minus dt stays infinity

                if (entry.remaining <= 0f) _blocks.Remove(_keys[i]);
                else _blocks[_keys[i]] = entry;
            }
        }
    }
}
