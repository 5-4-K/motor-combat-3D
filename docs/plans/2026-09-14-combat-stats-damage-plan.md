# Combat Stats, Damage, Destruction and HUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every car health, attack/defense damage, a destruction sequence and a HUD. Create the Core seams (abilities, stats, damage path, hostility, ticks, layers, registry) that effects and weapons will plug into later without editing this work.

**Architecture:**
- Small contracts and plain classes live in `MotorCombat.Core`.
- Rules and components live in a new `MotorCombat.Combat` assembly: `DamageRules`, `HealthState`, `Health`, `WreckSequence`.
- Driving, Ramming and HUD read only Core.
- `CarStatus` is replaced by a general `CarAbilities` blocker system that reproduces ram lock and reel exactly.

**Tech Stack:** Unity 6 (`6000.6.0f1`), URP, C#, PhysX, uGUI (`com.unity.ugui` 2.6.0), NUnit EditMode tests via the `unity` CLI.

**Spec:** `docs/specs/2026-09-14-combat-stats-damage-design.md`

**Plan deviations from the spec (rulings, carried to the SDD ledger):**
1. **Wreck roll.** Spec §3 says the sequence "rotates and fades the visual child" and moves `VisualName` into Core. But the placeholder box's nose marker is a *separate* child. So the plan instead:
   - rolls **every direct child of the car root except `DriverAnchor`**
   - fades every renderer under the root
   - does **not** move the constant

   Cost if wrong: one lookup changes.
2. **Reported damage.** `DamageResult.dealt` and `DamageReport.dealt` are the HP actually removed, clamped to the HP that was left, so kill credit and logs never over-report. Spec §2.1 left "dealt" ambiguous.
3. **Visuals restored on removal.** When the wreck is deactivated it restores its original materials, transforms and colours, so a future respawn needs no change to `WreckSequence`. The closed-spec rule requires this; the spec doesn't mention it.

## Global Constraints

- **Assemblies.** Every script lives in an asmdef folder under `Assets/_Project/Scripts/`. Gameplay assemblies (Aiming, Arena, Cameras, Combat, Controls, Driving, HUD, Ramming, Weapons) reference `MotorCombat.Core` only, plus Unity packages. Only `Cars`, `Bootstrap` and `EditorTools` compose across modules.
- **Where logic lives.** Decisions go in pure statics or plain classes; MonoBehaviours stay thin.
- **Decay.** Every decay is a rate in 1/s applied as `value * Mathf.Exp(-rate * dt)`.
- **Driving resistance.** `DrivePhysics` stays the only source of driving resistance. Don't touch Rigidbody damping or the frictionless material.
- **Damage formula:** `dealt = raw × attack/100 × 100/(100 + max(0, defense))`.
  - `attack` is the source's effective attack, or 100 when the source is null.
  - `defense` is the target's effective defense.
  - `raw` is `amount` for Flat, or `amount/100 × maxHealth` for MaxHealthPercent.
- **Stat modifiers:** `effective = max(0, base × (1 + Σpercent/100))`. Percentages from different sources add; the same source on the same stat replaces. `TopSpeed` has base 1.
- **Hostility:** `AreEnemies(a, b)` is true when either car is null. Otherwise it uses a replaceable rule that defaults to `a != b`.
- **Ticks:** the first tick is immediate, the next is due at `lastTick + interval`, and the schedule survives contact breaks.
- **Ability re-expression must preserve behaviour exactly:**

  | Block | Abilities | Duration | Refresh |
  |---|---|---|---|
  | Ram lock | `Throttle\|Steer\|Ram` | `attackerLockSeconds` | `KeepLonger` |
  | Ram reel | `Throttle\|Steer\|YawHold\|Grip\|Ram` | `reelSeconds` | `Restart` |
  | Wreck | `Throttle\|Steer\|Fire\|Ram\|Targetable` | +∞ | — |

- **Layers:** `Arena` = 8, `Car` = 9, `Wreck` = 10. `Wreck` collides only with `Arena`.
- **Placeholder values** (the user owns tuning):
  - Car stats: `maxHealth` 1000, `attack` 100, `defense` 0, `strength` 1, `resistance` 1
  - Ram damage: `flankDamage` 0, `rearDamage` 0
  - `WreckConfig`: `rollDegrees` 180, `rollSeconds` 0.8, `fadeSeconds` 1.5, `removeAfterSeconds` 1.5
  - HUD: `anchorMarginMetres` 0.4, `minWidth` 60, `maxWidth` 160, `barHeight` 8, name `fontSize` 14; canvas reference 1920×1080, matched on width
- **Code style.** Match the surrounding code:
  - XML `<summary>` comments that explain *why*
  - `[Tooltip]` on config fields
  - `Debug.LogError("[MotorCombat] ...", this)` for misconfiguration, checked in `Start`
- **The Unity Editor must be closed** to run the CLI. Always run the tests in this form:
  ```bash
  rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml
  ```
  Read `total`, `passed` and `failed` from the printed tag. If `test-results.xml` is missing, the run failed; compile errors appear in the CLI output.
- **Staging.** Unity generates `.meta` files on import, including for new folders. After a test run, stage `Assets/_Project` wholesale, plus any other paths the task names, then check `git status --porcelain`. Never commit `test-results.xml`.
- **Git.** Work on `main`, commit once per task, and never push. End commit messages with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- **Expected test totals:** 116 now → T1 124 → T2 133 → T3 173 → T4 190 → T5 201 → T6 201.

---

## File map

(All code paths are under `Assets/_Project/`.)

| File | Task | Responsibility |
|---|---|---|
| `Scripts/Core/CarAbilities.cs` (new) | 1 | `CarAbility` flags, `BlockRefresh`, `CarAbilities` blockers |
| `Scripts/Core/CarStatus.cs` (delete) | 1 | Replaced by `CarAbilities` |
| `Scripts/Core/CarController.cs` | 1,2,5 | `Abilities`, `Stats`, registry hooks |
| `Scripts/Driving/DrivingModule.cs` | 1,2 | Reads abilities and `TopSpeed` |
| `Scripts/Ramming/RammingModule.cs` | 1,2,3 | Blocks, effective strength/resistance, ram damage |
| `Tests/EditMode/CarAbilitiesTests.cs` (new), `CarStatusTests.cs` (delete) | 1 | |
| `Scripts/Core/CarStats.cs` (new) | 2 | `CarStat`, `CarStats` |
| `Scripts/Cars/CarDefinition.cs`, `Configs/CarDefinition.asset` | 2,3,4 | Stats, rename, `wreckConfig` |
| `Scripts/Cars/CarFactory.cs` | 2,3,4 | Stat bases, `Health`, layer, `WreckSequence` |
| `Scripts/Ramming/RamRules.cs` | 2,3 | Strength/resistance rename, `DamageFor` |
| `Tests/EditMode/CarStatsTests.cs` (new), `RamRulesTests.cs`, `CarFactoryTests.cs` | 2,3,4 | |
| `Scripts/Core/Damage*.cs`, `IDamageable.cs`, `Hostility.cs`, `TickSchedule.cs` (new) | 3 | Damage contracts |
| `Scripts/Combat/` (new asmdef): `DamageRules.cs`, `HealthState.cs`, `Health.cs` | 3 | Damage rules and component |
| `Scripts/Ramming/RamConfig.cs` | 3 | `flankDamage`, `rearDamage` |
| `Tests/EditMode/DamageRulesTests.cs`, `HealthStateTests.cs`, `TickScheduleTests.cs`, `HostilityTests.cs`, `HealthTests.cs` (new) | 3 | |
| `Scripts/Core/PhysicsLayers.cs` (new), `ProjectSettings/TagManager.asset` | 4 | Layers |
| `Scripts/Combat/WreckConfig.cs`, `WreckMath.cs`, `WreckMaterials.cs`, `WreckSequence.cs` (new) | 4 | Destruction |
| `Scripts/Arena/ArenaBuilder.cs`, `Scripts/Bootstrap/GameBootstrap.cs`, `Editor/ConfigAssetBootstrap.cs` | 4,5 | Layer, wiring, config asset |
| `Tests/EditMode/PhysicsLayersTests.cs`, `WreckMathTests.cs`, `WreckMaterialsTests.cs` (new), `ArenaBuilderTests.cs` | 4 | |
| `Scripts/Core/CarRegistry.cs` (new) | 5 | Live car list |
| `Scripts/HUD/HudRoot.cs`, `HudElements.cs`, `HealthBarLayout.cs`, `SelfHealthWidget.cs`, `EnemyHealthBars.cs` (new) | 5 | HUD |
| `Tests/EditMode/CarRegistryTests.cs`, `HealthBarLayoutTests.cs`, `HudRootTests.cs` (new) | 5 | |
| `Configs/WreckConfig.asset`, `docs/combat.md`, `docs/hud.md`, other docs | 6 | Assets and documentation |

---

### Task 1: Ability switches replace CarStatus

**Files:**
- Create: `Assets/_Project/Scripts/Core/CarAbilities.cs`
- Delete: `Assets/_Project/Scripts/Core/CarStatus.cs` and its `.meta`
- Modify: `Assets/_Project/Scripts/Core/CarController.cs`, `Assets/_Project/Scripts/Driving/DrivingModule.cs`, `Assets/_Project/Scripts/Ramming/RammingModule.cs`
- Test: create `Assets/_Project/Tests/EditMode/CarAbilitiesTests.cs`; delete `Assets/_Project/Tests/EditMode/CarStatusTests.cs` and its `.meta`

**Interfaces:**
- Consumes: nothing new.
- Produces, in namespace `MotorCombat.Core`:
  - `[Flags] enum CarAbility { None = 0, Throttle = 1, Steer = 2, YawHold = 4, Grip = 8, Fire = 16, Ram = 32, Targetable = 64 }`
  - `enum BlockRefresh { KeepLonger, Restart, IgnoreIfActive }`
  - `class CarAbilities` with:
    - `void Block(object source, CarAbility abilities, float seconds, BlockRefresh refresh)`
    - `void Unblock(object source)`
    - `void UnblockAll()`
    - `bool Has(CarAbility ability)`
    - `bool IsBlockedBy(object source)`
    - `float Remaining(object source)`
    - `void Advance(float dt)`
  - `CarController.Abilities` (`CarAbilities`, never null). `CarController.Status` is removed.

- [ ] **Step 1: Write the failing test**

Delete `Assets/_Project/Tests/EditMode/CarStatusTests.cs` and `Assets/_Project/Tests/EditMode/CarStatusTests.cs.meta`.

Create `Assets/_Project/Tests/EditMode/CarAbilitiesTests.cs` (15 tests):
```csharp
using System;
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarAbilitiesTests
    {
        static readonly object KeyA = new object();
        static readonly object KeyB = new object();

        const CarAbility All = CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip
                               | CarAbility.Fire | CarAbility.Ram | CarAbility.Targetable;

        [Test]
        public void New_HasEveryAbility()
        {
            var abilities = new CarAbilities();
            Assert.IsTrue(abilities.Has(All));
        }

        [Test]
        public void Block_RemovesOnlyTheBlockedAbilities()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle | CarAbility.Steer, 1f, BlockRefresh.Restart);

            Assert.IsFalse(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.Has(CarAbility.Steer));
            Assert.IsTrue(abilities.Has(CarAbility.Grip));
            Assert.IsFalse(abilities.Has(CarAbility.Throttle | CarAbility.Grip), "a multi-flag query fails if any flag is blocked");
        }

        [Test]
        public void TimedBlock_ExpiresAfterItsDuration()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 0.5f, BlockRefresh.KeepLonger);

            abilities.Advance(0.3f);
            Assert.IsFalse(abilities.Has(CarAbility.Throttle));

            abilities.Advance(0.3f);
            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.IsBlockedBy(KeyA));
        }

        [Test]
        public void Restart_ResetsTheRemainingTime()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Grip, 1f, BlockRefresh.Restart);
            abilities.Advance(0.8f);
            abilities.Block(KeyA, CarAbility.Grip, 1f, BlockRefresh.Restart);
            abilities.Advance(0.5f);

            Assert.IsFalse(abilities.Has(CarAbility.Grip));
            Assert.AreEqual(0.5f, abilities.Remaining(KeyA), 1e-5f);
        }

        [Test]
        public void KeepLonger_NeverShortensALongerBlock()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Steer, 1f, BlockRefresh.KeepLonger);
            abilities.Block(KeyA, CarAbility.Steer, 0.2f, BlockRefresh.KeepLonger);
            abilities.Advance(0.5f);

            Assert.IsFalse(abilities.Has(CarAbility.Steer));
            Assert.AreEqual(0.5f, abilities.Remaining(KeyA), 1e-5f);
        }

        [Test]
        public void KeepLonger_ExtendsAShorterBlock()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Steer, 0.2f, BlockRefresh.KeepLonger);
            abilities.Block(KeyA, CarAbility.Steer, 1f, BlockRefresh.KeepLonger);

            Assert.AreEqual(1f, abilities.Remaining(KeyA), 1e-5f);
        }

        [Test]
        public void IgnoreIfActive_DoesNothingWhileTheBlockIsActive()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Fire, 1f, BlockRefresh.IgnoreIfActive);
            abilities.Advance(0.8f);
            abilities.Block(KeyA, CarAbility.Fire, 1f, BlockRefresh.IgnoreIfActive);
            abilities.Advance(0.5f);

            Assert.IsTrue(abilities.Has(CarAbility.Fire), "the re-application was ignored, so the first block expired");
        }

        [Test]
        public void IgnoreIfActive_AppliesAgainAfterExpiry()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Fire, 0.1f, BlockRefresh.IgnoreIfActive);
            abilities.Advance(0.2f);
            abilities.Block(KeyA, CarAbility.Fire, 1f, BlockRefresh.IgnoreIfActive);

            Assert.IsFalse(abilities.Has(CarAbility.Fire));
        }

        [Test]
        public void Reapplying_ReplacesTheAbilitySet()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 1f, BlockRefresh.Restart);
            abilities.Block(KeyA, CarAbility.Steer, 1f, BlockRefresh.Restart);

            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.Has(CarAbility.Steer));
        }

        [Test]
        public void Sources_AreIndependent()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 0.5f, BlockRefresh.KeepLonger);
            abilities.Block(KeyB, CarAbility.Throttle, 1f, BlockRefresh.Restart);

            abilities.Advance(0.6f);
            Assert.IsFalse(abilities.Has(CarAbility.Throttle), "KeyB still blocks");

            abilities.Unblock(KeyB);
            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
        }

        [Test]
        public void UntimedBlock_NeverExpires()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Targetable, float.PositiveInfinity, BlockRefresh.KeepLonger);
            abilities.Advance(1000f);

            Assert.IsFalse(abilities.Has(CarAbility.Targetable));

            abilities.Unblock(KeyA);
            Assert.IsTrue(abilities.Has(CarAbility.Targetable));
        }

        [Test]
        public void UnblockAll_ClearsEverySource()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 1f, BlockRefresh.Restart);
            abilities.Block(KeyB, CarAbility.Grip, float.PositiveInfinity, BlockRefresh.Restart);
            abilities.UnblockAll();

            Assert.IsTrue(abilities.Has(All));
        }

        [Test]
        public void Remaining_IsZeroForAnUnknownSource()
        {
            var abilities = new CarAbilities();
            Assert.AreEqual(0f, abilities.Remaining(KeyA));
            Assert.IsFalse(abilities.IsBlockedBy(KeyA));
        }

        [Test]
        public void NewBlockWithZeroSeconds_IsIgnored()
        {
            var abilities = new CarAbilities();
            abilities.Block(KeyA, CarAbility.Throttle, 0f, BlockRefresh.Restart);

            Assert.IsTrue(abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(abilities.IsBlockedBy(KeyA));
        }

        [Test]
        public void NullSource_Throws()
        {
            var abilities = new CarAbilities();
            Assert.Throws<ArgumentNullException>(() => abilities.Block(null, CarAbility.Throttle, 1f, BlockRefresh.Restart));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`, and a compile error `The type or namespace name 'CarAbilities' could not be found`.

