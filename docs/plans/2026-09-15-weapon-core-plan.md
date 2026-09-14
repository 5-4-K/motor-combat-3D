# Weapon Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three-slot weapons with cooldown / wind-up / recovery, fixed and turret muzzles at one fire height, composed hurtboxes, a basic swept shot, a reusable hit payload (damage, effects, push + reel), a slots HUD and test weapons — weapons sub-project 3 of 6.

**Architecture:** Every decision lives in a pure static in `MotorCombat.Weapons` (`WeaponRules`, `WeaponTiming`, `MuzzleRules`, `ShotRules`, `PayloadRules`) or Core (`HurtboxRules`, `PushMath`); `WeaponModule`, `Shot` and `PayloadApplier` are thin adapters. Weapons references Core only; the HUD reads weapons through the Core interface `IWeaponSlots`.

**Tech Stack:** Unity 6 (6000.6.0f1), URP, C#, Unity Input System, NUnit EditMode tests via `unity test . --mode EditMode`.

**Spec:** `docs/specs/2026-09-15-weapon-core-design.md`

## Global Constraints

- Unity `6000.6.0f1`. **The Editor must be closed** for `unity test` and `unity run`; after a run, confirm `test-results.xml` was rewritten (a failed run leaves the old one).
- Test command: `unity test . --mode EditMode` from the repo root `E:\Work\motor-combat-3D`.
- Gameplay assemblies reference **`MotorCombat.Core` only**. `MotorCombat.Weapons` must not reference Combat, Effects, Ramming, Driving or anything else. HUD references Core (+ `UnityEngine.UI`) only.
- Decisions in pure statics; MonoBehaviours stay thin.
- Every decay is `exp(-rate × dt)`; never `value *= (1 - rate)`.
- Tuning numbers are the user's placeholders; copy them verbatim from the spec, never retune.
- Work on `main`; commit per task; **never push**. Commit messages end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- Stage `Assets/_Project` wholesale (folder `.meta` files sit one level above the folder), then check `git status --porcelain`.
- Test runs may rewrite `ProjectSettings/DynamicsManager.asset`; revert it before committing unless the task changes it.
- EditMode traps: MonoBehaviours defined in the test assembly can't be `AddComponent`'d (use real runtime components); `Awake`/`Start` never run; `Time.fixedTime` is not 0 (capture `_t0 = Time.fixedTime` in SetUp and pass times relative to it); Rigidbody velocity set/read-back works.
- An unexpected `Debug.LogError` fails an EditMode test; expect intentional ones with `LogAssert.Expect`.
- Never regenerate `Arena.unity` with the headless `ArenaSceneBuilder.BuildScene` (it drops URP camera/light data). This plan needs no scene change.
- Log prefixes: `[MotorCombat]` for configuration errors, `[Weapons]` for gameplay logs.

## File map

| File | Task | Responsibility |
|---|---|---|
| `Scripts/Core/CarInput.cs` (modify) | 1 | `firePressed` bits |
| `Scripts/Core/HurtboxBox.cs`, `Hurtbox.cs`, `HurtboxRules.cs` | 1 | Hurtbox data, collider→car map, fire-height coverage |
| `Scripts/Core/PhysicsLayers.cs` (modify), `ProjectSettings/TagManager.asset` (modify) | 1 | `Hurtbox` layer 11, ignores all |
| `Scripts/Core/PushMath.cs`; `Scripts/Ramming/RamRules.cs` (modify) | 1 | Shared spin formula |
| `Scripts/Core/IWeaponSlots.cs` | 1 | HUD read contract |
| `Scripts/Weapons/WeaponsConfig.cs`, `WeaponTypes.cs`, `WeaponConfig.cs`, `WeaponRules.cs` | 2 | Data + validation |
| `Scripts/Weapons/WeaponTiming.cs`, `MuzzleRules.cs`, `ShotRules.cs`, `PayloadRules.cs` | 3 | Pure rules |
| `Scripts/Weapons/PayloadApplier.cs`, `Shot.cs` | 4 | Apply a payload; the swept shot |
| `Scripts/Weapons/WeaponModule.cs` (rewrite), `IWeapon.cs` (delete); `Scripts/Controls/LocalInputProvider.cs` (modify) | 5 | Slots, timing, release; fire keys |
| `Scripts/Cars/CarDefinition.cs`, `CarFactory.cs` (modify) | 6 | Loadout, hurtboxes, wiring |
| `Scripts/HUD/HudShapes.cs`, `WeaponSlotLayout.cs`, `WeaponSlotsWidget.cs`; `Scripts/Bootstrap/GameBootstrap.cs` (modify) | 7 | Slots HUD |
| `Editor/ConfigAssetBootstrap.cs` (modify); `Configs/WeaponsConfig.asset`, `Configs/Weapons/*.asset`, `Configs/CarDefinition.asset`; docs | 8 | Assets and documentation |

All `Scripts/…`, `Tests/…`, `Editor/…` and `Configs/…` paths are under `Assets/_Project/`.

---

### Task 1: Core contracts — fire input, hurtboxes, Hurtbox layer, PushMath, IWeaponSlots

**Files:**
- Modify: `Assets/_Project/Scripts/Core/CarInput.cs`
- Create: `Assets/_Project/Scripts/Core/HurtboxBox.cs`, `Hurtbox.cs`, `HurtboxRules.cs`, `PushMath.cs`, `IWeaponSlots.cs`
- Modify: `Assets/_Project/Scripts/Core/PhysicsLayers.cs`, `ProjectSettings/TagManager.asset`, `Assets/_Project/Scripts/Ramming/RamRules.cs`
- Test: `Assets/_Project/Tests/EditMode/HurtboxRulesTests.cs`, `PushMathTests.cs`; modify `PhysicsLayersTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `CarInput.firePressed` (int); `HurtboxBox { Vector3 centre; Vector3 size; }`; `Hurtbox : MonoBehaviour { const string ObjectName = "Hurtbox"; CarController Car { get; set; } }`; `HurtboxRules.SpansHeight(HurtboxBox[] boxes, float carBoxHeight, float height) → bool`; `PhysicsLayers.HurtboxName`, `PhysicsLayers.Hurtbox`; `PushMath.SpinDelta(Vector3 contactPoint, Vector3 centre, Vector3 pushDelta, float width, float length, float spinScale) → float`; `WeaponSlotStatus { bool assigned; float cooldownRemaining; float cooldownDuration; bool blocked; }`; `IWeaponSlots { int SlotCount { get; } WeaponSlotStatus GetStatus(int slot); }`.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Tests/EditMode/HurtboxRulesTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class HurtboxRulesTests
    {
        const float CarHeight = 1.2f;   // floor is at local y = -0.6

        static HurtboxBox Box(float centreY, float sizeY)
        {
            return new HurtboxBox { centre = new Vector3(0f, centreY, 0f), size = new Vector3(2f, sizeY, 4f) };
        }

        [Test]
        public void SpansHeight_FullCarBox_CoversTheFireHeight()
        {
            Assert.IsTrue(HurtboxRules.SpansHeight(new[] { Box(0f, 1.2f) }, CarHeight, 0.6f));
        }

        [Test]
        public void SpansHeight_BoxAboveTheFireHeight_DoesNot()
        {
            // Box from 0.8 to 1.2 above the floor.
            Assert.IsFalse(HurtboxRules.SpansHeight(new[] { Box(0.4f, 0.4f) }, CarHeight, 0.6f));
        }

        [Test]
        public void SpansHeight_AnyOneBoxIsEnough()
        {
            Assert.IsTrue(HurtboxRules.SpansHeight(new[] { Box(0.4f, 0.4f), Box(-0.3f, 0.6f) }, CarHeight, 0.6f));
        }

        [Test]
        public void SpansHeight_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(HurtboxRules.SpansHeight(null, CarHeight, 0.6f));
            Assert.IsFalse(HurtboxRules.SpansHeight(new HurtboxBox[0], CarHeight, 0.6f));
        }
    }
}
```

`Assets/_Project/Tests/EditMode/PushMathTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Ramming;

namespace MotorCombat.Tests
{
    public class PushMathTests
    {
        const float W = 2f;
        const float L = 4f;

        [Test]
        public void SpinDelta_ThroughTheCentre_IsZero()
        {
            Assert.AreEqual(0f, PushMath.SpinDelta(Vector3.zero, Vector3.zero, new Vector3(5f, 0f, 0f), W, L, 1f), 1e-5f);
        }

        /// <summary>k² = (2² + 4²) / 12 = 5/3; cross((0,0,−2), (1,0,0)).y = −2; −2 / (5/3) = −1.2 rad/s.</summary>
        [Test]
        public void SpinDelta_TailPushedRight_SpinsNoseLeft()
        {
            Assert.AreEqual(-1.2f, PushMath.SpinDelta(new Vector3(0f, 0f, -2f), Vector3.zero, new Vector3(1f, 0f, 0f), W, L, 1f), 1e-4f);
        }

        [Test]
        public void RamRulesSpinDelta_MatchesPushMath()
        {
            var contact = new Vector3(0.7f, 0.3f, -1.9f);
            var shove = new Vector3(3f, 0f, 1f);
            Assert.AreEqual(
                PushMath.SpinDelta(contact, Vector3.zero, shove, W, L, 0.8f),
                RamRules.SpinDelta(contact, Vector3.zero, shove, W, L, 0.8f),
                1e-6f);
        }
    }
}
```

In `Assets/_Project/Tests/EditMode/PhysicsLayersTests.cs` add a `_hurtboxRow` captured and restored exactly like `_carRow` (add `_hurtboxRow = Capture(PhysicsLayers.Hurtbox);` to SetUp and `Restore(PhysicsLayers.Hurtbox, _hurtboxRow);` to TearDown), add `Assert.AreEqual(11, PhysicsLayers.Hurtbox);` to `LayerNames_ResolveToTheReservedIndices`, and add:

```csharp
        [Test]
        public void ConfigureCollisions_HurtboxTouchesNothing()
        {
            PhysicsLayers.ConfigureCollisions();

            for (int layer = 0; layer < 32; layer++)
            {
                Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Hurtbox, layer), "layer " + layer);
            }

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Car, PhysicsLayers.Car), "car rules untouched");
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Arena), "wreck rules untouched");
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `HurtboxRules`, `HurtboxBox`, `PushMath`, `PhysicsLayers.Hurtbox` do not exist.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/Core/CarInput.cs` — add after `aimDeltaX`:

```csharp
        /// <summary>
        /// Bit i is set when weapon slot i's key went down this frame (slot 0 = LMB,
        /// 1 = RMB, 2 = Space). A per-frame event like aimDeltaX: the weapon module
        /// consumes it once, never per physics step.
        /// </summary>
        public int firePressed;
```

`Assets/_Project/Scripts/Core/HurtboxBox.cs`:

```csharp
using System;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>One box of a car's hurtbox, in the car's local space relative to its centre. No rotation.</summary>
    [Serializable]
    public struct HurtboxBox
    {
        public Vector3 centre;
        public Vector3 size;
    }
}
```

`Assets/_Project/Scripts/Core/Hurtbox.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Sits on a car's hurtbox child, beside its trigger boxes, and maps any of
    /// them back to the car. Weapons reach it only through queries: the Hurtbox
    /// layer collides with nothing.
    /// </summary>
    public class Hurtbox : MonoBehaviour
    {
        public const string ObjectName = "Hurtbox";

        public CarController Car { get; set; }
    }
}
```

`Assets/_Project/Scripts/Core/HurtboxRules.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Core
{
    public static class HurtboxRules
    {
        /// <summary>
        /// True when at least one box's vertical range, measured from the car's
        /// floor, contains <paramref name="height"/>. The car root is the centre
        /// of its collision box, so the floor is at local y = −carBoxHeight / 2.
        /// </summary>
        public static bool SpansHeight(HurtboxBox[] boxes, float carBoxHeight, float height)
        {
            if (boxes == null) return false;

            float floor = -carBoxHeight * 0.5f;
            for (int i = 0; i < boxes.Length; i++)
            {
                float half = Mathf.Abs(boxes[i].size.y) * 0.5f;
                float bottom = boxes[i].centre.y - half - floor;
                float top = boxes[i].centre.y + half - floor;
                if (height >= bottom && height <= top) return true;
            }

            return false;
        }
    }
}
```

`Assets/_Project/Scripts/Core/PushMath.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>Shared by rams and weapon pushes. Mass is ignored everywhere a push is applied.</summary>
    public static class PushMath
    {
        /// <summary>
        /// Yaw rate (rad/s) a velocity change applied at a contact point gives a solid
        /// box of that footprint: spinScale × cross(r, Δv).y / k², with r the flat
        /// offset from the centre and k² = (width² + length²) / 12.
        /// </summary>
        public static float SpinDelta(Vector3 contactPoint, Vector3 centre, Vector3 pushDelta, float width, float length, float spinScale)
        {
            Vector3 offset = contactPoint - centre;
            offset.y = 0f;

            float radiusOfGyrationSquared = (width * width + length * length) / 12f;
            if (radiusOfGyrationSquared <= 0f) return 0f;

            return spinScale * Vector3.Cross(offset, pushDelta).y / radiusOfGyrationSquared;
        }
    }
}
```

`Assets/_Project/Scripts/Ramming/RamRules.cs` — add `using MotorCombat.Core;` at the top (it currently has only `using UnityEngine;`) and replace the body of `SpinDelta` (keep its signature and doc comment) with:

```csharp
            return PushMath.SpinDelta(contactPoint, victimCentre, shoveDelta, width, length, spinScale);
```

`Assets/_Project/Scripts/Core/IWeaponSlots.cs`:

```csharp
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
```

`Assets/_Project/Scripts/Core/PhysicsLayers.cs` — add beside the other names:

```csharp
        public const string HurtboxName = "Hurtbox";
        public static int Hurtbox => LayerMask.NameToLayer(HurtboxName);
```

and append at the end of `ConfigureCollisions()` (after the car/arena lines):

```csharp
            // Hurtboxes are reached only by weapon queries, never by contacts or
            // trigger events.
            int hurtbox = Hurtbox;
            if (hurtbox < 0)
            {
                Debug.LogError($"[MotorCombat] Physics layer '{HurtboxName}' must exist in Project Settings > Tags and Layers.");
                return;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(hurtbox, layer, true);
            }
```

Update the class doc comment's last paragraph to "Later sub-projects add their own layers beside these; `Hurtbox` (sub-project 3) touches nothing."

`ProjectSettings/TagManager.asset` — index 11 is the first empty entry after `Wreck`. Replace

```
  - Wreck
  - 
```

with

```
  - Wreck
  - Hurtbox
```

