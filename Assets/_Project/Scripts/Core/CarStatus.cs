using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Control state a car is in after a ram. Lives in Core so the module that
    /// CAUSES the state (ramming) and the module that OBEYS it (driving) never
    /// reference each other — they meet here.
    ///
    /// Two independent timers. A lock (attacker, and both cars in a head-on)
    /// never shortens a longer lock already running; a reel restarts, so
    /// chaining rams on a helpless car keeps it helpless.
    /// </summary>
    public class CarStatus
    {
        public float LockRemaining { get; private set; }
        public float ReelRemaining { get; private set; }

        public bool IsLocked => LockRemaining > 0f;
        public bool IsReeling => ReelRemaining > 0f;

        /// <summary>False while locked or reeling: throttle and steer are ignored.</summary>
        public bool CanDrive => !IsLocked && !IsReeling;

        /// <summary>A car that cannot drive cannot ram either.</summary>
        public bool CanAttack => CanDrive;

        public void Lock(float seconds)
        {
            LockRemaining = Mathf.Max(LockRemaining, seconds);
        }

        public void Reel(float seconds)
        {
            ReelRemaining = Mathf.Max(0f, seconds);
        }

        public void Advance(float dt)
        {
            LockRemaining = Mathf.Max(0f, LockRemaining - dt);
            ReelRemaining = Mathf.Max(0f, ReelRemaining - dt);
        }
    }
}