- [ ] **Step 3: Implement `CarAbilities`**

Create `Assets/_Project/Scripts/Core/CarAbilities.cs`:
```csharp
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
```

Delete `Assets/_Project/Scripts/Core/CarStatus.cs` and `Assets/_Project/Scripts/Core/CarStatus.cs.meta`.

- [ ] **Step 4: Point `CarController` at abilities**

In `Assets/_Project/Scripts/Core/CarController.cs`, replace:
```csharp
        /// <summary>Lock and reel timers. Written by ramming, obeyed by driving.</summary>
        public CarStatus Status { get; } = new CarStatus();
```
with:
```csharp
        /// <summary>Ability switches. Blocked by rams, wrecks and (later) effects; obeyed by driving, ramming and weapons.</summary>
        public CarAbilities Abilities { get; } = new CarAbilities();
```
Then replace `            Status.Advance(dt);` with `            Abilities.Advance(dt);`.

- [ ] **Step 5: Driving obeys abilities**

In `Assets/_Project/Scripts/Driving/DrivingModule.cs`, replace the whole `Tick` method with:
```csharp
        public void Tick(in CarInput input, float dt)
        {
            if (config == null) return;

            Rigidbody body = _car.Body;
            Vector3 forward = FlatForward();
            CarAbilities abilities = _car.Abilities;

            // A blocked ability ignores its input — a ram lock, a reel, a wreck or
            // (later) an effect. Aim is untouched: it runs in AimModule.
            float throttle = abilities.Has(CarAbility.Throttle) ? input.throttle : 0f;
            float steer = abilities.Has(CarAbility.Steer) ? input.steer : 0f;

            // 1. Thrust, brake and reverse.
            Vector3 force = DrivePhysics.DriveForce(
                forward,
                body.linearVelocity,
                throttle,
                config.enginePower,
                config.brakeForce,
                config.reversePower,
                config.reverseEpsilon);

            if (force != Vector3.zero)
            {
                body.AddForce(force, ForceMode.Force);
            }

            // 2. Drag, applied manually so terminal speed matches the formula.
            body.linearVelocity = DrivePhysics.ApplyDrag(body.linearVelocity, config.linearDrag, dt);

            // 3. Yaw, set directly. Never gated on speed MAGNITUDE, so turning on
            //    the spot works; the SIGN of travel can invert the steering sense
            //    so reversing handles like a real car. Skipped while YawHold is
            //    blocked: a reeling car spins freely and RammingModule decays it.
            if (abilities.Has(CarAbility.YawHold))
            {
                float forwardSpeed = Vector3.Dot(body.linearVelocity, forward);
                float yawRate = DrivePhysics.YawRate(
                    steer,
                    config.turnRate,
                    forwardSpeed,
                    config.flipSteeringInReverse,
                    config.reverseEpsilon);
                body.angularVelocity = Vector3.up * (yawRate * Mathf.Deg2Rad);
            }

            // 4. Grip. Whatever sideways velocity survives is the drift. Skipped
            //    while Grip is blocked, so a ram's shove survives.
            if (abilities.Has(CarAbility.Grip))
            {
                body.linearVelocity = DrivePhysics.ApplyGrip(
                    body.linearVelocity, forward, config.lateralGripStrength, dt);
            }
        }
```

- [ ] **Step 6: Ramming uses blocks**

In `Assets/_Project/Scripts/Ramming/RammingModule.cs`:

After the line `        float _resolvedAt = -1f;`, add:
```csharp

        // Block sources for the ability switches. One key per kind of block is
        // enough: every car owns its own CarAbilities.
        static readonly object LockBlock = new object();
        static readonly object ReelBlock = new object();

        const CarAbility LockMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Ram;
        const CarAbility ReelMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram;
```

In `Tick`, replace `            if (config == null || !Car.Status.IsReeling) return;` with:
```csharp
            if (config == null || Car.Abilities.Has(CarAbility.YawHold)) return;
```
Then replace the comment line `            // DrivingModule does not write yaw while reeling; the spin winds down here.` with:
```csharp
            // DrivingModule does not write yaw while YawHold is blocked; the spin winds down here.
```

In `OnCollisionEnter`, after the line `            if (partner == null || config == null || partner.config == null) return;`, add:
```csharp
            if (!Car.Abilities.Has(CarAbility.Targetable) || !partner.Car.Abilities.Has(CarAbility.Targetable)) return;
```

In `Participant`, replace `                canAttack = Car.Status.CanAttack` with:
```csharp
                canAttack = Car.Abilities.Has(CarAbility.Ram)
```

In `ApplyRam`, replace `            attacker.Car.Status.Lock(config.attackerLockSeconds);` with:
```csharp
            attacker.Car.Abilities.Block(LockBlock, LockMask, config.attackerLockSeconds, BlockRefresh.KeepLonger);
```
Then replace `            victim.Car.Status.Reel(config.reelSeconds);` with:
```csharp
            victim.Car.Abilities.Block(ReelBlock, ReelMask, config.reelSeconds, BlockRefresh.Restart);
```

In `ApplyHeadOn`, replace these two lines:
```csharp
            Car.Status.Lock(config.attackerLockSeconds);
            partner.Car.Status.Lock(config.attackerLockSeconds);
```
with:
```csharp
            Car.Abilities.Block(LockBlock, LockMask, config.attackerLockSeconds, BlockRefresh.KeepLonger);
            partner.Car.Abilities.Block(LockBlock, LockMask, config.attackerLockSeconds, BlockRefresh.KeepLonger);
```

Confirm nothing else still references `Status` or `CarStatus`:
```bash
grep -rn "CarStatus\|\.Status\b" Assets/_Project --include=*.cs
```
Expected: no output.

- [ ] **Step 7: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"` and `total="124"` (116 − 7 + 15).

- [ ] **Step 8: Commit**

```bash
git add -A Assets/_Project
git status --porcelain
git commit -m "Replace CarStatus with general ability switches" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Effective stats and the strength/resistance rename

**Files:**
- Create: `Assets/_Project/Scripts/Core/CarStats.cs`
- Modify:
  - `Assets/_Project/Scripts/Core/CarController.cs`
  - `Assets/_Project/Scripts/Cars/CarDefinition.cs`
  - `Assets/_Project/Configs/CarDefinition.asset`
  - `Assets/_Project/Scripts/Cars/CarFactory.cs`
  - `Assets/_Project/Scripts/Ramming/RamRules.cs`
  - `Assets/_Project/Scripts/Ramming/RammingModule.cs`
  - `Assets/_Project/Scripts/Ramming/RamConfig.cs` (doc comment only)
  - `Assets/_Project/Scripts/Driving/DrivingModule.cs`
- Test: create `Assets/_Project/Tests/EditMode/CarStatsTests.cs`; modify `RamRulesTests.cs` and `CarFactoryTests.cs`

**Interfaces:**
- Consumes: `CarController.Abilities` from Task 1.
- Produces:
  - `MotorCombat.Core.CarStat { Attack, Defense, Strength, Resistance, TopSpeed }`
  - `class CarStats` with:
    - `void SetBase(CarStat, float)`
    - `float Base(CarStat)`
    - `float Effective(CarStat)`
    - `void Add(object source, CarStat stat, float percent)`
    - `void Remove(object source)`
    - `void RemoveAll()`
  - `CarController.Stats`, never null.
  - `CarDefinition.maxHealth` (1000), `attack` (100), `defense` (0), `strength` (1), `resistance` (1).
  - `RamRules.ShoveDelta(Vector3 attackerFlatForward, float strength, float resistance, float speed, float scale)` and `RamRules.MinResistance` (0.01), replacing `MinDefense`.
  - `RammingModule` loses its public `attack` and `defense` fields.

- [ ] **Step 1: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/CarStatsTests.cs` (9 tests):
```csharp
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarStatsTests
    {
        static readonly object Corroded = new object();
        static readonly object Fortified = new object();

        [Test]
        public void NoModifiers_EffectiveEqualsBase()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            Assert.AreEqual(100f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void TopSpeed_DefaultsToOne()
        {
            Assert.AreEqual(1f, new CarStats().Effective(CarStat.TopSpeed), 1e-6f);
        }

        [Test]
        public void PercentagesFromDifferentSources_Add()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -30f);
            stats.Add(Fortified, CarStat.Defense, 20f);

            Assert.AreEqual(90f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void SameSourceOnTheSameStat_Replaces()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -30f);
            stats.Add(Corroded, CarStat.Defense, -10f);

            Assert.AreEqual(90f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void SameSourceOnDifferentStats_Coexist()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Attack, 100f);
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Attack, -20f);
            stats.Add(Corroded, CarStat.Defense, 50f);

            Assert.AreEqual(80f, stats.Effective(CarStat.Attack), 1e-4f);
            Assert.AreEqual(150f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void Remove_RemovesEveryModifierOfThatSource()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Attack, 100f);
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Attack, -20f);
            stats.Add(Corroded, CarStat.Defense, -20f);
            stats.Add(Fortified, CarStat.Defense, 10f);
            stats.Remove(Corroded);

            Assert.AreEqual(100f, stats.Effective(CarStat.Attack), 1e-4f);
            Assert.AreEqual(110f, stats.Effective(CarStat.Defense), 1e-4f);
        }

        [Test]
        public void RemoveAll_RestoresEveryBase()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -30f);
            stats.Add(Fortified, CarStat.TopSpeed, -50f);
            stats.RemoveAll();

            Assert.AreEqual(100f, stats.Effective(CarStat.Defense), 1e-4f);
            Assert.AreEqual(1f, stats.Effective(CarStat.TopSpeed), 1e-6f);
        }

        [Test]
        public void Effective_NeverGoesBelowZero()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Defense, 100f);
            stats.Add(Corroded, CarStat.Defense, -150f);

            Assert.AreEqual(0f, stats.Effective(CarStat.Defense));
        }

        [Test]
        public void Modifiers_OnlyAffectTheirOwnStat()
        {
            var stats = new CarStats();
            stats.SetBase(CarStat.Attack, 100f);
            stats.SetBase(CarStat.Strength, 1f);
            stats.Add(Corroded, CarStat.Attack, -50f);

            Assert.AreEqual(1f, stats.Effective(CarStat.Strength), 1e-6f);
        }
    }
}
```

In `Assets/_Project/Tests/EditMode/RamRulesTests.cs`, make three replacements.

Replace:
```csharp
        [Test]
        public void ShoveDelta_IsAttackOverDefenseTimesSpeedTimesScale_AlongHeading()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, attack: 2f, defense: 1f, speed: 10f, scale: 1.5f);
```
with:
```csharp
        [Test]
        public void ShoveDelta_IsStrengthOverResistanceTimesSpeedTimesScale_AlongHeading()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, strength: 2f, resistance: 1f, speed: 10f, scale: 1.5f);
```

Replace:
```csharp
        public void ShoveDelta_HigherDefenseShrinksItProportionally()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.right, attack: 1f, defense: 2f, speed: 10f, scale: 1f);
```
with:
```csharp
        public void ShoveDelta_HigherResistanceShrinksItProportionally()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.right, strength: 1f, resistance: 2f, speed: 10f, scale: 1f);
```

Replace:
```csharp
        public void ShoveDelta_ZeroDefenseIsClampedInsteadOfDividingByZero()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, 1f, 0f, 1f, 1f);
            Assert.AreEqual(1f / RamRules.MinDefense, shove.z, 1e-2f);
```
with:
```csharp
        public void ShoveDelta_ZeroResistanceIsClampedInsteadOfDividingByZero()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, 1f, 0f, 1f, 1f);
            Assert.AreEqual(1f / RamRules.MinResistance, shove.z, 1e-2f);
```

In `Assets/_Project/Tests/EditMode/CarFactoryTests.cs`, replace these two `SetUp` lines:
```csharp
            _definition.attack = 2f;
            _definition.defense = 3f;
```
with:
```csharp
            _definition.attack = 150f;
            _definition.defense = 40f;
            _definition.strength = 2f;
            _definition.resistance = 3f;
```
Then replace the whole test `Spawn_WiresRamConfigAndStatsFromTheDefinition` with:
```csharp
        [Test]
        public void Spawn_WiresRamConfigAndStatBasesFromTheDefinition()
        {
            Assert.AreSame(_definition.ramConfig, _car.GetComponent<RammingModule>().config);
            Assert.AreEqual(150f, _car.Stats.Base(CarStat.Attack));
            Assert.AreEqual(40f, _car.Stats.Base(CarStat.Defense));
            Assert.AreEqual(2f, _car.Stats.Base(CarStat.Strength));
            Assert.AreEqual(3f, _car.Stats.Base(CarStat.Resistance));
            Assert.AreEqual(1f, _car.Stats.Base(CarStat.TopSpeed));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`, with compile errors for `CarStats`/`CarStat` and `CarDefinition.strength`.

- [ ] **Step 3: Implement `CarStats`**

