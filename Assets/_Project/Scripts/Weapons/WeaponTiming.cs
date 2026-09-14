using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>One slot's timers, as absolute Time.fixedTime seconds — they keep running while the car is a wreck.</summary>
    public struct SlotTiming
    {
        public float cooldownEndsAt;
        public bool windingUp;
        public float releaseAt;

        /// <summary>The car's effective attack when the slot was pressed.</summary>
        public float attack;

        public static SlotTiming Ready => new SlotTiming { cooldownEndsAt = float.NegativeInfinity };
    }

    /// <summary>The car-wide recovery lock.</summary>
    public struct LockTiming
    {
        public float endsAt;

        /// <summary>The slot whose press started it; −1 for none.</summary>
        public int owner;

        public static LockTiming None => new LockTiming { endsAt = float.NegativeInfinity, owner = -1 };
    }

    /// <summary>
    /// Cooldown, wind-up and recovery. Cooldown and recovery both start on the
    /// press; the recovery lock blocks every slot, the one that started it included.
    /// </summary>
    public static class WeaponTiming
    {
        public const float Tolerance = 1e-4f;

        public static bool CanPress(in SlotTiming slot, bool valid, bool fireAllowed, in LockTiming lockTiming, float now)
        {
            return valid
                && fireAllowed
                && !slot.windingUp
                && now >= slot.cooldownEndsAt - Tolerance
                && now >= lockTiming.endsAt - Tolerance;
        }

        /// <summary>Starts cooldown and lock and snapshots attack. Returns true when the shot leaves in this same step.</summary>
        public static bool Press(ref SlotTiming slot, ref LockTiming lockTiming, int slotIndex, float now, float cooldown, float windUp, float recovery, float attack)
        {
            slot.cooldownEndsAt = now + cooldown;
            slot.attack = attack;
            lockTiming.endsAt = now + recovery;
            lockTiming.owner = slotIndex;

            if (windUp <= Tolerance)
            {
                slot.windingUp = false;
                return true;
            }

            slot.windingUp = true;
            slot.releaseAt = now + windUp;
            return false;
        }

        public static bool ShouldRelease(in SlotTiming slot, float now)
        {
            return slot.windingUp && now >= slot.releaseAt - Tolerance;
        }

        /// <summary>The shot never leaves. Cooldown and lock keep running.</summary>
        public static void Cancel(ref SlotTiming slot)
        {
            slot.windingUp = false;
        }

        public static float CooldownRemaining(in SlotTiming slot, float now)
        {
            return Mathf.Max(0f, slot.cooldownEndsAt - now);
        }

        /// <summary>
        /// Can't fire for a reason other than its own cooldown. The lock's own slot
        /// is on cooldown for all of its lock (cooldown ≥ recovery), so only other
        /// slots count as blocked by it.
        /// </summary>
        public static bool IsBlocked(bool valid, bool fireAllowed, in LockTiming lockTiming, int slotIndex, float now)
        {
            if (!valid) return false;
            if (!fireAllowed) return true;
            return now < lockTiming.endsAt - Tolerance && lockTiming.owner != slotIndex;
        }

        public static void ResetForRespawn(SlotTiming[] slots, ref LockTiming lockTiming)
        {
            for (int i = 0; i < slots.Length; i++) slots[i].windingUp = false;
            lockTiming = LockTiming.None;
        }
    }
}
