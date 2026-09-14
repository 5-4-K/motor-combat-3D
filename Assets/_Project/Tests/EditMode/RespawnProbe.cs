using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    /// <summary>Records respawn resets for CarRespawnTests.</summary>
    public class RespawnProbe : MonoBehaviour, IRespawnable
    {
        public int calls;
        public bool activeWhenReset;

        public void ResetForRespawn()
        {
            calls++;
            activeWhenReset = gameObject.activeSelf;
        }
    }
}