Create `Assets/_Project/Scripts/Core/CarStats.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorCombat.Core
{
    public enum CarStat
    {
        /// <summary>Damage multiplier; 100 deals a weapon's listed damage.</summary>
        Attack,

        /// <summary>Damage reduction with diminishing returns.</summary>
        Defense,

        /// <summary>Ram shove dealt.</summary>
        Strength,

        /// <summary>Ram shove received.</summary>
        Resistance,

        /// <summary>Multiplier on engine force, and so on terminal speed. Base 1.</summary>
        TopSpeed
    }

    /// <summary>
    /// Base stats plus percentage modifiers. Lives in Core so the systems that
    /// modify stats (effects) and the systems that read them (damage, ramming,
    /// driving) never reference each other.
    ///
    /// effective = max(0, base × (1 + Σ percent / 100)). Percentages from
    /// different sources ADD, so the order effects were applied never matters.
    /// </summary>
    public class CarStats
    {
        struct Modifier
        {
            public object source;
            public CarStat stat;
            public float percent;
        }

        static readonly int StatCount = Enum.GetValues(typeof(CarStat)).Length;

        readonly float[] _base = new float[StatCount];
        readonly List<Modifier> _modifiers = new List<Modifier>();

        public CarStats()
        {
            _base[(int)CarStat.TopSpeed] = 1f;
        }

        public void SetBase(CarStat stat, float value)
        {
            _base[(int)stat] = value;
        }

        public float Base(CarStat stat)
        {
            return _base[(int)stat];
        }

        public float Effective(CarStat stat)
        {
            float percent = 0f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].stat == stat) percent += _modifiers[i].percent;
            }
            return Mathf.Max(0f, _base[(int)stat] * (1f + percent / 100f));
        }

        /// <summary>Adds a percentage modifier. The same source on the same stat replaces its previous one.</summary>
        public void Add(object source, CarStat stat, float percent)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (ReferenceEquals(_modifiers[i].source, source) && _modifiers[i].stat == stat)
                {
                    _modifiers[i] = new Modifier { source = source, stat = stat, percent = percent };
                    return;
                }
            }

            _modifiers.Add(new Modifier { source = source, stat = stat, percent = percent });
        }

        /// <summary>Removes every modifier registered by this source, on every stat.</summary>
        public void Remove(object source)
        {
            _modifiers.RemoveAll(m => ReferenceEquals(m.source, source));
        }

        public void RemoveAll()
        {
            _modifiers.Clear();
        }
    }
}
```

In `Assets/_Project/Scripts/Core/CarController.cs`, add after the `Abilities` property:
```csharp

        /// <summary>Base stats and percentage modifiers. Set by CarFactory; modified by effects; read by damage, ramming and driving.</summary>
        public CarStats Stats { get; } = new CarStats();
```

- [ ] **Step 4: Rename and add stats on `CarDefinition`**

In `Assets/_Project/Scripts/Cars/CarDefinition.cs`, replace this block:
```csharp
        [Header("Ramming")]
        [Tooltip("Multiplies the shove this car deals when it rams. Balanced against the victim's defense as a ratio.")]
        public float attack = 1f;

        [Tooltip("Divides the shove this car receives when rammed. Must be above zero.")]
        [Min(0.01f)]
        public float defense = 1f;
```
with:
```csharp
        [Header("Combat stats")]
        [Tooltip("Hit points.")]
        [Min(1f)]
        public float maxHealth = 1000f;

        [Tooltip("Damage multiplier. 100 deals a weapon's listed damage; 150 deals 50% more.")]
        [Min(0f)]
        public float attack = 100f;

        [Tooltip("Damage reduction with diminishing returns: damage × 100 / (100 + defense). 0 takes full damage, 100 takes half.")]
        [Min(0f)]
        public float defense = 0f;

        [Header("Ramming")]
        [Tooltip("Multiplies the shove this car deals when it rams. Balanced against the victim's resistance as a ratio.")]
        public float strength = 1f;

        [Tooltip("Divides the shove this car receives when rammed. Must be above zero.")]
        [Min(0.01f)]
        public float resistance = 1f;
```

In `Assets/_Project/Configs/CarDefinition.asset`, rename two YAML keys and leave their values alone:
- `  attack: 1` becomes `  strength: 1`
- `  defense: 1` becomes `  resistance: 1`

Don't add the new fields by hand. Unity uses the C# defaults (1000, 100, 0) until the asset is next saved.

```bash
git diff Assets/_Project/Configs/CarDefinition.asset
```
Expected: exactly those two lines changed.

- [ ] **Step 5: Factory sets stat bases**

In `Assets/_Project/Scripts/Cars/CarFactory.cs`, replace:
```csharp
            var ramming = car.AddComponent<RammingModule>();
            ramming.config = definition.ramConfig;
            ramming.attack = definition.attack;
            ramming.defense = definition.defense;
```
with:
```csharp
            controller.Stats.SetBase(CarStat.Attack, definition.attack);
            controller.Stats.SetBase(CarStat.Defense, definition.defense);
            controller.Stats.SetBase(CarStat.Strength, definition.strength);
            controller.Stats.SetBase(CarStat.Resistance, definition.resistance);

            car.AddComponent<RammingModule>().config = definition.ramConfig;
```

- [ ] **Step 6: Ramming reads effective strength and resistance**

In `Assets/_Project/Scripts/Ramming/RamRules.cs`, replace:
```csharp
        /// <summary>Floor for defense so a misconfigured zero cannot divide by zero.</summary>
        public const float MinDefense = 0.01f;
```
with:
```csharp
        /// <summary>Floor for resistance so a misconfigured zero cannot divide by zero.</summary>
        public const float MinResistance = 0.01f;
```
Then replace the whole `ShoveDelta` method, including its XML comment, with:
```csharp
        /// <summary>
        /// Velocity change for the victim. Mass is deliberately ignored: strength and
        /// resistance are the only balance levers. With equal stats and scale 1 the
        /// victim leaves at the attacker's speed. Never vertical.
        /// </summary>
        /// <param name="attackerFlatForward">Horizontal unit heading of the attacker.</param>
        public static Vector3 ShoveDelta(Vector3 attackerFlatForward, float strength, float resistance, float speed, float scale)
        {
            Vector3 direction = attackerFlatForward;
            direction.y = 0f;
            return direction * (strength / Mathf.Max(MinResistance, resistance) * speed * scale);
        }
```

In `Assets/_Project/Scripts/Ramming/RamConfig.cs`, replace `    /// Global ram tuning. Per-car stats (attack, defense) live on CarDefinition.` with:
```csharp
    /// Global ram tuning. Per-car stats (strength, resistance) live on CarDefinition.
```

In `Assets/_Project/Scripts/Ramming/RammingModule.cs`, delete these lines:
```csharp
        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float attack = 1f;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float defense = 1f;

```
In `ApplyRam`, replace:
```csharp
            Vector3 shove = RamRules.ShoveDelta(attackerSide.flatForward, attacker.attack, victim.defense, attackerSide.forwardSpeed, scale);
```
with:
```csharp
            Vector3 shove = RamRules.ShoveDelta(
                attackerSide.flatForward,
                attacker.Car.Stats.Effective(CarStat.Strength),
                victim.Car.Stats.Effective(CarStat.Resistance),
                attackerSide.forwardSpeed,
                scale);
```
In `ApplyHeadOn`, replace:
```csharp
            // Each car is shoved by the OTHER car's attack, speed and heading, and
            // resists with its own defense. Through the centre: no spin.
            Vector3 toSelf = RamRules.ShoveDelta(them.flatForward, partner.attack, defense, them.forwardSpeed, config.headOnScale);
            Vector3 toPartner = RamRules.ShoveDelta(self.flatForward, attack, partner.defense, self.forwardSpeed, config.headOnScale);
```
with:
```csharp
            // Each car is shoved by the OTHER car's strength, speed and heading, and
            // resists with its own resistance. Through the centre: no spin.
            Vector3 toSelf = RamRules.ShoveDelta(
                them.flatForward,
                partner.Car.Stats.Effective(CarStat.Strength),
                Car.Stats.Effective(CarStat.Resistance),
                them.forwardSpeed,
                config.headOnScale);
            Vector3 toPartner = RamRules.ShoveDelta(
                self.flatForward,
                Car.Stats.Effective(CarStat.Strength),
                partner.Car.Stats.Effective(CarStat.Resistance),
                self.forwardSpeed,
                config.headOnScale);
```

- [ ] **Step 7: Driving reads TopSpeed**

In `Assets/_Project/Scripts/Driving/DrivingModule.cs`, inside the `DrivePhysics.DriveForce(` call, replace the argument line `                config.enginePower,` with:
```csharp
                config.enginePower * _car.Stats.Effective(CarStat.TopSpeed),
```

```bash
grep -rn "\.attack\b\|\.defense\b\|MinDefense" Assets/_Project --include=*.cs
```
Expected: only the `CarFactory.cs` lines reading `definition.attack` and `definition.defense`, plus the `CarFactoryTests.cs` `SetUp` lines.

- [ ] **Step 8: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="133"`.

- [ ] **Step 9: Commit**

```bash
git add -A Assets/_Project
git status --porcelain
git commit -m "Add effective stats; rename ram stats to strength and resistance" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Damage path, health and ram damage

**Files:**
- Create in `Assets/_Project/Scripts/Core/`: `DamageKind.cs`, `DamageRequest.cs`, `DamageOutcome.cs`, `DamageResult.cs`, `DamageReport.cs`, `IDamageable.cs`, `Hostility.cs`, `TickSchedule.cs`
- Create folder `Assets/_Project/Scripts/Combat/` containing `MotorCombat.Combat.asmdef`, `DamageRules.cs`, `HealthState.cs`, `Health.cs`
- Modify:
  - `Assets/_Project/Scripts/Cars/MotorCombat.Cars.asmdef`
  - `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`
  - `Assets/_Project/Scripts/Cars/CarFactory.cs`
  - `Assets/_Project/Scripts/Ramming/RamConfig.cs`
  - `Assets/_Project/Scripts/Ramming/RamRules.cs`
  - `Assets/_Project/Scripts/Ramming/RammingModule.cs`
- Test: create `DamageRulesTests.cs`, `HealthStateTests.cs`, `TickScheduleTests.cs`, `HostilityTests.cs`, `HealthTests.cs`; modify `RamRulesTests.cs` and `CarFactoryTests.cs`

**Interfaces:**
- Consumes: `CarAbilities`, `CarAbility` and `BlockRefresh` from Task 1; `CarStats`, `CarStat` and `CarController.Stats` from Task 2.
- Produces in **Core**:
  - `enum DamageKind { Flat, MaxHealthPercent }`
  - `struct DamageRequest { CarController source; string sourceTag; DamageKind kind; float amount; bool allowNonEnemy; }`
  - `enum DamageOutcome { Applied, NotTargetable, NotHostile, Blocked, ZeroAmount }`
  - `struct DamageResult { DamageOutcome outcome; float dealt; bool killed; static DamageResult Of(DamageOutcome) }`
  - `struct DamageReport { CarController source; CarController target; string sourceTag; DamageKind kind; float dealt; float healthAfter; }`
  - `interface IDamageable` with:
    - `float Current`, `float Max`, `bool IsDestroyed`
    - `DamageResult Apply(in DamageRequest)`
    - `void AddGate(object, Func<DamageRequest,bool>)`, `void RemoveGate(object)`
    - `event Action<DamageReport> Damaged`, `event Action<DamageReport> Destroyed`
  - `static class Hostility` with `static bool AreEnemies(CarController a, CarController b)`, `static void SetRule(Func<CarController,CarController,bool>)`, `static void ResetRule()`
  - `class TickSchedule` with `bool TryTick(object source, object target, float now, float interval)` and `void Forget(object target)`
- Produces in **Combat**:
  - `static class DamageRules` with `const float NeutralAttack = 100f`, `static float RawAmount(DamageKind, float amount, float maxHealth)`, `static float Mitigate(float raw, float attack, float defense)`
  - `class HealthState` with:
    - `HealthState(float max)`
    - `float Max`, `float Current`, `bool IsDestroyed`
    - `void AddGate(object, Func<DamageRequest,bool>)`, `void RemoveGate(object)`
    - `DamageResult Apply(in DamageRequest request, bool targetable, bool hostile, float sourceAttack, float targetDefense)`
  - `class Health : MonoBehaviour, IDamageable` with a public `float maxHealth` and `static readonly object WreckBlock`
- Produces in **Ramming**: `RamConfig.flankDamage`, `RamConfig.rearDamage` and `RamRules.DamageFor(RamType type, float flankDamage, float rearDamage)`

- [ ] **Step 1: Add assembly references**

Create `Assets/_Project/Scripts/Combat/MotorCombat.Combat.asmdef`:
```json
{
    "name": "MotorCombat.Combat",
    "rootNamespace": "MotorCombat.Combat",
    "references": ["MotorCombat.Core"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

In `Assets/_Project/Scripts/Cars/MotorCombat.Cars.asmdef`, add `"MotorCombat.Combat"` as the last entry in `references`.

In `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, add `"MotorCombat.Combat"` after `"MotorCombat.Ramming"` in `references`.

