using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    /// <summary>
    /// A spent shot (hit something, or ran out of range) must not sweep again if
    /// Advance runs a second time before its deferred Destroy() takes effect.
    /// </summary>
    public class ShotTests
    {
        Shot _shot;

        [TearDown]
        public void TearDown()
        {
            // In EditMode, Perish() already destroys the GameObject immediately
            // (Destroy() is illegal outside play mode), so this is a no-op on the
            // happy path — it only matters if a test left the shot alive.
            if (_shot != null) Object.DestroyImmediate(_shot.gameObject);
        }

        [Test]
        public void SpentShot_DoesNotAdvanceOrHitAgainOnASecondCall()
        {
            var launch = new ShotLaunch
            {
                source = null,
                sourceTag = "test",
                origin = new Vector3(0f, 1000f, 0f),   // far from any collider
                direction = Vector3.forward,
                settings = new ShotSettings { speed = 10f, range = 0.5f, radius = 0.1f },
                payload = new Payload { damageKind = DamageKind.Flat, damageAmount = 0f, effects = new EffectSpec[0] },
                attack = 100f
            };

            _shot = Shot.Launch(launch);

            // dt = 1s at speed 10 would ask for a 10m step, but StepDistance caps
            // it at the remaining range (0.5m), so this single Advance both moves
            // the shot the full 0.5m and exhausts its range in the same call.
            _shot.Advance(1f);

            Assert.IsTrue(_shot.Spent, "range ran out, so the shot must be marked spent");

            // EditMode has no deferred-destroy frame boundary: Perish() calls
            // DestroyImmediate synchronously, so the GameObject is already gone
            // by the time Advance returns. Reading its position is therefore not
            // meaningful here — Unity's overloaded == is how a destroyed
            // MonoBehaviour reports itself as gone.
            Assert.IsTrue(_shot == null, "a spent shot's GameObject is destroyed immediately in EditMode");

            // The regression this guards against: a second Advance on an
            // already-spent shot must be a pure no-op — no exception, and (since
            // _spent short-circuits before any Unity API call) no attempt to
            // touch the now-destroyed GameObject.
            Assert.DoesNotThrow(() => _shot.Advance(1f));

            _shot = null; // already destroyed; nothing left for TearDown to clean up
        }
    }
}