(exactly one line changes).

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (280 existing + 8 new = 288; `RamRulesTests` unchanged and green).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project ProjectSettings/TagManager.asset
git status --porcelain
git commit -m "Add fire input, hurtbox contracts, the Hurtbox layer, PushMath and IWeaponSlots to Core

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Weapon data and validation

**Files:**
- Create: `Assets/_Project/Scripts/Weapons/WeaponsConfig.cs`, `WeaponTypes.cs`, `WeaponConfig.cs`, `WeaponRules.cs`
- Modify: `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef` (add `"MotorCombat.Weapons"` after `"MotorCombat.Effects"`)
- Test: `Assets/_Project/Tests/EditMode/WeaponRulesTests.cs`

**Interfaces:**
- Consumes: `EffectType`, `EffectInfo.IsKnown`, `EffectInfo.IsTimed`, `DamageKind` (Core).
- Produces: `WeaponsConfig { float fireHeight = 0.6f; }`; enums `WeaponDelivery { Shot }`, `MuzzleKind { Turret, Fixed }`, `[Flags] FixedMuzzles { None = 0, Front = 1, Rear = 2, Left = 4, Right = 8 }`, `PushDirection { AlongTravel, AwayFromCentre }`; structs `ShotSettings { float speed, range, radius; }`, `EffectSpec { EffectType type; float magnitude; float duration; }`, `PushSpec { float speed; PushDirection direction; float spinScale; float reelSeconds; }`, `Payload { DamageKind damageKind; float damageAmount; EffectSpec[] effects; PushSpec push; }`; `WeaponConfig` (fields in Step 3); `WeaponRules.Validate(WeaponConfig, float fireHeight, List<string> errors) → bool`; `WeaponRules.ValidatePayload(in Payload, string label, List<string> errors)`.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Tests/EditMode/WeaponRulesTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class WeaponRulesTests
    {
        WeaponConfig _weapon;
        readonly List<string> _errors = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _weapon = ScriptableObject.CreateInstance<WeaponConfig>();
            _weapon.muzzle = MuzzleKind.Turret;
            _weapon.cooldownSeconds = 1f;
            _weapon.windUpSeconds = 0f;
            _weapon.recoverySeconds = 0.5f;
            _weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.3f };
            _weapon.hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0] };
            _weapon.selfEffects = new EffectSpec[0];
            _errors.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_weapon);
        }

        bool Valid() => WeaponRules.Validate(_weapon, 0.6f, _errors);

        [Test]
        public void Validate_AGoodWeapon_HasNoErrors()
        {
            Assert.IsTrue(Valid(), string.Join("; ", _errors));
            Assert.AreEqual(0, _errors.Count);
        }

        [Test]
        public void Validate_CooldownShorterThanRecovery_IsAnError()
        {
            _weapon.cooldownSeconds = 0.4f;
            Assert.IsFalse(Valid());
            StringAssert.Contains("recoverySeconds", _errors[0]);
        }

        [Test]
        public void Validate_CooldownEqualToRecovery_IsFine()
        {
            _weapon.cooldownSeconds = 0.5f;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_NegativeOrNonFiniteTiming_IsAnError()
        {
            _weapon.windUpSeconds = -1f;
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.windUpSeconds = float.NaN;
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_FixedWithNoMuzzle_IsAnError()
        {
            _weapon.muzzle = MuzzleKind.Fixed;
            _weapon.fixedMuzzles = FixedMuzzles.None;
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.fixedMuzzles = FixedMuzzles.Front | FixedMuzzles.Rear;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_ShotNumbersMustBePositive()
        {
            _weapon.shot = new ShotSettings { speed = 0f, range = 40f, radius = 0.3f };
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.shot = new ShotSettings { speed = 40f, range = -1f, radius = 0.3f };
            Assert.IsFalse(Valid());

            _errors.Clear();
            _weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0f };
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_RadiusAtOrAboveFireHeight_IsAnError_ButInfiniteHeightSkipsIt()
        {
            _weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.6f };
            Assert.IsFalse(Valid());

            _errors.Clear();
            Assert.IsTrue(WeaponRules.Validate(_weapon, float.PositiveInfinity, _errors));
        }

        [Test]
        public void Validate_NegativeDamage_IsAnError()
        {
            var payload = _weapon.hitPayload;
            payload.damageAmount = -5f;
            _weapon.hitPayload = payload;
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_TimedEffectWithoutDuration_IsAnError_OverhauledIsExempt()
        {
            var payload = _weapon.hitPayload;
            payload.effects = new[] { new EffectSpec { type = EffectType.Corroded, magnitude = 30f, duration = 0f } };
            _weapon.hitPayload = payload;
            Assert.IsFalse(Valid());

            _errors.Clear();
            payload.effects = new[] { new EffectSpec { type = EffectType.Overhauled, magnitude = 0f, duration = 0f } };
            _weapon.hitPayload = payload;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_SelfEffectsAreCheckedToo()
        {
            _weapon.selfEffects = new[] { new EffectSpec { type = EffectType.Spiked, magnitude = -20f, duration = 2f } };
            Assert.IsFalse(Valid());
        }

        [Test]
        public void Validate_PushWithoutReel_IsAnError()
        {
            var payload = _weapon.hitPayload;
            payload.push = new PushSpec { speed = 8f, direction = PushDirection.AlongTravel, spinScale = 1f, reelSeconds = 0f };
            _weapon.hitPayload = payload;
            Assert.IsFalse(Valid());
            StringAssert.Contains("reelSeconds", _errors[0]);

            _errors.Clear();
            payload.push.reelSeconds = 1f;
            _weapon.hitPayload = payload;
            Assert.IsTrue(Valid());
        }

        [Test]
        public void Validate_NullWeapon_IsAnError()
        {
            Assert.IsFalse(WeaponRules.Validate(null, 0.6f, _errors));
            Assert.AreEqual(1, _errors.Count);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `MotorCombat.Weapons.WeaponConfig` etc. do not exist.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/Weapons/WeaponsConfig.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>Rules shared by every weapon on every car.</summary>
    [CreateAssetMenu(menuName = "Motor Combat/Weapons Config", fileName = "WeaponsConfig")]
    public class WeaponsConfig : ScriptableObject
    {
        [Tooltip("Height above the car's floor, in metres, at which every muzzle on every car fires. Every car's hurtbox must span it.")]
        public float fireHeight = 0.6f;
    }
}
```

`Assets/_Project/Scripts/Weapons/WeaponTypes.cs`:

```csharp
using System;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>How a weapon delivers its payload. Later sub-projects append values.</summary>
    public enum WeaponDelivery { Shot }

    /// <summary>A weapon fires from the turret or from fixed muzzles — never both.</summary>
    public enum MuzzleKind { Turret, Fixed }

    [Flags]
    public enum FixedMuzzles
    {
        None = 0,
        Front = 1,
        Rear = 2,
        Left = 4,
        Right = 8
    }

    public enum PushDirection
    {
        /// <summary>The way the hitbox was moving.</summary>
        AlongTravel,

        /// <summary>From the hitbox's centre out to the hit car's centre.</summary>
        AwayFromCentre
    }

    [Serializable]
    public struct ShotSettings
    {
        public float speed;    // m/s
        public float range;    // m
        public float radius;   // m
    }

    [Serializable]
    public struct EffectSpec
    {
        public EffectType type;
        public float magnitude;
        public float duration;
    }

    /// <summary>A push always reels: speed > 0 needs reelSeconds > 0.</summary>
    [Serializable]
    public struct PushSpec
    {
        /// <summary>Velocity change in m/s. Mass, strength and resistance are ignored. 0 = no push.</summary>
        public float speed;
        public PushDirection direction;

        /// <summary>0 never spins; 1 is the physically correct spin for an off-centre hit.</summary>
        public float spinScale;

        public float reelSeconds;
    }

    /// <summary>What a hitbox does to a car it hits. Reusable: shots now; explosions, fields and auras later.</summary>
    [Serializable]
    public struct Payload
    {
        public DamageKind damageKind;

        /// <summary>0 = no damage.</summary>
        public float damageAmount;

        public EffectSpec[] effects;
        public PushSpec push;
    }
}
```

`Assets/_Project/Scripts/Weapons/WeaponConfig.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>One weapon. Every request it sends is tagged with this asset's name.</summary>
    [CreateAssetMenu(menuName = "Motor Combat/Weapon", fileName = "Weapon")]
    public class WeaponConfig : ScriptableObject
    {
        public string displayName;
        public WeaponDelivery delivery = WeaponDelivery.Shot;

        [Header("Muzzle")]
        [Tooltip("Turret: fires from the front along the crosshair. Fixed: fires straight out of each selected face.")]
        public MuzzleKind muzzle = MuzzleKind.Turret;

        [Tooltip("Used only when muzzle is Fixed. One or more.")]
        public FixedMuzzles fixedMuzzles = FixedMuzzles.Front;

        [Header("Timing (seconds, all start on the press)")]
        [Tooltip("Before this weapon can be pressed again.")]
        public float cooldownSeconds = 1f;

        [Tooltip("Delay from the press until the shot leaves.")]
        public float windUpSeconds;

        [Tooltip("No weapon, this one included, can be pressed until it ends. Must not exceed the cooldown.")]
        public float recoverySeconds = 0.2f;

        [Header("Shot")]
        public ShotSettings shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.3f };

        [Header("Payload")]
        public Payload hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0], push = new PushSpec { spinScale = 1f } };

        [Tooltip("Applied to the firing car when the shot leaves.")]
        public EffectSpec[] selfEffects = new EffectSpec[0];

        static readonly List<string> Errors = new List<string>();

        void OnValidate()
        {
            Errors.Clear();
            if (WeaponRules.Validate(this, float.PositiveInfinity, Errors)) return;

            for (int i = 0; i < Errors.Count; i++)
            {
                Debug.LogWarning($"[MotorCombat] Weapon '{name}': {Errors[i]}", this);
            }
        }
    }
}
```

`Assets/_Project/Scripts/Weapons/WeaponRules.cs`:

```csharp
using System;
using System.Collections.Generic;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>Configuration checks. Pure: no scene, no logging.</summary>
    public static class WeaponRules
    {
        const FixedMuzzles AllFixed = FixedMuzzles.Front | FixedMuzzles.Rear | FixedMuzzles.Left | FixedMuzzles.Right;

        /// <summary>
        /// Adds one message per broken rule. <paramref name="fireHeight"/> =
        /// +∞ skips the floor-clearance rule (the Inspector has no car).
        /// </summary>
        public static bool Validate(WeaponConfig weapon, float fireHeight, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            int before = errors.Count;
            if (weapon == null)
            {
                errors.Add("no weapon config");
                return false;
            }

            NonNegative(weapon.cooldownSeconds, "cooldownSeconds", errors);
            NonNegative(weapon.windUpSeconds, "windUpSeconds", errors);
            NonNegative(weapon.recoverySeconds, "recoverySeconds", errors);

            if (IsFinite(weapon.cooldownSeconds) && IsFinite(weapon.recoverySeconds) && weapon.cooldownSeconds < weapon.recoverySeconds)
            {
                errors.Add($"cooldownSeconds ({weapon.cooldownSeconds}) is shorter than recoverySeconds ({weapon.recoverySeconds}); the cooldown must be at least the recovery");
            }

            if (weapon.muzzle == MuzzleKind.Fixed && (weapon.fixedMuzzles & AllFixed) == 0)
            {
                errors.Add("muzzle is Fixed but no fixed muzzle is selected");
            }

            Positive(weapon.shot.speed, "shot.speed", errors);
            Positive(weapon.shot.range, "shot.range", errors);
            Positive(weapon.shot.radius, "shot.radius", errors);

            if (IsFinite(weapon.shot.radius) && weapon.shot.radius >= fireHeight)
            {
                errors.Add($"shot.radius ({weapon.shot.radius}) must be below the fire height ({fireHeight}), or the shot hits the floor");
            }

            ValidatePayload(weapon.hitPayload, "hitPayload", errors);
            ValidateEffects(weapon.selfEffects, "selfEffects", errors);

            return errors.Count == before;
        }

        /// <summary>Checks one payload. Public so later hitboxes (explosions, fields) validate theirs the same way.</summary>
        public static void ValidatePayload(in Payload payload, string label, List<string> errors)
        {
            NonNegative(payload.damageAmount, label + ".damageAmount", errors);
            ValidateEffects(payload.effects, label + ".effects", errors);

            NonNegative(payload.push.speed, label + ".push.speed", errors);
            if (!IsFinite(payload.push.spinScale)) errors.Add($"{label}.push.spinScale must be a finite number");

            if (payload.push.speed > 0f && !(payload.push.reelSeconds > 0f && IsFinite(payload.push.reelSeconds)))
            {
                errors.Add($"{label}.push has a speed but reelSeconds ({payload.push.reelSeconds}) is not above 0; a push always reels");
            }
        }

        static void ValidateEffects(EffectSpec[] effects, string label, List<string> errors)
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Length; i++)
            {
                EffectSpec spec = effects[i];
                string at = $"{label}[{i}] ({spec.type})";

                if (!EffectInfo.IsKnown(spec.type)) errors.Add($"{at} is not a known effect");
                NonNegative(spec.magnitude, at + ".magnitude", errors);

                if (EffectInfo.IsTimed(spec.type) && !(spec.duration > 0f))
                {
                    errors.Add($"{at}.duration ({spec.duration}) must be above 0");
                }
            }
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void NonNegative(float value, string field, List<string> errors)
        {
            if (!IsFinite(value) || value < 0f) errors.Add($"{field} must be a finite number ≥ 0 (is {value})");
        }

        static void Positive(float value, string field, List<string> errors)
        {
            if (!IsFinite(value) || value <= 0f) errors.Add($"{field} must be a finite number above 0 (is {value})");
        }
    }
}
```

Note: `EffectInfo.IsKnown` already exists in Core (`EffectInfo.cs`). A timed effect's duration of +∞ passes (`> 0` holds).

Add `"MotorCombat.Weapons"` to the test asmdef's `references` right after `"MotorCombat.Effects"`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (288 + 12 = 300).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Add weapon config data and validation

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Pure weapon rules — timing, muzzles, shot hits, push

**Files:**
- Create: `Assets/_Project/Scripts/Weapons/WeaponTiming.cs`, `MuzzleRules.cs`, `ShotRules.cs`, `PayloadRules.cs`
- Test: `Assets/_Project/Tests/EditMode/WeaponTimingTests.cs`, `MuzzleRulesTests.cs`, `ShotRulesTests.cs`, `PayloadRulesTests.cs`

**Interfaces:**
- Consumes: `FixedMuzzles`, `PushSpec`, `PushDirection` (Task 2).
- Produces:
  - `SlotTiming { float cooldownEndsAt; bool windingUp; float releaseAt; float attack; static SlotTiming Ready; }`, `LockTiming { float endsAt; int owner; static LockTiming None; }`
  - `WeaponTiming.Tolerance = 1e-4f`; `CanPress(in SlotTiming, bool valid, bool fireAllowed, in LockTiming, float now) → bool`; `Press(ref SlotTiming, ref LockTiming, int slotIndex, float now, float cooldown, float windUp, float recovery, float attack) → bool releaseNow`; `ShouldRelease(in SlotTiming, float now) → bool`; `Cancel(ref SlotTiming)`; `CooldownRemaining(in SlotTiming, float now) → float`; `IsBlocked(bool valid, bool fireAllowed, in LockTiming, int slotIndex, float now) → bool`; `ResetForRespawn(SlotTiming[] slots, ref LockTiming)`.
  - `Muzzle { Vector3 position; Vector3 direction; }`; `MuzzleRules.FixedOrder` (Front, Rear, Left, Right); `MuzzleRules.Fixed(FixedMuzzles which, Vector3 rootPosition, Quaternion rootRotation, Vector3 boxSize, float fireHeight) → Muzzle`; `MuzzleRules.Turret(Vector3 rootPosition, Quaternion rootRotation, Vector3 boxSize, float fireHeight, float aimYaw) → Muzzle`.
  - `ShotCandidate { float distance; bool wall; bool ownCar; bool targetable; bool enemy; }`; `ShotRules.RangeTolerance = 1e-4f`; `ShotRules.IsStopper(in ShotCandidate) → bool`; `ShotRules.Resolve(ShotCandidate[] candidates, int count) → int`; `ShotRules.StepDistance(float speed, float dt, float remaining) → float`.
  - `PayloadRules.PushDelta(in PushSpec, Vector3 travelDirection, Vector3 hitboxCentre, Vector3 targetCentre) → Vector3`.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Tests/EditMode/WeaponTimingTests.cs`:

```csharp
using NUnit.Framework;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class WeaponTimingTests
    {
        SlotTiming _slot;
        LockTiming _lock;

        [SetUp]
        public void SetUp()
        {
            _slot = SlotTiming.Ready;
            _lock = LockTiming.None;
        }

        [Test]
        public void CanPress_FreshSlot_WhenValidAndFireAllowed()
        {
            Assert.IsTrue(WeaponTiming.CanPress(_slot, true, true, _lock, 10f));
            Assert.IsFalse(WeaponTiming.CanPress(_slot, false, true, _lock, 10f), "invalid weapon");
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, false, _lock, 10f), "Fire blocked");
        }

        [Test]
        public void Press_StartsCooldownAndLockOnThePress()
        {
            bool releaseNow = WeaponTiming.Press(ref _slot, ref _lock, 1, 10f, 2f, 0f, 0.5f, 120f);

            Assert.IsTrue(releaseNow, "no wind-up fires in the same step");
            Assert.AreEqual(12f, _slot.cooldownEndsAt, 1e-5f);
            Assert.AreEqual(10.5f, _lock.endsAt, 1e-5f);
            Assert.AreEqual(1, _lock.owner);
            Assert.AreEqual(120f, _slot.attack, "attack snapshot taken on the press");
            Assert.AreEqual(2f, WeaponTiming.CooldownRemaining(_slot, 10f), 1e-5f);
        }

        [Test]
        public void Cooldown_BlocksTheSameSlotUntilItEnds()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 2f, 0f, 0f, 100f);
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, true, _lock, 11.9f));
            Assert.IsTrue(WeaponTiming.CanPress(_slot, true, true, _lock, 12f));
        }

        [Test]
        public void Lock_BlocksEverySlot_IncludingItsOwner()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 0.5f, 0f, 1f, 100f);   // cooldown < recovery: an invalid config, but the rule must still hold
            var other = SlotTiming.Ready;

            Assert.IsFalse(WeaponTiming.CanPress(other, true, true, _lock, 10.9f), "another slot");
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, true, _lock, 10.9f), "the owner");
            Assert.IsTrue(WeaponTiming.CanPress(other, true, true, _lock, 11f));
        }

        [Test]
        public void ZeroRecovery_LeavesNoLock_ForTheSameStep()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 1f, 0f, 0f, 100f);
            Assert.IsTrue(WeaponTiming.CanPress(SlotTiming.Ready, true, true, _lock, 10f));
        }

        [Test]
        public void WindUp_ReleasesAfterTheDelay()
        {
            bool releaseNow = WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 3f, 0.5f, 1f, 100f);

            Assert.IsFalse(releaseNow);
            Assert.IsTrue(_slot.windingUp);
            Assert.IsFalse(WeaponTiming.ShouldRelease(_slot, 10.4f));
            Assert.IsTrue(WeaponTiming.ShouldRelease(_slot, 10.5f));
        }

        [Test]
        public void Cancel_StopsTheWindUp_AndKeepsCooldownAndLock()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 3f, 0.5f, 1f, 100f);
            WeaponTiming.Cancel(ref _slot);

            Assert.IsFalse(_slot.windingUp);
            Assert.IsFalse(WeaponTiming.ShouldRelease(_slot, 20f));
            Assert.AreEqual(13f, _slot.cooldownEndsAt, 1e-5f);
            Assert.AreEqual(11f, _lock.endsAt, 1e-5f);
        }

        [Test]
        public void CanPress_IsFalseWhileTheSlotIsWindingUp()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 0.2f, 1f, 0f, 100f);   // wind-up longer than cooldown
            Assert.IsFalse(WeaponTiming.CanPress(_slot, true, true, _lock, 10.5f));
        }

        [Test]
        public void IsBlocked_ByFire_OrByAnotherSlotsLock_NotByOwnLockOrCooldown()
        {
            WeaponTiming.Press(ref _slot, ref _lock, 0, 10f, 3f, 0f, 1f, 100f);

            Assert.IsFalse(WeaponTiming.IsBlocked(true, true, _lock, 0, 10.5f), "own lock shows as cooldown only");
            Assert.IsTrue(WeaponTiming.IsBlocked(true, true, _lock, 1, 10.5f), "another slot's recovery");
            Assert.IsFalse(WeaponTiming.IsBlocked(true, true, _lock, 1, 11f), "lock over");
            Assert.IsTrue(WeaponTiming.IsBlocked(true, false, _lock, 0, 20f), "Fire blocked");
            Assert.IsFalse(WeaponTiming.IsBlocked(false, false, _lock, 0, 20f), "an empty slot is never blocked");
        }

        [Test]
        public void ResetForRespawn_ClearsWindUpsAndLock_KeepsCooldowns()
        {
            var slots = new[] { SlotTiming.Ready, SlotTiming.Ready };
            WeaponTiming.Press(ref slots[0], ref _lock, 0, 10f, 5f, 1f, 2f, 100f);

            WeaponTiming.ResetForRespawn(slots, ref _lock);

            Assert.IsFalse(slots[0].windingUp);
            Assert.AreEqual(15f, slots[0].cooldownEndsAt, 1e-5f);
            Assert.AreEqual(-1, _lock.owner);
            Assert.IsTrue(WeaponTiming.CanPress(slots[1], true, true, _lock, 10.1f));
        }
    }
}
```

`Assets/_Project/Tests/EditMode/MuzzleRulesTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class MuzzleRulesTests
    {
        static readonly Vector3 Box = new Vector3(2f, 1.2f, 4f);    // width, height, length
        static readonly Vector3 Root = new Vector3(10f, 0.6f, 20f); // grounded: floor at y = 0

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void Fixed_Front_IsTheFrontFaceCentre_AtFireHeight()
        {
            Muzzle m = MuzzleRules.Fixed(FixedMuzzles.Front, Root, Quaternion.identity, Box, 0.5f);
            AssertVector(new Vector3(10f, 0.5f, 22f), m.position);
            AssertVector(Vector3.forward, m.direction);
        }

        [Test]
        public void Fixed_RearLeftRight_PointStraightOutOfTheirFaces()
        {
            Muzzle rear = MuzzleRules.Fixed(FixedMuzzles.Rear, Root, Quaternion.identity, Box, 0.5f);
            Muzzle left = MuzzleRules.Fixed(FixedMuzzles.Left, Root, Quaternion.identity, Box, 0.5f);
            Muzzle right = MuzzleRules.Fixed(FixedMuzzles.Right, Root, Quaternion.identity, Box, 0.5f);

            AssertVector(new Vector3(10f, 0.5f, 18f), rear.position);
            AssertVector(Vector3.back, rear.direction);
            AssertVector(new Vector3(9f, 0.5f, 20f), left.position);
            AssertVector(Vector3.left, left.direction);
            AssertVector(new Vector3(11f, 0.5f, 20f), right.position);
            AssertVector(Vector3.right, right.direction);
        }

        [Test]
        public void Fixed_RotatesWithTheCarsYaw()
        {
            Muzzle m = MuzzleRules.Fixed(FixedMuzzles.Front, Root, Quaternion.Euler(0f, 90f, 0f), Box, 0.5f);
            AssertVector(new Vector3(12f, 0.5f, 20f), m.position);
            AssertVector(Vector3.right, m.direction);
        }

        [Test]
        public void Directions_AreFlat_WhenTheCarIsPitched()
        {
            Muzzle m = MuzzleRules.Fixed(FixedMuzzles.Front, Root, Quaternion.Euler(-10f, 0f, 0f), Box, 0.5f);
            Assert.AreEqual(0f, m.direction.y, 1e-5f);
            Assert.AreEqual(1f, m.direction.magnitude, 1e-4f);
        }

        [Test]
        public void Turret_SitsAtTheFront_AndFollowsTheAim()
        {
            Muzzle m = MuzzleRules.Turret(Root, Quaternion.identity, Box, 0.5f, 90f);
            AssertVector(new Vector3(10f, 0.5f, 22f), m.position);
            AssertVector(Vector3.right, m.direction);
        }

        [Test]
        public void FixedOrder_IsFrontRearLeftRight()
        {
            CollectionAssert.AreEqual(
                new[] { FixedMuzzles.Front, FixedMuzzles.Rear, FixedMuzzles.Left, FixedMuzzles.Right },
                MuzzleRules.FixedOrder);
        }
    }
}
```

`Assets/_Project/Tests/EditMode/ShotRulesTests.cs`:

```csharp
using NUnit.Framework;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class ShotRulesTests
    {
        static ShotCandidate Wall(float d) => new ShotCandidate { distance = d, wall = true };
        static ShotCandidate Enemy(float d) => new ShotCandidate { distance = d, targetable = true, enemy = true };

        [Test]
        public void Resolve_NothingHit_IsMinusOne()
        {
            Assert.AreEqual(-1, ShotRules.Resolve(new ShotCandidate[0], 0));
        }

        [Test]
        public void Resolve_PassesOwnCar_WrecksAndNonEnemies()
        {
            var candidates = new[]
            {
                new ShotCandidate { distance = 0f, ownCar = true, targetable = true, enemy = false },
                new ShotCandidate { distance = 1f, targetable = false, enemy = true },   // wreck
                new ShotCandidate { distance = 2f, targetable = true, enemy = false },   // teammate
            };
            Assert.AreEqual(-1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_TheNearestStopperWins_InAnyOrder()
        {
            var candidates = new[] { Wall(9f), Enemy(4f), Enemy(6f) };
            Assert.AreEqual(1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_AWallInFront_StopsBeforeAnEnemyBehindIt()
        {
            var candidates = new[] { Enemy(6f), Wall(3f) };
            Assert.AreEqual(1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_EqualDistance_TheEnemyBeatsTheWall()
        {
            var candidates = new[] { Wall(5f), Enemy(5f) };
            Assert.AreEqual(1, ShotRules.Resolve(candidates, candidates.Length));
        }

        [Test]
        public void Resolve_OnlyReadsTheFirstCountEntries()
        {
            var candidates = new[] { new ShotCandidate { distance = 1f, ownCar = true }, Enemy(2f) };
            Assert.AreEqual(-1, ShotRules.Resolve(candidates, 1));
        }

        [Test]
        public void StepDistance_IsClippedToTheRemainingRange()
        {
            Assert.AreEqual(1f, ShotRules.StepDistance(50f, 0.02f, 10f), 1e-5f);
            Assert.AreEqual(0.3f, ShotRules.StepDistance(50f, 0.02f, 0.3f), 1e-5f);
            Assert.AreEqual(0f, ShotRules.StepDistance(50f, 0.02f, -1f), 1e-5f);
        }
    }
}
```

`Assets/_Project/Tests/EditMode/PayloadRulesTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    public class PayloadRulesTests
    {
        static PushSpec Push(float speed, PushDirection direction) => new PushSpec { speed = speed, direction = direction, spinScale = 1f, reelSeconds = 1f };

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }

        [Test]
        public void AlongTravel_PushesTheWayTheShotFlew_Flattened()
        {
            Vector3 dv = PayloadRules.PushDelta(Push(8f, PushDirection.AlongTravel), new Vector3(0f, 1f, 1f), Vector3.zero, new Vector3(5f, 0f, 0f));
            AssertVector(new Vector3(0f, 0f, 8f), dv);
        }

        [Test]
        public void AwayFromCentre_PushesFromTheHitboxCentreToTheCar_Flattened()
        {
            Vector3 dv = PayloadRules.PushDelta(Push(8f, PushDirection.AwayFromCentre), Vector3.forward, new Vector3(0f, 2f, 0f), new Vector3(3f, 0f, 0f));
            AssertVector(new Vector3(8f, 0f, 0f), dv);
        }

        [Test]
        public void AwayFromCentre_CoincidentCentres_FallBackToTravel()
        {
            Vector3 dv = PayloadRules.PushDelta(Push(8f, PushDirection.AwayFromCentre), Vector3.left, new Vector3(1f, 5f, 1f), new Vector3(1f, 0f, 1f));
            AssertVector(new Vector3(-8f, 0f, 0f), dv);
        }

        [Test]
        public void ZeroOrNegativeSpeed_IsNoPush()
        {
            AssertVector(Vector3.zero, PayloadRules.PushDelta(Push(0f, PushDirection.AlongTravel), Vector3.forward, Vector3.zero, Vector3.one));
            AssertVector(Vector3.zero, PayloadRules.PushDelta(Push(-3f, PushDirection.AlongTravel), Vector3.forward, Vector3.zero, Vector3.one));
        }

        [Test]
        public void NoUsableDirection_IsNoPush()
        {
            AssertVector(Vector3.zero, PayloadRules.PushDelta(Push(8f, PushDirection.AlongTravel), Vector3.up, Vector3.zero, Vector3.one));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `SlotTiming`, `WeaponTiming`, `MuzzleRules`, `ShotRules`, `PayloadRules` do not exist.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/Weapons/WeaponTiming.cs`:

```csharp
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
```

`Assets/_Project/Scripts/Weapons/MuzzleRules.cs`:

```csharp
using System;
using UnityEngine;

namespace MotorCombat.Weapons
{
    public struct Muzzle
    {
        public Vector3 position;

        /// <summary>Flat and normalised.</summary>
        public Vector3 direction;
    }

    /// <summary>
    /// Where each muzzle is, from the car's collision box. Height is always the
    /// car's floor plus the global fire height; the root is the box centre.
    /// </summary>
    public static class MuzzleRules
    {
        /// <summary>The order a multi-muzzle weapon fires in.</summary>
        public static readonly FixedMuzzles[] FixedOrder = { FixedMuzzles.Front, FixedMuzzles.Rear, FixedMuzzles.Left, FixedMuzzles.Right };

        public static Muzzle Fixed(FixedMuzzles which, Vector3 rootPosition, Quaternion rootRotation, Vector3 boxSize, float fireHeight)
        {
            Vector3 forward = FlatForward(rootRotation);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float halfWidth = boxSize.x * 0.5f;
            float halfLength = boxSize.z * 0.5f;

            switch (which)
            {
                case FixedMuzzles.Front: return At(rootPosition + forward * halfLength, forward, rootPosition.y, boxSize.y, fireHeight);
                case FixedMuzzles.Rear: return At(rootPosition - forward * halfLength, -forward, rootPosition.y, boxSize.y, fireHeight);
                case FixedMuzzles.Left: return At(rootPosition - right * halfWidth, -right, rootPosition.y, boxSize.y, fireHeight);
                case FixedMuzzles.Right: return At(rootPosition + right * halfWidth, right, rootPosition.y, boxSize.y, fireHeight);
                default: throw new ArgumentException($"expected exactly one fixed muzzle, got {which}", nameof(which));
            }
        }

        /// <summary>At the front muzzle, pointing along the car's aim.</summary>
        public static Muzzle Turret(Vector3 rootPosition, Quaternion rootRotation, Vector3 boxSize, float fireHeight, float aimYaw)
        {
            Muzzle front = Fixed(FixedMuzzles.Front, rootPosition, rootRotation, boxSize, fireHeight);
            front.direction = Quaternion.AngleAxis(aimYaw, Vector3.up) * front.direction;
            return front;
        }

        static Muzzle At(Vector3 position, Vector3 direction, float rootY, float boxHeight, float fireHeight)
        {
            position.y = rootY - boxHeight * 0.5f + fireHeight;
            return new Muzzle { position = position, direction = direction };
        }

        static Vector3 FlatForward(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;
        }
    }
}
```

`Assets/_Project/Scripts/Weapons/ShotRules.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>One thing a shot's sweep touched this step.</summary>
    public struct ShotCandidate
    {
        public float distance;

        /// <summary>The arena (floor or wall).</summary>
        public bool wall;

        public bool ownCar;

        /// <summary>Has Targetable and is not destroyed — false for a wreck.</summary>
        public bool targetable;

        public bool enemy;
    }

    public static class ShotRules
    {
        public const float RangeTolerance = 1e-4f;
        const float SameDistance = 1e-5f;

        /// <summary>A wall, or a live enemy that isn't the shot's own car. Everything else is passed through.</summary>
        public static bool IsStopper(in ShotCandidate candidate)
        {
            return candidate.wall || (!candidate.ownCar && candidate.targetable && candidate.enemy);
        }

        /// <summary>Index of the nearest stopper among the first <paramref name="count"/> entries, or −1. At equal distance a car beats a wall.</summary>
        public static int Resolve(ShotCandidate[] candidates, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (!IsStopper(candidates[i])) continue;

                if (best < 0)
                {
                    best = i;
                    continue;
                }

                float delta = candidates[i].distance - candidates[best].distance;
                bool nearer = delta < -SameDistance;
                bool tieCarOverWall = Mathf.Abs(delta) <= SameDistance && !candidates[i].wall && candidates[best].wall;
                if (nearer || tieCarOverWall) best = i;
            }

            return best;
        }

        public static float StepDistance(float speed, float dt, float remaining)
        {
            return Mathf.Max(0f, Mathf.Min(speed * dt, remaining));
        }
    }
}
```

`Assets/_Project/Scripts/Weapons/PayloadRules.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Weapons
{
    public static class PayloadRules
    {
        const float MinDirectionSqr = 1e-6f;

        /// <summary>The velocity change a push gives the hit car: flat, of length push.speed, or zero.</summary>
        public static Vector3 PushDelta(in PushSpec push, Vector3 travelDirection, Vector3 hitboxCentre, Vector3 targetCentre)
        {
            if (!(push.speed > 0f)) return Vector3.zero;

            Vector3 direction = push.direction == PushDirection.AwayFromCentre
                ? Flat(targetCentre - hitboxCentre)
                : Flat(travelDirection);

            if (push.direction == PushDirection.AwayFromCentre && direction.sqrMagnitude < MinDirectionSqr)
            {
                direction = Flat(travelDirection);
            }

            if (direction.sqrMagnitude < MinDirectionSqr) return Vector3.zero;
            return direction.normalized * push.speed;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (300 + 10 + 6 + 7 + 5 = 328).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Add pure weapon rules: timing, muzzles, shot hits and push direction

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: PayloadApplier and the swept Shot

**Files:**
- Create: `Assets/_Project/Scripts/Weapons/PayloadApplier.cs`, `Shot.cs`
- Test: `Assets/_Project/Tests/EditMode/PayloadApplierTests.cs`

**Interfaces:**
- Consumes: `Payload`, `PushSpec`, `EffectSpec`, `ShotSettings` (Task 2); `PayloadRules.PushDelta`, `ShotCandidate`, `ShotRules` (Task 3); `PushMath.SpinDelta`, `Hurtbox`, `PhysicsLayers.HurtboxName/ArenaName/Arena` (Task 1); `IEffectReceiver`, `IDamageable`, `Hostility`, `CarAbility.Targetable` (Core).
- Produces: `PayloadHit { CarController source; string sourceTag; float attack; CarController target; Vector3 point; Vector3 travelDirection; Vector3 hitboxCentre; }`; `PayloadApplier.Apply(in Payload, in PayloadHit)`; `ShotLaunch { CarController source; string sourceTag; Vector3 origin; Vector3 direction; ShotSettings settings; Payload payload; float attack; }`; `Shot.Launch(in ShotLaunch) → Shot`; `Shot.Advance(float dt)`.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Tests/EditMode/PayloadApplierTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Effects;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    /// <summary>A payload landing on a real Health + CarEffects target, without a scene.</summary>
    public class PayloadApplierTests
    {
        GameObject _targetObject;
        GameObject _sourceObject;
        CarController _target;
        CarController _source;
        Rigidbody _body;
        Health _health;
        CarEffects _effects;
        EffectsConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EffectsConfig>();

            _sourceObject = new GameObject("Source", typeof(CarController));
            _source = _sourceObject.GetComponent<CarController>();
            _source.Stats.SetBase(CarStat.Attack, 100f);

            _targetObject = new GameObject("Target", typeof(CarController));
            _target = _targetObject.GetComponent<CarController>();
            _body = _targetObject.GetComponent<Rigidbody>();
            _targetObject.AddComponent<BoxCollider>().size = new Vector3(2f, 1f, 4f);
            _health = _targetObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
            _effects = _targetObject.AddComponent<CarEffects>();
            _effects.config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_targetObject);
            Object.DestroyImmediate(_sourceObject);
            Object.DestroyImmediate(_config);
        }

        PayloadHit Hit(Vector3 point, Vector3 travel, CarController source = null, float attack = 100f)
        {
            return new PayloadHit
            {
                source = source != null ? source : _source,
                sourceTag = "test",
                attack = attack,
                target = _target,
                point = point,
                travelDirection = travel,
                hitboxCentre = point
            };
        }

        static Payload Damage(float amount) => new Payload { damageKind = DamageKind.Flat, damageAmount = amount, effects = new EffectSpec[0] };

        static PushSpec Push(float speed) => new PushSpec { speed = speed, direction = PushDirection.AlongTravel, spinScale = 1f, reelSeconds = 1f };

        [Test]
        public void Damage_UsesTheAttackSnapshot()
        {
            PayloadApplier.Apply(Damage(50f), Hit(Vector3.zero, Vector3.forward, attack: 150f));
            Assert.AreEqual(925f, _health.Current, 1e-3f);
        }

        [Test]
        public void Effects_AreApplied()
        {
            Payload payload = Damage(0f);
            payload.effects = new[] { new EffectSpec { type = EffectType.Corroded, magnitude = 30f, duration = 4f } };

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.IsTrue(_effects.Has(EffectType.Corroded));
            Assert.AreEqual(1000f, _health.Current, "no damage when the amount is 0");
        }

        [Test]
        public void Push_AddsVelocityAlongTravel_AndReels()
        {
            _body.linearVelocity = new Vector3(0f, 0f, 2f);
            Payload payload = Damage(0f);
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.AreEqual(10f, _body.linearVelocity.z, 1e-3f, "added, not overwritten");
            Assert.IsTrue(_effects.Has(EffectType.Reeling));
            Assert.AreEqual(1f, _effects.Remaining(EffectType.Reeling), 1e-3f);
        }

        /// <summary>Hit on the tail, pushed toward +X: cross((0,0,−2), (8,0,0)).y = −16; k² = (4 + 16)/12 = 5/3 → −9.6 rad/s.</summary>
        [Test]
        public void Push_OffCentreHit_Spins()
        {
            Payload payload = Damage(0f);
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(new Vector3(0f, 0f, -2f), Vector3.right));

            Assert.AreEqual(-9.6f, _body.angularVelocity.y, 1e-3f);
        }

        [Test]
        public void NoPushSpeed_NoVelocityAndNoReel()
        {
            PayloadApplier.Apply(Damage(10f), Hit(Vector3.zero, Vector3.forward));

            Assert.AreEqual(Vector3.zero, _body.linearVelocity);
            Assert.IsFalse(_effects.Has(EffectType.Reeling));
        }

        [Test]
        public void AStunInTheEffectsList_DoesNotCancelThePush()
        {
            _body.linearVelocity = new Vector3(5f, 0f, 0f);
            Payload payload = Damage(0f);
            payload.effects = new[] { new EffectSpec { type = EffectType.Stunned, duration = 3f } };
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.AreEqual(0f, _body.linearVelocity.x, 1e-3f, "the stun stopped the car first");
            Assert.AreEqual(8f, _body.linearVelocity.z, 1e-3f, "then the push landed");
        }

        [Test]
        public void OwnCar_ReceivesNothing()
        {
            Payload payload = Damage(50f);
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward, source: _target));

            Assert.AreEqual(1000f, _health.Current);
            Assert.AreEqual(Vector3.zero, _body.linearVelocity);
        }

        [Test]
        public void AKillFromTheEffects_StopsBeforePushAndDamage()
        {
            Payload payload = Damage(50f);
            payload.effects = new[] { new EffectSpec { type = EffectType.Overheated, magnitude = 5000f, duration = 3f } };
            payload.push = Push(8f);

            PayloadApplier.Apply(payload, Hit(Vector3.zero, Vector3.forward));

            Assert.IsTrue(_health.IsDestroyed);
            Assert.AreEqual(Vector3.zero, _body.linearVelocity);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `PayloadApplier`, `PayloadHit` do not exist.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/Weapons/PayloadApplier.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>Where and how a hitbox touched a car.</summary>
    public struct PayloadHit
    {
        public CarController source;
        public string sourceTag;

        /// <summary>Attack snapshotted when the weapon was pressed.</summary>
        public float attack;

        public CarController target;
        public Vector3 point;
        public Vector3 travelDirection;
        public Vector3 hitboxCentre;
    }

    /// <summary>
    /// Lands a payload on a live enemy. Order: effects → (stop if they killed it)
    /// → push + Reeling → damage. Effects go first so a Stunned in the list stops
    /// the car before the push, instead of cancelling it.
    /// </summary>
    public static class PayloadApplier
    {
        public static void Apply(in Payload payload, in PayloadHit hit)
        {
            CarController target = hit.target;
            if (target == null) return;
            if (!target.Abilities.Has(CarAbility.Targetable)) return;
            if (!Hostility.AreEnemies(hit.source, target)) return;

            var receiver = target.GetComponent<IEffectReceiver>();
            var damageable = target.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsDestroyed) return;

            if (receiver != null && payload.effects != null)
            {
                for (int i = 0; i < payload.effects.Length; i++)
                {
                    EffectSpec spec = payload.effects[i];
                    receiver.Apply(new EffectRequest
                    {
                        source = hit.source,
                        sourceTag = hit.sourceTag,
                        type = spec.type,
                        magnitude = spec.magnitude,
                        duration = spec.duration,
                        attack = hit.attack
                    });
                }
            }

            if (damageable != null && damageable.IsDestroyed) return;

            Push(payload.push, hit, receiver);

            if (damageable != null && payload.damageAmount > 0f)
            {
                damageable.Apply(new DamageRequest
                {
                    source = hit.source,
                    sourceTag = hit.sourceTag,
                    kind = payload.damageKind,
                    amount = payload.damageAmount,
                    attack = hit.attack
                });
            }
        }

        static void Push(in PushSpec push, in PayloadHit hit, IEffectReceiver receiver)
        {
            if (!(push.speed > 0f)) return;

            CarController target = hit.target;
            Vector3 centre = target.transform.position;
            Vector3 delta = PayloadRules.PushDelta(push, hit.travelDirection, hit.hitboxCentre, centre);
            if (delta == Vector3.zero) return;

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity += delta;

                BoxCollider box = target.GetComponent<BoxCollider>();
                if (box != null)
                {
                    Vector3 angular = body.angularVelocity;
                    angular.y += PushMath.SpinDelta(hit.point, centre, delta, box.size.x, box.size.z, push.spinScale);
                    body.angularVelocity = angular;
                }
            }

            if (receiver != null)
            {
                receiver.Apply(new EffectRequest
                {
                    source = hit.source,
                    sourceTag = hit.sourceTag,
                    type = EffectType.Reeling,
                    duration = push.reelSeconds,
                    attack = hit.attack
                });
            }
        }
    }
}
```