- [ ] **Step 2: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/DamageRulesTests.cs` (9 tests):
```csharp
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class DamageRulesTests
    {
        [TestCase(100f, 0f, 100f)]
        [TestCase(100f, 100f, 50f)]
        [TestCase(150f, 100f, 75f)]
        [TestCase(100f, 50f, 66.6667f)]
        [TestCase(100f, 150f, 40f)]
        [TestCase(100f, 300f, 25f)]
        public void Mitigate_MatchesTheSpecTable(float attack, float defense, float expected)
        {
            Assert.AreEqual(expected, DamageRules.Mitigate(100f, attack, defense), 1e-3f);
        }

        [Test]
        public void Mitigate_NegativeDefenseIsTreatedAsZero()
        {
            Assert.AreEqual(100f, DamageRules.Mitigate(100f, 100f, -50f), 1e-4f);
        }

        [Test]
        public void RawAmount_FlatIsTheAmount()
        {
            Assert.AreEqual(40f, DamageRules.RawAmount(DamageKind.Flat, 40f, 1000f), 1e-4f);
        }

        [Test]
        public void RawAmount_MaxHealthPercentIsAPercentOfMaxHealth()
        {
            Assert.AreEqual(250f, DamageRules.RawAmount(DamageKind.MaxHealthPercent, 25f, 1000f), 1e-4f);
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/HealthStateTests.cs` (13 tests):
```csharp
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class HealthStateTests
    {
        static DamageRequest Flat(float amount, string tag = "test")
        {
            return new DamageRequest { kind = DamageKind.Flat, amount = amount, sourceTag = tag };
        }

        static DamageResult Hit(HealthState state, DamageRequest request, bool targetable = true, bool hostile = true, float attack = 100f, float defense = 0f)
        {
            return state.Apply(request, targetable, hostile, attack, defense);
        }

        [Test]
        public void New_StartsAtMax()
        {
            var state = new HealthState(1000f);
            Assert.AreEqual(1000f, state.Current);
            Assert.AreEqual(1000f, state.Max);
            Assert.IsFalse(state.IsDestroyed);
        }

        [Test]
        public void NotTargetable_ChangesNothing()
        {
            var state = new HealthState(1000f);
            var result = Hit(state, Flat(100f), targetable: false);

            Assert.AreEqual(DamageOutcome.NotTargetable, result.outcome);
            Assert.AreEqual(1000f, state.Current);
        }

        [Test]
        public void NotTargetable_IsCheckedBeforeHostility()
        {
            var state = new HealthState(1000f);
            Assert.AreEqual(DamageOutcome.NotTargetable, Hit(state, Flat(100f), targetable: false, hostile: false).outcome);
        }

        [Test]
        public void NotHostile_ChangesNothing()
        {
            var state = new HealthState(1000f);
            var result = Hit(state, Flat(100f), hostile: false);

            Assert.AreEqual(DamageOutcome.NotHostile, result.outcome);
            Assert.AreEqual(1000f, state.Current);
        }

        [Test]
        public void AllowNonEnemy_BypassesHostility()
        {
            var state = new HealthState(1000f);
            var request = Flat(100f);
            request.allowNonEnemy = true;

            Assert.AreEqual(DamageOutcome.Applied, Hit(state, request, hostile: false).outcome);
            Assert.AreEqual(900f, state.Current, 1e-4f);
        }

        [Test]
        public void Gate_BlocksAndReceivesTheRequest()
        {
            var state = new HealthState(1000f);
            object armored = new object();
            state.AddGate(armored, r => r.sourceTag == "blocked");

            Assert.AreEqual(DamageOutcome.Blocked, Hit(state, Flat(100f, "blocked")).outcome);
            Assert.AreEqual(DamageOutcome.Applied, Hit(state, Flat(100f, "other")).outcome);
        }

        [Test]
        public void RemoveGate_StopsBlocking()
        {
            var state = new HealthState(1000f);
            object armored = new object();
            state.AddGate(armored, r => true);
            state.RemoveGate(armored);

            Assert.AreEqual(DamageOutcome.Applied, Hit(state, Flat(100f)).outcome);
        }

        [Test]
        public void ZeroAttack_GivesZeroAmount()
        {
            var state = new HealthState(1000f);
            Assert.AreEqual(DamageOutcome.ZeroAmount, Hit(state, Flat(100f), attack: 0f).outcome);
            Assert.AreEqual(1000f, state.Current);
        }

        [Test]
        public void Flat_IsMitigatedByAttackAndDefense()
        {
            var state = new HealthState(1000f);
            var result = Hit(state, Flat(100f), attack: 100f, defense: 100f);

            Assert.AreEqual(50f, result.dealt, 1e-4f);
            Assert.AreEqual(950f, state.Current, 1e-4f);
        }

        [Test]
        public void MaxHealthPercent_UsesMaxHealth()
        {
            var state = new HealthState(1000f);
            Hit(state, Flat(500f));
            var request = new DamageRequest { kind = DamageKind.MaxHealthPercent, amount = 10f };
            var result = Hit(state, request);

            Assert.AreEqual(100f, result.dealt, 1e-4f, "10% of MAX (1000), not of current (500)");
        }

        [Test]
        public void Overkill_ClampsAtZero_AndReportsTheHpActuallyRemoved()
        {
            var state = new HealthState(100f);
            var result = Hit(state, Flat(150f));

            Assert.AreEqual(0f, state.Current);
            Assert.AreEqual(100f, result.dealt, 1e-4f);
            Assert.IsTrue(result.killed);
            Assert.IsTrue(state.IsDestroyed);
        }

        [Test]
        public void Killed_IsReportedOnce()
        {
            var state = new HealthState(100f);
            Hit(state, Flat(150f));
            var second = Hit(state, Flat(150f));

            Assert.AreEqual(DamageOutcome.NotTargetable, second.outcome);
            Assert.IsFalse(second.killed);
        }

        [Test]
        public void NonLethalHit_IsNotAKill()
        {
            var state = new HealthState(100f);
            var result = Hit(state, Flat(99f));

            Assert.IsFalse(result.killed);
            Assert.IsFalse(state.IsDestroyed);
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/TickScheduleTests.cs` (7 tests):
```csharp
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class TickScheduleTests
    {
        static readonly object Zone = new object();
        static readonly object OtherZone = new object();
        static readonly object Car = new object();
        static readonly object OtherCar = new object();

        [Test]
        public void FirstTick_IsImmediate()
        {
            Assert.IsTrue(new TickSchedule().TryTick(Zone, Car, 3.7f, 0.5f));
        }

        [Test]
        public void NextTick_WaitsForTheInterval()
        {
            var schedule = new TickSchedule();
            Assert.IsTrue(schedule.TryTick(Zone, Car, 0f, 0.5f));
            Assert.IsFalse(schedule.TryTick(Zone, Car, 0.3f, 0.5f));
            Assert.IsTrue(schedule.TryTick(Zone, Car, 0.5f, 0.5f));
        }

        /// <summary>Leaving and re-entering must not tick faster than the interval.</summary>
        [Test]
        public void Schedule_SurvivesContactBreaks()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            Assert.IsFalse(schedule.TryTick(Zone, Car, 0.2f, 0.5f), "re-entry at 0.2 s");
        }

        [Test]
        public void Targets_AreIndependent()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            Assert.IsTrue(schedule.TryTick(Zone, OtherCar, 0.1f, 0.5f));
        }

        [Test]
        public void Sources_AreIndependent()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            Assert.IsTrue(schedule.TryTick(OtherZone, Car, 0.1f, 0.5f));
        }

        [Test]
        public void Forget_ClearsATarget()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);
            schedule.Forget(Car);
            Assert.IsTrue(schedule.TryTick(Zone, Car, 0.1f, 0.5f));
        }

        /// <summary>25 physics steps of 0.02 s sum to slightly under 0.5 in float.</summary>
        [Test]
        public void AccumulatedFixedSteps_StillTickOnTime()
        {
            var schedule = new TickSchedule();
            schedule.TryTick(Zone, Car, 0f, 0.5f);

            float now = 0f;
            for (int i = 0; i < 25; i++) now += 0.02f;

            Assert.IsTrue(schedule.TryTick(Zone, Car, now, 0.5f));
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/HostilityTests.cs` (4 tests):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class HostilityTests
    {
        GameObject _a;
        GameObject _b;

        [SetUp]
        public void SetUp()
        {
            _a = new GameObject("A", typeof(CarController));
            _b = new GameObject("B", typeof(CarController));
        }

        [TearDown]
        public void TearDown()
        {
            Hostility.ResetRule();
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        CarController A => _a.GetComponent<CarController>();
        CarController B => _b.GetComponent<CarController>();

        [Test]
        public void DifferentCars_AreEnemies()
        {
            Assert.IsTrue(Hostility.AreEnemies(A, B));
        }

        [Test]
        public void ACar_IsNotItsOwnEnemy()
        {
            Assert.IsFalse(Hostility.AreEnemies(A, A));
        }

        [Test]
        public void NullSource_IsAnEnemyOfEveryone()
        {
            Assert.IsTrue(Hostility.AreEnemies(null, A));
        }

        [Test]
        public void ReplacedRule_IsUsed_AndResetRestoresTheDefault()
        {
            Hostility.SetRule((x, y) => false);
            Assert.IsFalse(Hostility.AreEnemies(A, B));

            Hostility.ResetRule();
            Assert.IsTrue(Hostility.AreEnemies(A, B));
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/HealthTests.cs` (5 tests):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    /// <summary>
    /// The Health component end to end, without a scene. EditMode never runs
    /// Awake, and Health needs none: it reads CarController.Abilities/Stats,
    /// which are initialised with the object.
    /// </summary>
    public class HealthTests
    {
        GameObject _sourceObject;
        GameObject _targetObject;
        CarController _source;
        CarController _target;
        Health _health;

        [SetUp]
        public void SetUp()
        {
            _sourceObject = new GameObject("Source", typeof(CarController));
            _source = _sourceObject.GetComponent<CarController>();
            _source.Stats.SetBase(CarStat.Attack, 100f);

            _targetObject = new GameObject("Target", typeof(CarController));
            _target = _targetObject.GetComponent<CarController>();
            _health = _targetObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sourceObject);
            Object.DestroyImmediate(_targetObject);
        }

        DamageRequest Flat(float amount, CarController source)
        {
            return new DamageRequest { source = source, sourceTag = "test", kind = DamageKind.Flat, amount = amount };
        }

        [Test]
        public void Apply_RaisesDamagedWithAReport()
        {
            DamageReport? seen = null;
            _health.Damaged += r => seen = r;

            _health.Apply(Flat(100f, _source));

            Assert.IsTrue(seen.HasValue);
            Assert.AreSame(_source, seen.Value.source);
            Assert.AreSame(_target, seen.Value.target);
            Assert.AreEqual(100f, seen.Value.dealt, 1e-4f);
            Assert.AreEqual(900f, seen.Value.healthAfter, 1e-4f);
        }

        [Test]
        public void Apply_UsesSourceAttackAndTargetDefense()
        {
            _source.Stats.SetBase(CarStat.Attack, 150f);
            _target.Stats.SetBase(CarStat.Defense, 100f);

            var result = _health.Apply(Flat(100f, _source));

            Assert.AreEqual(75f, result.dealt, 1e-3f);
        }

        [Test]
        public void Apply_IgnoresDamageFromItself()
        {
            bool raised = false;
            _health.Damaged += r => raised = true;

            var result = _health.Apply(Flat(100f, _target));

            Assert.AreEqual(DamageOutcome.NotHostile, result.outcome);
            Assert.IsFalse(raised);
        }

        [Test]
        public void Kill_RaisesDestroyedOnce_AndBlocksTheWreckAbilities()
        {
            int destroyed = 0;
            _health.Destroyed += r => destroyed++;

            _health.Apply(Flat(5000f, _source));
            _health.Apply(Flat(5000f, _source));

            Assert.AreEqual(1, destroyed);
            Assert.IsTrue(_health.IsDestroyed);
            Assert.IsFalse(_target.Abilities.Has(CarAbility.Targetable));
            Assert.IsFalse(_target.Abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(_target.Abilities.Has(CarAbility.Fire));
            Assert.IsTrue(_target.Abilities.Has(CarAbility.Grip), "the wreck slides to a stop under grip");
            Assert.IsTrue(_target.Abilities.Has(CarAbility.YawHold), "the wreck does not spin");
        }

        [Test]
        public void Kill_ClearsEarlierBlocksAndModifiers()
        {
            object reel = new object();
            object corroded = new object();
            _target.Abilities.Block(reel, CarAbility.YawHold | CarAbility.Grip, 5f, BlockRefresh.Restart);
            _target.Stats.SetBase(CarStat.Defense, 100f);
            _target.Stats.Add(corroded, CarStat.Defense, -50f);

            _health.Apply(Flat(5000f, _source));

            Assert.IsTrue(_target.Abilities.Has(CarAbility.YawHold | CarAbility.Grip));
            Assert.AreEqual(100f, _target.Stats.Effective(CarStat.Defense), 1e-4f);
        }
    }
}
```

In `Assets/_Project/Tests/EditMode/RamRulesTests.cs`, add this after the `ScaleFor_PicksTheScaleForEachType` test:
```csharp

        [Test]
        public void DamageFor_OnlyFlankAndRearDealDamage()
        {
            Assert.AreEqual(30f, RamRules.DamageFor(RamType.Flank, 30f, 20f));
            Assert.AreEqual(20f, RamRules.DamageFor(RamType.Rear, 30f, 20f));
            Assert.AreEqual(0f, RamRules.DamageFor(RamType.HeadOn, 30f, 20f), "head-ons never deal damage");
            Assert.AreEqual(0f, RamRules.DamageFor(RamType.None, 30f, 20f));
        }
