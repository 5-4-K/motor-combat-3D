namespace MotorCombat.Core
{
    /// <summary>
    /// A car component holding per-life state. CarRespawn resets every one on
    /// the car, so a later module (weapon cooldowns) joins respawn by
    /// implementing this, without touching the respawn code.
    /// </summary>
    public interface IRespawnable
    {
        void ResetForRespawn();
    }
}