`Assets/_Project/Scripts/Weapons/Shot.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    public struct ShotLaunch
    {
        public CarController source;
        public string sourceTag;
        public Vector3 origin;

        /// <summary>Flat and normalised.</summary>
        public Vector3 direction;

        public ShotSettings settings;
        public Payload payload;
        public float attack;
    }

    /// <summary>
    /// A straight shot. No collider and no Rigidbody: each physics step it sweeps a
    /// sphere from here to where it will be, against hurtboxes and the arena, so a
    /// fast shot can't tunnel and every hit has a point. ShotRules decides what
    /// stops it.
    /// </summary>
    public class Shot : MonoBehaviour
    {
        const int MaxHits = 16;

        static readonly RaycastHit[] Hits = new RaycastHit[MaxHits];
        static readonly ShotCandidate[] Candidates = new ShotCandidate[MaxHits];
        static readonly CarController[] HitCars = new CarController[MaxHits];
        static Material _material;

        ShotLaunch _launch;
        float _remaining;
        int _mask;
        int _arenaLayer;

        public float Remaining => _remaining;

        public static Shot Launch(in ShotLaunch launch)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Shot ({launch.sourceTag})";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = launch.origin;
            go.transform.localScale = Vector3.one * (launch.settings.radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = SharedMaterial();

            var shot = go.AddComponent<Shot>();
            shot._launch = launch;
            shot._remaining = launch.settings.range;
            shot._mask = LayerMask.GetMask(PhysicsLayers.HurtboxName, PhysicsLayers.ArenaName);
            shot._arenaLayer = PhysicsLayers.Arena;
            return shot;
        }

        void FixedUpdate()
        {
            Advance(Time.fixedDeltaTime);
        }

        public void Advance(float dt)
        {
            Vector3 position = transform.position;
            Vector3 direction = _launch.direction;
            float step = ShotRules.StepDistance(_launch.settings.speed, dt, _remaining);

            int count = Physics.SphereCastNonAlloc(position, _launch.settings.radius, direction, Hits, step, _mask, QueryTriggerInteraction.Collide);
            if (count > MaxHits) count = MaxHits;

            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                bool wall = collider.gameObject.layer == _arenaLayer;

                CarController car = null;
                if (!wall)
                {
                    var hurtbox = collider.GetComponent<Hurtbox>();
                    if (hurtbox != null) car = hurtbox.Car;
                }

                HitCars[i] = car;
                Candidates[i] = new ShotCandidate
                {
                    distance = Hits[i].distance,
                    wall = wall,
                    ownCar = car != null && car == _launch.source,
                    targetable = car != null && IsTargetable(car),
                    enemy = car != null && Hostility.AreEnemies(_launch.source, car)
                };
            }

            int stop = ShotRules.Resolve(Candidates, count);
            if (stop >= 0)
            {
                if (!Candidates[stop].wall)
                {
                    RaycastHit hit = Hits[stop];

                    // A sweep that starts inside a collider reports distance 0 and point zero.
                    Vector3 point = hit.distance <= 0f ? hit.collider.ClosestPoint(position) : hit.point;

                    PayloadApplier.Apply(_launch.payload, new PayloadHit
                    {
                        source = _launch.source,
                        sourceTag = _launch.sourceTag,
                        attack = _launch.attack,
                        target = HitCars[stop],
                        point = point,
                        travelDirection = direction,
                        hitboxCentre = position + direction * hit.distance
                    });
                }

                ClearBuffers(count);
                Destroy(gameObject);
                return;
            }

            ClearBuffers(count);
            transform.position = position + direction * step;
            _remaining -= step;
            if (_remaining <= ShotRules.RangeTolerance) Destroy(gameObject);
        }

        static bool IsTargetable(CarController car)
        {
            if (!car.Abilities.Has(CarAbility.Targetable)) return false;
            var damageable = car.GetComponent<IDamageable>();
            return damageable == null || !damageable.IsDestroyed;
        }

        /// <summary>Drop references so a static buffer never keeps a destroyed car alive.</summary>
        static void ClearBuffers(int count)
        {
            for (int i = 0; i < count; i++) HitCars[i] = null;
        }

        static Material SharedMaterial()
        {
            if (_material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
                _material = new Material(shader) { name = "MotorCombatShot", color = new Color(1f, 0.85f, 0.3f) };
            }

            return _material;
        }
    }
}
```

Shot sweeps are not unit-tested (EditMode physics queries against runtime-built colliders are unreliable); they are verified by the acceptance checklist in Task 8.

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (328 + 8 = 336).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Add the payload applier and the swept shot

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: WeaponModule and the fire keys

**Files:**
- Rewrite: `Assets/_Project/Scripts/Weapons/WeaponModule.cs`
- Delete: `Assets/_Project/Scripts/Weapons/IWeapon.cs` and `IWeapon.cs.meta`
- Modify: `Assets/_Project/Scripts/Controls/LocalInputProvider.cs`
- Test: `Assets/_Project/Tests/EditMode/WeaponModuleTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: `WeaponModule : MonoBehaviour, ICarModule, IRespawnable, IWeaponSlots` with `const int Slots = 3`, fields `WeaponsConfig config`, `WeaponConfig[] loadout`, `bool logWeapons`; `event Action<ShotLaunch> Fired`; `void Press(int slot)`; `void Step(float now)`; `WeaponSlotStatus GetStatus(int slot, float now)`; `IWeaponSlots.GetStatus(int slot)` (uses `Time.fixedTime`).

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Tests/EditMode/WeaponModuleTests.cs`:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Effects;
using MotorCombat.Weapons;

namespace MotorCombat.Tests
{
    /// <summary>
    /// WeaponModule on a real car without a scene. Times are relative to
    /// Time.fixedTime captured in SetUp (it is not 0 in EditMode). Fired shots are
    /// real GameObjects; TearDown destroys them.
    /// </summary>
    public class WeaponModuleTests
    {
        static readonly object TestBlock = new object();

        GameObject _carObject;
        CarController _car;
        Health _health;
        CarEffects _effects;
        WeaponModule _weapons;
        WeaponsConfig _weaponsConfig;
        EffectsConfig _effectsConfig;
        readonly List<WeaponConfig> _configs = new List<WeaponConfig>();
        readonly List<ShotLaunch> _fired = new List<ShotLaunch>();
        float _t0;

        [SetUp]
        public void SetUp()
        {
            _t0 = Time.fixedTime;
            _weaponsConfig = ScriptableObject.CreateInstance<WeaponsConfig>();
            _effectsConfig = ScriptableObject.CreateInstance<EffectsConfig>();

            _carObject = new GameObject("Car", typeof(CarController));
            _carObject.transform.position = new Vector3(0f, 0.6f, 0f);
            _car = _carObject.GetComponent<CarController>();
            _car.Stats.SetBase(CarStat.Attack, 100f);
            _carObject.AddComponent<BoxCollider>().size = new Vector3(2f, 1.2f, 4f);
            _health = _carObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
            _effects = _carObject.AddComponent<CarEffects>();
            _effects.config = _effectsConfig;
            _weapons = _carObject.AddComponent<WeaponModule>();
            _weapons.config = _weaponsConfig;
            _weapons.Fired += launch => _fired.Add(launch);
            _fired.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Shot shot in Object.FindObjectsByType<Shot>(FindObjectsSortMode.None)) Object.DestroyImmediate(shot.gameObject);
            Object.DestroyImmediate(_carObject);
            foreach (WeaponConfig config in _configs) Object.DestroyImmediate(config);
            _configs.Clear();
            Object.DestroyImmediate(_weaponsConfig);
            Object.DestroyImmediate(_effectsConfig);
        }