```

In `Assets/_Project/Tests/EditMode/CarFactoryTests.cs`:
1. Add `using MotorCombat.Combat;` below `using MotorCombat.Ramming;`.
2. In `SetUp`, before `_car = CarFactory.Spawn(`, add `            _definition.maxHealth = 750f;`.
3. After the `Spawn_WiresRamConfigAndStatBasesFromTheDefinition` test, add:
```csharp

        [Test]
        public void Spawn_AddsHealthWithMaxFromTheDefinition()
        {
            var health = _car.GetComponent<Health>();
            Assert.IsNotNull(health);
            Assert.AreEqual(750f, health.Max);
            Assert.IsNotNull(_car.GetComponent<IDamageable>());
        }
```

- [ ] **Step 3: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`, with compile errors for `DamageRequest`, `HealthState`, `TickSchedule`, `Hostility` and `RamRules.DamageFor`.

- [ ] **Step 4: Core damage contracts**

`Assets/_Project/Scripts/Core/DamageKind.cs`:
```csharp
namespace MotorCombat.Core
{
    public enum DamageKind
    {
        /// <summary>A fixed number of hit points.</summary>
        Flat,

        /// <summary>A percentage of the target's MAX health.</summary>
        MaxHealthPercent
    }
}
```

`Assets/_Project/Scripts/Core/DamageRequest.cs`:
```csharp
namespace MotorCombat.Core
{
    /// <summary>
    /// One instance of damage. Every source — rams now, weapons and effects later —
    /// sends this same request, so the rules that apply to damage live in exactly
    /// one place. Tick damage is not a kind: a source sends ordinary requests on
    /// a <see cref="TickSchedule"/>.
    /// </summary>
    public struct DamageRequest
    {
        /// <summary>The car that dealt it; null for the environment.</summary>
        public CarController source;

        /// <summary>"ram", a weapon id, an effect id — for attribution and logs.</summary>
        public string sourceTag;

        public DamageKind kind;

        /// <summary>Hit points for Flat; percent of max health for MaxHealthPercent.</summary>
        public float amount;

        /// <summary>False (default): only enemies take it. True: self and allies too, e.g. a self-inflicted debuff.</summary>
        public bool allowNonEnemy;
    }
}
```

`Assets/_Project/Scripts/Core/DamageOutcome.cs`:
```csharp
namespace MotorCombat.Core
{
    public enum DamageOutcome
    {
        Applied,
        NotTargetable,
        NotHostile,
        Blocked,
        ZeroAmount
    }
}
```

`Assets/_Project/Scripts/Core/DamageResult.cs`:
```csharp
namespace MotorCombat.Core
{
    /// <summary>Returned immediately, so a caller (a projectile, a zone) knows at once what its hit did.</summary>
    public struct DamageResult
    {
        public DamageOutcome outcome;

        /// <summary>Hit points actually removed — never more than the target had left.</summary>
        public float dealt;

        /// <summary>True only on the hit that took health to zero.</summary>
        public bool killed;

        public static DamageResult Of(DamageOutcome outcome)
        {
            return new DamageResult { outcome = outcome };
        }
    }
}
```

`Assets/_Project/Scripts/Core/DamageReport.cs`:
```csharp
namespace MotorCombat.Core
{
    /// <summary>What an applied hit did. Raised to the HUD now and to game modes (kill credit) later.</summary>
    public struct DamageReport
    {
        public CarController source;
        public CarController target;
        public string sourceTag;
        public DamageKind kind;

        /// <summary>Hit points actually removed.</summary>
        public float dealt;

        public float healthAfter;
    }
}
```

`Assets/_Project/Scripts/Core/IDamageable.cs`:
```csharp
using System;

namespace MotorCombat.Core
{
    /// <summary>
    /// Something that takes damage. Implemented in the Combat assembly; declared
    /// here so ramming, weapons, effects and the HUD use it without referencing
    /// Combat.
    /// </summary>
    public interface IDamageable
    {
        float Current { get; }
        float Max { get; }
        bool IsDestroyed { get; }

        DamageResult Apply(in DamageRequest request);

        /// <summary>A gate returning true blocks the request (e.g. Armored). Keyed by source; re-adding replaces.</summary>
        void AddGate(object source, Func<DamageRequest, bool> blocks);

        void RemoveGate(object source);

        event Action<DamageReport> Damaged;
        event Action<DamageReport> Destroyed;
    }
}
```

`Assets/_Project/Scripts/Core/Hostility.cs`:
```csharp
using System;

namespace MotorCombat.Core
{
    /// <summary>
    /// Who is an enemy of whom. There are no teams yet, so every other car is an
    /// enemy. Team modes replace the rule; no caller changes.
    /// </summary>
    public static class Hostility
    {
        static readonly Func<CarController, CarController, bool> DefaultRule = (a, b) => a != b;

        static Func<CarController, CarController, bool> _rule = DefaultRule;

        /// <summary>True when either car is null (the environment hurts everyone), otherwise the current rule.</summary>
        public static bool AreEnemies(CarController a, CarController b)
        {
            if (a == null || b == null) return true;
            return _rule(a, b);
        }

        public static void SetRule(Func<CarController, CarController, bool> rule)
        {
            _rule = rule ?? DefaultRule;
        }

        public static void ResetRule()
        {
            _rule = DefaultRule;
        }
    }
}
```

`Assets/_Project/Scripts/Core/TickSchedule.cs`:
```csharp
using System.Collections.Generic;

namespace MotorCombat.Core
{
    /// <summary>
    /// When a tick of damage (or any periodic effect) is due. The first tick for a
    /// (source, target) pair is immediate; the next is due one interval after the
    /// last. The schedule survives contact breaks, so brushing in and out of a
    /// zone cannot tick faster than its interval.
    /// </summary>
    public class TickSchedule
    {
        // Physics time accumulates in float steps; 25 × 0.02 lands just under 0.5.
        const float Tolerance = 1e-4f;

        readonly Dictionary<(object source, object target), float> _lastTick = new Dictionary<(object, object), float>();
        readonly List<(object source, object target)> _forget = new List<(object source, object target)>();

        /// <summary>Returns true, and records the tick, when one is due.</summary>
        public bool TryTick(object source, object target, float now, float interval)
        {
            var key = (source, target);
            if (_lastTick.TryGetValue(key, out float last) && now < last + interval - Tolerance)
            {
                return false;
            }

            _lastTick[key] = now;
            return true;
        }

        /// <summary>Clears every entry for a target, e.g. when it is removed.</summary>
        public void Forget(object target)
        {
            _forget.Clear();
            foreach (var key in _lastTick.Keys)
            {
                if (Equals(key.target, target)) _forget.Add(key);
            }
            for (int i = 0; i < _forget.Count; i++) _lastTick.Remove(_forget[i]);
        }
    }
}
```

- [ ] **Step 5: Combat rules and health**

`Assets/_Project/Scripts/Combat/DamageRules.cs`:
```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// The damage formula. MOBA pattern: attack scales the hit as a percentage,
    /// defense removes a share with diminishing returns and never reaches zero.
    /// </summary>
    public static class DamageRules
    {
        /// <summary>Attack used when there is no source car (the environment).</summary>
        public const float NeutralAttack = 100f;

        public static float RawAmount(DamageKind kind, float amount, float maxHealth)
        {
            return kind == DamageKind.MaxHealthPercent ? amount / 100f * maxHealth : amount;
        }

        /// <summary>raw × attack/100 × 100/(100 + defense). Negative defense counts as 0.</summary>
        public static float Mitigate(float raw, float attack, float defense)
        {
            return raw * (attack / 100f) * (100f / (100f + Mathf.Max(0f, defense)));
        }
    }
}
```

`Assets/_Project/Scripts/Combat/HealthState.cs`:
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// The damage path as a plain class: targetable → hostility → gates → raw
    /// amount → mitigation → apply. The caller supplies the facts that need a
    /// scene (targetable, hostile, attack, defense), so every rule is testable
    /// without one.
    /// </summary>
    public class HealthState
    {
        readonly List<KeyValuePair<object, Func<DamageRequest, bool>>> _gates =
            new List<KeyValuePair<object, Func<DamageRequest, bool>>>();

        public float Max { get; }
        public float Current { get; private set; }
        public bool IsDestroyed { get; private set; }

        public HealthState(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        public void AddGate(object source, Func<DamageRequest, bool> blocks)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));

            RemoveGate(source);
            _gates.Add(new KeyValuePair<object, Func<DamageRequest, bool>>(source, blocks));
        }

        public void RemoveGate(object source)
        {
            for (int i = _gates.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_gates[i].Key, source)) _gates.RemoveAt(i);
            }
        }

        public DamageResult Apply(in DamageRequest request, bool targetable, bool hostile, float sourceAttack, float targetDefense)
        {
            if (!targetable || IsDestroyed) return DamageResult.Of(DamageOutcome.NotTargetable);
            if (!request.allowNonEnemy && !hostile) return DamageResult.Of(DamageOutcome.NotHostile);

            for (int i = 0; i < _gates.Count; i++)
            {
                if (_gates[i].Value(request)) return DamageResult.Of(DamageOutcome.Blocked);
            }

            float raw = DamageRules.RawAmount(request.kind, request.amount, Max);
            float mitigated = DamageRules.Mitigate(raw, sourceAttack, targetDefense);

            // Written as !(x > 0) so a NaN from a broken config is rejected too.
            if (!(mitigated > 0f)) return DamageResult.Of(DamageOutcome.ZeroAmount);

            float dealt = Mathf.Min(mitigated, Current);
            Current -= dealt;

            bool killed = false;
            if (Current <= 0f)
            {
                Current = 0f;
                IsDestroyed = true;
                killed = true;
            }

            return new DamageResult { outcome = DamageOutcome.Applied, dealt = dealt, killed = killed };
        }
    }
}
```

`Assets/_Project/Scripts/Combat/Health.cs`:
```csharp
using System;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// A car's health. Thin adapter: gathers targetable, hostility, attack and
    /// defense from the car, runs <see cref="HealthState"/>, raises events, and on
    /// the killing hit turns the car into a wreck.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class Health : MonoBehaviour, IDamageable
    {
        /// <summary>Source key for the wreck's untimed ability block.</summary>
        public static readonly object WreckBlock = new object();

        const CarAbility WreckMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire
                                     | CarAbility.Ram | CarAbility.Targetable;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float maxHealth = 1000f;

        public event Action<DamageReport> Damaged;
        public event Action<DamageReport> Destroyed;

        HealthState _state;
        CarController _car;

        // Created on first use, after CarFactory has set maxHealth.
        HealthState State => _state ?? (_state = new HealthState(maxHealth));
        CarController Car => _car != null ? _car : (_car = GetComponent<CarController>());

        public float Current => State.Current;
        public float Max => State.Max;
        public bool IsDestroyed => State.IsDestroyed;

        public void AddGate(object source, Func<DamageRequest, bool> blocks) => State.AddGate(source, blocks);
        public void RemoveGate(object source) => State.RemoveGate(source);

        public DamageResult Apply(in DamageRequest request)
        {
            CarController car = Car;

            DamageResult result = State.Apply(
                request,
                car.Abilities.Has(CarAbility.Targetable),
                Hostility.AreEnemies(request.source, car),
                request.source != null ? request.source.Stats.Effective(CarStat.Attack) : DamageRules.NeutralAttack,
                car.Stats.Effective(CarStat.Defense));

            if (result.outcome != DamageOutcome.Applied) return result;

            var report = new DamageReport
            {
                source = request.source,
                target = car,
                sourceTag = request.sourceTag,
                kind = request.kind,
                dealt = result.dealt,
                healthAfter = State.Current
            };

            Damaged?.Invoke(report);

            if (result.killed)
            {
                // A wreck carries nothing over: ram lock, reel and every effect end.
                car.Abilities.UnblockAll();
                car.Stats.RemoveAll();
                car.Abilities.Block(WreckBlock, WreckMask, float.PositiveInfinity, BlockRefresh.KeepLonger);

                Destroyed?.Invoke(report);
            }

            return result;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: take 25% max HP")]
        void DebugTakeQuarter()
        {
            Apply(new DamageRequest { sourceTag = "debug", kind = DamageKind.MaxHealthPercent, amount = 25f });
        }

        [ContextMenu("Debug: destroy")]
        void DebugDestroy()
        {
            Apply(new DamageRequest { sourceTag = "debug", kind = DamageKind.Flat, amount = 1e9f });
        }
#endif
    }
}
```

- [ ] **Step 6: Factory adds health**

In `Assets/_Project/Scripts/Cars/CarFactory.cs`, add `using MotorCombat.Combat;` below `using MotorCombat.Core;`. Then, before the line `            controller.Bind(provider);`, add:
```csharp
            car.AddComponent<Health>().maxHealth = definition.maxHealth;
```

- [ ] **Step 7: Ram damage**

In `Assets/_Project/Scripts/Ramming/RamConfig.cs`, add this before the line `        [Header("States (seconds)")]`:
```csharp
        [Header("Damage (flat, scaled by attack and defense)")]
        [Tooltip("Flat damage a flank ram deals to the victim. Head-ons never deal damage.")]
        [Min(0f)]
        public float flankDamage = 0f;

        [Tooltip("Flat damage a rear ram deals to the victim.")]
        [Min(0f)]
        public float rearDamage = 0f;

```

In `Assets/_Project/Scripts/Ramming/RamRules.cs`, add this after the `ScaleFor` method:
```csharp

        /// <summary>Flat damage a ram deals. Only flank and rear rams damage; head-ons never do.</summary>
        public static float DamageFor(RamType type, float flankDamage, float rearDamage)
        {
            switch (type)
            {
                case RamType.Flank: return flankDamage;
                case RamType.Rear: return rearDamage;
                default: return 0f;
            }
        }
```

In `Assets/_Project/Scripts/Ramming/RammingModule.cs`, inside `ApplyRam`, add this after the line `            victim.Car.Abilities.Block(ReelBlock, ReelMask, config.reelSeconds, BlockRefresh.Restart);`:
```csharp

            float damage = RamRules.DamageFor(type, config.flankDamage, config.rearDamage);
            if (damage > 0f)
            {
                victim.GetComponent<IDamageable>()?.Apply(new DamageRequest
                {
                    source = attacker.Car,
                    sourceTag = "ram",
                    kind = DamageKind.Flat,
                    amount = damage
                });
            }
```

- [ ] **Step 8: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="173"`.

- [ ] **Step 9: Commit**

```bash
git add -A Assets/_Project
git status --porcelain
git commit -m "Add damage path, health component and ram damage" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Physics layers and the wreck sequence

**Files:**
- Modify: `ProjectSettings/TagManager.asset`
- Create: `Assets/_Project/Scripts/Core/PhysicsLayers.cs`
- Create in `Assets/_Project/Scripts/Combat/`: `WreckConfig.cs`, `WreckMath.cs`, `WreckMaterials.cs`, `WreckSequence.cs`
- Modify:
  - `Assets/_Project/Scripts/Arena/ArenaBuilder.cs`
  - `Assets/_Project/Scripts/Cars/CarDefinition.cs`
  - `Assets/_Project/Scripts/Cars/CarFactory.cs`
  - `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`
  - `Assets/_Project/Scripts/Bootstrap/MotorCombat.Bootstrap.asmdef`
  - `Assets/_Project/Editor/ConfigAssetBootstrap.cs`
  - `Assets/_Project/Editor/MotorCombat.EditorTools.asmdef`
- Test: create `PhysicsLayersTests.cs`, `WreckMathTests.cs`, `WreckMaterialsTests.cs`; modify `ArenaBuilderTests.cs`, `CarFactoryTests.cs`

**Interfaces:**
- Consumes: `Health` (its `Destroyed` event) and `DamageReport` from Task 3.
- Produces:
  - `MotorCombat.Core.PhysicsLayers` with:
    - `const string ArenaName/CarName/WreckName`
    - `static int Arena/Car/Wreck`
    - `static void ConfigureCollisions()`
  - `MotorCombat.Combat.WreckConfig` with fields `rollDegrees`, `rollSeconds`, `fadeSeconds`, `removeAfterSeconds`
  - `WreckMath`:
    - `RollAngle(float elapsed, float rollSeconds, float rollDegrees)`
    - `Lift(float angleDegrees, float halfWidth, float halfHeight)`
    - `Alpha(float elapsed, float fadeSeconds)`
  - `WreckMaterials.MakeTransparent(Material)`
  - `WreckSequence : MonoBehaviour` with a public `WreckConfig config`
  - `CarDefinition.wreckConfig`

- [ ] **Step 1: Name the layers**

In `ProjectSettings/TagManager.asset`, the `layers:` list has 32 entries. A blank entry is the line `  - ` (dash, space). Change entries 8, 9 and 10 (0-based; entry 0 is `Default`, entry 5 is `UI`) so the start of the list reads exactly:
```
  layers:
  - Default
  - TransparentFX
  - Ignore Raycast
  - 
  - Water
  - UI
  - 
  - 
  - Arena
  - Car
  - Wreck
  - 
