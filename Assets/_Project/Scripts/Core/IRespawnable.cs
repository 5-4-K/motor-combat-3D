namespace MotorCombat.Core
{
    /// <summary>
    /// A car component holding per-life state. CarRespawn resets every one on
    /// the car, so a later module (weapon cooldowns) joins respawn by
    /// implementing this, without touching the respawn code. Component order
    /// is not guaranteed, so a reset must only clear its own state — never add
    /// a block or a stat modifier, since Health.ResetForRespawn clears every
    /// block and modifier on the car and may run before or after this one.
    /// </summary>
    public interface IRespawnable
    {
        void ResetForRespawn();
    }
}