        WeaponConfig Weapon(float cooldown = 1f, float windUp = 0f, float recovery = 0.5f, MuzzleKind muzzle = MuzzleKind.Turret, FixedMuzzles fixedMuzzles = FixedMuzzles.Front)
        {
            var weapon = ScriptableObject.CreateInstance<WeaponConfig>();
            weapon.name = "TestWeapon" + _configs.Count;
            weapon.muzzle = muzzle;
            weapon.fixedMuzzles = fixedMuzzles;
            weapon.cooldownSeconds = cooldown;
            weapon.windUpSeconds = windUp;
            weapon.recoverySeconds = recovery;
            weapon.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.3f };
            weapon.hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 10f, effects = new EffectSpec[0] };
            weapon.selfEffects = new EffectSpec[0];
            _configs.Add(weapon);
            return weapon;
        }

        void Load(params WeaponConfig[] loadout) => _weapons.loadout = loadout;

        void PressAt(int slot, float elapsed)
        {
            _weapons.Press(slot);
            _weapons.Step(_t0 + elapsed);
        }

        WeaponSlotStatus Status(int slot, float elapsed) => _weapons.GetStatus(slot, _t0 + elapsed);

        [Test]
        public void Press_FiresAndStartsTheCooldown()
        {
            Load(Weapon(cooldown: 2f));
            PressAt(0, 0f);

            Assert.AreEqual(1, _fired.Count);
            WeaponSlotStatus status = Status(0, 0.5f);
            Assert.IsTrue(status.assigned);
            Assert.AreEqual(1.5f, status.cooldownRemaining, 1e-3f);
            Assert.AreEqual(2f, status.cooldownDuration, 1e-5f);
        }

        [Test]
        public void Press_DuringCooldown_IsDropped_NotRemembered()
        {
            Load(Weapon(cooldown: 1f, recovery: 0f));
            PressAt(0, 0f);
            PressAt(0, 0.5f);
            _weapons.Step(_t0 + 1.2f);

            Assert.AreEqual(1, _fired.Count);
        }

        [Test]
        public void RecoveryLock_BlocksTheOtherSlots_AndShowsThemBlocked()
        {
            Load(Weapon(cooldown: 3f, recovery: 1f), Weapon(cooldown: 1f, recovery: 0.2f));
            PressAt(0, 0f);
            PressAt(1, 0.5f);

            Assert.AreEqual(1, _fired.Count);
            Assert.IsTrue(Status(1, 0.5f).blocked);
            Assert.IsFalse(Status(0, 0.5f).blocked, "the lock's own slot shows its cooldown only");

            PressAt(1, 1f);
            Assert.AreEqual(2, _fired.Count);
        }

        [Test]
        public void SameStep_TheLowestSlotWins()
        {
            Load(Weapon(recovery: 0.5f), Weapon(recovery: 0.5f));
            _weapons.Press(1);
            _weapons.Press(0);
            _weapons.Step(_t0);

            Assert.AreEqual(1, _fired.Count);
            Assert.AreEqual("TestWeapon0", _fired[0].sourceTag);
        }

        [Test]
        public void WindUp_ReleasesAfterTheDelay()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);
            _weapons.Step(_t0 + 0.3f);
            Assert.AreEqual(0, _fired.Count);

            _weapons.Step(_t0 + 0.5f);
            Assert.AreEqual(1, _fired.Count);
        }

        [Test]
        public void FireBlockedDuringWindUp_CancelsTheShot_AndKeepsTheCooldown()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);

            _car.Abilities.Block(TestBlock, CarAbility.Fire, 10f, BlockRefresh.KeepLonger);
            _weapons.Step(_t0 + 0.2f);
            _car.Abilities.Unblock(TestBlock);
            _weapons.Step(_t0 + 0.6f);

            Assert.AreEqual(0, _fired.Count);
            Assert.AreEqual(2.4f, Status(0, 0.6f).cooldownRemaining, 1e-3f);
        }

        [Test]
        public void FireBlocked_DropsThePress_AndShowsBlocked()
        {
            Load(Weapon());
            _car.Abilities.Block(TestBlock, CarAbility.Fire, 10f, BlockRefresh.KeepLonger);
            PressAt(0, 0f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsTrue(Status(0, 0f).blocked);
            Assert.AreEqual(0f, Status(0, 0f).cooldownRemaining, "a dropped press starts nothing");
        }

        [Test]
        public void Destroyed_CancelsTheWindUp()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);

            _health.Apply(new DamageRequest { sourceTag = "test", kind = DamageKind.Flat, amount = 5000f });
            _weapons.Step(_t0 + 0.6f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsTrue(Status(0, 0.6f).blocked, "a wreck can't fire");
        }

        [Test]
        public void ResetForRespawn_ClearsTheLock_AndKeepsCooldowns()
        {
            Load(Weapon(cooldown: 5f, recovery: 2f), Weapon(cooldown: 1f, recovery: 0f));
            PressAt(0, 0f);

            _weapons.ResetForRespawn();
            PressAt(1, 0.1f);

            Assert.AreEqual(2, _fired.Count, "the lock is gone");
            Assert.AreEqual(4.9f, Status(0, 0.1f).cooldownRemaining, 1e-3f, "the cooldown carried over");
        }

        [Test]
        public void SelfEffects_LandWhenTheShotLeaves_NotOnThePress()
        {
            WeaponConfig weapon = Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f);
            weapon.selfEffects = new[] { new EffectSpec { type = EffectType.Spiked, magnitude = 20f, duration = 2f } };
            Load(weapon);

            PressAt(0, 0f);
            Assert.IsFalse(_effects.Has(EffectType.Spiked), "not on the press");

            _weapons.Step(_t0 + 0.5f);
            Assert.IsTrue(_effects.Has(EffectType.Spiked));
        }

        [Test]
        public void AttackSnapshot_IsTakenOnThePress()
        {
            Load(Weapon(cooldown: 3f, windUp: 0.5f, recovery: 1f));
            PressAt(0, 0f);
            _car.Stats.SetBase(CarStat.Attack, 300f);
            _weapons.Step(_t0 + 0.5f);

            Assert.AreEqual(100f, _fired[0].attack, 1e-4f);
        }

        [Test]
        public void FixedMuzzles_FireOneShotEach_InOrder()
        {
            Load(Weapon(muzzle: MuzzleKind.Fixed, fixedMuzzles: FixedMuzzles.Rear | FixedMuzzles.Front));
            PressAt(0, 0f);

            Assert.AreEqual(2, _fired.Count);
            Assert.AreEqual(1f, _fired[0].direction.z, 1e-4f, "front first");
            Assert.AreEqual(-1f, _fired[1].direction.z, 1e-4f);
            Assert.AreEqual(0.6f, _fired[0].origin.y, 1e-4f, "floor 0 + fire height 0.6");
        }

        [Test]
        public void AnInvalidWeapon_DisablesItsSlot()
        {
            Load(Weapon(cooldown: 0.1f, recovery: 0.5f));
            LogAssert.Expect(LogType.Error, new Regex("slot 1.*recoverySeconds"));

            PressAt(0, 0f);

            Assert.AreEqual(0, _fired.Count);
            Assert.IsFalse(Status(0, 0f).assigned);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `WeaponModule.Press`, `Step`, `GetStatus`, `Fired`, `config`, `loadout` do not exist.

- [ ] **Step 3: Implement**

Delete `Assets/_Project/Scripts/Weapons/IWeapon.cs` and its `.meta` (`git rm`). Nothing else references `IWeapon` (the old `WeaponModule` is the only user and is rewritten below).

`Assets/_Project/Scripts/Weapons/WeaponModule.cs` (whole file):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>
    /// A car's three weapon slots. Thin adapter: WeaponRules validates,
    /// WeaponTiming decides, MuzzleRules places, Shot flies.
    ///
    /// A press is a per-frame event: FrameTick collects it, the next physics step
    /// accepts or drops it. Nothing is buffered.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class WeaponModule : MonoBehaviour, ICarModule, IRespawnable, IWeaponSlots
    {
        public const int Slots = 3;

        public WeaponsConfig config;

        [Tooltip("Slot 1 (LMB), slot 2 (RMB), slot 3 (Space).")]
        public WeaponConfig[] loadout = new WeaponConfig[Slots];

        [Tooltip("Logs presses, drops, releases and cancelled wind-ups on this car.")]
        public bool logWeapons;

        /// <summary>Raised once per shot, just before it is launched.</summary>
        public event Action<ShotLaunch> Fired;

        readonly SlotTiming[] _slots = { SlotTiming.Ready, SlotTiming.Ready, SlotTiming.Ready };
        readonly WeaponConfig[] _valid = new WeaponConfig[Slots];
        readonly List<string> _errors = new List<string>();
        LockTiming _lock = LockTiming.None;
        int _pending;
        bool _ready;

        CarController _car;
        IDamageable _health;
        IEffectReceiver _effects;
        BoxCollider _box;

        CarController Car => _car != null ? _car : (_car = GetComponent<CarController>());

        public int SlotCount => Slots;

        void Start()
        {
            EnsureReady();
        }

        void OnDestroy()
        {
            if (_health != null) _health.Destroyed -= OnDestroyedByDamage;
        }

        /// <summary>
        /// Validates once, on first use rather than in Awake: CarFactory assigns
        /// the fields after AddComponent, and EditMode never runs Start.
        /// </summary>
        void EnsureReady()
        {
            if (_ready) return;
            _ready = true;

            _health = GetComponent<IDamageable>();
            _effects = GetComponent<IEffectReceiver>();
            _box = GetComponent<BoxCollider>();
            if (_health != null) _health.Destroyed += OnDestroyedByDamage;

            if (config == null)
            {
                Debug.LogError($"[MotorCombat] WeaponModule on '{name}' has no WeaponsConfig assigned; every weapon slot is disabled.", this);
                return;
            }

            if (loadout != null && loadout.Length > Slots)
            {
                Debug.LogWarning($"[MotorCombat] WeaponModule on '{name}' has {loadout.Length} loadout entries; only the first {Slots} are used.", this);
            }

            for (int i = 0; i < Slots; i++)
            {
                WeaponConfig weapon = loadout != null && i < loadout.Length ? loadout[i] : null;
                if (weapon == null) continue;

                _errors.Clear();
                if (WeaponRules.Validate(weapon, config.fireHeight, _errors))
                {
                    _valid[i] = weapon;
                    continue;
                }

                for (int e = 0; e < _errors.Count; e++)
                {
                    Debug.LogError($"[MotorCombat] Weapon '{weapon.name}' in slot {i + 1} of '{name}' is disabled: {_errors[e]}", this);
                }
            }
        }

        // --- ICarModule -----------------------------------------------------------

        public void FrameTick(in CarInput input, float dt)
        {
            _pending |= input.firePressed;
        }

        public void Tick(in CarInput input, float dt)
        {
            Step(Time.fixedTime);
        }

        /// <summary>Queues a press as if the slot's key went down this frame.</summary>
        public void Press(int slot)
        {
            if (slot >= 0 && slot < Slots) _pending |= 1 << slot;
        }

        /// <summary>One physics step. Public so tests can drive time.</summary>
        public void Step(float now)
        {
            EnsureReady();
            bool fireAllowed = FireAllowed();

            for (int i = 0; i < Slots; i++)
            {
                if (!_slots[i].windingUp) continue;

                if (!fireAllowed)
                {
                    WeaponTiming.Cancel(ref _slots[i]);
                    Log(i, "wind-up cancelled (Fire blocked)");
                }
                else if (WeaponTiming.ShouldRelease(_slots[i], now))
                {
                    _slots[i].windingUp = false;
                    Release(i);
                }
            }

            int pending = _pending;
            _pending = 0;

            for (int i = 0; i < Slots; i++)
            {
                if ((pending & (1 << i)) == 0) continue;

                WeaponConfig weapon = _valid[i];
                if (!WeaponTiming.CanPress(_slots[i], weapon != null, fireAllowed, _lock, now))
                {
                    Log(i, "press dropped");
                    continue;
                }

                bool releaseNow = WeaponTiming.Press(
                    ref _slots[i], ref _lock, i, now,
                    weapon.cooldownSeconds, weapon.windUpSeconds, weapon.recoverySeconds,
                    Car.Stats.Effective(CarStat.Attack));

                Log(i, releaseNow ? "pressed" : "pressed, winding up");
                if (releaseNow) Release(i);
            }
        }

        // --- IWeaponSlots -----------------------------------------------------------

        public WeaponSlotStatus GetStatus(int slot)
        {
            return GetStatus(slot, Time.fixedTime);
        }

        public WeaponSlotStatus GetStatus(int slot, float now)
        {
            EnsureReady();
            if (slot < 0 || slot >= Slots || _valid[slot] == null) return default;

            return new WeaponSlotStatus
            {
                assigned = true,
                cooldownRemaining = WeaponTiming.CooldownRemaining(_slots[slot], now),
                cooldownDuration = _valid[slot].cooldownSeconds,
                blocked = WeaponTiming.IsBlocked(true, FireAllowed(), _lock, slot, now)
            };
        }

        // --- IRespawnable -----------------------------------------------------------

        /// <summary>Clears the lock, wind-ups and queued presses. Cooldowns keep running.</summary>
        public void ResetForRespawn()
        {
            WeaponTiming.ResetForRespawn(_slots, ref _lock);
            _pending = 0;
        }

        // --- Internals ------------------------------------------------------------

        bool FireAllowed()
        {
            return Car.Abilities.Has(CarAbility.Fire) && (_health == null || !_health.IsDestroyed);
        }

        void OnDestroyedByDamage(DamageReport report)
        {
            for (int i = 0; i < Slots; i++) WeaponTiming.Cancel(ref _slots[i]);
        }

        void Release(int slot)
        {
            WeaponConfig weapon = _valid[slot];
            CarController car = Car;
            float attack = _slots[slot].attack;

            if (_effects != null && weapon.selfEffects != null)
            {
                for (int i = 0; i < weapon.selfEffects.Length; i++)
                {
                    EffectSpec spec = weapon.selfEffects[i];
                    _effects.Apply(new EffectRequest
                    {
                        source = car,
                        sourceTag = weapon.name,
                        type = spec.type,
                        magnitude = spec.magnitude,
                        duration = spec.duration,
                        allowNonEnemy = true,
                        attack = attack
                    });
                }
            }

            Transform root = car.transform;
            Vector3 boxSize = _box != null ? _box.size : Vector3.one;

            if (weapon.muzzle == MuzzleKind.Turret)
            {
                Launch(weapon, MuzzleRules.Turret(root.position, root.rotation, boxSize, config.fireHeight, car.AimYaw), attack);
            }
            else
            {
                for (int i = 0; i < MuzzleRules.FixedOrder.Length; i++)
                {
                    FixedMuzzles which = MuzzleRules.FixedOrder[i];
                    if ((weapon.fixedMuzzles & which) == 0) continue;
                    Launch(weapon, MuzzleRules.Fixed(which, root.position, root.rotation, boxSize, config.fireHeight), attack);
                }
            }

            Log(slot, "fired");
        }

        void Launch(WeaponConfig weapon, in Muzzle muzzle, float attack)
        {
            var launch = new ShotLaunch
            {
                source = Car,
                sourceTag = weapon.name,
                origin = muzzle.position,
                direction = muzzle.direction,
                settings = weapon.shot,
                payload = weapon.hitPayload,
                attack = attack
            };

            Fired?.Invoke(launch);
            Shot.Launch(launch);
        }

        void Log(int slot, string message)
        {
            if (logWeapons) Debug.Log($"[Weapons] {name} slot {slot + 1}: {message}", this);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: fire slot 1")] void DebugFireSlot1() => DebugFire(0);
        [ContextMenu("Debug: fire slot 2")] void DebugFireSlot2() => DebugFire(1);
        [ContextMenu("Debug: fire slot 3")] void DebugFireSlot3() => DebugFire(2);

        /// <summary>Queues a press; the next physics step applies the normal rules.</summary>
        void DebugFire(int slot)
        {
            Press(slot);
            Debug.Log($"[Weapons] {name}: debug press queued for slot {slot + 1}", this);
        }
#endif
    }
}
```

`Assets/_Project/Scripts/Controls/LocalInputProvider.cs` — inside `Sample()`, in the `keyboard != null` block add:

```csharp
                if (keyboard.spaceKey.wasPressedThisFrame) input.firePressed |= 1 << 2;
```

and in the `mouse != null` block add:

```csharp
                if (mouse.leftButton.wasPressedThisFrame) input.firePressed |= 1 << 0;
                if (mouse.rightButton.wasPressedThisFrame) input.firePressed |= 1 << 1;
```

Update its class comment: "the control set is three axes and three fire keys".

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (336 + 13 = 349). `CarFactoryTests.Spawn_AttachesAllFiveModules` still passes.

- [ ] **Step 5: Commit**

```bash
git add -A Assets/_Project
git status --porcelain
git commit -m "Build WeaponModule: slots, timing, release and fire keys

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Car composition — loadout, hurtboxes, wiring

**Files:**
- Modify: `Assets/_Project/Scripts/Cars/CarDefinition.cs`, `CarFactory.cs`
- Test: `Assets/_Project/Tests/EditMode/CarFactoryTests.cs`

**Interfaces:**
- Consumes: `WeaponsConfig`, `WeaponConfig`, `WeaponModule` (Tasks 2, 5); `HurtboxBox`, `Hurtbox`, `HurtboxRules`, `PhysicsLayers.Hurtbox` (Task 1).
- Produces: `CarDefinition.weaponsConfig`, `CarDefinition.loadout`, `CarDefinition.hurtboxes`; a `"Hurtbox"` child on every spawned car.

- [ ] **Step 1: Write the failing tests**

In `CarFactoryTests.cs`: add `using System.Text.RegularExpressions;`, `using UnityEngine.TestTools;`, `using MotorCombat.Weapons;`. In `SetUp`, before `CarFactory.Spawn`, add:

```csharp
            _definition.weaponsConfig = ScriptableObject.CreateInstance<WeaponsConfig>();
            _definition.hurtboxes = new[] { new HurtboxBox { centre = Vector3.zero, size = new Vector3(2f, 1.2f, 4.5f) } };
            _definition.loadout = new WeaponConfig[3];
```

In `TearDown` add `Object.DestroyImmediate(_definition.weaponsConfig);` before destroying `_definition`. Add tests:

```csharp
        [Test]
        public void Spawn_BuildsTheHurtboxChild_OnTheHurtboxLayer()
        {
            Transform child = _car.transform.Find(Hurtbox.ObjectName);
            Assert.IsNotNull(child);
            Assert.AreEqual(PhysicsLayers.Hurtbox, child.gameObject.layer);
            Assert.AreSame(_car, child.GetComponent<Hurtbox>().Car);

            var boxes = child.GetComponents<BoxCollider>();
            Assert.AreEqual(1, boxes.Length);
            Assert.IsTrue(boxes[0].isTrigger);
            Assert.AreEqual(new Vector3(2f, 1.2f, 4.5f), boxes[0].size);
        }

        [Test]
        public void Spawn_BuildsOneTriggerPerHurtboxEntry()
        {
            _definition.hurtboxes = new[]
            {
                new HurtboxBox { centre = new Vector3(0f, -0.2f, 1f), size = new Vector3(2f, 0.8f, 2f) },
                new HurtboxBox { centre = new Vector3(0f, 0.1f, -1f), size = new Vector3(2f, 1.2f, 2.5f) }
            };
            _modelCar = CarFactory.Spawn(_definition, Vector3.zero, Quaternion.identity, null, Color.white);

            var boxes = _modelCar.transform.Find(Hurtbox.ObjectName).GetComponents<BoxCollider>();
            Assert.AreEqual(2, boxes.Length);
            Assert.AreEqual(new Vector3(0f, -0.2f, 1f), boxes[0].center);
            Assert.AreEqual(new Vector3(2f, 1.2f, 2.5f), boxes[1].size);
        }

        [Test]
        public void Spawn_WithNoHurtboxes_LogsAnError_AndBuildsNone()
        {
            _definition.hurtboxes = new HurtboxBox[0];
            LogAssert.Expect(LogType.Error, new Regex("no hurtboxes"));

            _modelCar = CarFactory.Spawn(_definition, Vector3.zero, Quaternion.identity, null, Color.white);

            Assert.IsNull(_modelCar.transform.Find(Hurtbox.ObjectName));
        }

        [Test]
        public void Spawn_WiresTheWeaponModuleFromTheDefinition()
        {
            var weapons = _car.GetComponent<WeaponModule>();
            Assert.AreSame(_definition.weaponsConfig, weapons.config);
            Assert.AreSame(_definition.loadout, weapons.loadout);
            Assert.AreSame(weapons, _car.GetComponent<IWeaponSlots>());
        }
```

(`_modelCar` is already destroyed in `TearDown`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `CarDefinition.weaponsConfig`, `hurtboxes`, `loadout` do not exist.

- [ ] **Step 3: Implement**

`CarDefinition.cs` — add `using MotorCombat.Core;` and `using MotorCombat.Weapons;`, and append after `effectsConfig`:

```csharp

        [Header("Weapons")]
        public WeaponsConfig weaponsConfig;

        [Tooltip("Slot 1 (LMB), slot 2 (RMB), slot 3 (Space). Leave an entry empty for an empty slot.")]
        public WeaponConfig[] loadout = new WeaponConfig[3];

        [Tooltip("Boxes weapons hit, in the car's local space relative to its centre. At least one must span " +
                 "WeaponsConfig.fireHeight above the floor. Empty means weapons can't hit this car.")]
        public HurtboxBox[] hurtboxes = new HurtboxBox[0];
```

`CarFactory.cs` — right after `var controller = car.AddComponent<CarController>();` add:

```csharp

            BuildHurtboxes(car, controller, definition);
```

replace `car.AddComponent<WeaponModule>();` with:

```csharp
            var weapons = car.AddComponent<WeaponModule>();
            weapons.config = definition.weaponsConfig;
            weapons.loadout = definition.loadout;
```

and add this method in a new `// --- Hurtboxes ---` section before `// --- Visuals ---`:

```csharp
        // --- Hurtboxes --------------------------------------------------------

        /// <summary>
        /// One trigger box per entry on a child on the Hurtbox layer, which collides
        /// with nothing: weapons find it only by query. A trigger adds no contacts,
        /// mass or inertia to the root Rigidbody.
        /// </summary>
        static void BuildHurtboxes(GameObject car, CarController controller, CarDefinition definition)
        {
            if (definition.hurtboxes == null || definition.hurtboxes.Length == 0)
            {
                Debug.LogError($"[MotorCombat] CarDefinition '{definition.name}' has no hurtboxes; weapons can't hit this car.", definition);
                return;
            }

            var child = new GameObject(Hurtbox.ObjectName);
            child.transform.SetParent(car.transform, false);

            int layer = PhysicsLayers.Hurtbox;
            if (layer >= 0) child.layer = layer;

            child.AddComponent<Hurtbox>().Car = controller;

            foreach (HurtboxBox box in definition.hurtboxes)
            {
                var collider = child.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = box.centre;
                collider.size = box.size;
            }

            if (definition.weaponsConfig != null
                && !HurtboxRules.SpansHeight(definition.hurtboxes, definition.height, definition.weaponsConfig.fireHeight))
            {
                Debug.LogWarning($"[MotorCombat] CarDefinition '{definition.name}': no hurtbox spans the fire height ({definition.weaponsConfig.fireHeight} m); shots could pass this car.", definition);
            }
        }
```

`WreckSequence` rolls every direct child of the root except `DriverAnchor`, so the hurtbox child rolls with a wreck and is restored on removal. That is harmless (a wreck isn't `Targetable`); do not change `WreckSequence`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (349 + 4 = 353).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Give cars a loadout and composed hurtboxes, and wire WeaponModule

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Slots HUD

**Files:**
- Create: `Assets/_Project/Scripts/HUD/HudShapes.cs`, `WeaponSlotLayout.cs`, `WeaponSlotsWidget.cs`
- Modify: `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`
- Test: `Assets/_Project/Tests/EditMode/WeaponSlotLayoutTests.cs`, `HudShapesTests.cs`, `WeaponSlotsWidgetTests.cs`

**Interfaces:**
- Consumes: `IWeaponSlots`, `WeaponSlotStatus` (Task 1); `CarDefinition.weaponsConfig` (Task 6).
- Produces: `HudShapes.Circle` (Sprite), `HudShapes.CreateRing(float thicknessFraction) → Sprite`, `HudShapes.CircleAlpha(float distance, float radius)`, `HudShapes.RingAlpha(float distance, float radius, float thickness)`; `WeaponSlotLayout.CentreX`, `CooldownFill`, `KeyLabel`; `WeaponSlotsWidget { CarController viewer; void Build(RectTransform); void Show(IWeaponSlots); int SlotViews; bool SlashShown(int); float OverlayFill(int); bool LabelShown(int); Color BorderColour(int); static Color BlockedColour, EmptyRingColour; }`.

- [ ] **Step 1: Write the failing tests**

`Assets/_Project/Tests/EditMode/WeaponSlotLayoutTests.cs`:

```csharp
using NUnit.Framework;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class WeaponSlotLayoutTests
    {
        [Test]
        public void CentreX_ThreeCircles_AreCentredOnZero()
        {
            Assert.AreEqual(-92f, WeaponSlotLayout.CentreX(0, 3, 72f, 20f), 1e-4f);
            Assert.AreEqual(0f, WeaponSlotLayout.CentreX(1, 3, 72f, 20f), 1e-4f);
            Assert.AreEqual(92f, WeaponSlotLayout.CentreX(2, 3, 72f, 20f), 1e-4f);
        }

        [Test]
        public void CooldownFill_IsTheRemainingFraction_Clamped()
        {
            Assert.AreEqual(0.25f, WeaponSlotLayout.CooldownFill(1f, 4f), 1e-5f);
            Assert.AreEqual(1f, WeaponSlotLayout.CooldownFill(9f, 4f), 1e-5f);
            Assert.AreEqual(0f, WeaponSlotLayout.CooldownFill(0f, 4f), 1e-5f);
        }

        [Test]
        public void CooldownFill_ZeroDurationOrNaN_IsEmpty()
        {
            Assert.AreEqual(0f, WeaponSlotLayout.CooldownFill(1f, 0f), 1e-5f);
            Assert.AreEqual(0f, WeaponSlotLayout.CooldownFill(float.NaN, 4f), 1e-5f);
        }

        [Test]
        public void KeyLabel_MatchesTheFireKeys()
        {
            Assert.AreEqual("LMB", WeaponSlotLayout.KeyLabel(0));
            Assert.AreEqual("RMB", WeaponSlotLayout.KeyLabel(1));
            Assert.AreEqual("SPC", WeaponSlotLayout.KeyLabel(2));
            Assert.AreEqual("", WeaponSlotLayout.KeyLabel(3));
        }
    }
}
```

`Assets/_Project/Tests/EditMode/HudShapesTests.cs`:

```csharp
using NUnit.Framework;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class HudShapesTests
    {
        [Test]
        public void CircleAlpha_SolidInside_ClearOutside_SoftAtTheEdge()
        {
            Assert.AreEqual(1f, HudShapes.CircleAlpha(10f, 64f), 1e-5f);
            Assert.AreEqual(0f, HudShapes.CircleAlpha(70f, 64f), 1e-5f);
            Assert.AreEqual(0.5f, HudShapes.CircleAlpha(64f, 64f), 1e-5f);
        }

        [Test]
        public void RingAlpha_IsSolidOnlyInTheBand()
        {
            Assert.AreEqual(0f, HudShapes.RingAlpha(10f, 64f, 8f), 1e-5f, "centre");
            Assert.AreEqual(1f, HudShapes.RingAlpha(60f, 64f, 8f), 1e-5f, "band");
            Assert.AreEqual(0f, HudShapes.RingAlpha(70f, 64f, 8f), 1e-5f, "outside");
        }

        [Test]
        public void Circle_IsASquareSprite_Cached()
        {
            Assert.IsNotNull(HudShapes.Circle);
            Assert.AreEqual(HudShapes.Circle.rect.width, HudShapes.Circle.rect.height);
            Assert.AreSame(HudShapes.Circle, HudShapes.Circle);
        }
    }
}
```

`Assets/_Project/Tests/EditMode/WeaponSlotsWidgetTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class WeaponSlotsWidgetTests
    {
        sealed class FakeSlots : IWeaponSlots
        {
            public readonly WeaponSlotStatus[] statuses = new WeaponSlotStatus[3];
            public int SlotCount => 3;
            public WeaponSlotStatus GetStatus(int slot) => statuses[slot];
        }

        GameObject _parent;
        WeaponSlotsWidget _widget;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Canvas", typeof(RectTransform));
            _widget = _parent.AddComponent<WeaponSlotsWidget>();
            _widget.Build((RectTransform)_parent.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
        }

        [Test]
        public void Show_DrawsCooldownBlockedAndEmptySlots()
        {
            var slots = new FakeSlots();
            slots.statuses[0] = new WeaponSlotStatus { assigned = true, cooldownRemaining = 1f, cooldownDuration = 4f };
            slots.statuses[1] = new WeaponSlotStatus { assigned = true, cooldownRemaining = 2f, cooldownDuration = 4f, blocked = true };

            _widget.Show(slots);

            Assert.AreEqual(3, _widget.SlotViews);
            Assert.AreEqual(0.25f, _widget.OverlayFill(0), 1e-5f);
            Assert.IsFalse(_widget.SlashShown(0));
            Assert.IsTrue(_widget.LabelShown(0));

            Assert.AreEqual(0.5f, _widget.OverlayFill(1), 1e-5f, "cooldown and blocked show together");
            Assert.IsTrue(_widget.SlashShown(1));
            Assert.AreEqual(WeaponSlotsWidget.BlockedColour, _widget.BorderColour(1));

            Assert.IsFalse(_widget.LabelShown(2), "empty slot");
            Assert.IsFalse(_widget.SlashShown(2));
            Assert.AreEqual(WeaponSlotsWidget.EmptyRingColour, _widget.BorderColour(2));
        }

        [Test]
        public void Show_Null_DrawsThreeEmptySlots()
        {
            _widget.Show(null);

            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(_widget.LabelShown(i));
                Assert.AreEqual(0f, _widget.OverlayFill(i));
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `unity test . --mode EditMode`
Expected: compile errors — `WeaponSlotLayout`, `HudShapes`, `WeaponSlotsWidget` do not exist.

- [ ] **Step 3: Implement**

`Assets/_Project/Scripts/HUD/WeaponSlotLayout.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.HUD
{
    /// <summary>Where each weapon circle sits and what it shows. Placeholder art: key labels until icons exist.</summary>
    public static class WeaponSlotLayout
    {
        /// <summary>X of circle <paramref name="index"/>'s centre in a row of <paramref name="count"/>, centred on 0.</summary>
        public static float CentreX(int index, int count, float diameter, float gap)
        {
            float total = count * diameter + Mathf.Max(0, count - 1) * gap;
            return -total * 0.5f + diameter * 0.5f + index * (diameter + gap);
        }

        /// <summary>How much of the circle the dark overlay covers: remaining / duration, clamped to 0–1.</summary>
        public static float CooldownFill(float remaining, float duration)
        {
            if (!(duration > 0f) || !(remaining > 0f)) return 0f;
            return Mathf.Clamp01(remaining / duration);
        }

        public static string KeyLabel(int index)
        {
            switch (index)
            {
                case 0: return "LMB";
                case 1: return "RMB";
                case 2: return "SPC";
                default: return "";
            }
        }
    }
}
```

`Assets/_Project/Scripts/HUD/HudShapes.cs`:

```csharp
using System;
using UnityEngine;

namespace MotorCombat.HUD
{
    /// <summary>Circle and ring sprites generated in code, like the arena's textures. White, so an Image tints them.</summary>
    public static class HudShapes
    {
        const int Size = 128;

        static Sprite _circle;

        public static Sprite Circle => _circle != null ? _circle : (_circle = Build("HudCircle", d => CircleAlpha(d, Size * 0.5f)));

        /// <summary>A new ring whose band is <paramref name="thicknessFraction"/> of the diameter. The caller owns it.</summary>
        public static Sprite CreateRing(float thicknessFraction)
        {
            float thickness = Mathf.Max(1f, thicknessFraction * Size);
            return Build("HudRing", d => RingAlpha(d, Size * 0.5f, thickness));
        }

        /// <summary>1 inside, 0 outside, a one-pixel soft edge centred on the radius.</summary>
        public static float CircleAlpha(float distanceFromCentre, float radius)
        {
            return Mathf.Clamp01(radius - distanceFromCentre + 0.5f);
        }

        public static float RingAlpha(float distanceFromCentre, float radius, float thickness)
        {
            return Mathf.Min(CircleAlpha(distanceFromCentre, radius), 1f - CircleAlpha(distanceFromCentre, radius - thickness));
        }

        static Sprite Build(string name, Func<float, float> alpha)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            float centre = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - centre;
                    float dy = y + 0.5f - centre;
                    byte a = (byte)Mathf.RoundToInt(alpha(Mathf.Sqrt(dx * dx + dy * dy)) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }
}
```

`Assets/_Project/Scripts/HUD/WeaponSlotsWidget.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The viewer's weapon slots: three circles top-centre. A dark overlay drains
    /// top-first over the cooldown; a red border and slash mark a slot that can't
    /// fire for any other reason. Both can show at once. Stays visible while the
    /// car is a wreck — its slots then read as blocked.
    /// </summary>
    public class WeaponSlotsWidget : MonoBehaviour
    {
        public const int SlotCount = 3;

        public static readonly Color BackgroundColour = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color OverlayColour = new Color(0.25f, 0.25f, 0.25f, 0.85f);
        public static readonly Color RingColour = new Color(1f, 1f, 1f, 0.9f);
        public static readonly Color BlockedColour = new Color(0.9f, 0.15f, 0.15f, 1f);
        public static readonly Color EmptyRingColour = new Color(1f, 1f, 1f, 0.25f);

        public CarController viewer;

        [Tooltip("Reference pixels.")] public float diameter = 72f;
        [Tooltip("Reference pixels.")] public float gap = 20f;
        [Tooltip("Reference pixels from the top of the screen.")] public float topMargin = 24f;
        [Tooltip("Reference pixels.")] public float borderThickness = 4f;
        [Tooltip("Reference pixels.")] public float slashThickness = 6f;
        public int fontSize = 18;

        class SlotView
        {
            public Image background;
            public Image overlay;
            public Image border;
            public Image slash;
            public Text label;
        }

        readonly SlotView[] _views = new SlotView[SlotCount];
        Sprite _ring;
        IWeaponSlots _slots;
        CarController _slotsOwner;

        public RectTransform Root { get; private set; }

        public int SlotViews => Root == null ? 0 : SlotCount;
        public bool SlashShown(int index) => _views[index].slash.enabled;
        public float OverlayFill(int index) => _views[index].overlay.enabled ? _views[index].overlay.fillAmount : 0f;
        public bool LabelShown(int index) => _views[index].label.enabled;
        public Color BorderColour(int index) => _views[index].border.color;

        public void Build(RectTransform canvas)
        {
            Root = HudElements.Rect("WeaponSlots", canvas);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 1f);
            Root.pivot = new Vector2(0.5f, 1f);
            Root.anchoredPosition = new Vector2(0f, -topMargin);
            Root.sizeDelta = new Vector2(0f, diameter);

            _ring = HudShapes.CreateRing(borderThickness / diameter);

            for (int i = 0; i < SlotCount; i++)
            {
                _views[i] = CreateView(i);
            }

            Show(null);
        }

        void OnDestroy()
        {
            if (_ring != null)
            {
                DestroyImmediate(_ring.texture);
                DestroyImmediate(_ring);
            }
        }

        void LateUpdate()
        {
            if (Root == null) return;

            if (viewer != _slotsOwner)
            {
                _slotsOwner = viewer;
                _slots = viewer != null ? viewer.GetComponent<IWeaponSlots>() : null;
            }

            Show(viewer != null ? _slots : null);
        }

        /// <summary>Draws every slot from <paramref name="slots"/>; null draws three empty slots.</summary>
        public void Show(IWeaponSlots slots)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponSlotStatus status = slots != null && i < slots.SlotCount ? slots.GetStatus(i) : default;
                Apply(_views[i], status);
            }
        }

        static void Apply(SlotView view, in WeaponSlotStatus status)
        {
            if (!status.assigned)
            {
                view.background.enabled = false;
                view.overlay.enabled = false;
                view.label.enabled = false;
                view.slash.enabled = false;
                view.border.color = EmptyRingColour;
                return;
            }

            float fill = WeaponSlotLayout.CooldownFill(status.cooldownRemaining, status.cooldownDuration);

            view.background.enabled = true;
            view.label.enabled = true;
            view.overlay.enabled = fill > 0f;
            view.overlay.fillAmount = fill;
            view.slash.enabled = status.blocked;
            view.border.color = status.blocked ? BlockedColour : RingColour;
        }

        SlotView CreateView(int index)
        {
            RectTransform root = HudElements.Rect("Slot" + (index + 1), Root);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(diameter, diameter);
            root.anchoredPosition = new Vector2(WeaponSlotLayout.CentreX(index, SlotCount, diameter, gap), 0f);

            var view = new SlotView();

            view.background = HudElements.Image("Background", root, BackgroundColour);
            view.background.sprite = HudShapes.Circle;
            HudElements.Fill(view.background.rectTransform);

            view.label = HudElements.Text("Key", root, fontSize, TextAnchor.MiddleCenter);
            view.label.text = WeaponSlotLayout.KeyLabel(index);
            HudElements.Fill(view.label.rectTransform);

            view.overlay = HudElements.Image("Cooldown", root, OverlayColour);
            view.overlay.sprite = HudShapes.Circle;
            view.overlay.type = Image.Type.Filled;
            view.overlay.fillMethod = Image.FillMethod.Vertical;
            view.overlay.fillOrigin = (int)Image.OriginVertical.Bottom;   // shrinks toward the bottom: the top clears first
            HudElements.Fill(view.overlay.rectTransform);

            view.border = HudElements.Image("Border", root, RingColour);
            view.border.sprite = _ring;
            HudElements.Fill(view.border.rectTransform);

            view.slash = HudElements.Image("Slash", root, BlockedColour);
            RectTransform slash = view.slash.rectTransform;
            slash.anchorMin = slash.anchorMax = new Vector2(0.5f, 0.5f);
            slash.pivot = new Vector2(0.5f, 0.5f);
            slash.sizeDelta = new Vector2(diameter - borderThickness, slashThickness);
            slash.localRotation = Quaternion.Euler(0f, 0f, -45f);   // top-left to bottom-right

            return view;
        }
    }
}
```

`Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs` — in `Validate()`, after the `effectsConfig` line add:

```csharp
            if (carDefinition.weaponsConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.weaponsConfig is not assigned."); return false; }
```

and at the end of `Start()`, after the self-effects widget:

```csharp

            var weaponSlots = hud.gameObject.AddComponent<WeaponSlotsWidget>();
            weaponSlots.viewer = player;
            weaponSlots.Build(hud.Rect);
```

(`MotorCombat.Bootstrap` already references `MotorCombat.Weapons`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `unity test . --mode EditMode`
Expected: all pass (353 + 4 + 3 + 2 = 362).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Add the weapon slots HUD

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Assets, docs and the acceptance checklist

**Files:**
- Modify: `Assets/_Project/Editor/ConfigAssetBootstrap.cs`
- Generate: `Assets/_Project/Configs/WeaponsConfig.asset`, `Assets/_Project/Configs/Weapons/TestTurret.asset`, `TestFrontRear.asset`, `TestSides.asset` (+ `.meta`, folder `.meta`); modify `Assets/_Project/Configs/CarDefinition.asset`
- Create: `docs/weapons.md`
- Modify: `docs/combat.md`, `docs/architecture.md`, `docs/hud.md`, `docs/tuning.md`, `docs/ramming.md`, `docs/workflow.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: everything above.
- Produces: playable weapons in `Arena.unity` (no scene edit — `GameBootstrap` gains no serialized fields).

- [ ] **Step 1: Extend ConfigAssetBootstrap**

Add `using MotorCombat.Core;` and `using MotorCombat.Weapons;`. In `CreateDefaults()`, after `var effects = …` add:

```csharp
            Directory.CreateDirectory(ConfigDir + "/Weapons");
            AssetDatabase.Refresh();

            var weapons = GetOrCreate<WeaponsConfig>("WeaponsConfig");

            var turret = GetOrCreate<WeaponConfig>("Weapons/TestTurret", w =>
            {
                w.displayName = "Test Turret";
                w.muzzle = MuzzleKind.Turret;
                w.cooldownSeconds = 0.5f;
                w.windUpSeconds = 0f;
                w.recoverySeconds = 0.2f;
                w.shot = new ShotSettings { speed = 60f, range = 60f, radius = 0.25f };
                w.hitPayload = new Payload { damageKind = DamageKind.Flat, damageAmount = 50f, effects = new EffectSpec[0] };
                w.selfEffects = new EffectSpec[0];
            });

            var frontRear = GetOrCreate<WeaponConfig>("Weapons/TestFrontRear", w =>
            {
                w.displayName = "Test Front+Rear";
                w.muzzle = MuzzleKind.Fixed;
                w.fixedMuzzles = FixedMuzzles.Front | FixedMuzzles.Rear;
                w.cooldownSeconds = 3f;
                w.windUpSeconds = 0.5f;
                w.recoverySeconds = 1f;
                w.shot = new ShotSettings { speed = 40f, range = 40f, radius = 0.35f };
                w.hitPayload = new Payload
                {
                    damageKind = DamageKind.Flat,
                    damageAmount = 80f,
                    effects = new[] { new EffectSpec { type = EffectType.Corroded, magnitude = 30f, duration = 4f } }
                };
                w.selfEffects = new EffectSpec[0];
            });

            var sides = GetOrCreate<WeaponConfig>("Weapons/TestSides", w =>
            {
                w.displayName = "Test Sides";
                w.muzzle = MuzzleKind.Fixed;
                w.fixedMuzzles = FixedMuzzles.Left | FixedMuzzles.Right;
                w.cooldownSeconds = 4f;
                w.windUpSeconds = 0f;
                w.recoverySeconds = 0.5f;
                w.shot = new ShotSettings { speed = 50f, range = 30f, radius = 0.4f };
                w.hitPayload = new Payload
                {
                    damageKind = DamageKind.Flat,
                    damageAmount = 40f,
                    effects = new EffectSpec[0],
                    push = new PushSpec { speed = 8f, direction = PushDirection.AlongTravel, spinScale = 1f, reelSeconds = 1f }
                };
                w.selfEffects = new[] { new EffectSpec { type = EffectType.Spiked, magnitude = 20f, duration = 2f } };
            });
```

After `if (car.effectsConfig == null) …` add:

```csharp
            if (car.weaponsConfig == null) car.weaponsConfig = weapons;
            if (car.loadout == null || car.loadout.Length == 0 || System.Array.TrueForAll(car.loadout, w => w == null))
            {
                car.loadout = new[] { turret, frontRear, sides };
            }
            if (car.hurtboxes == null || car.hurtboxes.Length == 0)
            {
                car.hurtboxes = new[] { new HurtboxBox { centre = Vector3.zero, size = new Vector3(car.width, car.height, car.length) } };
            }
```

Replace `GetOrCreate<T>` with an overload pair:

```csharp
        static T GetOrCreate<T>(string assetName) where T : ScriptableObject
        {
            return GetOrCreate<T>(assetName, null);
        }

        /// <summary><paramref name="initialise"/> runs only when the asset is newly created, so re-running never clobbers tuning.</summary>
        static T GetOrCreate<T>(string assetName, System.Action<T> initialise) where T : ScriptableObject
        {
            string path = $"{ConfigDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            initialise?.Invoke(created);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
```

- [ ] **Step 2: Generate and verify the assets**

With the Editor closed, run:

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
```

Verify by reading the YAML (no Editor):
- `Assets/_Project/Configs/WeaponsConfig.asset` has `fireHeight: 0.6`.
- Each `Configs/Weapons/*.asset` has the values from Step 1 (`cooldownSeconds`, `windUpSeconds`, `recoverySeconds`, `shot`, `hitPayload`, `selfEffects`, `muzzle` 0/1, `fixedMuzzles` 3 for Front+Rear and 12 for Left+Right).
- `Configs/CarDefinition.asset` gained `weaponsConfig` (guid of `WeaponsConfig.asset.meta`), `loadout` with the three weapon guids in order TestTurret, TestFrontRear, TestSides, and one `hurtboxes` entry with `centre: {x: 0, y: 0, z: 0}` and `size: {x: 2.2075, y: 1.3422, z: 4.6568}`; every other field unchanged (`git diff` shows only additions).
- `git status` shows no change to `Arena.unity`.

If `CreateDefaults` did not run (stale assets, no new files), check the Unity log for the error before retrying; do not hand-write weapon `.asset` files.

Then run `unity test . --mode EditMode` — expected all green (362).

- [ ] **Step 3: Write the docs**

Create `docs/weapons.md` covering, in this order: overview (slots and keys, turret vs fixed muzzles, fire height, the shot, payload); `WeaponConfig` field table and `WeaponsConfig`; validation rules (spec §2.3) and where they report; timing (press → cooldown + lock, wind-up, cancel, same-step lowest slot wins, respawn); input (`CarInput.firePressed`, consumed once per frame); muzzles (spec §4); hurtboxes (`CarDefinition.hurtboxes`, the `Hurtbox` layer ignores everything, empty list error, fire-height warning, the child rolls with a wreck); the shot's sweep and `ShotRules` (spec §6); payload order and push (spec §7, `PushMath` shared with rams, the applier itself refuses non-enemies, untargetable and destroyed cars); `IWeaponSlots` and the HUD; debug menus "Debug: fire slot N"; test weapons table (spec §10); tests table with real counts; the Claude decisions list (spec §13) marked as awaiting confirmation.

Update:
- `docs/combat.md`: physics layers table gains `| Hurtbox | 11 | Car hurtbox children — weapons query it; it collides with nothing |`; the intro sentence lists weapons as a damage source now; the "What resets" table gains `| WeaponModule | Clears the recovery lock, wind-ups and queued presses; cooldowns keep running |`.
- `docs/architecture.md`: diagram line for `WeaponModule` becomes `WeaponModule (FrameTick + FixedUpdate) slots → timing → muzzles → Shot`; `CarInput { throttle, steer, aimDeltaX, firePressed }`; replace the "Weapons is a seam, not a feature" section with a short "Weapons" section pointing to weapons.md; the input section notes `firePressed` is a per-frame event consumed once like `aimDeltaX`; the Core contracts sentence gains `Hurtbox`, `PushMath`, `IWeaponSlots`; the test count.
- `docs/hud.md`: a "Weapon slots" section (spec §9).
- `docs/tuning.md`: `WeaponsConfig.fireHeight`, the three test weapons' fields, `CarDefinition.loadout` and `hurtboxes`.
- `docs/ramming.md`: one sentence under the spin formula — the formula lives in `PushMath` (Core), shared with weapon pushes.
- `docs/workflow.md`: test count and fixture table (add `HurtboxRulesTests`, `PushMathTests`, `WeaponRulesTests`, `WeaponTimingTests`, `MuzzleRulesTests`, `ShotRulesTests`, `PayloadRulesTests`, `PayloadApplierTests`, `WeaponModuleTests`, `WeaponSlotLayoutTests`, `HudShapesTests`, `WeaponSlotsWidgetTests`, updated `CarFactoryTests` and `PhysicsLayersTests` counts — take counts from `test-results.xml`); change the checklist intro to "Rows 1–37 passed on 2026-09-14; rows 38–53 are new with weapons."; the Running-it controls list gains `LMB / RMB / Space — fire weapon slots 1 / 2 / 3`; add rows:

| # | Check | Expected |
|---|---|---|
| 38 | Park facing the dummy, crosshair centred, LMB | A small shot leaves your nose, flies straight and hits the dummy's rear; its bar drops 50 |
| 39 | Swing the crosshair to the cone edge, LMB | The shot leaves the front along the crosshair, not along the car |
| 40 | Tap LMB as fast as you can | At most one shot per 0.5 s; circle 1's grey drains top-first and is gone when it can fire |
| 41 | RMB facing the dummy | About 0.5 s later two shots leave the front and the rear together; the dummy's bar drops 80 and a CORR chip appears |
| 42 | RMB, then LMB within 1 s | LMB does nothing; circle 1 shows a red border and slash until RMB's recovery ends, then LMB fires |
| 43 | Park beside the dummy (dummy on your right), Space | Shots leave both sides; the dummy is shoved away and shows REEL (it spins if the hit is off its centre); your SPIK chip shows and your top speed drops for 2 s |
| 44 | Temporarily set TestFrontRear.windUpSeconds to 3; RMB, then PlayerCar's CarEffects → Debug: apply Stunned within 3 s; set it back to 0.5 | No shot leaves; circle 2 keeps draining its cooldown; all circles show the slash while stunned |
| 45 | Drive in front of the dummy; DummyCar's WeaponModule → Debug: fire slot 1 | A shot leaves the dummy's nose and your bar drops 50 |
| 46 | RMB with nothing behind you | The rear shot starts on your own tail and your HP doesn't change |
| 47 | LMB at the wall with nothing in between | The shot disappears at the wall |
| 48 | Temporarily set TestTurret.shot.range to 10, LMB into open space; set it back to 60 | The shot vanishes about 10 m out |
| 49 | Fire all three, then PlayerCar's Health → Debug: destroy | The circles stay up with slashes while the grey keeps draining; after respawn the slashes go and remaining cooldowns carry over |
| 50 | Dummy's Health → Debug: destroy; LMB through the wreck while it fades | Shots pass through the wreck |
| 51 | Dummy → Debug: apply Armored; Space beside it | It is still shoved and reels; its bar doesn't move |
| 52 | Set TestTurret.cooldownSeconds to 0.1 (recovery is 0.2) and press Play | The console shows an error naming TestTurret and recoverySeconds; circle 1 is an empty ring; set it back to 0.5 |
| 53 | Re-walk ram rows 13–20 | Unchanged — the hurtbox child and PushMath change nothing about rams |

- `CLAUDE.md`: first paragraph — "Ramming, health, destruction, respawn, effects and a weapon core (three slots, a basic shot) work; no menus, no netcode."; docs table gains `| [docs/weapons.md](docs/weapons.md) | Weapon slots, timing, muzzles, hurtboxes, shots, payloads or the slots HUD |`; "Not built yet" replaces "Weapons." with "Weapon deliveries beyond the basic shot (bursts, pierce, bounce, homing, explosions, areas, auras, beams, maneuvers)." and drops the `WeaponModule` seam sentence.

- [ ] **Step 4: Run tests**

Run: `unity test . --mode EditMode`
Expected: all pass; the count matches the one written into the docs.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project docs CLAUDE.md
git status --porcelain
git commit -m "Generate weapon assets and document the weapon core

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