```
All remaining entries stay blank, and the list must still have 32 entries:
```bash
sed -n '/layers:/,/m_SortingLayers/p' ProjectSettings/TagManager.asset | grep -c "^  - "
```
Expected: `32`.

- [ ] **Step 2: Add assembly references**

- In `Assets/_Project/Scripts/Bootstrap/MotorCombat.Bootstrap.asmdef`, add `"MotorCombat.Combat"` as the last `references` entry.
- In `Assets/_Project/Editor/MotorCombat.EditorTools.asmdef`, add `"MotorCombat.Combat"` as the last `references` entry.

- [ ] **Step 3: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/PhysicsLayersTests.cs` (3 tests):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    /// <summary>
    /// ConfigureCollisions changes the project-wide layer matrix, so every test
    /// restores the rows it touched.
    /// </summary>
    public class PhysicsLayersTests
    {
        bool[] _carRow;
        bool[] _wreckRow;

        [SetUp]
        public void SetUp()
        {
            _carRow = Capture(PhysicsLayers.Car);
            _wreckRow = Capture(PhysicsLayers.Wreck);
        }

        [TearDown]
        public void TearDown()
        {
            Restore(PhysicsLayers.Car, _carRow);
            Restore(PhysicsLayers.Wreck, _wreckRow);
        }

        static bool[] Capture(int layer)
        {
            var row = new bool[32];
            if (layer < 0) return row;
            for (int i = 0; i < 32; i++) row[i] = Physics.GetIgnoreLayerCollision(layer, i);
            return row;
        }

        static void Restore(int layer, bool[] row)
        {
            if (layer < 0) return;
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(layer, i, row[i]);
        }

        [Test]
        public void LayerNames_ResolveToTheReservedIndices()
        {
            Assert.AreEqual(8, PhysicsLayers.Arena);
            Assert.AreEqual(9, PhysicsLayers.Car);
            Assert.AreEqual(10, PhysicsLayers.Wreck);
        }

        [Test]
        public void ConfigureCollisions_WreckCollidesOnlyWithTheArena()
        {
            PhysicsLayers.ConfigureCollisions();

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Arena));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Car));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, PhysicsLayers.Wreck));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Wreck, 0), "Default");
        }

        [Test]
        public void ConfigureCollisions_CarsCollideWithCarsAndTheArena()
        {
            PhysicsLayers.ConfigureCollisions();

            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Car, PhysicsLayers.Car));
            Assert.IsFalse(Physics.GetIgnoreLayerCollision(PhysicsLayers.Car, PhysicsLayers.Arena));
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/WreckMathTests.cs` (10 tests):
```csharp
using NUnit.Framework;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class WreckMathTests
    {
        [Test]
        public void RollAngle_StartsAtZero() => Assert.AreEqual(0f, WreckMath.RollAngle(0f, 0.8f, 180f), 1e-4f);

        [Test]
        public void RollAngle_IsFullAtAndAfterRollSeconds()
        {
            Assert.AreEqual(180f, WreckMath.RollAngle(0.8f, 0.8f, 180f), 1e-3f);
            Assert.AreEqual(180f, WreckMath.RollAngle(5f, 0.8f, 180f), 1e-3f);
        }

        [Test]
        public void RollAngle_IsHalfwayAtHalfTime() => Assert.AreEqual(90f, WreckMath.RollAngle(0.4f, 0.8f, 180f), 1e-3f);

        [Test]
        public void RollAngle_ZeroDurationIsFull() => Assert.AreEqual(180f, WreckMath.RollAngle(0f, 0f, 180f), 1e-4f);

        [Test]
        public void Lift_IsZeroWhenUpright() => Assert.AreEqual(0f, WreckMath.Lift(0f, 1.1f, 0.67f), 1e-4f);

        /// <summary>On its side the car stands on its half WIDTH instead of its half height.</summary>
        [Test]
        public void Lift_AtNinetyDegreesIsHalfWidthMinusHalfHeight() => Assert.AreEqual(0.43f, WreckMath.Lift(90f, 1.1f, 0.67f), 1e-3f);

        [Test]
        public void Lift_IsZeroWhenUpsideDown() => Assert.AreEqual(0f, WreckMath.Lift(180f, 1.1f, 0.67f), 1e-3f);

        [Test]
        public void Lift_IsNeverNegative() => Assert.AreEqual(0f, WreckMath.Lift(90f, 0.5f, 1f), 1e-4f);

        [Test]
        public void Alpha_FadesLinearly()
        {
            Assert.AreEqual(1f, WreckMath.Alpha(0f, 1.5f), 1e-4f);
            Assert.AreEqual(0.5f, WreckMath.Alpha(0.75f, 1.5f), 1e-4f);
            Assert.AreEqual(0f, WreckMath.Alpha(2f, 1.5f), 1e-4f);
        }

        [Test]
        public void Alpha_ZeroDurationIsInvisible() => Assert.AreEqual(0f, WreckMath.Alpha(0f, 0f), 1e-4f);
    }
}
```

Create `Assets/_Project/Tests/EditMode/WreckMaterialsTests.cs` (1 test):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Combat;

namespace MotorCombat.Tests
{
    public class WreckMaterialsTests
    {
        [Test]
        public void MakeTransparent_SwitchesUrpLitToTransparent()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) Assert.Ignore("URP Lit shader not available in this run");

            var material = new Material(lit);
            WreckMaterials.MakeTransparent(material);

            Assert.AreEqual(1f, material.GetFloat("_Surface"));
            Assert.AreEqual(0f, material.GetFloat("_ZWrite"));
            Assert.AreEqual(3000, material.renderQueue);
            Assert.IsTrue(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));

            Object.DestroyImmediate(material);
        }
    }
}
```

In `Assets/_Project/Tests/EditMode/ArenaBuilderTests.cs`, add `using MotorCombat.Core;` below `using MotorCombat.Arena;`, then add this test at the end of the class:
```csharp

        [Test]
        public void Build_PutsGroundAndWallOnTheArenaLayer()
        {
            var config = Config();
            config.groundMaterial = AnyMaterial();
            config.wallMaterial = AnyMaterial();

            GameObject root = ArenaBuilder.Build(config, CarLength);
            try
            {
                Assert.AreEqual(PhysicsLayers.Arena, root.transform.Find("Ground").gameObject.layer);
                Assert.AreEqual(PhysicsLayers.Arena, root.transform.Find("Wall").gameObject.layer);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
```

In `Assets/_Project/Tests/EditMode/CarFactoryTests.cs`:
1. In `SetUp`, before `_car = CarFactory.Spawn(`, add `            _definition.wreckConfig = ScriptableObject.CreateInstance<WreckConfig>();`.
2. In `TearDown`, before `Object.DestroyImmediate(_definition);`, add `            Object.DestroyImmediate(_definition.wreckConfig);`.
3. After `Spawn_AddsHealthWithMaxFromTheDefinition`, add:
```csharp

        [Test]
        public void Spawn_PutsTheRootOnTheCarLayer()
        {
            Assert.AreEqual(PhysicsLayers.Car, _car.gameObject.layer);
        }

        [Test]
        public void Spawn_AddsTheWreckSequenceWithItsConfig()
        {
            var wreck = _car.GetComponent<WreckSequence>();
            Assert.IsNotNull(wreck);
            Assert.AreSame(_definition.wreckConfig, wreck.config);
        }
```

- [ ] **Step 4: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`, with compile errors for `PhysicsLayers`, `WreckMath`, `WreckMaterials`, `WreckConfig` and `CarDefinition.wreckConfig`.

- [ ] **Step 5: Implement `PhysicsLayers`**

`Assets/_Project/Scripts/Core/PhysicsLayers.cs`:
```csharp
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Physics layers and which of them collide. Configured in code, once at
    /// boot, so the rules are reviewable here rather than hidden in a
    /// ProjectSettings bit matrix. The names must exist in TagManager.
    ///
    /// Later sub-projects add their own layers (projectiles, obstacles, zones)
    /// beside these.
    /// </summary>
    public static class PhysicsLayers
    {
        public const string ArenaName = "Arena";
        public const string CarName = "Car";
        public const string WreckName = "Wreck";

        public static int Arena => LayerMask.NameToLayer(ArenaName);
        public static int Car => LayerMask.NameToLayer(CarName);
        public static int Wreck => LayerMask.NameToLayer(WreckName);

        /// <summary>A wreck touches only the arena (floor and wall). Cars touch cars and the arena.</summary>
        public static void ConfigureCollisions()
        {
            int arena = Arena;
            int car = Car;
            int wreck = Wreck;

            if (arena < 0 || car < 0 || wreck < 0)
            {
                Debug.LogError($"[MotorCombat] Physics layers '{ArenaName}', '{CarName}' and '{WreckName}' must exist in Project Settings > Tags and Layers.");
                return;
            }

            for (int layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(wreck, layer, layer != arena);
            }

            Physics.IgnoreLayerCollision(car, car, false);
            Physics.IgnoreLayerCollision(car, arena, false);
        }
    }
}
```

In `Assets/_Project/Scripts/Arena/ArenaBuilder.cs`, add `using MotorCombat.Core;` to the usings if it isn't already there. In `CreatePiece`, after `            piece.transform.SetParent(parent, false);`, add:
```csharp

            int arenaLayer = PhysicsLayers.Arena;
            if (arenaLayer >= 0) piece.layer = arenaLayer;
```

- [ ] **Step 6: Implement the wreck pieces**

`Assets/_Project/Scripts/Combat/WreckConfig.cs`:
```csharp
using UnityEngine;

namespace MotorCombat.Combat
{
    /// <summary>How a destroyed car leaves. Placeholders — tuning belongs to the designer.</summary>
    [CreateAssetMenu(menuName = "Motor Combat/Wreck Config", fileName = "WreckConfig")]
    public class WreckConfig : ScriptableObject
    {
        [Tooltip("Barrel roll about the car's length axis, in degrees. Visual only; the physics box stays flat.")]
        public float rollDegrees = 180f;

        [Tooltip("Seconds the roll takes, eased.")]
        [Min(0f)]
        public float rollSeconds = 0.8f;

        [Tooltip("Seconds to fade from opaque to invisible.")]
        [Min(0f)]
        public float fadeSeconds = 1.5f;

        [Tooltip("Seconds after destruction when the car is deactivated (not deleted, so game modes can respawn it).")]
        [Min(0f)]
        public float removeAfterSeconds = 1.5f;
    }
}
```

`Assets/_Project/Scripts/Combat/WreckMath.cs`:
```csharp
using UnityEngine;

namespace MotorCombat.Combat
{
    /// <summary>Pure timing and geometry for the wreck roll and fade.</summary>
    public static class WreckMath
    {
        /// <summary>Eased roll angle in degrees at <paramref name="elapsed"/> seconds.</summary>
        public static float RollAngle(float elapsed, float rollSeconds, float rollDegrees)
        {
            float t = rollSeconds > 0f ? Mathf.Clamp01(elapsed / rollSeconds) : 1f;
            return Mathf.SmoothStep(0f, rollDegrees, t);
        }

        /// <summary>
        /// How far the model must rise so its lowest corner stays on the floor
        /// while rolled. A car is wider than it is tall, so on its side it would
        /// otherwise sink by halfWidth − halfHeight.
        /// </summary>
        public static float Lift(float angleDegrees, float halfWidth, float halfHeight)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float reach = halfWidth * Mathf.Abs(Mathf.Sin(radians)) + halfHeight * Mathf.Abs(Mathf.Cos(radians));
            return Mathf.Max(0f, reach - halfHeight);
        }

        /// <summary>Linear fade from 1 to 0.</summary>
        public static float Alpha(float elapsed, float fadeSeconds)
        {
            return fadeSeconds > 0f ? 1f - Mathf.Clamp01(elapsed / fadeSeconds) : 0f;
        }
    }
}
```

`Assets/_Project/Scripts/Combat/WreckMaterials.cs`:
```csharp
using UnityEngine;
using UnityEngine.Rendering;

namespace MotorCombat.Combat
{
    /// <summary>
    /// URP Lit materials are opaque, and an opaque material cannot be faded by
    /// changing its colour's alpha. This switches a (cloned) material to URP's
    /// transparent surface, which is what the material inspector does.
    /// </summary>
    public static class WreckMaterials
    {
        public static void MakeTransparent(Material material)
        {
            if (material == null || !material.HasProperty("_Surface")) return;

            material.SetFloat("_Surface", 1f);   // Transparent
            material.SetFloat("_Blend", 0f);     // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
```

`Assets/_Project/Scripts/Combat/WreckSequence.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// What a destroyed car does after the killing hit. Health has already
    /// blocked its abilities; this moves it to the Wreck layer (so it phases
    /// through everything but the arena), barrel-rolls and fades the model, and
    /// deactivates the car.
    ///
    /// Visual only: the physics box stays flat and keeps sliding to a stop.
    /// On removal the original materials, transforms and colours are put back,
    /// so a future respawn only has to reactivate the car.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class WreckSequence : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public WreckConfig config;

        struct Rolled
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
        }

        struct Faded
        {
            public Renderer renderer;
            public int index;
            public Color colour;
        }

        struct Swapped
        {
            public Renderer renderer;
            public Material[] original;
        }

        readonly List<Rolled> _rolled = new List<Rolled>();
        readonly List<Faded> _faded = new List<Faded>();
        readonly List<Swapped> _swapped = new List<Swapped>();
        readonly List<Material> _clones = new List<Material>();

        MaterialPropertyBlock _block;
        Health _health;
        float _elapsed;
        float _halfWidth;
        float _halfHeight;
        bool _running;

        void Awake()
        {
            // Subscribed in Awake: the killing hit may arrive before Start.
            _health = GetComponent<Health>();
            _health.Destroyed += OnDestroyedByDamage;
        }

