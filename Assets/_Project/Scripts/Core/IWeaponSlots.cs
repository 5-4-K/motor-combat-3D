namespace MotorCombat.Core
{
    /// <summary>What the HUD shows for one weapon slot.</summary>
    public struct WeaponSlotStatus
    {
        /// <summary>The slot holds a weapon that passed validation.</summary>
        public bool assigned;

        public float cooldownRemaining;
        public float cooldownDuration;

        /// <summary>Can't fire for a reason other than its own cooldown: Fire blocked, a wreck, or another slot's recovery.</summary>
        public bool blocked;
    }

    /// <summary>A car's weapon slots, read-only. Implemented by the Weapons assembly; read by the HUD.</summary>
    public interface IWeaponSlots
    {
        int SlotCount { get; }

        /// <summary>Out of range returns an unassigned status.</summary>
        WeaponSlotStatus GetStatus(int slot);
    }
}
