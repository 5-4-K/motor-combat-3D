using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MotorCombat.Core
{
    /// <summary>
    /// Compares source keys by reference identity, never by value. Every
    /// system that keys state off a caller-supplied "source" object — ability
    /// blocks, stat modifiers, damage gates, the tick schedule — must treat
    /// two equal-content-but-distinct instances (two boxed ints, two `string`s
    /// built at runtime) as different sources, or an effect built around one
    /// of those types could unblock/replace/remove state that belongs to a
    /// different instance entirely. `System.Collections.Generic.
    /// ReferenceEqualityComparer` is deliberately not used here: this
    /// project's identity contract is a Core concern of its own, not
    /// borrowed from whatever the runtime happens to ship.
    /// </summary>
    internal sealed class ReferenceKeyComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceKeyComparer Instance = new ReferenceKeyComparer();

        bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => obj != null ? RuntimeHelpers.GetHashCode(obj) : 0;
    }
}