        void Start()
        {
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] WreckSequence on '{name}' has no WreckConfig assigned — a destroyed car will vanish instantly.", this);
            }
        }

        void OnDestroy()
        {
            if (_health != null) _health.Destroyed -= OnDestroyedByDamage;
            RestoreVisuals();
        }

        void OnDestroyedByDamage(DamageReport report)
        {
            int wreckLayer = PhysicsLayers.Wreck;
            if (wreckLayer >= 0) gameObject.layer = wreckLayer;

            if (config == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var box = GetComponent<BoxCollider>();
            _halfWidth = box != null ? box.size.x * 0.5f : 1f;
            _halfHeight = box != null ? box.size.y * 0.5f : 0.5f;

            CaptureVisuals();
            _elapsed = 0f;
            _running = true;
        }

        void Update()
        {
            if (!_running) return;

            _elapsed += Time.deltaTime;

            float angle = WreckMath.RollAngle(_elapsed, config.rollSeconds, config.rollDegrees);
            Quaternion roll = Quaternion.AngleAxis(angle, Vector3.forward);
            Vector3 lift = Vector3.up * WreckMath.Lift(angle, _halfWidth, _halfHeight);

            // Rotating each child's offset about the root rolls the whole model
            // about the collider centre, whatever the child's own pivot.
            for (int i = 0; i < _rolled.Count; i++)
            {
                Rolled rolled = _rolled[i];
                if (rolled.transform == null) continue;
                rolled.transform.localRotation = roll * rolled.rotation;
                rolled.transform.localPosition = roll * rolled.position + lift;
            }

            float alpha = WreckMath.Alpha(_elapsed, config.fadeSeconds);
            for (int i = 0; i < _faded.Count; i++)
            {
                Faded faded = _faded[i];
                if (faded.renderer == null) continue;

                faded.renderer.GetPropertyBlock(_block, faded.index);
                Color colour = faded.colour;
                colour.a *= alpha;
                _block.SetColor(BaseColorId, colour);
                faded.renderer.SetPropertyBlock(_block, faded.index);
            }

            if (_elapsed >= config.removeAfterSeconds)
            {
                _running = false;
                RestoreVisuals();
                gameObject.SetActive(false);
            }
        }

        void CaptureVisuals()
        {
            RestoreVisuals();
            if (_block == null) _block = new MaterialPropertyBlock();

            // Every direct child but the first-person eye: the model, or the
            // placeholder box AND its separate nose marker.
            foreach (Transform child in transform)
            {
                if (child.name == CarController.DriverAnchorName) continue;
                _rolled.Add(new Rolled { transform = child, position = child.localPosition, rotation = child.localRotation });
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] original = renderer.sharedMaterials;
                var copies = new Material[original.Length];

                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;

                    copies[i] = new Material(original[i]);
                    WreckMaterials.MakeTransparent(copies[i]);
                    _clones.Add(copies[i]);

                    // Start from the team tint in the property block when there
                    // is one, so fading never loses the car's colour.
                    renderer.GetPropertyBlock(_block, i);
                    Color colour = _block.HasColor(BaseColorId)
                        ? _block.GetColor(BaseColorId)
                        : copies[i].HasProperty(BaseColorId) ? copies[i].GetColor(BaseColorId) : Color.white;

                    _faded.Add(new Faded { renderer = renderer, index = i, colour = colour });
                }

                _swapped.Add(new Swapped { renderer = renderer, original = original });
                renderer.sharedMaterials = copies;
            }
        }

        void RestoreVisuals()
        {
            for (int i = 0; i < _rolled.Count; i++)
            {
                if (_rolled[i].transform == null) continue;
                _rolled[i].transform.localPosition = _rolled[i].position;
                _rolled[i].transform.localRotation = _rolled[i].rotation;
            }

            if (_block != null)
            {
                for (int i = 0; i < _faded.Count; i++)
                {
                    Faded faded = _faded[i];
                    if (faded.renderer == null) continue;
                    faded.renderer.GetPropertyBlock(_block, faded.index);
                    _block.SetColor(BaseColorId, faded.colour);
                    faded.renderer.SetPropertyBlock(_block, faded.index);
                }
            }

            for (int i = 0; i < _swapped.Count; i++)
            {
                if (_swapped[i].renderer != null) _swapped[i].renderer.sharedMaterials = _swapped[i].original;
            }

            for (int i = 0; i < _clones.Count; i++)
            {
                if (_clones[i] != null) Destroy(_clones[i]);
            }

            _rolled.Clear();
            _faded.Clear();
            _swapped.Clear();
            _clones.Clear();
        }
    }
}
```

- [ ] **Step 7: Wire definition, factory, bootstrap and config asset**

In `Assets/_Project/Scripts/Cars/CarDefinition.cs`, add `using MotorCombat.Combat;` below `using MotorCombat.Ramming;`. Then after `        public RamConfig ramConfig;`, add:
```csharp
        public WreckConfig wreckConfig;
```

In `Assets/_Project/Scripts/Cars/CarFactory.cs`:
1. After `            var car = new GameObject(definition.name);`, add:
```csharp

            int carLayer = PhysicsLayers.Car;
            if (carLayer >= 0) car.layer = carLayer;
```
2. Replace `            car.AddComponent<Health>().maxHealth = definition.maxHealth;` with:
```csharp
            car.AddComponent<Health>().maxHealth = definition.maxHealth;
            car.AddComponent<WreckSequence>().config = definition.wreckConfig;
```

In `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`:
1. Add `using MotorCombat.Core;` to the usings.
2. In `Start()`, after `            if (!Validate()) return;`, add:
```csharp

            PhysicsLayers.ConfigureCollisions();
```
3. In `Validate()`, after the existing line `            if (carDefinition.ramConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.ramConfig is not assigned."); return false; }`, add:
```csharp
            if (carDefinition.wreckConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.wreckConfig is not assigned."); return false; }
```

In `Assets/_Project/Editor/ConfigAssetBootstrap.cs`:
1. Add `using MotorCombat.Combat;` to the usings.
2. After the line `            var ram = GetOrCreate<RamConfig>("RamConfig");`, add:
```csharp
            var wreck = GetOrCreate<WreckConfig>("WreckConfig");
```
3. After `            if (car.ramConfig == null) car.ramConfig = ram;`, add:
```csharp
            if (car.wreckConfig == null) car.wreckConfig = wreck;
```

- [ ] **Step 8: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="190"`. `WreckMaterialsTests` may report as passed or ignored; either way it counts in `total`, and `failed` must be 0.

- [ ] **Step 9: Commit**

```bash
git add -A Assets/_Project ProjectSettings/TagManager.asset
git status --porcelain
git commit -m "Add physics layers and the wreck roll-and-fade sequence" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Car registry and HUD

**Files:**
- Create: `Assets/_Project/Scripts/Core/CarRegistry.cs`
- Modify:
  - `Assets/_Project/Scripts/Core/CarController.cs`
  - `Assets/_Project/Scripts/HUD/MotorCombat.HUD.asmdef`
  - `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`
  - `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`
- Create in `Assets/_Project/Scripts/HUD/`: `HudRoot.cs`, `HudElements.cs`, `HealthBarLayout.cs`, `SelfHealthWidget.cs`, `EnemyHealthBars.cs`
- Test: create `CarRegistryTests.cs`, `HealthBarLayoutTests.cs`, `HudRootTests.cs`

**Interfaces:**
- Consumes: `IDamageable`, `Hostility`, `CarController`.
- Produces:
  - `CarRegistry.All` (`IReadOnlyList<CarController>`), `Register`, `Unregister`
  - `HudRoot.Create()`, returning a `HudRoot` with `Canvas` and `Rect`
  - `HealthBarLayout.IsVisible`, `BarWidth`, `Fill`, `SelfText`
  - `SelfHealthWidget.viewer` and `Build(RectTransform)`
  - `EnemyHealthBars.viewer`, `EnemyHealthBars.view` and `Build(RectTransform)`

- [ ] **Step 1: Add assembly references**

- In `Assets/_Project/Scripts/HUD/MotorCombat.HUD.asmdef`, set `"references": ["MotorCombat.Core", "UnityEngine.UI"]`.
- In `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, add `"MotorCombat.HUD"` and `"UnityEngine.UI"` after `"MotorCombat.Combat"`.

- [ ] **Step 2: Write the failing tests**

Create `Assets/_Project/Tests/EditMode/CarRegistryTests.cs` (3 tests):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    /// <summary>EditMode never calls OnEnable, so these register explicitly.</summary>
    public class CarRegistryTests
    {
        GameObject _object;
        CarController _car;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Registered", typeof(CarController));
            _car = _object.GetComponent<CarController>();
        }

        [TearDown]
        public void TearDown()
        {
            CarRegistry.Unregister(_car);
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void Register_AddsTheCar()
        {
            CarRegistry.Register(_car);
            CollectionAssert.Contains(CarRegistry.All, _car);
        }

        [Test]
        public void Register_Twice_KeepsOneEntry()
        {
            CarRegistry.Register(_car);
            CarRegistry.Register(_car);

            int count = 0;
            foreach (var car in CarRegistry.All) if (car == _car) count++;
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Unregister_RemovesTheCar()
        {
            CarRegistry.Register(_car);
            CarRegistry.Unregister(_car);
            CollectionAssert.DoesNotContain(CarRegistry.All, _car);
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/HealthBarLayoutTests.cs` (7 tests):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class HealthBarLayoutTests
    {
        [Test]
        public void BehindTheCamera_IsHidden()
        {
            Assert.IsFalse(HealthBarLayout.IsVisible(new Vector3(960f, 540f, -5f), 1920f, 1080f, 100f));
        }

        [Test]
        public void OnScreen_IsVisible()
        {
            Assert.IsTrue(HealthBarLayout.IsVisible(new Vector3(960f, 540f, 20f), 1920f, 1080f, 100f));
        }

        [Test]
        public void FarOffScreen_IsHidden()
        {
            Assert.IsFalse(HealthBarLayout.IsVisible(new Vector3(-500f, 540f, 20f), 1920f, 1080f, 100f));
        }

        /// <summary>Widths come in as screen pixels and leave as reference pixels, clamped.</summary>
        [Test]
        public void BarWidth_ConvertsToReferencePixelsAndClamps()
        {
            Assert.AreEqual(100f, HealthBarLayout.BarWidth(200f, 2f, 60f, 160f), 1e-4f);
            Assert.AreEqual(60f, HealthBarLayout.BarWidth(10f, 1f, 60f, 160f), 1e-4f);
            Assert.AreEqual(160f, HealthBarLayout.BarWidth(900f, 1f, 60f, 160f), 1e-4f);
        }

        [Test]
        public void Fill_IsTheHealthFraction_Clamped()
        {
            Assert.AreEqual(0.35f, HealthBarLayout.Fill(350f, 1000f), 1e-5f);
            Assert.AreEqual(0f, HealthBarLayout.Fill(10f, 0f));
            Assert.AreEqual(1f, HealthBarLayout.Fill(1200f, 1000f));
        }

        [Test]
        public void SelfText_RoundsUp()
        {
            Assert.AreEqual("667 / 1000", HealthBarLayout.SelfText(666.67f, 1000f));
        }

        [Test]
        public void SelfText_NeverShowsZeroWhileAlive()
        {
            Assert.AreEqual("1 / 1000", HealthBarLayout.SelfText(0.01f, 1000f));
        }
    }
}
```

Create `Assets/_Project/Tests/EditMode/HudRootTests.cs` (1 test):
```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class HudRootTests
    {
        /// <summary>The fairness rule: HUD proportions follow screen WIDTH, identical on every monitor.</summary>
        [Test]
        public void Create_ScalesWithScreenWidth()
        {
            HudRoot hud = HudRoot.Create();
            try
            {
                var scaler = hud.GetComponent<CanvasScaler>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, hud.Canvas.renderMode);
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
                Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
                Assert.AreEqual(0f, scaler.matchWidthOrHeight);
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`, with compile errors for `CarRegistry`, `HealthBarLayout` and `HudRoot`.

- [ ] **Step 4: Registry**

`Assets/_Project/Scripts/Core/CarRegistry.cs`:
```csharp
using System.Collections.Generic;

namespace MotorCombat.Core
{
    /// <summary>
    /// Every active car. The HUD (and later weapons and bots) find cars here
    /// instead of referencing whoever spawned them. A deactivated wreck drops out.
    /// </summary>
    public static class CarRegistry
    {
        static readonly List<CarController> Cars = new List<CarController>();

        public static IReadOnlyList<CarController> All => Cars;

        public static void Register(CarController car)
        {
            if (car != null && !Cars.Contains(car)) Cars.Add(car);
        }

        public static void Unregister(CarController car)
        {
            Cars.Remove(car);
        }
    }
}
```

In `Assets/_Project/Scripts/Core/CarController.cs`, add after the `Bind` method:
```csharp

        void OnEnable()
        {
            CarRegistry.Register(this);
        }

        void OnDisable()
        {
            CarRegistry.Unregister(this);
        }
```

- [ ] **Step 5: HUD building blocks**

`Assets/_Project/Scripts/HUD/HealthBarLayout.cs`:
```csharp
using UnityEngine;

namespace MotorCombat.HUD
{
    /// <summary>Pure placement and text rules for health bars.</summary>
    public static class HealthBarLayout
    {
        /// <summary>In front of the camera and within the screen plus a margin.</summary>
        public static bool IsVisible(Vector3 screenPoint, float screenWidth, float screenHeight, float margin)
        {
            return screenPoint.z > 0f
                   && screenPoint.x >= -margin && screenPoint.x <= screenWidth + margin
                   && screenPoint.y >= -margin && screenPoint.y <= screenHeight + margin;
        }

        /// <summary>Projected car width in screen pixels → bar width in reference pixels, clamped.</summary>
        public static float BarWidth(float projectedWidthPixels, float canvasScale, float minWidth, float maxWidth)
        {
            float reference = projectedWidthPixels / Mathf.Max(0.0001f, canvasScale);
            return Mathf.Clamp(reference, minWidth, maxWidth);
        }

        public static float Fill(float current, float max)
        {
            return max > 0f ? Mathf.Clamp01(current / max) : 0f;
        }

        /// <summary>Rounded UP, so a living car never reads 0.</summary>
        public static string SelfText(float current, float max)
        {
            return $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
```

`Assets/_Project/Scripts/HUD/HudRoot.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The HUD canvas, built in code like the arena. It scales with screen WIDTH
    /// against 1920 — the same fairness rule as the camera and crosshair: every
    /// monitor sees identical proportions. Widgets are separate components added
    /// under it; later sub-projects add their own.
    /// </summary>
    public class HudRoot : MonoBehaviour
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        public Canvas Canvas { get; private set; }
        public RectTransform Rect { get; private set; }

        public static HudRoot Create()
        {
            var go = new GameObject("HUD", typeof(RectTransform));

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            var root = go.AddComponent<HudRoot>();
            root.Canvas = canvas;
            root.Rect = (RectTransform)go.transform;
            return root;
        }
    }
}
```

`Assets/_Project/Scripts/HUD/HudElements.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;

namespace MotorCombat.HUD
{
    /// <summary>Tiny factories for code-built UI. Nothing here takes input.</summary>
    public static class HudElements
    {
        static Font _font;

        /// <summary>Unity's built-in font; no TextMeshPro resources required.</summary>
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Image(string name, Transform parent, Color colour)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        public static Text Text(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            var text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>Stretches a child over its parent.</summary>
        public static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Sets a fill child's right edge to a 0–1 fraction of its parent.</summary>
        public static void SetFill(RectTransform fill, float fraction)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }
    }
}
```

- [ ] **Step 6: Widgets**

`Assets/_Project/Scripts/HUD/SelfHealthWidget.cs`:
```csharp
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The viewer's own HP, bottom-centre, in every camera mode — in first person
    /// you never see your own car, so its health lives on the screen.
    /// </summary>
    public class SelfHealthWidget : MonoBehaviour
    {
        public CarController viewer;

        [Tooltip("Reference pixels.")] public float width = 520f;
        [Tooltip("Reference pixels.")] public float height = 16f;
        [Tooltip("Reference pixels from the bottom of the screen.")] public float bottomMargin = 40f;
        public int fontSize = 22;

        static readonly Color Track = new Color(0.08f, 0.12f, 0.08f, 0.85f);
        static readonly Color FillColour = new Color(0.32f, 0.76f, 0.35f);

        RectTransform _root;
        RectTransform _fill;
        Text _label;
        IDamageable _health;
        CarController _healthOwner;

        public void Build(RectTransform canvas)
        {
            _root = HudElements.Rect("SelfHealth", canvas);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 0f);
            _root.anchoredPosition = new Vector2(0f, bottomMargin);
            _root.sizeDelta = new Vector2(width, height);

            var track = HudElements.Image("Track", _root, Track);
            HudElements.Fill(track.rectTransform);

            _fill = HudElements.Image("Fill", _root, FillColour).rectTransform;
            HudElements.SetFill(_fill, 1f);

            _label = HudElements.Text("Label", _root, fontSize, TextAnchor.LowerCenter);
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(0f, fontSize + 6f);
        }

        void LateUpdate()
        {
            if (_root == null) return;

            if (viewer != _healthOwner)
            {
                _healthOwner = viewer;
                _health = viewer != null ? viewer.GetComponent<IDamageable>() : null;
            }

            bool show = viewer != null && _health != null && !_health.IsDestroyed;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            HudElements.SetFill(_fill, HealthBarLayout.Fill(_health.Current, _health.Max));
            _label.text = HealthBarLayout.SelfText(_health.Current, _health.Max);
        }
    }
}
```

`Assets/_Project/Scripts/HUD/EnemyHealthBars.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// A bar and name over every enemy car, always on. Drawn in screen space from
    /// a point above the roof, so it faces the viewer from any angle. Width follows
    /// the car's on-screen width, clamped. Nearer bars draw on top.
    ///
    /// Runs after the camera has moved this frame, or bars lag one frame behind.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class EnemyHealthBars : MonoBehaviour
    {
        public CarController viewer;
        public Camera view;

        [Tooltip("Metres above the car's roof.")] public float anchorMarginMetres = 0.4f;
        [Tooltip("Reference pixels.")] public float minWidth = 60f;
        [Tooltip("Reference pixels.")] public float maxWidth = 160f;
        [Tooltip("Reference pixels.")] public float barHeight = 8f;
        public int fontSize = 14;
        [Tooltip("Screen pixels beyond the edge before a bar is hidden.")] public float screenMargin = 100f;

        static readonly Color Track = new Color(0.12f, 0.02f, 0.02f, 0.85f);
        static readonly Color FillColour = new Color(0.88f, 0.25f, 0.23f);

        class Bar
        {
            public RectTransform root;
            public RectTransform fill;
            public Text name;
            public float depth;
        }

        readonly Dictionary<CarController, Bar> _bars = new Dictionary<CarController, Bar>();
        readonly List<Bar> _visible = new List<Bar>();
        readonly List<CarController> _stale = new List<CarController>();

        RectTransform _layer;
        Canvas _canvas;

        public void Build(RectTransform canvas)
        {
            _canvas = canvas.GetComponent<Canvas>();
            _layer = HudElements.Rect("EnemyBars", canvas);
            HudElements.Fill(_layer);
        }

        void LateUpdate()
        {
            if (_layer == null) return;

            foreach (Bar bar in _bars.Values) bar.root.gameObject.SetActive(false);
            _visible.Clear();

            if (viewer != null && view != null)
            {
                float scale = _canvas != null ? _canvas.scaleFactor : 1f;
                IReadOnlyList<CarController> cars = CarRegistry.All;

                for (int i = 0; i < cars.Count; i++)
                {
                    CarController car = cars[i];
                    if (car == null || car == viewer || !Hostility.AreEnemies(viewer, car)) continue;

                    var health = car.GetComponent<IDamageable>();
                    if (health == null || health.IsDestroyed) continue;

                    var box = car.GetComponent<BoxCollider>();
                    float carHeight = box != null ? box.size.y : 1f;
                    float carWidth = box != null ? box.size.x : 2f;

                    Vector3 anchor = car.transform.position + Vector3.up * (carHeight * 0.5f + anchorMarginMetres);
                    Vector3 screen = view.WorldToScreenPoint(anchor);
                    if (!HealthBarLayout.IsVisible(screen, Screen.width, Screen.height, screenMargin)) continue;

                    Vector3 halfSpan = view.transform.right * (carWidth * 0.5f);
                    Vector3 left = view.WorldToScreenPoint(anchor - halfSpan);
                    Vector3 right = view.WorldToScreenPoint(anchor + halfSpan);
                    float projected = Vector2.Distance(left, right);

                    Bar bar = GetOrCreate(car);
                    bar.root.gameObject.SetActive(true);
                    bar.root.anchoredPosition = new Vector2(screen.x, screen.y) / scale;
                    bar.root.sizeDelta = new Vector2(HealthBarLayout.BarWidth(projected, scale, minWidth, maxWidth), barHeight);
                    HudElements.SetFill(bar.fill, HealthBarLayout.Fill(health.Current, health.Max));
                    if (bar.name.text != car.name) bar.name.text = car.name;

                    bar.depth = screen.z;
                    _visible.Add(bar);
                }
            }

            // Farthest first, so nearer bars end up on top.
            _visible.Sort((a, b) => b.depth.CompareTo(a.depth));
            for (int i = 0; i < _visible.Count; i++) _visible[i].root.SetSiblingIndex(i);

            RemoveDeletedCars();
        }

        Bar GetOrCreate(CarController car)
        {
            if (_bars.TryGetValue(car, out Bar existing)) return existing;

            var root = HudElements.Rect("Bar", _layer);
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = new Vector2(0.5f, 0f);

            var track = HudElements.Image("Track", root, Track);
            HudElements.Fill(track.rectTransform);

            var fill = HudElements.Image("Fill", root, FillColour).rectTransform;
            HudElements.SetFill(fill, 1f);

            var label = HudElements.Text("Name", root, fontSize, TextAnchor.LowerCenter);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            labelRect.sizeDelta = new Vector2(0f, fontSize + 4f);

            var bar = new Bar { root = root, fill = fill, name = label };
            _bars[car] = bar;
            return bar;
        }

        void RemoveDeletedCars()
        {
            _stale.Clear();
            foreach (var pair in _bars)
            {
                if (pair.Key == null) _stale.Add(pair.Key);
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Bar bar = _bars[_stale[i]];
                if (bar.root != null) Destroy(bar.root.gameObject);
                _bars.Remove(_stale[i]);
            }
        }
    }
}
```

About `RemoveDeletedCars`: a destroyed Unity object compares equal to `null` but is still a distinct dictionary key, so looking it up with `_bars[_stale[i]]` still works.

- [ ] **Step 7: Bootstrap builds the HUD**

In `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`, add this at the end of `Start()`, after `crosshair.view = cameraRig.GetComponent<Camera>();`:
```csharp

            var hud = HudRoot.Create();

            var selfHealth = hud.gameObject.AddComponent<SelfHealthWidget>();
            selfHealth.viewer = player;
            selfHealth.Build(hud.Rect);

            var enemyBars = hud.gameObject.AddComponent<EnemyHealthBars>();
            enemyBars.viewer = player;
            enemyBars.view = cameraRig.GetComponent<Camera>();
            enemyBars.Build(hud.Rect);
```

- [ ] **Step 8: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="201"`.

- [ ] **Step 9: Commit**

```bash
git add -A Assets/_Project
git status --porcelain
git commit -m "Add car registry and health HUD" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Generate assets and document

**Files:**
- Generate: `Assets/_Project/Configs/WreckConfig.asset` (+ `.meta`). This also modifies `Assets/_Project/Configs/CarDefinition.asset`.
- Create: `docs/combat.md`, `docs/hud.md`
- Modify: `CLAUDE.md`, `docs/architecture.md`, `docs/ramming.md`, `docs/driving-physics.md`, `docs/tuning.md`, `docs/workflow.md`

**Interfaces:**
- Consumes: everything from Tasks 1–5.
- Produces: a playable scene with health, destruction and the HUD.

- [ ] **Step 1: Generate the asset**

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
```

- [ ] **Step 2: Verify the generated assets**

```bash
git status --porcelain
git diff Assets/_Project/Configs/CarDefinition.asset
cat Assets/_Project/Configs/WreckConfig.asset
```
Expected:
- **`WreckConfig.asset`** and its `.meta` are new, with `rollDegrees: 180`, `rollSeconds: 0.8`, `fadeSeconds: 1.5` and `removeAfterSeconds: 1.5`.
- **`CarDefinition.asset`**: the diff **only adds** these lines:
  - `maxHealth: 1000`
  - `attack: 100`
  - `defense: 0`
  - `wreckConfig: {fileID: 11400000, guid: <WreckConfig guid>, type: 2}`, where the guid matches `WreckConfig.asset.meta`

  `strength: 1` and `resistance: 1` stay as renamed in Task 2. No existing value may change (length, mass, visualPrefab, visualOffset, tintedMaterials, driveConfig, aimConfig, ramConfig…). If any does, stop and report BLOCKED.
- **No other asset changes**, with two allowed exceptions: `DriveConfig.asset` may gain `flipSteeringInReverse: 1`, and `RamConfig.asset` may gain `flankDamage: 0` / `rearDamage: 0`.

- [ ] **Step 3: Write `docs/combat.md`**

Write it as a current-state reference in the style of `docs/ramming.md` and `docs/driving-physics.md`: short sections, tables, and explanations of *why*. Take the content from `docs/specs/2026-09-14-combat-stats-damage-design.md`, and check that it matches the code. Sections:
1. `# Combat`: one paragraph covering health, the single damage path and destruction. Say that effects and weapons plug into these seams without editing them.
2. `## Stats`: the §1.1 table with the current placeholder values; effective stats and additive percentages (§1.2); `TopSpeed` read by driving.
3. `## The damage path`: the seven steps of §2.1 as a numbered list; the `DamageRequest` fields; `DamageResult.dealt` is the HP actually removed.
4. `## Formula`: the §2.2 table, and why there's no floor.
5. `## Tick damage`: the `TickSchedule` rule (immediate first tick; survives contact breaks).
6. `## Hostility`: the default rule, the null source, and the replaceable rule.
7. `## Ability switches`: the `CarAbility` table, the refresh modes, and the table showing ram lock, reel and wreck as blocks.
8. `## Destruction`:
   - the wreck block and the `Wreck` layer
   - the `WreckConfig` fields
   - lift during the roll
   - the fade via transparent clones
   - visuals restored on removal
   - the debug context-menu entries
9. `## Physics layers`: the §5 table.
10. `## Tests`: `CarAbilitiesTests` 15, `CarStatsTests` 9, `DamageRulesTests` 9, `HealthStateTests` 13, `TickScheduleTests` 7, `HostilityTests` 4, `HealthTests` 5, `PhysicsLayersTests` 3, `WreckMathTests` 10, `WreckMaterialsTests` 1.

- [ ] **Step 4: Write `docs/hud.md`**

Sections:
1. `# HUD`.
2. `## Canvas`: built in code, scales with width against 1920 (the fairness rule), widgets as separate components.
3. `## Own health`: bottom centre, rounded up.
4. `## Enemy bars`:
   - where the anchor sits, and when a bar is visible
   - the width clamp, the name label and draw order
   - drawn in screen space, so it always faces you
   - runs after the camera in execution order
5. `## Not included`: occlusion, damage numbers, the chip trail.
6. `## Tests`: `HealthBarLayoutTests` 7, `HudRootTests` 1, `CarRegistryTests` 3.

- [ ] **Step 5: Update the other docs**

**`CLAUDE.md`:**
- In the Documentation table, add these two rows after the `ramming.md` row:
  - `| [docs/combat.md](docs/combat.md) | Health, damage, stats, ability switches, destruction, physics layers |`
  - `| [docs/hud.md](docs/hud.md) | Anything drawn on screen besides the crosshair |`
- In the opening summary, change `Ramming works; no weapons, no menus, no netcode.` to `Ramming, health and destruction work; no weapons, no menus, no netcode.`
- In "Not built yet", change `Weapons, damage, health, respawn.` to `Weapons, effects, respawn.`

**`docs/architecture.md`:**
- Add `MotorCombat.Combat` to the assembly list (now 14 assemblies) and to the diagram (`Health`, `WreckSequence`).
- Add a `## Core holds the contracts` section. List `CarAbilities`, `CarStats`, `IDamageable`/`DamageRequest`, `Hostility`, `TickSchedule`, `CarRegistry` and `PhysicsLayers`, and explain the closed-spec rule.
- Update the test count to 201.

**`docs/ramming.md`:**
- Change `attack`/`defense` to `strength`/`resistance` wherever they mean the ram stats.
- Describe lock and reel as ability blocks, with their masks and refresh modes.
- Add `flankDamage` and `rearDamage` (flat damage; head-ons deal none).
- Note that wrecks can't ram or be rammed.

**`docs/driving-physics.md`:** throttle, steer, yaw and grip are gated by `CarAbility`, and engine force is multiplied by `Effective(TopSpeed)`.

**`docs/tuning.md`:**
- `CarDefinition` table:
  - add rows `maxHealth` 1000, `attack` 100, `defense` 0 and `wreckConfig`
  - rename the old ramming `attack`/`defense` rows to `strength` 1 and `resistance` 1
- `RamConfig` table: add rows `flankDamage` 0 and `rearDamage` 0.
- In the angular-speed note, replace `attack` with `strength`.
- Add a `## WreckConfig` section covering its four fields.

**`docs/workflow.md`:**
- Set the test total to 201.
- In the fixture table:
  - remove `CarStatusTests`
  - add the new fixtures with the counts above
  - set `CarFactoryTests` 19, `RamRulesTests` 37 and `ArenaBuilderTests` 7
- Change the checklist intro to say rows 21–26 are new and not yet walked.
- Append these rows:

```
| 21 | Play | Your HP bar bottom-centre reads 1000 / 1000; the dummy has a red bar and its name above it |
| 22 | Drive around the dummy and away from it | Its bar faces you from every side and shrinks with distance, never below a readable size |
| 23 | Dummy's Health component → right-click → Debug: take 25% max HP | Its bar shortens to three quarters |
| 24 | Temporarily set RamConfig.flankDamage to 100, flank-ram the dummy, then head-on it; set it back to 0 | Flank shortens the bar; head-on does not |
| 25 | Dummy's Health → Debug: destroy | Bar vanishes at once; dummy slides to a stop, rolls sideways, fades, disappears at ~1.5 s; you can drive through it but it never sinks or passes the wall |
| 26 | Re-walk ram rows 13–20 | Unchanged — the ability re-expression preserves them |
```

- [ ] **Step 6: Run tests once more**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="201"`.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project docs CLAUDE.md
git status --porcelain
git commit -m "Generate WreckConfig asset and document combat and HUD" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
