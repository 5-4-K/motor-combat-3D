# Effects and Respawn Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:**
- Destroyed cars respawn.
- The ten effects work through one effect system that weapons will apply through Core contracts.
- The ram reel becomes the Reeling effect.
- Active effects show as HUD chips.

**Architecture:**
- **Core** gets the contracts: `IRespawnable` and `CarRespawn`, plus `EffectType`, `EffectRequest`, `IEffectReceiver` and the rest, and `DamageRequest.attack`.
- **A new `MotorCombat.Effects` assembly** holds:
  - pure `EffectRules`
  - `EffectSet` timers
  - per-effect behaviour classes
  - a thin `CarEffects` component
- **Bootstrap** gets a placeholder `RespawnRule`.
- **HUD** gets effect chips.

**Tech Stack:** Unity 6 (`6000.6.0f1`), URP, C#, PhysX, uGUI, NUnit EditMode tests via the `unity` CLI.

**Spec:** `docs/specs/2026-09-14-effects-respawn-design.md`

## Global Constraints

- **Assemblies.** Every script lives in an asmdef folder under `Assets/_Project/Scripts/`.
  - Gameplay assemblies (Aiming, Arena, Cameras, Combat, Controls, Driving, Effects, HUD, Ramming, Weapons) reference `MotorCombat.Core` only, plus Unity packages.
  - Only `Cars`, `Bootstrap` and `EditorTools` compose across modules.
- **Where logic lives.** Decisions go in pure statics or plain classes; MonoBehaviours stay thin.
- **Decay.** Every decay is a rate in 1/s applied as `value * Mathf.Exp(-rate * dt)`.
- **Source keys** are compared by reference identity. Each effect behaviour instance is the one key for its block, modifier, gate and ticks.
- **Effect ability masks:**
  - Stunned `Throttle|Steer|Fire|Ram`
  - Suppressed `Fire`
  - Reeling `Throttle|Steer|YawHold|Grip|Ram`
  - Every block is registered with `float.PositiveInfinity` and `BlockRefresh.KeepLonger`, and removed in `OnEnd`.
- **Stat effects:**
  - Corroded: Defense −m%
  - Fortified: Defense +m%
  - Spiked: TopSpeed −m%
  - Exhausted: Attack −m%
- **Apply order:**
  1. Targetable (and not destroyed)
  2. Hostility (unless `allowNonEnemy`)
  3. Valid
  4. Overhauled ends everything
  5. Active: stacks → `Restarted`; otherwise `AlreadyActive`
  6. New → `Applied`
- **Step order:** advance timers; anything at or below `1e-4` s expires (`OnEnd`, `Ended`); then `OnStep` on each still-active effect in `EffectType` order.
- **The ram lock is not an effect** and stays exactly as it is.
- **Placeholders** (the user owns tuning):
  - `EffectsConfig`:
    - stacks: only `reelingStacks` = true
    - `overheatedDamageKind` Flat, `overheatedTickSeconds` 1, `reelingSpinDecayRate` 2
    - `debugDuration` 3, `debugPercent` 30, `debugOverheatAmount` 20
  - `RespawnConfig`: `respawnDelaySeconds` 3, `clearanceMargin` 0.1
  - Chips:
    - self: 72×24, gap 6, font 15, bottom 96
    - enemy: 48×14, gap 3, font 10
- **Code style.** Match the surrounding code:
  - XML `<summary>` comments that explain *why*
  - `[Tooltip]` on config fields
  - `Debug.LogError("[MotorCombat] ...", this)` for misconfiguration, checked in `Start`
- **The Unity Editor must be closed** to run the CLI. Always run the tests in this form:
  ```bash
  rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml
  ```
  Read `total`, `passed` and `failed` from the printed tag. If `test-results.xml` is missing, the run failed, and compile errors appear in the CLI output.
- **Staging.** After a run, stage `Assets/_Project` wholesale (Unity writes `.meta` files, including for folders), plus the other paths the task names. Never commit `test-results.xml`.
- **`ProjectSettings/DynamicsManager.asset`:** test runs rewrite it. Revert it (`git checkout -- ProjectSettings/DynamicsManager.asset`) before every commit.
- **Git.** Work on `main`, commit once per task, never push. End commit messages with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.
- **Expected test totals:** 206 now → T1 213 → T2 218 → T3 224 → T4 244 → T5 267 → T6 267 → T7 272 → T8 272.

---

## File map

(All code paths are under `Assets/_Project/`.)

| File | Task | Responsibility |
|---|---|---|
| `Scripts/Core/IRespawnable.cs` (new) | 1 | Per-life reset contract |
| `Scripts/Core/CarRespawn.cs` (new) | 1 | Respawn mechanism |
| `Scripts/Core/CarController.cs` | 1 | `ClearMotionSnapshot()` |
| `Scripts/Combat/HealthState.cs` | 1 | `Revive()` |
| `Scripts/Combat/Health.cs` | 1, 3 | `IRespawnable`; attack snapshot |
| `Scripts/Combat/WreckSequence.cs` | 1 | `IRespawnable`, `OnDisable` |
| `Scripts/Bootstrap/RespawnConfig.cs`, `RespawnRules.cs`, `RespawnRule.cs` (new) | 2 | Placeholder respawn rule |
| `Scripts/Bootstrap/GameBootstrap.cs` | 2, 6, 7 | Wiring |
| `Editor/ArenaSceneBuilder.cs`, `Editor/ConfigAssetBootstrap.cs` | 2, 6 | Assets and scene |
| `Scripts/Core/EffectType.cs`, `EffectRequest.cs`, `EffectOutcome.cs`, `ActiveEffect.cs`, `EffectReport.cs`, `IEffectReceiver.cs`, `EffectInfo.cs` (new) | 3 | Effect contracts |
| `Scripts/Core/DamageRequest.cs` | 3 | `attack` snapshot |
| `Scripts/Effects/EffectsConfig.cs`, `EffectRules.cs`, `EffectSet.cs` (new assembly) | 4 | Config, rules, timers |
| `Scripts/Effects/EffectHost.cs`, `EffectBehaviour.cs`, the six behaviour classes, `CarEffects.cs` | 5 | Behaviours and component |
| `Scripts/Ramming/RammingModule.cs`, `RamConfig.cs`, `RamRules.cs` | 6 | Reel via effect |
| `Scripts/Cars/CarDefinition.cs`, `CarFactory.cs` | 6 | `effectsConfig`, add `CarEffects` |
| `Scripts/HUD/EffectChipLayout.cs`, `EffectChipRow.cs`, `SelfEffectsWidget.cs` (new), `EnemyHealthBars.cs` | 7 | Chips |
| `Configs/*.asset`, `Scenes/Arena.unity`, `docs/*`, `CLAUDE.md` | 8 | Assets and docs |
| Tests under `Tests/EditMode/` | 1–7 | |

---

### Task 1: Respawn mechanism

**Files:**
- Create: `Scripts/Core/IRespawnable.cs`, `Scripts/Core/CarRespawn.cs`, `Tests/EditMode/RespawnProbe.cs`, `Tests/EditMode/CarRespawnTests.cs`
- Modify: `Scripts/Core/CarController.cs`, `Scripts/Combat/HealthState.cs`, `Scripts/Combat/Health.cs`, `Scripts/Combat/WreckSequence.cs`, `Tests/EditMode/HealthStateTests.cs`, `Tests/EditMode/HealthTests.cs`

**Interfaces:**
- Produces: `interface IRespawnable { void ResetForRespawn(); }`, `static void CarRespawn.Respawn(CarController car, Vector3 position, Quaternion rotation)`, `void CarController.ClearMotionSnapshot()`, `void HealthState.Revive()`, and `Health` and `WreckSequence` implementing `IRespawnable`.

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/RespawnProbe.cs`. It gets its own file because Unity needs a MonoBehaviour's file name to match its class:

```csharp
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
```

`Tests/EditMode/CarRespawnTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarRespawnTests
    {
        GameObject _object;
        CarController _car;
        RespawnProbe _probe;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Car", typeof(CarController));
            _car = _object.GetComponent<CarController>();
            _probe = _object.AddComponent<RespawnProbe>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void Respawn_ResetsEveryRespawnableBeforeReactivating()
        {
            _object.SetActive(false);

            CarRespawn.Respawn(_car, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(1, _probe.calls);
            Assert.IsFalse(_probe.activeWhenReset, "state must be reset before the car is live again");
        }

        [Test]
        public void Respawn_ReactivatesTheCarAtThePose()
        {
            _object.SetActive(false);
            var position = new Vector3(3f, 0.7f, -8f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            CarRespawn.Respawn(_car, position, rotation);

            Assert.IsTrue(_object.activeSelf);
            Assert.AreEqual(position, _object.transform.position);
            Assert.Less(Quaternion.Angle(rotation, _object.transform.rotation), 0.01f);
        }

        [Test]
        public void Respawn_PutsTheRootBackOnTheCarLayer()
        {
            _object.layer = PhysicsLayers.Wreck;

            CarRespawn.Respawn(_car, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(PhysicsLayers.Car, _object.layer);
        }

        [Test]
        public void Respawn_ZeroesAimAndVelocity()
        {
            var body = _object.GetComponent<Rigidbody>();
            _car.AimYaw = 25f;
            body.linearVelocity = new Vector3(4f, 0f, 2f);
            body.angularVelocity = new Vector3(0f, 3f, 0f);

            CarRespawn.Respawn(_car, Vector3.zero, Quaternion.identity);

            Assert.AreEqual(0f, _car.AimYaw);
            Assert.AreEqual(Vector3.zero, body.linearVelocity);
            Assert.AreEqual(Vector3.zero, body.angularVelocity);
        }
    }
}
```

Append to `HealthStateTests` (inside the class, after the last test):

```csharp
        [Test]
        public void Revive_RestoresFullHealthAfterDeath()
        {
            var state = new HealthState(1000f);
            Hit(state, Flat(5000f));

            state.Revive();

            Assert.AreEqual(1000f, state.Current);
            Assert.IsFalse(state.IsDestroyed);
            Assert.AreEqual(DamageOutcome.Applied, Hit(state, Flat(100f)).outcome);
            Assert.AreEqual(900f, state.Current, 1e-4f);
        }

        [Test]
        public void Revive_KeepsGates()
        {
            var state = new HealthState(1000f);
            state.AddGate(new object(), r => true);

            state.Revive();

            Assert.AreEqual(DamageOutcome.Blocked, Hit(state, Flat(100f)).outcome);
        }
```

Append to `HealthTests` (inside the class, after the last test):

```csharp
        [Test]
        public void ResetForRespawn_RevivesTheWreckAndClearsItsBlocksAndModifiers()
        {
            _health.Apply(Flat(5000f, _source));
            _target.Stats.SetBase(CarStat.Defense, 100f);
            _target.Stats.Add(new object(), CarStat.Defense, 50f);

            _health.ResetForRespawn();

            Assert.IsFalse(_health.IsDestroyed);
            Assert.AreEqual(1000f, _health.Current);
            Assert.IsTrue(_target.Abilities.Has(CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram | CarAbility.Targetable));
            Assert.AreEqual(100f, _target.Stats.Effective(CarStat.Defense), 1e-4f);
            Assert.AreEqual(DamageOutcome.Applied, _health.Apply(Flat(100f, _source)).outcome);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with compile errors for `IRespawnable`, `CarRespawn`, `HealthState.Revive` and `Health.ResetForRespawn`.

- [ ] **Step 3: Implement**

`Scripts/Core/IRespawnable.cs`:

```csharp
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
```

`Scripts/Core/CarRespawn.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Brings a car back to life at a pose — or teleports a live one. The
    /// mechanism only: WHEN to respawn belongs to a game mode (today the
    /// placeholder RespawnRule).
    /// </summary>
    public static class CarRespawn
    {
        static readonly List<IRespawnable> Respawnables = new List<IRespawnable>();

        public static void Respawn(CarController car, Vector3 position, Quaternion rotation)
        {
            if (car == null) throw new ArgumentNullException(nameof(car));

            GameObject root = car.gameObject;

            // Reset first, while a wreck is still inactive, so nothing runs a
            // frame against the old life's state.
            Respawnables.Clear();
            root.GetComponents(Respawnables);
            for (int i = 0; i < Respawnables.Count; i++)
            {
                Respawnables[i].ResetForRespawn();
            }
            Respawnables.Clear();

            int carLayer = PhysicsLayers.Car;
            if (carLayer >= 0) root.layer = carLayer;

            car.transform.SetPositionAndRotation(position, rotation);
            car.AimYaw = 0f;

            // A ram in the first step reads the pre-step snapshot; without this
            // it would read the wreck's last slide.
            car.ClearMotionSnapshot();

            root.SetActive(true);

            var body = root.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }
    }
}
```

`Scripts/Core/CarController.cs`: add this method directly after `Bind`:

```csharp
        /// <summary>
        /// Zeroes the pre-step velocity snapshot. Called on respawn or teleport,
        /// so a ram in the next step never reads motion from before the jump.
        /// </summary>
        public void ClearMotionSnapshot()
        {
            PreStepVelocity = Vector3.zero;
            PreStepAngularVelocity = Vector3.zero;
        }
```

`Scripts/Combat/HealthState.cs`: add after `RemoveGate`:

```csharp
        /// <summary>
        /// Back to full health and alive. Gates stay: each belongs to its
        /// source (for example Armored), which removes it itself.
        /// </summary>
        public void Revive()
        {
            Current = Max;
            IsDestroyed = false;
        }
```

`Scripts/Combat/Health.cs`:
- change the class line to `public class Health : MonoBehaviour, IDamageable, IRespawnable`
- add after `RemoveGate`:

```csharp
        /// <summary>
        /// Revives the car: drops the wreck block and anything else blocked or
        /// modified, and refills health. Effects end their own state through
        /// CarEffects' own reset.
        /// </summary>
        public void ResetForRespawn()
        {
            CarController car = Car;
            car.Abilities.UnblockAll();
            car.Stats.RemoveAll();
            State.Revive();
        }
```

`Scripts/Combat/WreckSequence.cs`:
- change the class line to `public class WreckSequence : MonoBehaviour, IRespawnable`
- replace the summary sentence "On removal the original materials, transforms and colours are put back, so a future respawn only has to reactivate the car." with "On removal, on disable and on respawn the original materials, transforms and colours are put back, so a respawned car looks as it did before death."
- add after `OnDestroy`:

```csharp
        void OnDisable()
        {
            // A car switched off mid-roll must not resume the roll against
            // live visuals when it is switched back on.
            StopAndRestore();
        }

        public void ResetForRespawn()
        {
            StopAndRestore();
        }

        void StopAndRestore()
        {
            _running = false;
            RestoreVisuals();
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="213"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Add the respawn mechanism: IRespawnable, CarRespawn, health revive, wreck reset

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Placeholder respawn rule

**Files:**
- Create: `Scripts/Bootstrap/RespawnConfig.cs`, `Scripts/Bootstrap/RespawnRules.cs`, `Scripts/Bootstrap/RespawnRule.cs`, `Tests/EditMode/RespawnRulesTests.cs`
- Modify: `Scripts/Bootstrap/GameBootstrap.cs`, `Editor/ArenaSceneBuilder.cs`, `Editor/ConfigAssetBootstrap.cs`, `Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`

**Interfaces:**
- Consumes: `CarRespawn.Respawn` and `IRespawnable` from Task 1; `IDamageable.Destroyed` and `PhysicsLayers.Car` from Core.
- Produces:
  - `RespawnConfig { float respawnDelaySeconds = 3f; float clearanceMargin = 0.1f; }`
  - `static bool RespawnRules.IsDue(float now, float destroyedAt, float delaySeconds, bool carActive)`
  - `static void RespawnRules.SpawnBox(Vector3 position, Quaternion rotation, Vector3 boxCenter, Vector3 boxSize, float margin, out Vector3 center, out Vector3 halfExtents)`
  - `RespawnRule.Track(CarController car, Vector3 position, Quaternion rotation)`
  - `GameBootstrap.respawnConfig`

- [ ] **Step 1: Write the failing tests**

Add `"MotorCombat.Bootstrap"` to the `references` array of `Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, after `"MotorCombat.HUD"`.

`Tests/EditMode/RespawnRulesTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Bootstrap;

namespace MotorCombat.Tests
{
    public class RespawnRulesTests
    {
        [Test]
        public void IsDue_FalseWhileTheWreckIsStillActive()
        {
            Assert.IsFalse(RespawnRules.IsDue(10f, 0f, 3f, carActive: true));
        }

        [Test]
        public void IsDue_FalseBeforeTheDelay()
        {
            Assert.IsFalse(RespawnRules.IsDue(2.9f, 0f, 3f, carActive: false));
        }

        [Test]
        public void IsDue_TrueAtTheDelayOnceInactive()
        {
            // 150 x 0.02 accumulates to just under 3 in float steps.
            float now = 0f;
            for (int i = 0; i < 150; i++) now += 0.02f;

            Assert.IsTrue(RespawnRules.IsDue(now, 0f, 3f, carActive: false));
        }

        [Test]
        public void IsDue_TreatsANegativeDelayAsZero()
        {
            Assert.IsTrue(RespawnRules.IsDue(0f, 0f, -5f, carActive: false));
        }

        [Test]
        public void SpawnBox_RotatesTheColliderCentreAndAddsTheMargin()
        {
            RespawnRules.SpawnBox(
                new Vector3(1f, 0f, 0f),
                Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(2f, 1f, 4f),
                0.1f,
                out Vector3 center,
                out Vector3 halfExtents);

            Assert.AreEqual(2f, center.x, 1e-4f);
            Assert.AreEqual(0f, center.y, 1e-4f);
            Assert.AreEqual(0f, center.z, 1e-4f);
            Assert.AreEqual(new Vector3(1.1f, 0.6f, 2.1f), halfExtents);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with compile errors for `RespawnRules`.

- [ ] **Step 3: Implement**

`Scripts/Bootstrap/RespawnConfig.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Bootstrap
{
    /// <summary>
    /// Tuning for the placeholder respawn rule. A future game mode brings its own
    /// rule and config. Every value here is a placeholder — tuning belongs to the designer.
    /// </summary>
    [CreateAssetMenu(menuName = "Motor Combat/Respawn Config", fileName = "RespawnConfig")]
    public class RespawnConfig : ScriptableObject
    {
        [Tooltip("Seconds from destruction before the car may respawn. The wreck's roll and fade always finish first.")]
        [Min(0f)]
        public float respawnDelaySeconds = 3f;

        [Tooltip("Metres added around the car's box on every side when checking that the spawn point is clear.")]
        [Min(0f)]
        public float clearanceMargin = 0.1f;
    }
}
```

`Scripts/Bootstrap/RespawnRules.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Bootstrap
{
    /// <summary>The respawn rule's decisions, without a scene.</summary>
    public static class RespawnRules
    {
        // Physics time accumulates in float steps; 150 x 0.02 lands just under 3.
        const float Tolerance = 1e-4f;

        /// <summary>
        /// True once the delay has passed AND the car is inactive — the wreck
        /// sequence deactivates it when the roll and fade are done, so respawn
        /// never cuts them short.
        /// </summary>
        public static bool IsDue(float now, float destroyedAt, float delaySeconds, bool carActive)
        {
            return !carActive && now >= destroyedAt + Mathf.Max(0f, delaySeconds) - Tolerance;
        }

        /// <summary>World centre and half extents of the car's box at a spawn pose, grown by a margin on every side.</summary>
        public static void SpawnBox(Vector3 position, Quaternion rotation, Vector3 boxCenter, Vector3 boxSize, float margin,
                                    out Vector3 center, out Vector3 halfExtents)
        {
            center = position + rotation * boxCenter;
            halfExtents = boxSize * 0.5f + Vector3.one * Mathf.Max(0f, margin);
        }
    }
}
```

`Scripts/Bootstrap/RespawnRule.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Bootstrap
{
    /// <summary>
    /// Placeholder until game modes exist: a destroyed car comes back at its
    /// original spawn pose once the delay has passed, the wreck sequence has
    /// finished, and nothing is parked on the spot. Waiting for a clear spot,
    /// rather than spawning into an overlap, avoids PhysX launching both cars
    /// apart. Replace this component, not CarRespawn, to change the rule.
    /// </summary>
    public class RespawnRule : MonoBehaviour
    {
        public RespawnConfig config;

        class Entry
        {
            public CarController car;
            public IDamageable health;
            public Vector3 position;
            public Quaternion rotation;
            public bool pending;
            public float destroyedAt;
            public Action<DamageReport> onDestroyed;
        }

        readonly List<Entry> _entries = new List<Entry>();

        void Start()
        {
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] RespawnRule on '{name}' has no RespawnConfig assigned — destroyed cars will not respawn.", this);
            }
        }

        public void Track(CarController car, Vector3 position, Quaternion rotation)
        {
            if (car == null) return;

            var health = car.GetComponent<IDamageable>();
            if (health == null)
            {
                Debug.LogError($"[MotorCombat] RespawnRule cannot track '{car.name}': it has no IDamageable.", this);
                return;
            }

            var entry = new Entry { car = car, health = health, position = position, rotation = rotation };
            entry.onDestroyed = report =>
            {
                entry.pending = true;
                entry.destroyedAt = Time.fixedTime;
            };
            health.Destroyed += entry.onDestroyed;
            _entries.Add(entry);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].health != null) _entries[i].health.Destroyed -= _entries[i].onDestroyed;
            }
            _entries.Clear();
        }

        void FixedUpdate()
        {
            if (config == null) return;

            float now = Time.fixedTime;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!entry.pending || entry.car == null) continue;
                if (!RespawnRules.IsDue(now, entry.destroyedAt, config.respawnDelaySeconds, entry.car.gameObject.activeSelf)) continue;
                if (!IsClear(entry)) continue;

                entry.pending = false;
                CarRespawn.Respawn(entry.car, entry.position, entry.rotation);
            }
        }

        bool IsClear(Entry entry)
        {
            var box = entry.car.GetComponent<BoxCollider>();
            Vector3 size = box != null ? box.size : Vector3.one;
            Vector3 offset = box != null ? box.center : Vector3.zero;

            RespawnRules.SpawnBox(entry.position, entry.rotation, offset, size, config.clearanceMargin,
                out Vector3 center, out Vector3 halfExtents);

            // The dead car is inactive, so it never blocks its own spot. Wrecks
            // sit on the Wreck layer and phase through cars anyway.
            int carLayer = PhysicsLayers.Car;
            int mask = carLayer >= 0 ? 1 << carLayer : Physics.DefaultRaycastLayers;
            return !Physics.CheckBox(center, halfExtents, entry.rotation, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
```

`Scripts/Bootstrap/GameBootstrap.cs`:
- Under `[Header("Configs")]`, after `public CarDefinition carDefinition;`, add `public RespawnConfig respawnConfig;`.
- In `Validate()`, after the `carDefinition == null` line, add:

```csharp
            if (respawnConfig == null) { Debug.LogError("[MotorCombat] GameBootstrap.respawnConfig is not assigned."); return false; }
```

- Replace the block from `var playerInput = …` through `dummy.name = "DummyCar";` with:

```csharp
            var playerPosition = new Vector3(0f, halfHeight, 0f);
            var playerInput = gameObject.AddComponent<LocalInputProvider>();
            var player = CarFactory.Spawn(
                carDefinition,
                playerPosition,
                Quaternion.identity,
                playerInput,
                new Color(0.20f, 0.55f, 0.90f));
            player.name = "PlayerCar";

            var dummyPosition = new Vector3(0f, halfHeight, dummyDistanceInCarLengths * carDefinition.length);
            var dummyInput = gameObject.AddComponent<NullInputProvider>();
            var dummy = CarFactory.Spawn(
                carDefinition,
                dummyPosition,
                Quaternion.identity,
                dummyInput,
                new Color(0.85f, 0.35f, 0.25f));
            dummy.name = "DummyCar";

            var respawn = gameObject.AddComponent<RespawnRule>();
            respawn.config = respawnConfig;
            respawn.Track(player, playerPosition, Quaternion.identity);
            respawn.Track(dummy, dummyPosition, Quaternion.identity);
```

`Editor/ArenaSceneBuilder.cs`:
- After `var carDefinition = Load<CarDefinition>("CarDefinition");`, add `var respawnConfig = Load<RespawnConfig>("RespawnConfig");`.
- Change the null check to `if (cameraConfig == null || arenaConfig == null || carDefinition == null || respawnConfig == null)`.
- After `bootstrap.carDefinition = carDefinition;`, add `bootstrap.respawnConfig = respawnConfig;`.

`Editor/ConfigAssetBootstrap.cs`:
- Add `using MotorCombat.Bootstrap;`.
- After `var wreck = GetOrCreate<WreckConfig>("WreckConfig");`, add `GetOrCreate<RespawnConfig>("RespawnConfig");`.

(The assets and scene are regenerated in Task 7. Until then the scene's `respawnConfig` is unassigned, and Play logs the validation error — expected mid-plan.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="218"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Add a placeholder respawn rule: delay, wait for the wreck, wait for a clear spawn

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Effect contracts (Core) and the damage attack snapshot

**Files:**
- Create: `Scripts/Core/EffectType.cs`, `Scripts/Core/EffectRequest.cs`, `Scripts/Core/EffectOutcome.cs`, `Scripts/Core/ActiveEffect.cs`, `Scripts/Core/EffectReport.cs`, `Scripts/Core/IEffectReceiver.cs`, `Scripts/Core/EffectInfo.cs`, `Tests/EditMode/EffectInfoTests.cs`
- Modify: `Scripts/Core/DamageRequest.cs`, `Scripts/Combat/Health.cs`, `Tests/EditMode/HealthTests.cs`

**Interfaces:**
- Produces (all `MotorCombat.Core`):
  - `enum EffectType { Stunned = 0, Suppressed = 1, Overheated = 2, Corroded = 3, Reeling = 4, Spiked = 5, Fortified = 6, Armored = 7, Overhauled = 8, Exhausted = 9 }`
  - `struct EffectRequest { CarController source; string sourceTag; EffectType type; float magnitude; float duration; bool allowNonEnemy; float? attack; }`
  - `enum EffectOutcome { Applied, Restarted, AlreadyActive, NotTargetable, NotHostile, Invalid }`
  - `struct ActiveEffect { EffectType type; float remaining; float duration; float magnitude; CarController source; }`
  - `struct EffectReport { CarController source; CarController target; string sourceTag; EffectType type; EffectOutcome outcome; float magnitude; float duration; }`
  - `interface IEffectReceiver` — exactly as in the code below
  - `static class EffectInfo { const int Count = 10; bool IsKnown; bool IsBuff; bool IsTimed; bool UsesMagnitude; }`
  - `DamageRequest.attack` (`float?`)

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/EffectInfoTests.cs`:

```csharp
using System;
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class EffectInfoTests
    {
        [Test]
        public void IsKnown_CoversExactlyTheDeclaredTypes()
        {
            Assert.AreEqual(Enum.GetValues(typeof(EffectType)).Length, EffectInfo.Count);
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                Assert.IsTrue(EffectInfo.IsKnown(type), type.ToString());
            }
            Assert.IsFalse(EffectInfo.IsKnown((EffectType)(-1)));
            Assert.IsFalse(EffectInfo.IsKnown((EffectType)EffectInfo.Count));
        }

        [Test]
        public void IsBuff_OnlyFortifiedArmoredAndOverhauled()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                bool expected = type == EffectType.Fortified || type == EffectType.Armored || type == EffectType.Overhauled;
                Assert.AreEqual(expected, EffectInfo.IsBuff(type), type.ToString());
            }
        }

        [Test]
        public void IsTimed_EverythingButOverhauled()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                Assert.AreEqual(type != EffectType.Overhauled, EffectInfo.IsTimed(type), type.ToString());
            }
        }

        [Test]
        public void UsesMagnitude_OnlyOverheatedAndTheStatEffects()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                bool expected = type == EffectType.Overheated || type == EffectType.Corroded || type == EffectType.Spiked
                                || type == EffectType.Fortified || type == EffectType.Exhausted;
                Assert.AreEqual(expected, EffectInfo.UsesMagnitude(type), type.ToString());
            }
        }
    }
}
```

Append to `HealthTests` (inside the class, after the last test):

```csharp
        [Test]
        public void Apply_UsesTheAttackSnapshotWhenSet()
        {
            var request = Flat(100f, _source);
            request.attack = 200f;

            var result = _health.Apply(request);

            Assert.AreEqual(200f, result.dealt, 1e-3f, "the snapshot wins over the source's live attack of 100");
        }

        [Test]
        public void Apply_UsesTheAttackSnapshotForANullSourceToo()
        {
            var request = Flat(100f, null);
            request.attack = 50f;

            var result = _health.Apply(request);

            Assert.AreEqual(50f, result.dealt, 1e-3f);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with compile errors for `EffectType`, `EffectInfo` and `DamageRequest.attack`.

- [ ] **Step 3: Implement**

`Scripts/Core/EffectType.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>
    /// Every effect in the game. Values are explicit: configs, HUD order and
    /// arrays are indexed by them.
    /// </summary>
    public enum EffectType
    {
        /// <summary>Complete stop once; no throttle, steer, weapons or ramming.</summary>
        Stunned = 0,

        /// <summary>Weapons disabled.</summary>
        Suppressed = 1,

        /// <summary>Takes damage every tick interval.</summary>
        Overheated = 2,

        /// <summary>Defense reduced by a percentage.</summary>
        Corroded = 3,

        /// <summary>No throttle, steer or grip; spins freely, as after a ram.</summary>
        Reeling = 4,

        /// <summary>Top speed reduced by a percentage.</summary>
        Spiked = 5,

        /// <summary>Defense boosted by a percentage.</summary>
        Fortified = 6,

        /// <summary>Immune to damage, not to effects.</summary>
        Armored = 7,

        /// <summary>Instantly removes every effect. Never itself active.</summary>
        Overhauled = 8,

        /// <summary>Attack reduced by a percentage.</summary>
        Exhausted = 9
    }
}
```

`Scripts/Core/EffectRequest.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>
    /// One application of an effect. Every source — the ram now, weapons later —
    /// sends this through the target's <see cref="IEffectReceiver"/>. Size and
    /// duration come from the source's config; per-effect rules (stacking,
    /// Overheated's damage kind and interval) come from the effects config.
    /// </summary>
    public struct EffectRequest
    {
        /// <summary>The car that applied it; null for the environment or a debug menu.</summary>
        public CarController source;

        /// <summary>"ram", a weapon id, "debug" — for attribution and logs.</summary>
        public string sourceTag;

        public EffectType type;

        /// <summary>
        /// Always a positive size; the effect decides the sign. Percent for
        /// Corroded, Fortified, Spiked and Exhausted; damage per tick for
        /// Overheated (hit points or percent of max health, per the effects
        /// config). Ignored by the other effects.
        /// </summary>
        public float magnitude;

        /// <summary>Seconds. Ignored by Overhauled.</summary>
        public float duration;

        /// <summary>False (default): only enemies receive it. True: self and allies too — self-buffs and self-debuffs.</summary>
        public bool allowNonEnemy;

        /// <summary>Attack captured when the source fired. Null: the source's effective attack at apply time (100 for a null source).</summary>
        public float? attack;
    }
}
```

`Scripts/Core/EffectOutcome.cs`:

```csharp
namespace MotorCombat.Core
{
    public enum EffectOutcome
    {
        /// <summary>The effect started (or, for Overhauled, every effect ended).</summary>
        Applied,

        /// <summary>Already active and allowed to stack: its timer restarted and the new size replaced the old.</summary>
        Restarted,

        /// <summary>Already active and not allowed to stack: nothing changed.</summary>
        AlreadyActive,

        /// <summary>The target is a wreck or otherwise not targetable.</summary>
        NotTargetable,

        /// <summary>The source is not the target's enemy and allowNonEnemy was not set.</summary>
        NotHostile,

        /// <summary>Unknown type, a bad duration or magnitude, or no effects config.</summary>
        Invalid
    }
}
```

`Scripts/Core/ActiveEffect.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>A snapshot of one active effect, for the HUD and anything else that only reads.</summary>
    public struct ActiveEffect
    {
        public EffectType type;
        public float remaining;
        public float duration;
        public float magnitude;
        public CarController source;
    }
}
```

`Scripts/Core/EffectReport.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>Raised when an effect is applied or restarted.</summary>
    public struct EffectReport
    {
        public CarController source;
        public CarController target;
        public string sourceTag;
        public EffectType type;
        public EffectOutcome outcome;
        public float magnitude;
        public float duration;
    }
}
```

`Scripts/Core/IEffectReceiver.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace MotorCombat.Core
{
    /// <summary>
    /// Something that takes effects. Implemented in the Effects assembly;
    /// declared here so ramming, weapons and the HUD use it without referencing
    /// Effects — the same arrangement as <see cref="IDamageable"/>.
    /// </summary>
    public interface IEffectReceiver
    {
        EffectOutcome Apply(in EffectRequest request);

        bool Has(EffectType type);

        /// <summary>Seconds left, or 0 when the effect is not active.</summary>
        float Remaining(EffectType type);

        /// <summary>Clears the list, then fills it with every active effect in EffectType order.</summary>
        void GetActive(List<ActiveEffect> into);

        /// <summary>Outcome Applied or Restarted.</summary>
        event Action<EffectReport> Applied;

        /// <summary>An effect ended: expiry, Overhauled, death or respawn.</summary>
        event Action<EffectType> Ended;
    }
}
```

`Scripts/Core/EffectInfo.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>Fixed facts about each effect type, shared by the effect system and the HUD.</summary>
    public static class EffectInfo
    {
        /// <summary>Number of effect types; values run 0 to Count − 1.</summary>
        public const int Count = 10;

        public static bool IsKnown(EffectType type)
        {
            return (int)type >= 0 && (int)type < Count;
        }

        public static bool IsBuff(EffectType type)
        {
            return type == EffectType.Fortified || type == EffectType.Armored || type == EffectType.Overhauled;
        }

        /// <summary>Everything but Overhauled, which is instant.</summary>
        public static bool IsTimed(EffectType type)
        {
            return IsKnown(type) && type != EffectType.Overhauled;
        }

        /// <summary>Effects whose request magnitude means something.</summary>
        public static bool UsesMagnitude(EffectType type)
        {
            return type == EffectType.Overheated || type == EffectType.Corroded || type == EffectType.Spiked
                   || type == EffectType.Fortified || type == EffectType.Exhausted;
        }
    }
}
```

`Scripts/Core/DamageRequest.cs`:
- Add after the `allowNonEnemy` field:

```csharp

        /// <summary>
        /// Attack captured when the source fired (or, for Overheated, when the
        /// effect landed). Null: the source's effective attack at impact, 100 for
        /// a null source.
        /// </summary>
        public float? attack;
```

- In the class summary, change "Every source — rams now, weapons and effects later —" to "Every source — rams, effects, and weapons later —".

`Scripts/Combat/Health.cs`: in `Apply`, replace the attack argument line

```csharp
                request.source != null ? request.source.Stats.Effective(CarStat.Attack) : DamageRules.NeutralAttack,
```

with

```csharp
                request.attack ?? (request.source != null ? request.source.Stats.Effective(CarStat.Attack) : DamageRules.NeutralAttack),
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="224"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Add Core effect contracts and an attack snapshot on DamageRequest

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Effects assembly — config, rules and timers

**Files:**
- Create: `Scripts/Effects/MotorCombat.Effects.asmdef`, `Scripts/Effects/EffectsConfig.cs`, `Scripts/Effects/EffectRules.cs`, `Scripts/Effects/EffectSet.cs`, `Tests/EditMode/EffectRulesTests.cs`, `Tests/EditMode/EffectSetTests.cs`, `Tests/EditMode/EffectsConfigTests.cs`
- Modify: `Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`

**Interfaces:**
- Consumes: the Task 3 contracts, `CarAbility`, `CarStat`, `DamageKind`.
- Produces (namespace `MotorCombat.Effects`):
  - `EffectsConfig`:
    - fields `stunnedStacks, suppressedStacks, overheatedStacks, corrodedStacks, reelingStacks (= true), spikedStacks, fortifiedStacks, armoredStacks, exhaustedStacks`
    - `DamageKind overheatedDamageKind = Flat`, `float overheatedTickSeconds = 1f`, `float reelingSpinDecayRate = 2f`
    - `float debugDuration = 3f`, `float debugPercent = 30f`, `float debugOverheatAmount = 20f`
    - `bool Stacks(EffectType)`
  - `EffectRules`:
    - `const float ExpiryTolerance = 1e-4f`, `const float NeutralAttack = 100f`
    - `EffectOutcome Decide(in EffectRequest, bool targetable, bool hostile, bool alreadyActive, bool stacks)`
    - `bool IsValid(in EffectRequest)`
    - `float AttackSnapshot(float? requested, bool hasSource, float sourceAttack)`
    - `CarAbility BlockMask(EffectType)`
    - `bool StatChange(EffectType, float magnitude, out CarStat stat, out float percent)`
    - `float DecaySpin(float yawRate, float rate, float dt)`
  - `EffectSet`:
    - nested `struct Entry { EffectType type; float remaining; float duration; float magnitude; CarController source; string sourceTag; float attack; bool allowNonEnemy; }`
    - `bool IsActive(EffectType)`, `bool TryGet(EffectType, out Entry)`, `float Remaining(EffectType)`
    - `void Set(in Entry)`, `bool Remove(EffectType)`
    - `void Advance(float dt, List<EffectType> expired)`, `void ActiveTypes(List<EffectType> into)`

- [ ] **Step 1: Write the failing tests**

Add `"MotorCombat.Effects"` to the `references` array of `Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, after `"MotorCombat.Combat"`.

`Tests/EditMode/EffectRulesTests.cs`:

```csharp
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    public class EffectRulesTests
    {
        static EffectRequest Request(EffectType type, float magnitude = 30f, float duration = 3f)
        {
            return new EffectRequest { type = type, magnitude = magnitude, duration = duration, sourceTag = "test" };
        }

        static void AssertInvalid(EffectRequest request, string label)
        {
            Assert.AreEqual(EffectOutcome.Invalid, EffectRules.Decide(request, true, true, false, false), label);
        }

        [Test]
        public void Decide_NotTargetableComesFirst()
        {
            Assert.AreEqual(EffectOutcome.NotTargetable,
                EffectRules.Decide(Request(EffectType.Stunned), targetable: false, hostile: false, alreadyActive: true, stacks: true));
        }

        [Test]
        public void Decide_NotHostile_UnlessAllowNonEnemy()
        {
            var request = Request(EffectType.Fortified);
            Assert.AreEqual(EffectOutcome.NotHostile, EffectRules.Decide(request, true, false, false, false));

            request.allowNonEnemy = true;
            Assert.AreEqual(EffectOutcome.Applied, EffectRules.Decide(request, true, false, false, false));
        }

        [Test]
        public void Decide_RejectsInvalidRequests()
        {
            AssertInvalid(Request(EffectType.Corroded, magnitude: -1f), "negative magnitude");
            AssertInvalid(Request(EffectType.Corroded, magnitude: float.NaN), "NaN magnitude");
            AssertInvalid(Request(EffectType.Overheated, magnitude: float.PositiveInfinity), "infinite magnitude");
            AssertInvalid(Request(EffectType.Stunned, duration: 0f), "zero duration");
            AssertInvalid(Request(EffectType.Stunned, duration: float.NaN), "NaN duration");
            AssertInvalid(Request((EffectType)42), "unknown type");
        }

        [Test]
        public void Decide_IgnoresMagnitudeForEffectsThatDoNotUseIt()
        {
            Assert.AreEqual(EffectOutcome.Applied, EffectRules.Decide(Request(EffectType.Stunned, magnitude: -5f), true, true, false, false));
        }

        [Test]
        public void Decide_AllowsAnUntimedDuration()
        {
            Assert.AreEqual(EffectOutcome.Applied,
                EffectRules.Decide(Request(EffectType.Fortified, duration: float.PositiveInfinity), true, true, false, false));
        }

        [Test]
        public void Decide_OverhauledNeedsNoDuration_AndIsNeverAlreadyActive()
        {
            Assert.AreEqual(EffectOutcome.Applied,
                EffectRules.Decide(Request(EffectType.Overhauled, magnitude: 0f, duration: 0f), true, true, alreadyActive: true, stacks: false));
        }

        [Test]
        public void Decide_AlreadyActive_RestartsOnlyWhenItStacks()
        {
            var request = Request(EffectType.Reeling);
            Assert.AreEqual(EffectOutcome.Restarted, EffectRules.Decide(request, true, true, alreadyActive: true, stacks: true));
            Assert.AreEqual(EffectOutcome.AlreadyActive, EffectRules.Decide(request, true, true, alreadyActive: true, stacks: false));
        }

        [Test]
        public void Decide_New_IsApplied()
        {
            Assert.AreEqual(EffectOutcome.Applied, EffectRules.Decide(Request(EffectType.Suppressed), true, true, false, true));
        }

        [Test]
        public void AttackSnapshot_PrefersTheRequest_ThenTheSource_ThenNeutral()
        {
            Assert.AreEqual(200f, EffectRules.AttackSnapshot(200f, true, 150f));
            Assert.AreEqual(150f, EffectRules.AttackSnapshot(null, true, 150f));
            Assert.AreEqual(100f, EffectRules.AttackSnapshot(null, false, 150f));
        }

        [Test]
        public void StatChange_MapsTheFourStatEffects()
        {
            AssertStat(EffectType.Corroded, CarStat.Defense, -30f);
            AssertStat(EffectType.Fortified, CarStat.Defense, 30f);
            AssertStat(EffectType.Spiked, CarStat.TopSpeed, -30f);
            AssertStat(EffectType.Exhausted, CarStat.Attack, -30f);
            Assert.IsFalse(EffectRules.StatChange(EffectType.Stunned, 30f, out _, out _));
            Assert.IsFalse(EffectRules.StatChange(EffectType.Overheated, 30f, out _, out _));
        }

        static void AssertStat(EffectType type, CarStat expectedStat, float expectedPercent)
        {
            Assert.IsTrue(EffectRules.StatChange(type, 30f, out CarStat stat, out float percent), type.ToString());
            Assert.AreEqual(expectedStat, stat, type.ToString());
            Assert.AreEqual(expectedPercent, percent, 1e-5f, type.ToString());
        }

        [Test]
        public void BlockMask_MatchesTheSpec()
        {
            Assert.AreEqual(CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram, EffectRules.BlockMask(EffectType.Stunned));
            Assert.AreEqual(CarAbility.Fire, EffectRules.BlockMask(EffectType.Suppressed));
            Assert.AreEqual(CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram, EffectRules.BlockMask(EffectType.Reeling));
            Assert.AreEqual(CarAbility.None, EffectRules.BlockMask(EffectType.Corroded));
            Assert.AreEqual(CarAbility.None, EffectRules.BlockMask(EffectType.Armored));
        }

        [Test]
        public void DecaySpin_IsTimestepIndependent()
        {
            float oneStep = EffectRules.DecaySpin(3f, 2f, 0.02f);
            float twoSteps = EffectRules.DecaySpin(EffectRules.DecaySpin(3f, 2f, 0.01f), 2f, 0.01f);
            Assert.AreEqual(oneStep, twoSteps, 1e-5f);
            Assert.Less(oneStep, 3f);
        }
    }
}
```

`Tests/EditMode/EffectSetTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    public class EffectSetTests
    {
        static EffectSet.Entry Entry(EffectType type, float duration)
        {
            return new EffectSet.Entry { type = type, remaining = duration, duration = duration, magnitude = 10f };
        }

        [Test]
        public void Set_MakesTheTypeActive()
        {
            var set = new EffectSet();
            set.Set(Entry(EffectType.Corroded, 3f));

            Assert.IsTrue(set.IsActive(EffectType.Corroded));
            Assert.AreEqual(3f, set.Remaining(EffectType.Corroded));
            Assert.IsTrue(set.TryGet(EffectType.Corroded, out EffectSet.Entry entry));
            Assert.AreEqual(10f, entry.magnitude);
            Assert.IsFalse(set.IsActive(EffectType.Stunned));
            Assert.AreEqual(0f, set.Remaining(EffectType.Stunned));
        }

        [Test]
        public void Advance_ExpiresAtTheDuration_DespiteFloatSteps()
        {
            var set = new EffectSet();
            var expired = new List<EffectType>();
            set.Set(Entry(EffectType.Corroded, 3f));

            for (int i = 0; i < 149; i++) set.Advance(0.02f, expired);
            Assert.IsTrue(set.IsActive(EffectType.Corroded), "one step still to go");
            Assert.AreEqual(0, expired.Count);

            set.Advance(0.02f, expired);
            Assert.IsFalse(set.IsActive(EffectType.Corroded));
            CollectionAssert.AreEqual(new[] { EffectType.Corroded }, expired);
        }

        [Test]
        public void Advance_ReportsExpiredTypesInTypeOrder()
        {
            var set = new EffectSet();
            var expired = new List<EffectType>();
            set.Set(Entry(EffectType.Exhausted, 1f));
            set.Set(Entry(EffectType.Stunned, 1f));

            set.Advance(1f, expired);

            CollectionAssert.AreEqual(new[] { EffectType.Stunned, EffectType.Exhausted }, expired);
        }

        [Test]
        public void Advance_NeverExpiresAnUntimedEffect()
        {
            var set = new EffectSet();
            var expired = new List<EffectType>();
            set.Set(Entry(EffectType.Fortified, float.PositiveInfinity));

            set.Advance(1000f, expired);

            Assert.IsTrue(set.IsActive(EffectType.Fortified));
        }

        [Test]
        public void Remove_ReportsWhetherItWasActive()
        {
            var set = new EffectSet();
            set.Set(Entry(EffectType.Armored, 2f));

            Assert.IsTrue(set.Remove(EffectType.Armored));
            Assert.IsFalse(set.Remove(EffectType.Armored));
            Assert.IsFalse(set.IsActive(EffectType.Armored));
        }

        [Test]
        public void ActiveTypes_ListsInTypeOrder()
        {
            var set = new EffectSet();
            var types = new List<EffectType> { EffectType.Armored };
            set.Set(Entry(EffectType.Reeling, 2f));
            set.Set(Entry(EffectType.Stunned, 2f));

            set.ActiveTypes(types);

            CollectionAssert.AreEqual(new[] { EffectType.Stunned, EffectType.Reeling }, types, "cleared first, then filled");
        }
    }
}
```

`Tests/EditMode/EffectsConfigTests.cs`:

```csharp
using System;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    public class EffectsConfigTests
    {
        EffectsConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EffectsConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Stacks_DefaultsToReelingOnly()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                Assert.AreEqual(type == EffectType.Reeling, _config.Stacks(type), type.ToString());
            }
        }

        /// <summary>Catches a copy-paste slip: each type must read its own field and only its own field.</summary>
        [Test]
        public void Stacks_ReadsEachEffectsOwnField()
        {
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                if (type == EffectType.Overhauled) continue;

                string name = type.ToString();
                var field = typeof(EffectsConfig).GetField(char.ToLowerInvariant(name[0]) + name.Substring(1) + "Stacks");
                Assert.IsNotNull(field, name);

                bool original = (bool)field.GetValue(_config);
                field.SetValue(_config, !original);

                foreach (EffectType other in Enum.GetValues(typeof(EffectType)))
                {
                    bool expected = other == EffectType.Reeling;
                    if (other == type) expected = !original;
                    Assert.AreEqual(expected, _config.Stacks(other), $"after flipping {name}, {other}");
                }

                field.SetValue(_config, original);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with compile errors for the `MotorCombat.Effects` namespace.

- [ ] **Step 3: Implement**

`Scripts/Effects/MotorCombat.Effects.asmdef`:

```json
{
    "name": "MotorCombat.Effects",
    "rootNamespace": "MotorCombat.Effects",
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

`Scripts/Effects/EffectsConfig.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Global per-effect rules, the same for every source. How big and how long
    /// an effect is comes from whatever applies it (RamConfig, later weapon
    /// configs). Every value here is a placeholder — tuning belongs to the designer.
    /// </summary>
    [CreateAssetMenu(menuName = "Motor Combat/Effects Config", fileName = "EffectsConfig")]
    public class EffectsConfig : ScriptableObject
    {
        const string StackTip = "On: a new copy landing on a car that already has this effect restarts its timer, and the new size replaces the old. Off: the new copy does nothing.";

        [Header("Stacking")]
        [Tooltip(StackTip)] public bool stunnedStacks;
        [Tooltip(StackTip)] public bool suppressedStacks;
        [Tooltip(StackTip)] public bool overheatedStacks;
        [Tooltip(StackTip)] public bool corrodedStacks;
        [Tooltip(StackTip)] public bool reelingStacks = true;
        [Tooltip(StackTip)] public bool spikedStacks;
        [Tooltip(StackTip)] public bool fortifiedStacks;
        [Tooltip(StackTip)] public bool armoredStacks;
        [Tooltip(StackTip)] public bool exhaustedStacks;

        [Header("Overheated")]
        [Tooltip("Flat: magnitude is hit points per tick. MaxHealthPercent: magnitude is percent of the victim's max health per tick. Applies to every source of Overheated.")]
        public DamageKind overheatedDamageKind = DamageKind.Flat;

        [Tooltip("Seconds between ticks, for every source. The first tick lands when the effect does.")]
        [Min(0.02f)]
        public float overheatedTickSeconds = 1f;

        [Header("Reeling")]
        [Tooltip("Rate in 1/s at which a reeling car's spin decays, as exp(-rate × dt).")]
        public float reelingSpinDecayRate = 2f;

        [Header("Debug (Inspector context menus on CarEffects)")]
        [Tooltip("Seconds.")]
        [Min(0.01f)]
        public float debugDuration = 3f;

        [Tooltip("Percent, for Corroded, Spiked, Fortified and Exhausted.")]
        [Min(0f)]
        public float debugPercent = 30f;

        [Tooltip("Damage per tick for Overheated, in the damage kind above.")]
        [Min(0f)]
        public float debugOverheatAmount = 20f;

        public bool Stacks(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return stunnedStacks;
                case EffectType.Suppressed: return suppressedStacks;
                case EffectType.Overheated: return overheatedStacks;
                case EffectType.Corroded: return corrodedStacks;
                case EffectType.Reeling: return reelingStacks;
                case EffectType.Spiked: return spikedStacks;
                case EffectType.Fortified: return fortifiedStacks;
                case EffectType.Armored: return armoredStacks;
                case EffectType.Exhausted: return exhaustedStacks;
                default: return false;   // Overhauled is instant; unknown types never stack
            }
        }
    }
}
```

`Scripts/Effects/EffectRules.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>Every effect decision that needs no scene.</summary>
    public static class EffectRules
    {
        /// <summary>A timer at or below this many seconds has expired. Physics time accumulates in float steps.</summary>
        public const float ExpiryTolerance = 1e-4f;

        /// <summary>Attack when there is neither a snapshot nor a source. Equals DamageRules.NeutralAttack, which lives in Combat.</summary>
        public const float NeutralAttack = 100f;

        const CarAbility StunnedMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram;
        const CarAbility SuppressedMask = CarAbility.Fire;
        const CarAbility ReelingMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram;

        /// <summary>
        /// targetable → hostility → valid → Overhauled → already active (stacks or not) → new.
        /// Overhauled is always Applied once it passes the first three checks: it is never itself active.
        /// </summary>
        public static EffectOutcome Decide(in EffectRequest request, bool targetable, bool hostile, bool alreadyActive, bool stacks)
        {
            if (!targetable) return EffectOutcome.NotTargetable;
            if (!request.allowNonEnemy && !hostile) return EffectOutcome.NotHostile;
            if (!IsValid(request)) return EffectOutcome.Invalid;
            if (request.type == EffectType.Overhauled) return EffectOutcome.Applied;
            if (alreadyActive) return stacks ? EffectOutcome.Restarted : EffectOutcome.AlreadyActive;
            return EffectOutcome.Applied;
        }

        /// <summary>
        /// Known type; a timed effect needs a positive duration (+∞ allowed);
        /// a sized effect needs a finite, non-negative magnitude. Written as
        /// !(x > 0) so NaN from a broken config is rejected too.
        /// </summary>
        public static bool IsValid(in EffectRequest request)
        {
            if (!EffectInfo.IsKnown(request.type)) return false;
            if (EffectInfo.IsTimed(request.type) && !(request.duration > 0f)) return false;
            if (EffectInfo.UsesMagnitude(request.type) && !(request.magnitude >= 0f && !float.IsInfinity(request.magnitude))) return false;
            return true;
        }

        /// <summary>The attack an effect's damage uses for its whole life, fixed at apply.</summary>
        public static float AttackSnapshot(float? requested, bool hasSource, float sourceAttack)
        {
            return requested ?? (hasSource ? sourceAttack : NeutralAttack);
        }

        public static CarAbility BlockMask(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return StunnedMask;
                case EffectType.Suppressed: return SuppressedMask;
                case EffectType.Reeling: return ReelingMask;
                default: return CarAbility.None;
            }
        }

        /// <summary>The stat and signed percentage a stat effect of this magnitude applies. False for non-stat effects.</summary>
        public static bool StatChange(EffectType type, float magnitude, out CarStat stat, out float percent)
        {
            switch (type)
            {
                case EffectType.Corroded: stat = CarStat.Defense; percent = -magnitude; return true;
                case EffectType.Fortified: stat = CarStat.Defense; percent = magnitude; return true;
                case EffectType.Spiked: stat = CarStat.TopSpeed; percent = -magnitude; return true;
                case EffectType.Exhausted: stat = CarStat.Attack; percent = -magnitude; return true;
                default: stat = default; percent = 0f; return false;
            }
        }

        /// <summary>Exponential spin decay. <paramref name="rate"/> is in 1/s.</summary>
        public static float DecaySpin(float yawRate, float rate, float dt)
        {
            return yawRate * Mathf.Exp(-rate * dt);
        }
    }
}
```

`Scripts/Effects/EffectSet.cs`:

```csharp
using System.Collections.Generic;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Which effects a car has, and for how long. The ONLY timer an effect has:
    /// its blocks, modifiers and gates are untimed and removed when this says it
    /// ended, so they can never outlive or undershoot it. At most one entry per
    /// type — the no-stacking rule makes a second copy a restart, never a second entry.
    /// </summary>
    public class EffectSet
    {
        public struct Entry
        {
            public EffectType type;
            public float remaining;
            public float duration;
            public float magnitude;
            public CarController source;
            public string sourceTag;

            /// <summary>Attack fixed at apply; used by Overheated's ticks.</summary>
            public float attack;

            public bool allowNonEnemy;
        }

        readonly Entry[] _entries = new Entry[EffectInfo.Count];
        readonly bool[] _active = new bool[EffectInfo.Count];

        public bool IsActive(EffectType type)
        {
            return EffectInfo.IsKnown(type) && _active[(int)type];
        }

        public bool TryGet(EffectType type, out Entry entry)
        {
            if (!IsActive(type))
            {
                entry = default;
                return false;
            }

            entry = _entries[(int)type];
            return true;
        }

        public float Remaining(EffectType type)
        {
            return IsActive(type) ? _entries[(int)type].remaining : 0f;
        }

        /// <summary>Adds or replaces the entry for its type.</summary>
        public void Set(in Entry entry)
        {
            _entries[(int)entry.type] = entry;
            _active[(int)entry.type] = true;
        }

        public bool Remove(EffectType type)
        {
            if (!IsActive(type)) return false;

            _active[(int)type] = false;
            _entries[(int)type] = default;
            return true;
        }

        /// <summary>Counts every timer down; clears <paramref name="expired"/>, then adds each type that ended, in type order, and removes it.</summary>
        public void Advance(float dt, List<EffectType> expired)
        {
            expired.Clear();

            for (int i = 0; i < _entries.Length; i++)
            {
                if (!_active[i]) continue;

                _entries[i].remaining -= dt;   // infinity minus dt stays infinity
                if (_entries[i].remaining <= EffectRules.ExpiryTolerance)
                {
                    expired.Add((EffectType)i);
                    _active[i] = false;
                    _entries[i] = default;
                }
            }
        }

        /// <summary>Clears <paramref name="into"/>, then adds every active type in type order.</summary>
        public void ActiveTypes(List<EffectType> into)
        {
            into.Clear();
            for (int i = 0; i < _active.Length; i++)
            {
                if (_active[i]) into.Add((EffectType)i);
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="244"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Add the Effects assembly: EffectsConfig, EffectRules and EffectSet

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Effect behaviours and CarEffects

**Files:**
- Create in `Scripts/Effects/`: `EffectHost.cs`, `EffectBehaviour.cs`, `BlockEffect.cs`, `StunnedEffect.cs`, `ReelingEffect.cs`, `StatEffect.cs`, `ArmoredEffect.cs`, `OverheatedEffect.cs`, `CarEffects.cs`
- Create: `Tests/EditMode/CarEffectsTests.cs`

**Interfaces:**
- Consumes: Task 4's `EffectsConfig`, `EffectRules` and `EffectSet`; Task 3's contracts; `IDamageable`, `TickSchedule`, `Hostility`, `CarAbilities`, `CarStats` and `ICarModule` from Core; `IRespawnable` from Task 1.
- Produces: `public class CarEffects : MonoBehaviour, IEffectReceiver, ICarModule, IRespawnable` with:
  - `public EffectsConfig config;`
  - `public bool logEffects;`
  - `public void Step(float dt, float now)`, which `Tick` calls with `Time.fixedTime`
  - the `IEffectReceiver` members

  Task 6 adds it in `CarFactory` and applies Reeling from `RammingModule` through `IEffectReceiver`.

**Note for the implementer:** EditMode never runs `Awake` or `FixedUpdate`, so the tests call `Apply` (at `Time.fixedTime` = 0) and then `Step(dt, now)` with explicit times. Some tests set and read `Rigidbody` velocities in EditMode. If Unity refuses that readback (values stay zero), report it as a concern and don't weaken the other assertions.

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/CarEffectsTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Combat;
using MotorCombat.Effects;

namespace MotorCombat.Tests
{
    /// <summary>
    /// CarEffects end to end on a real Health, without a scene. Apply happens at
    /// Time.fixedTime = 0 (EditMode); Step is then driven with explicit times.
    /// </summary>
    public class CarEffectsTests
    {
        GameObject _carObject;
        GameObject _sourceObject;
        CarController _car;
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

            _carObject = new GameObject("Car", typeof(CarController));
            _car = _carObject.GetComponent<CarController>();
            _body = _carObject.GetComponent<Rigidbody>();
            _car.Stats.SetBase(CarStat.Attack, 100f);
            _health = _carObject.AddComponent<Health>();
            _health.maxHealth = 1000f;
            _effects = _carObject.AddComponent<CarEffects>();
            _effects.config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_carObject);
            Object.DestroyImmediate(_sourceObject);
            Object.DestroyImmediate(_config);
        }

        EffectOutcome Apply(EffectType type, float magnitude = 30f, float duration = 3f, bool fromSelf = false)
        {
            return _effects.Apply(new EffectRequest
            {
                source = fromSelf ? _car : _source,
                sourceTag = "test",
                type = type,
                magnitude = magnitude,
                duration = duration,
                allowNonEnemy = fromSelf
            });
        }

        static DamageRequest Flat(float amount, CarController source)
        {
            return new DamageRequest { source = source, sourceTag = "test", kind = DamageKind.Flat, amount = amount };
        }

        // --- Each effect ---------------------------------------------------------

        [Test]
        public void Stunned_BlocksDrivingWeaponsAndRamming_AndStopsTheCarOnce()
        {
            _body.linearVelocity = new Vector3(5f, 1f, 3f);
            _body.angularVelocity = new Vector3(0f, 2f, 0f);

            Assert.AreEqual(EffectOutcome.Applied, Apply(EffectType.Stunned));

            Assert.IsFalse(_car.Abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Steer));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Fire));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Ram));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Grip | CarAbility.YawHold), "stop once, then pushable: grip and yaw stay");
            Assert.AreEqual(new Vector3(0f, 1f, 0f), _body.linearVelocity, "horizontal zeroed, vertical kept");
            Assert.AreEqual(Vector3.zero, _body.angularVelocity);

            _body.linearVelocity = new Vector3(4f, 0f, 0f);
            _effects.Step(0.5f, 0.5f);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), _body.linearVelocity, "a shove after the stun landed is not undone");
        }

        [Test]
        public void Stunned_EndsAfterItsDuration_AndGivesTheAbilitiesBack()
        {
            Apply(EffectType.Stunned, duration: 1f);

            _effects.Step(0.5f, 0.5f);
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Throttle));

            _effects.Step(0.5f, 1f);
            Assert.IsFalse(_effects.Has(EffectType.Stunned));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle | CarAbility.Steer | CarAbility.Fire | CarAbility.Ram));
        }

        [Test]
        public void Suppressed_BlocksOnlyFire()
        {
            Apply(EffectType.Suppressed);

            Assert.IsFalse(_car.Abilities.Has(CarAbility.Fire));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram | CarAbility.Targetable));
        }

        [Test]
        public void Corroded_LowersDefense_UntilItEnds()
        {
            _car.Stats.SetBase(CarStat.Defense, 100f);

            Apply(EffectType.Corroded, magnitude: 30f, duration: 2f);
            Assert.AreEqual(70f, _car.Stats.Effective(CarStat.Defense), 1e-3f);

            _effects.Step(2f, 2f);
            Assert.AreEqual(100f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
        }

        [Test]
        public void Spiked_LowersTopSpeed()
        {
            Apply(EffectType.Spiked, magnitude: 30f);
            Assert.AreEqual(0.7f, _car.Stats.Effective(CarStat.TopSpeed), 1e-4f);
        }

        [Test]
        public void Exhausted_LowersAttack()
        {
            Apply(EffectType.Exhausted, magnitude: 30f);
            Assert.AreEqual(70f, _car.Stats.Effective(CarStat.Attack), 1e-3f);
        }

        [Test]
        public void DifferentEffectsOnTheSameStat_Add()
        {
            _car.Stats.SetBase(CarStat.Defense, 100f);

            Apply(EffectType.Corroded, magnitude: 30f);
            Apply(EffectType.Fortified, magnitude: 20f, fromSelf: true);

            Assert.AreEqual(90f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
        }

        [Test]
        public void Reeling_BlocksLikeARamReel_AndDecaysTheSpin()
        {
            _body.angularVelocity = new Vector3(0f, 3f, 0f);

            Apply(EffectType.Reeling, duration: 1f);

            Assert.IsFalse(_car.Abilities.Has(CarAbility.Throttle));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Steer));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.YawHold));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Grip));
            Assert.IsFalse(_car.Abilities.Has(CarAbility.Ram));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Fire));

            _effects.Step(0.1f, 0.1f);
            Assert.AreEqual(3f * Mathf.Exp(-2f * 0.1f), _body.angularVelocity.y, 1e-4f);
        }

        [Test]
        public void Armored_BlocksAllDamage_IncludingSelfInflicted_UntilItEnds()
        {
            Apply(EffectType.Armored, duration: 1f, fromSelf: true);

            Assert.AreEqual(DamageOutcome.Blocked, _health.Apply(Flat(100f, _source)).outcome);
            var self = Flat(100f, _car);
            self.allowNonEnemy = true;
            Assert.AreEqual(DamageOutcome.Blocked, _health.Apply(self).outcome);

            _effects.Step(1f, 1f);
            Assert.AreEqual(DamageOutcome.Applied, _health.Apply(Flat(100f, _source)).outcome);
        }

        [Test]
        public void Overheated_TicksOnApply_ThenEveryInterval_ButNotAtExpiry()
        {
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);
            Assert.AreEqual(990f, _health.Current, 1e-3f, "first tick lands on apply");

            _effects.Step(0.5f, 0.5f);
            Assert.AreEqual(990f, _health.Current, 1e-3f);

            _effects.Step(0.5f, 1f);
            Assert.AreEqual(980f, _health.Current, 1e-3f);

            _effects.Step(1f, 2f);
            Assert.AreEqual(970f, _health.Current, 1e-3f);

            _effects.Step(1f, 3f);
            Assert.AreEqual(970f, _health.Current, 1e-3f, "expired before it could tick at 3 s");
            Assert.IsFalse(_effects.Has(EffectType.Overheated));
        }

        [Test]
        public void Overheated_KeepsTheAttackFromApplyTime()
        {
            _source.Stats.SetBase(CarStat.Attack, 200f);
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);
            Assert.AreEqual(980f, _health.Current, 1e-3f);

            _source.Stats.SetBase(CarStat.Attack, 50f);
            _effects.Step(1f, 1f);
            Assert.AreEqual(960f, _health.Current, 1e-3f, "the later drop in the source's attack does not weaken the burn");
        }

        [Test]
        public void Overheated_UsesTheConfiguredDamageKind()
        {
            _config.overheatedDamageKind = DamageKind.MaxHealthPercent;

            Apply(EffectType.Overheated, magnitude: 1f, duration: 3f);

            Assert.AreEqual(990f, _health.Current, 1e-3f, "1% of 1000 max health");
        }

        [Test]
        public void Overheated_TicksAreBlockedByArmored()
        {
            Apply(EffectType.Armored, duration: 5f, fromSelf: true);
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);
            _effects.Step(1f, 1f);

            Assert.AreEqual(1000f, _health.Current, 1e-3f);
        }

        // --- Stacking, cleanse, rejections --------------------------------------

        [Test]
        public void Reapply_WhenTheEffectDoesNotStack_ChangesNothing()
        {
            _car.Stats.SetBase(CarStat.Defense, 100f);
            Apply(EffectType.Corroded, magnitude: 30f, duration: 4f);
            _effects.Step(3f, 3f);

            Assert.AreEqual(EffectOutcome.AlreadyActive, Apply(EffectType.Corroded, magnitude: 50f, duration: 4f));

            Assert.AreEqual(70f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
            Assert.AreEqual(1f, _effects.Remaining(EffectType.Corroded), 1e-3f);
        }

        [Test]
        public void Reapply_WhenTheEffectStacks_RestartsTheTimerAndReplacesTheSize()
        {
            _config.corrodedStacks = true;
            _car.Stats.SetBase(CarStat.Defense, 100f);
            Apply(EffectType.Corroded, magnitude: 30f, duration: 4f);
            _effects.Step(3f, 3f);

            Assert.AreEqual(EffectOutcome.Restarted, Apply(EffectType.Corroded, magnitude: 50f, duration: 4f));

            Assert.AreEqual(50f, _car.Stats.Effective(CarStat.Defense), 1e-3f);
            Assert.AreEqual(4f, _effects.Remaining(EffectType.Corroded), 1e-3f);
        }

        [Test]
        public void Overhauled_EndsEveryEffect_BuffsIncluded()
        {
            var ended = new List<EffectType>();
            _effects.Ended += ended.Add;
            Apply(EffectType.Stunned);
            Apply(EffectType.Fortified, fromSelf: true);

            Assert.AreEqual(EffectOutcome.Applied, Apply(EffectType.Overhauled, magnitude: 0f, duration: 0f, fromSelf: true));

            Assert.IsFalse(_effects.Has(EffectType.Stunned));
            Assert.IsFalse(_effects.Has(EffectType.Fortified));
            Assert.IsFalse(_effects.Has(EffectType.Overhauled), "instant, never itself active");
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle));
            CollectionAssert.AreEquivalent(new[] { EffectType.Stunned, EffectType.Fortified }, ended);
        }

        [Test]
        public void NonEnemySource_IsRejectedUnlessAllowed()
        {
            var request = new EffectRequest { source = _car, sourceTag = "test", type = EffectType.Corroded, magnitude = 30f, duration = 3f };
            Assert.AreEqual(EffectOutcome.NotHostile, _effects.Apply(request));

            request.allowNonEnemy = true;
            Assert.AreEqual(EffectOutcome.Applied, _effects.Apply(request));
        }

        [Test]
        public void Wreck_TakesNoEffects()
        {
            _health.Apply(Flat(5000f, _source));

            Assert.AreEqual(EffectOutcome.NotTargetable, Apply(EffectType.Stunned));
        }

        [Test]
        public void Death_EndsEveryEffect()
        {
            Apply(EffectType.Corroded);
            Apply(EffectType.Overheated, magnitude: 10f, duration: 3f);

            _health.Apply(Flat(5000f, _source));

            Assert.IsFalse(_effects.Has(EffectType.Corroded));
            Assert.IsFalse(_effects.Has(EffectType.Overheated));
            _effects.Step(1f, 1f);
            Assert.AreEqual(0f, _health.Current);
        }

        [Test]
        public void ResetForRespawn_EndsEveryEffect()
        {
            Apply(EffectType.Suppressed);
            Apply(EffectType.Corroded);

            _effects.ResetForRespawn();

            Assert.IsFalse(_effects.Has(EffectType.Suppressed));
            Assert.IsFalse(_effects.Has(EffectType.Corroded));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Fire));
        }

        [Test]
        public void GetActive_ListsEffectsInTypeOrder_WithTheirTimeLeft()
        {
            Apply(EffectType.Exhausted, duration: 2f);
            Apply(EffectType.Stunned, duration: 3f);
            _effects.Step(0.5f, 0.5f);

            var active = new List<ActiveEffect> { new ActiveEffect() };
            _effects.GetActive(active);

            Assert.AreEqual(2, active.Count, "cleared first, then filled");
            Assert.AreEqual(EffectType.Stunned, active[0].type);
            Assert.AreEqual(2.5f, active[0].remaining, 1e-3f);
            Assert.AreEqual(EffectType.Exhausted, active[1].type);
            Assert.AreEqual(1.5f, active[1].remaining, 1e-3f);
        }

        [Test]
        public void MissingConfig_IsInvalid()
        {
            _effects.config = null;

            Assert.AreEqual(EffectOutcome.Invalid, Apply(EffectType.Stunned));
            Assert.IsTrue(_car.Abilities.Has(CarAbility.Throttle));
        }

        [Test]
        public void Applied_IsRaisedForApplyAndRestart_NotForRejections()
        {
            var reports = new List<EffectReport>();
            _effects.Applied += reports.Add;

            Apply(EffectType.Reeling);      // Applied
            Apply(EffectType.Reeling);      // Restarted: Reeling stacks by default
            Apply(EffectType.Suppressed);   // Applied
            Apply(EffectType.Suppressed);   // AlreadyActive: no event

            Assert.AreEqual(3, reports.Count);
            Assert.AreEqual(EffectOutcome.Restarted, reports[1].outcome);
            Assert.AreSame(_car, reports[0].target);
            Assert.AreSame(_source, reports[0].source);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with compile errors for `CarEffects`.

- [ ] **Step 3: Implement**

`Scripts/Effects/EffectHost.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>What effect behaviours act on. One per car, built by CarEffects.</summary>
    public sealed class EffectHost
    {
        public EffectHost(CarController car, Rigidbody body, IDamageable health, TickSchedule ticks)
        {
            Car = car;
            Body = body;
            Health = health;
            Ticks = ticks;
        }

        public CarController Car { get; }
        public Rigidbody Body { get; }

        /// <summary>May be null on a car without health; effects that deal or gate damage then do nothing.</summary>
        public IDamageable Health { get; }

        /// <summary>This car's own schedule, used by Overheated.</summary>
        public TickSchedule Ticks { get; }

        public EffectsConfig Config { get; set; }

        /// <summary>Physics time of the current apply or step.</summary>
        public float Now { get; set; }
    }
}
```

`Scripts/Effects/EffectBehaviour.cs`:

```csharp
namespace MotorCombat.Effects
{
    /// <summary>
    /// What one effect does. One instance per effect per car, and that instance
    /// is the source key for every block, modifier, gate and tick it registers,
    /// so its OnEnd removes exactly what its OnStart added. Timing lives in
    /// EffectSet, never here.
    /// </summary>
    public abstract class EffectBehaviour
    {
        /// <summary>The effect has just landed.</summary>
        public virtual void OnStart(EffectHost host, in EffectSet.Entry entry) { }

        /// <summary>A stacking copy landed while active; the entry already holds the new size and timer.</summary>
        public virtual void OnRefresh(EffectHost host, in EffectSet.Entry entry) { }

        /// <summary>Every physics step while active, after expiry has been handled.</summary>
        public virtual void OnStep(EffectHost host, in EffectSet.Entry entry, float dt) { }

        /// <summary>Expired, cleansed, or the car died or respawned.</summary>
        public virtual void OnEnd(EffectHost host) { }
    }
}
```

`Scripts/Effects/BlockEffect.cs`:

```csharp
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Switches abilities off for as long as the effect lasts. The block is
    /// untimed; EffectSet decides when it ends. Suppressed uses this directly.
    /// </summary>
    public class BlockEffect : EffectBehaviour
    {
        readonly CarAbility _mask;

        public BlockEffect(EffectType type)
        {
            _mask = EffectRules.BlockMask(type);
        }

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            host.Car.Abilities.Block(this, _mask, float.PositiveInfinity, BlockRefresh.KeepLonger);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Car.Abilities.Unblock(this);
        }
    }
}
```

`Scripts/Effects/StunnedEffect.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// A complete stop, once, when the stun lands (and again if a stacking stun
    /// lands). Afterwards grip and drag stay on, so a ram or weapon impulse can
    /// still shove a stunned car.
    /// </summary>
    public sealed class StunnedEffect : BlockEffect
    {
        public StunnedEffect() : base(EffectType.Stunned) { }

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            base.OnStart(host, entry);
            Stop(host);
        }

        public override void OnRefresh(EffectHost host, in EffectSet.Entry entry)
        {
            Stop(host);
        }

        static void Stop(EffectHost host)
        {
            Rigidbody body = host.Body;
            if (body == null) return;

            // Vertical velocity is kept: gravity and the floor own it.
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
            body.angularVelocity = Vector3.zero;
        }
    }
}
```

`Scripts/Effects/ReelingEffect.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// No throttle, steer, grip or yaw hold — the car slides and spins freely,
    /// as after a ram. Driving does not write yaw while YawHold is blocked, so
    /// the spin winds down here.
    /// </summary>
    public sealed class ReelingEffect : BlockEffect
    {
        public ReelingEffect() : base(EffectType.Reeling) { }

        public override void OnStep(EffectHost host, in EffectSet.Entry entry, float dt)
        {
            Rigidbody body = host.Body;
            if (body == null || host.Config == null) return;

            body.angularVelocity = Vector3.up * EffectRules.DecaySpin(body.angularVelocity.y, host.Config.reelingSpinDecayRate, dt);
        }
    }
}
```

`Scripts/Effects/StatEffect.cs`:

```csharp
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>Corroded, Fortified, Spiked and Exhausted: one percentage modifier for as long as the effect lasts.</summary>
    public sealed class StatEffect : EffectBehaviour
    {
        readonly EffectType _type;

        public StatEffect(EffectType type)
        {
            _type = type;
        }

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            AddModifier(host, entry.magnitude);
        }

        /// <summary>The same source replaces its modifier, so a stacking copy's size takes over.</summary>
        public override void OnRefresh(EffectHost host, in EffectSet.Entry entry)
        {
            AddModifier(host, entry.magnitude);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Car.Stats.Remove(this);
        }

        void AddModifier(EffectHost host, float magnitude)
        {
            if (EffectRules.StatChange(_type, magnitude, out CarStat stat, out float percent))
            {
                host.Car.Stats.Add(this, stat, percent);
            }
        }
    }
}
```

`Scripts/Effects/ArmoredEffect.cs`:

```csharp
using System;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>Immune to all damage, self-inflicted included. Effects still land.</summary>
    public sealed class ArmoredEffect : EffectBehaviour
    {
        static readonly Func<DamageRequest, bool> BlockEverything = request => true;

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            host.Health?.AddGate(this, BlockEverything);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Health?.RemoveGate(this);
        }
    }
}
```

`Scripts/Effects/OverheatedEffect.cs`:

```csharp
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// Damage on a fixed rhythm: the first tick on apply, then every
    /// overheatedTickSeconds. Each tick uses the attack captured at apply, so
    /// weakening the attacker later does not weaken a burn already running;
    /// defense is read live. A stacking re-apply keeps the rhythm, so it can
    /// never tick early.
    /// </summary>
    public sealed class OverheatedEffect : EffectBehaviour
    {
        public const string DamageTag = "overheated";

        public override void OnStart(EffectHost host, in EffectSet.Entry entry)
        {
            TryTick(host, entry);
        }

        public override void OnStep(EffectHost host, in EffectSet.Entry entry, float dt)
        {
            TryTick(host, entry);
        }

        public override void OnEnd(EffectHost host)
        {
            host.Ticks.Forget(host.Car);
        }

        void TryTick(EffectHost host, in EffectSet.Entry entry)
        {
            if (host.Health == null || host.Config == null) return;
            if (!host.Ticks.TryTick(this, host.Car, host.Now, host.Config.overheatedTickSeconds)) return;

            host.Health.Apply(new DamageRequest
            {
                source = entry.source,
                sourceTag = DamageTag,
                kind = host.Config.overheatedDamageKind,
                amount = entry.magnitude,
                allowNonEnemy = entry.allowNonEnemy,
                attack = entry.attack
            });
        }
    }
}
```

`Scripts/Effects/CarEffects.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Effects
{
    /// <summary>
    /// A car's effects. Thin adapter: EffectRules decides, EffectSet times,
    /// one EffectBehaviour per effect acts. Ticks as the car's last module, so
    /// it runs after driving and ramming each physics step.
    ///
    /// Every effect ends on death (Health has already cleared the car's blocks
    /// and modifiers by then) and on respawn.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarEffects : MonoBehaviour, IEffectReceiver, ICarModule, IRespawnable
    {
        public EffectsConfig config;

        [Tooltip("Logs every applied, restarted, rejected and ended effect on this car.")]
        public bool logEffects;

        public event Action<EffectReport> Applied;
        public event Action<EffectType> Ended;

        readonly EffectSet _set = new EffectSet();
        readonly List<EffectType> _expired = new List<EffectType>();
        readonly List<EffectType> _stepping = new List<EffectType>();
        readonly List<EffectType> _ending = new List<EffectType>();

        EffectHost _host;
        EffectBehaviour[] _behaviours;

        // Built on first use rather than in Awake: EditMode never runs Awake, and
        // a collision may apply an effect before Start.
        EffectHost Host
        {
            get
            {
                if (_host == null)
                {
                    _host = new EffectHost(GetComponent<CarController>(), GetComponent<Rigidbody>(), GetComponent<IDamageable>(), new TickSchedule());
                    _behaviours = CreateBehaviours();
                    if (_host.Health != null) _host.Health.Destroyed += OnDestroyedByDamage;
                }

                _host.Config = config;
                return _host;
            }
        }

        static EffectBehaviour[] CreateBehaviours()
        {
            var behaviours = new EffectBehaviour[EffectInfo.Count];
            behaviours[(int)EffectType.Stunned] = new StunnedEffect();
            behaviours[(int)EffectType.Suppressed] = new BlockEffect(EffectType.Suppressed);
            behaviours[(int)EffectType.Overheated] = new OverheatedEffect();
            behaviours[(int)EffectType.Corroded] = new StatEffect(EffectType.Corroded);
            behaviours[(int)EffectType.Reeling] = new ReelingEffect();
            behaviours[(int)EffectType.Spiked] = new StatEffect(EffectType.Spiked);
            behaviours[(int)EffectType.Fortified] = new StatEffect(EffectType.Fortified);
            behaviours[(int)EffectType.Armored] = new ArmoredEffect();
            behaviours[(int)EffectType.Exhausted] = new StatEffect(EffectType.Exhausted);
            // Overhauled is instant and never active, so it has no behaviour.
            return behaviours;
        }

        void Start()
        {
            // Checked in Start, not Awake — CarFactory assigns config after AddComponent.
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] CarEffects on '{name}' has no EffectsConfig assigned — every effect applied to this car will be rejected.", this);
            }
        }

        void OnDestroy()
        {
            if (_host != null && _host.Health != null) _host.Health.Destroyed -= OnDestroyedByDamage;
        }

        // --- IEffectReceiver ------------------------------------------------------

        public EffectOutcome Apply(in EffectRequest request)
        {
            if (config == null)
            {
                Log(request, EffectOutcome.Invalid);
                return EffectOutcome.Invalid;
            }

            EffectHost host = Host;
            host.Now = Time.fixedTime;
            CarController car = host.Car;

            bool targetable = car.Abilities.Has(CarAbility.Targetable) && (host.Health == null || !host.Health.IsDestroyed);
            EffectOutcome outcome = EffectRules.Decide(
                request,
                targetable,
                Hostility.AreEnemies(request.source, car),
                _set.IsActive(request.type),
                config.Stacks(request.type));

            switch (outcome)
            {
                case EffectOutcome.Applied when request.type == EffectType.Overhauled:
                    EndAll();
                    break;

                case EffectOutcome.Applied:
                {
                    EffectSet.Entry entry = ToEntry(request);
                    _set.Set(entry);
                    _behaviours[(int)request.type].OnStart(host, entry);
                    break;
                }

                case EffectOutcome.Restarted:
                {
                    EffectSet.Entry entry = ToEntry(request);
                    _set.Set(entry);
                    _behaviours[(int)request.type].OnRefresh(host, entry);
                    break;
                }

                default:
                    Log(request, outcome);
                    return outcome;
            }

            Log(request, outcome);
            Applied?.Invoke(new EffectReport
            {
                source = request.source,
                target = car,
                sourceTag = request.sourceTag,
                type = request.type,
                outcome = outcome,
                magnitude = request.magnitude,
                duration = request.duration
            });
            return outcome;
        }

        public bool Has(EffectType type)
        {
            return _set.IsActive(type);
        }

        public float Remaining(EffectType type)
        {
            return _set.Remaining(type);
        }

        public void GetActive(List<ActiveEffect> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));

            into.Clear();
            for (int i = 0; i < EffectInfo.Count; i++)
            {
                if (!_set.TryGet((EffectType)i, out EffectSet.Entry entry)) continue;

                into.Add(new ActiveEffect
                {
                    type = entry.type,
                    remaining = entry.remaining,
                    duration = entry.duration,
                    magnitude = entry.magnitude,
                    source = entry.source
                });
            }
        }

        // --- ICarModule -------------------------------------------------------------

        public void Tick(in CarInput input, float dt)
        {
            Step(dt, Time.fixedTime);
        }

        public void FrameTick(in CarInput input, float dt) { }

        /// <summary>One physics step: expire, then step what is still active. Public so tests can drive time.</summary>
        public void Step(float dt, float now)
        {
            if (_host == null || config == null) return;   // nothing has ever been applied

            EffectHost host = Host;
            host.Now = now;

            _set.Advance(dt, _expired);
            for (int i = 0; i < _expired.Count; i++)
            {
                NotifyEnded(_expired[i]);
            }

            _set.ActiveTypes(_stepping);
            for (int i = 0; i < _stepping.Count; i++)
            {
                EffectType type = _stepping[i];

                // An earlier step this loop may have ended it — Overheated killing the car ends everything.
                if (!_set.TryGet(type, out EffectSet.Entry entry)) continue;
                _behaviours[(int)type].OnStep(host, entry, dt);
            }
        }

        // --- IRespawnable -----------------------------------------------------------

        public void ResetForRespawn()
        {
            EndAll();
        }

        // --- Internals ------------------------------------------------------------

        void OnDestroyedByDamage(DamageReport report)
        {
            EndAll();
        }

        void EndAll()
        {
            if (_host == null) return;

            _set.ActiveTypes(_ending);
            for (int i = 0; i < _ending.Count; i++)
            {
                if (_set.Remove(_ending[i])) NotifyEnded(_ending[i]);
            }
        }

        /// <summary>The entry is already gone from the set.</summary>
        void NotifyEnded(EffectType type)
        {
            _behaviours[(int)type].OnEnd(Host);
            if (logEffects) Debug.Log($"[Effects] {name}: {type} ended", this);
            Ended?.Invoke(type);
        }

        static EffectSet.Entry ToEntry(in EffectRequest request)
        {
            CarController source = request.source;
            bool hasSource = source != null;

            return new EffectSet.Entry
            {
                type = request.type,
                remaining = request.duration,
                duration = request.duration,
                magnitude = request.magnitude,
                source = source,
                sourceTag = request.sourceTag,
                attack = EffectRules.AttackSnapshot(request.attack, hasSource, hasSource ? source.Stats.Effective(CarStat.Attack) : 0f),
                allowNonEnemy = request.allowNonEnemy
            };
        }

        void Log(in EffectRequest request, EffectOutcome outcome)
        {
            if (!logEffects) return;

            string from = request.source != null ? request.source.name : "environment";
            Debug.Log($"[Effects] {name}: {request.type} from {from} ({request.sourceTag}) → {outcome}", this);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: apply Stunned")] void DebugStunned() => DebugApply(EffectType.Stunned);
        [ContextMenu("Debug: apply Suppressed")] void DebugSuppressed() => DebugApply(EffectType.Suppressed);
        [ContextMenu("Debug: apply Overheated")] void DebugOverheated() => DebugApply(EffectType.Overheated);
        [ContextMenu("Debug: apply Corroded")] void DebugCorroded() => DebugApply(EffectType.Corroded);
        [ContextMenu("Debug: apply Reeling")] void DebugReeling() => DebugApply(EffectType.Reeling);
        [ContextMenu("Debug: apply Spiked")] void DebugSpiked() => DebugApply(EffectType.Spiked);
        [ContextMenu("Debug: apply Fortified")] void DebugFortified() => DebugApply(EffectType.Fortified);
        [ContextMenu("Debug: apply Armored")] void DebugArmored() => DebugApply(EffectType.Armored);
        [ContextMenu("Debug: apply Overhauled")] void DebugOverhauled() => DebugApply(EffectType.Overhauled);
        [ContextMenu("Debug: apply Exhausted")] void DebugExhausted() => DebugApply(EffectType.Exhausted);

        /// <summary>Applies an effect to this car with the debug sizes from EffectsConfig, from no source.</summary>
        void DebugApply(EffectType type)
        {
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] CarEffects on '{name}' has no EffectsConfig — cannot apply a debug effect.", this);
                return;
            }

            EffectOutcome outcome = Apply(new EffectRequest
            {
                sourceTag = "debug",
                type = type,
                magnitude = type == EffectType.Overheated ? config.debugOverheatAmount : config.debugPercent,
                duration = config.debugDuration,
                allowNonEnemy = true
            });
            Debug.Log($"[Effects] {name}: debug {type} → {outcome}", this);
        }
#endif
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="267"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Add the ten effect behaviours and the CarEffects component

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Wire effects into cars and move the ram reel onto Reeling

**Files:**
- Modify:
  - `Scripts/Ramming/RammingModule.cs`, `Scripts/Ramming/RamConfig.cs`, `Scripts/Ramming/RamRules.cs`
  - `Scripts/Cars/CarDefinition.cs`, `Scripts/Cars/CarFactory.cs`, `Scripts/Cars/MotorCombat.Cars.asmdef`
  - `Scripts/Bootstrap/GameBootstrap.cs`, `Scripts/Bootstrap/MotorCombat.Bootstrap.asmdef`
  - `Editor/ConfigAssetBootstrap.cs`, `Editor/MotorCombat.EditorTools.asmdef`
  - `Tests/EditMode/CarFactoryTests.cs`, `Tests/EditMode/RamRulesTests.cs`

**Interfaces:**
- Consumes: `CarEffects` and `EffectsConfig` from Tasks 4–5; `IEffectReceiver`, `EffectRequest` and `EffectType` from Task 3.
- Produces: `CarDefinition.effectsConfig`, and every factory-built car carrying `CarEffects` as its last component.

- [ ] **Step 1: Write the failing tests**

In `Tests/EditMode/CarFactoryTests.cs`:
- Add `using MotorCombat.Effects;`.
- In `SetUp`, after `_definition.wreckConfig = ScriptableObject.CreateInstance<WreckConfig>();`, add `_definition.effectsConfig = ScriptableObject.CreateInstance<EffectsConfig>();`.
- In `TearDown`, after `Object.DestroyImmediate(_definition.wreckConfig);`, add `Object.DestroyImmediate(_definition.effectsConfig);`.
- Replace `Spawn_AttachesAllFourModules` with:

```csharp
        [Test]
        public void Spawn_AttachesAllFiveModules()
        {
            var modules = _car.GetComponents<ICarModule>();
            Assert.AreEqual(5, modules.Length, "driving, aiming, ramming, weapons, effects");
            Assert.IsInstanceOf<CarEffects>(modules[4], "effects tick last, after driving and ramming");
        }
```

- Add after `Spawn_AddsTheWreckSequenceWithItsConfig`:

```csharp
        [Test]
        public void Spawn_AddsCarEffectsWithItsConfig()
        {
            var effects = _car.GetComponent<CarEffects>();
            Assert.IsNotNull(effects);
            Assert.AreSame(_definition.effectsConfig, effects.config);
            Assert.AreSame(effects, _car.GetComponent<IEffectReceiver>());
        }
```

In `Tests/EditMode/RamRulesTests.cs`, delete the whole `DecaySpin_IsTimestepIndependent` test. It now lives in `EffectRulesTests`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with a compile error for `CarDefinition.effectsConfig`.

- [ ] **Step 3: Implement**

`Scripts/Cars/MotorCombat.Cars.asmdef`: add `"MotorCombat.Effects"` to `references`, after `"MotorCombat.Combat"`.

`Scripts/Cars/CarDefinition.cs`:
- Add `using MotorCombat.Effects;`.
- After `public WreckConfig wreckConfig;`, add `public EffectsConfig effectsConfig;`.

`Scripts/Cars/CarFactory.cs`:
- Add `using MotorCombat.Effects;`.
- After `car.AddComponent<WreckSequence>().config = definition.wreckConfig;`, add:

```csharp

            // Last, so it ticks after driving and ramming every physics step.
            car.AddComponent<CarEffects>().config = definition.effectsConfig;
```

`Scripts/Ramming/RamRules.cs`: delete the `DecaySpin` method and its summary comment. Effects owns spin decay now.

`Scripts/Ramming/RamConfig.cs`:
- Delete the `spinDecayRate` field and its tooltip. The `[Header("Spin")]` stays above `spinScale`.
- Change the `reelSeconds` tooltip to `"How long a flank or rear victim Reels (the Reeling effect: no throttle, steer or grip; spins freely). Stacking and spin decay are set in EffectsConfig."`.

`Scripts/Ramming/RammingModule.cs`:
- Replace the block-sources comment and the two keys

```csharp
        // Block sources for the ability switches. One key per kind of block is
        // enough: every car owns its own CarAbilities.
        static readonly object LockBlock = new object();
        static readonly object ReelBlock = new object();

        const CarAbility LockMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Ram;
        const CarAbility ReelMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.YawHold | CarAbility.Grip | CarAbility.Ram;
```

  with

```csharp
        // Block source for the attacker's lock. One key is enough: every car
        // owns its own CarAbilities. The lock is how ram restitution feels, not
        // an effect; the victim's reel IS an effect (Reeling), applied below.
        static readonly object LockBlock = new object();

        const CarAbility LockMask = CarAbility.Throttle | CarAbility.Steer | CarAbility.Ram;
```

- In `Start`, after the `config == null` check, add:

```csharp

            if (GetComponent<IEffectReceiver>() == null)
            {
                Debug.LogWarning($"[MotorCombat] RammingModule on '{name}' found no IEffectReceiver — cars it rams will be shoved but will not reel.", this);
            }
```

- Replace the whole `Tick` method with:

```csharp
        public void Tick(in CarInput input, float dt)
        {
            // Nothing per step: the reel's spin decay belongs to the Reeling effect.
        }
```

- In `ApplyRam`, replace

```csharp
            victim.Car.Abilities.Block(ReelBlock, ReelMask, config.reelSeconds, BlockRefresh.Restart);
```

  with

```csharp
            victim.GetComponent<IEffectReceiver>()?.Apply(new EffectRequest
            {
                source = attacker.Car,
                sourceTag = "ram",
                type = EffectType.Reeling,
                duration = config.reelSeconds
            });
```

`Scripts/Bootstrap/MotorCombat.Bootstrap.asmdef`: add `"MotorCombat.Effects"` to `references`, after `"MotorCombat.Combat"`.

`Scripts/Bootstrap/GameBootstrap.cs`: in `Validate()`, after the `wreckConfig == null` line, add:

```csharp
            if (carDefinition.effectsConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.effectsConfig is not assigned."); return false; }
```

`Editor/MotorCombat.EditorTools.asmdef`: add `"MotorCombat.Effects"` to `references`, after `"MotorCombat.Combat"`.

`Editor/ConfigAssetBootstrap.cs`:
- Add `using MotorCombat.Effects;`.
- After `GetOrCreate<RespawnConfig>("RespawnConfig");`, add `var effects = GetOrCreate<EffectsConfig>("EffectsConfig");`.
- After `if (car.wreckConfig == null) car.wreckConfig = wreck;`, add `if (car.effectsConfig == null) car.effectsConfig = effects;`.

(`CarDefinition.asset` and the scene are regenerated in Task 8. Until then Play logs the `effectsConfig` validation error — expected mid-plan.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="267"`, `failed="0"` (CarFactoryTests +1, RamRulesTests −1).

Check the module boundary is intact (the output should be empty):

```bash
grep -rn "MotorCombat.Effects" Assets/_Project/Scripts/Ramming Assets/_Project/Scripts/HUD Assets/_Project/Scripts/Combat Assets/_Project/Scripts/Driving
```

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Attach CarEffects to every car; the ram reel is now the Reeling effect

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Effect chips on the HUD

**Files:**
- Create: `Scripts/HUD/EffectChipLayout.cs`, `Scripts/HUD/EffectChipRow.cs`, `Scripts/HUD/SelfEffectsWidget.cs`, `Tests/EditMode/EffectChipLayoutTests.cs`, `Tests/EditMode/EffectChipRowTests.cs`
- Modify: `Scripts/HUD/EnemyHealthBars.cs`, `Scripts/Bootstrap/GameBootstrap.cs`

**Interfaces:**
- Consumes: `IEffectReceiver`, `ActiveEffect`, `EffectType` and `EffectInfo` (Core); `HudElements` (HUD).
- Produces:
  - `EffectChipLayout.Label(EffectType)`, `Colour(EffectType)`, `Text(EffectType, float remaining)`, `RowX(int index, int count, float chipWidth, float gap)`
  - `EffectChipRow(RectTransform parent, string name, float chipWidth, float chipHeight, float gap, int fontSize)` with `Root`, `VisibleCount`, `TextAt(int)`, `Show(IEffectReceiver)`
  - `SelfEffectsWidget { CarController viewer; Build(RectTransform canvas); }`

- [ ] **Step 1: Write the failing tests**

`Tests/EditMode/EffectChipLayoutTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using MotorCombat.Core;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class EffectChipLayoutTests
    {
        [Test]
        public void Label_IsFourLettersAndUniquePerEffect()
        {
            var seen = new HashSet<string>();
            foreach (EffectType type in Enum.GetValues(typeof(EffectType)))
            {
                string label = EffectChipLayout.Label(type);
                Assert.AreEqual(4, label.Length, type.ToString());
                Assert.IsTrue(seen.Add(label), "duplicate label " + label);
            }
        }

        [Test]
        public void Text_RoundsUpToTenths_SoAChipNeverReadsZeroWhileOn()
        {
            Assert.AreEqual("STUN 1.2s", EffectChipLayout.Text(EffectType.Stunned, 1.2f));
            Assert.AreEqual("STUN 1.3s", EffectChipLayout.Text(EffectType.Stunned, 1.21f));
            Assert.AreEqual("STUN 0.1s", EffectChipLayout.Text(EffectType.Stunned, 0.01f));
            Assert.AreEqual("STUN 0.0s", EffectChipLayout.Text(EffectType.Stunned, 0f));
        }

        [Test]
        public void Text_UsesInvariantCulture_AndDropsTheTimeWhenUntimed()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("CORR 1.5s", EffectChipLayout.Text(EffectType.Corroded, 1.5f));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }

            Assert.AreEqual("FORT", EffectChipLayout.Text(EffectType.Fortified, float.PositiveInfinity));
        }

        [Test]
        public void RowX_CentresTheRow()
        {
            Assert.AreEqual(0f, EffectChipLayout.RowX(0, 1, 40f, 4f), 1e-4f);
            Assert.AreEqual(-22f, EffectChipLayout.RowX(0, 2, 40f, 4f), 1e-4f);
            Assert.AreEqual(22f, EffectChipLayout.RowX(1, 2, 40f, 4f), 1e-4f);
            Assert.AreEqual(-44f, EffectChipLayout.RowX(0, 3, 40f, 4f), 1e-4f);
            Assert.AreEqual(0f, EffectChipLayout.RowX(1, 3, 40f, 4f), 1e-4f);
        }
    }
}
```

`Tests/EditMode/EffectChipRowTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.HUD;

namespace MotorCombat.Tests
{
    public class EffectChipRowTests
    {
        sealed class FakeReceiver : IEffectReceiver
        {
            public readonly List<ActiveEffect> active = new List<ActiveEffect>();

            public EffectOutcome Apply(in EffectRequest request) => EffectOutcome.Invalid;
            public bool Has(EffectType type) => active.Exists(e => e.type == type);
            public float Remaining(EffectType type) => 0f;

            public void GetActive(List<ActiveEffect> into)
            {
                into.Clear();
                into.AddRange(active);
            }

            public event Action<EffectReport> Applied { add { } remove { } }
            public event Action<EffectType> Ended { add { } remove { } }
        }

        static int ActiveChildren(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                if (child.gameObject.activeSelf) count++;
            }
            return count;
        }

        [Test]
        public void Show_ShowsOneChipPerActiveEffect_AndHidesTheRest()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            try
            {
                var row = new EffectChipRow((RectTransform)parent.transform, "Row", 40f, 14f, 3f, 10);
                var receiver = new FakeReceiver();
                receiver.active.Add(new ActiveEffect { type = EffectType.Stunned, remaining = 1.2f, duration = 3f });
                receiver.active.Add(new ActiveEffect { type = EffectType.Corroded, remaining = 2f, duration = 3f });
                receiver.active.Add(new ActiveEffect { type = EffectType.Armored, remaining = 0.5f, duration = 3f });

                row.Show(receiver);
                Assert.AreEqual(3, row.VisibleCount);
                Assert.AreEqual(3, ActiveChildren(row.Root));
                Assert.AreEqual("STUN 1.2s", row.TextAt(0));

                receiver.active.RemoveRange(1, 2);
                row.Show(receiver);
                Assert.AreEqual(1, row.VisibleCount);
                Assert.AreEqual(1, ActiveChildren(row.Root), "pooled chips are hidden, not destroyed");

                row.Show(null);
                Assert.AreEqual(0, row.VisibleCount);
                Assert.AreEqual(0, ActiveChildren(row.Root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: no `test-results.xml`, with compile errors for `EffectChipLayout` and `EffectChipRow`.

- [ ] **Step 3: Implement**

`Scripts/HUD/EffectChipLayout.cs`:

```csharp
using System.Globalization;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>What an effect chip says, its colour, and where it sits in a row. Placeholder art: text chips until icons exist.</summary>
    public static class EffectChipLayout
    {
        public static string Label(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return "STUN";
                case EffectType.Suppressed: return "SUPP";
                case EffectType.Overheated: return "HEAT";
                case EffectType.Corroded: return "CORR";
                case EffectType.Reeling: return "REEL";
                case EffectType.Spiked: return "SPIK";
                case EffectType.Fortified: return "FORT";
                case EffectType.Armored: return "ARMR";
                case EffectType.Overhauled: return "OVHL";
                case EffectType.Exhausted: return "EXHS";
                default: return "????";
            }
        }

        public static Color Colour(EffectType type)
        {
            switch (type)
            {
                case EffectType.Stunned: return new Color(1.00f, 0.85f, 0.20f, 0.9f);
                case EffectType.Suppressed: return new Color(0.60f, 0.60f, 0.70f, 0.9f);
                case EffectType.Overheated: return new Color(1.00f, 0.45f, 0.15f, 0.9f);
                case EffectType.Corroded: return new Color(0.55f, 0.80f, 0.25f, 0.9f);
                case EffectType.Reeling: return new Color(0.75f, 0.45f, 0.95f, 0.9f);
                case EffectType.Spiked: return new Color(0.90f, 0.30f, 0.35f, 0.9f);
                case EffectType.Fortified: return new Color(0.30f, 0.60f, 1.00f, 0.9f);
                case EffectType.Armored: return new Color(0.85f, 0.85f, 0.90f, 0.9f);
                case EffectType.Overhauled: return new Color(0.35f, 0.90f, 0.75f, 0.9f);
                case EffectType.Exhausted: return new Color(0.60f, 0.45f, 0.30f, 0.9f);
                default: return new Color(1f, 1f, 1f, 0.9f);
            }
        }

        /// <summary>
        /// "STUN 1.2s". Rounded UP to tenths so a chip never reads 0.0s while
        /// the effect is still on; invariant culture so it never reads "1,2s".
        /// An untimed effect shows only its label.
        /// </summary>
        public static string Text(EffectType type, float remaining)
        {
            if (float.IsInfinity(remaining)) return Label(type);

            // The small offset stops float noise (1.2 × 10 = 12.0000005) rounding up a whole tenth.
            float tenths = Mathf.Ceil(remaining * 10f - 1e-3f) / 10f;
            if (!(tenths > 0f)) tenths = 0f;   // also turns −0 and NaN into 0

            return Label(type) + " " + tenths.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        /// <summary>X offset of chip <paramref name="index"/> in a row of <paramref name="count"/> centred on 0.</summary>
        public static float RowX(int index, int count, float chipWidth, float gap)
        {
            return (index - (count - 1) * 0.5f) * (chipWidth + gap);
        }
    }
}
```

`Scripts/HUD/EffectChipRow.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// A centred row of effect chips. Chips are pooled: created the first time
    /// they are needed, then only shown, hidden and relabelled, so a flickering
    /// effect never allocates UI. The owner positions <see cref="Root"/>.
    /// </summary>
    public sealed class EffectChipRow
    {
        static readonly Color LabelColour = new Color(0.05f, 0.05f, 0.05f);

        class Chip
        {
            public RectTransform root;
            public Image background;
            public Text label;
            public string text;
        }

        readonly List<Chip> _chips = new List<Chip>();
        readonly List<ActiveEffect> _active = new List<ActiveEffect>();
        readonly float _chipWidth;
        readonly float _chipHeight;
        readonly float _gap;
        readonly int _fontSize;

        public EffectChipRow(RectTransform parent, string name, float chipWidth, float chipHeight, float gap, int fontSize)
        {
            _chipWidth = chipWidth;
            _chipHeight = chipHeight;
            _gap = gap;
            _fontSize = fontSize;

            Root = HudElements.Rect(name, parent);
            Root.sizeDelta = new Vector2(0f, chipHeight);
        }

        public RectTransform Root { get; }

        public int VisibleCount { get; private set; }

        public string TextAt(int index)
        {
            return _chips[index].text;
        }

        /// <summary>Shows the receiver's active effects in EffectType order; null shows nothing.</summary>
        public void Show(IEffectReceiver receiver)
        {
            if (receiver != null) receiver.GetActive(_active);
            else _active.Clear();

            int count = _active.Count;
            while (_chips.Count < count) _chips.Add(CreateChip());

            for (int i = 0; i < _chips.Count; i++)
            {
                Chip chip = _chips[i];
                bool show = i < count;
                if (chip.root.gameObject.activeSelf != show) chip.root.gameObject.SetActive(show);
                if (!show) continue;

                ActiveEffect effect = _active[i];
                chip.root.anchoredPosition = new Vector2(EffectChipLayout.RowX(i, count, _chipWidth, _gap), 0f);
                chip.background.color = EffectChipLayout.Colour(effect.type);

                string text = EffectChipLayout.Text(effect.type, effect.remaining);
                if (text != chip.text)
                {
                    chip.text = text;
                    chip.label.text = text;
                }
            }

            VisibleCount = count;
        }

        Chip CreateChip()
        {
            RectTransform root = HudElements.Rect("Chip", Root);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(_chipWidth, _chipHeight);

            Image background = HudElements.Image("Background", root, Color.white);
            HudElements.Fill(background.rectTransform);

            Text label = HudElements.Text("Label", root, _fontSize, TextAnchor.MiddleCenter);
            HudElements.Fill(label.rectTransform);
            label.color = LabelColour;

            return new Chip { root = root, background = background, label = label };
        }
    }
}
```

`Scripts/HUD/SelfEffectsWidget.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// The viewer's own active effects, as chips centred above the self health
    /// bar — in first person you never see your own car, so its state lives on
    /// the screen.
    /// </summary>
    public class SelfEffectsWidget : MonoBehaviour
    {
        public CarController viewer;

        [Tooltip("Reference pixels.")] public float chipWidth = 72f;
        [Tooltip("Reference pixels.")] public float chipHeight = 24f;
        [Tooltip("Reference pixels.")] public float gap = 6f;
        public int fontSize = 15;
        [Tooltip("Reference pixels from the bottom of the screen; clears the self health bar and its label.")] public float bottomMargin = 96f;

        EffectChipRow _row;
        IEffectReceiver _receiver;
        CarController _receiverOwner;

        public void Build(RectTransform canvas)
        {
            _row = new EffectChipRow(canvas, "SelfEffects", chipWidth, chipHeight, gap, fontSize);
            RectTransform root = _row.Root;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, bottomMargin);
        }

        void LateUpdate()
        {
            if (_row == null) return;

            if (viewer != _receiverOwner)
            {
                _receiverOwner = viewer;
                _receiver = viewer != null ? viewer.GetComponent<IEffectReceiver>() : null;
            }

            _row.Show(viewer != null ? _receiver : null);
        }
    }
}
```

`Scripts/HUD/EnemyHealthBars.cs`:
- In the class summary, after "Width follows the car's on-screen width, clamped.", add " Active effects show as chips under the bar."
- After the `screenMargin` field, add:

```csharp
        [Tooltip("Reference pixels.")] public float effectChipWidth = 48f;
        [Tooltip("Reference pixels.")] public float effectChipHeight = 14f;
        [Tooltip("Reference pixels.")] public float effectChipGap = 3f;
        public int effectFontSize = 10;
```

- In `class Bar`, after `public Text name;`, add:

```csharp
            public EffectChipRow effects;
            public IEffectReceiver receiver;
```

- In `LateUpdate`, after `if (bar.name.text != car.name) bar.name.text = car.name;`, add `bar.effects.Show(bar.receiver);`.
- In `GetOrCreate`, replace `var bar = new Bar { root = root, fill = fill, name = label };` with:

```csharp
            var effects = new EffectChipRow(root, "Effects", effectChipWidth, effectChipHeight, effectChipGap, effectFontSize);
            RectTransform effectsRect = effects.Root;
            effectsRect.anchorMin = effectsRect.anchorMax = new Vector2(0.5f, 0f);
            effectsRect.pivot = new Vector2(0.5f, 1f);
            effectsRect.anchoredPosition = new Vector2(0f, -3f);

            var bar = new Bar
            {
                root = root,
                fill = fill,
                name = label,
                effects = effects,
                receiver = car.GetComponent<IEffectReceiver>()
            };
```

`Scripts/Bootstrap/GameBootstrap.cs`: at the end of `Start()`, after `enemyBars.Build(hud.Rect);`, add:

```csharp

            var selfEffects = hud.gameObject.AddComponent<SelfEffectsWidget>();
            selfEffects.viewer = player;
            selfEffects.Build(hud.Rect);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="272"`, `failed="0"`.

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project
git status --porcelain
git commit -m "Show active effects as chips on the HUD and under enemy health bars

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Regenerate assets and document effects and respawn

**Files:**
- Create: `docs/effects.md`
- Modify:
  - `Assets/_Project/Configs/RamConfig.asset`, `Assets/_Project/Configs/CarDefinition.asset`
  - `Assets/_Project/Scenes/Arena.unity` (generated)
  - `Assets/_Project/Configs/EffectsConfig.asset`, `Assets/_Project/Configs/RespawnConfig.asset` (generated, new)
  - `docs/combat.md`, `docs/ramming.md`, `docs/tuning.md`, `docs/hud.md`, `docs/workflow.md`, `docs/architecture.md`, `CLAUDE.md`

**Interfaces:**
- Consumes: everything from Tasks 1–7.
- Produces: generated assets, a regenerated scene, and docs that match the code.

- [ ] **Step 1: Regenerate the assets and the scene** (Editor closed)

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
unity run . -- -executeMethod MotorCombat.EditorTools.ArenaSceneBuilder.BuildScene
```

Then:
- Delete the orphaned `spinDecayRate: 2` line from `Assets/_Project/Configs/RamConfig.asset`. The field no longer exists, and Unity only drops it on a re-save.
- Check `git diff Assets/_Project/Configs Assets/_Project/Scenes`:
  - `EffectsConfig.asset` and `RespawnConfig.asset` exist with the placeholders from Global Constraints
  - `CarDefinition.asset` gains an `effectsConfig` reference and **no other value changes** — the user's tuned numbers must be untouched
  - `RamConfig.asset` loses only `spinDecayRate`
  - `Arena.unity` gains `respawnConfig` on `GameBootstrap`
  - If any hand-tuned value changed, restore it from `git show HEAD:<path>` and report it.

- [ ] **Step 2: Write `docs/effects.md`**

A page in the style of `docs/combat.md`: short sections, tables, rules stated with the reason. Take every fact from the spec (`docs/specs/2026-09-14-effects-respawn-design.md`) §2–§5 and §7, and from the code. It must cover, in this order:
1. **What an effect is:** a timed state, sized and timed by the source's config, with global rules in `EffectsConfig`.
2. **Applying one:** the `EffectRequest` field table; the apply order (targetable → hostility → valid → Overhauled → stacks/AlreadyActive → Applied); the `EffectOutcome` table; the attack snapshot.
3. **The ten effects:** the §4 table (abilities, stat, behaviour, what magnitude means).
4. **Stacking:** the per-effect `…Stacks` setting; restart replaces the size; `OnRefresh` per effect; Overheated keeps its rhythm.
5. **Timing:**
   - `EffectSet` is the only timer, and blocks, modifiers and gates are untimed
   - step order
   - the `1e-4` expiry tolerance
   - an effect never acts in the step it ends, with the 3 s burn example
6. **Death and respawn:** every effect ends; a wreck takes no effects.
7. **Source keys:** one behaviour instance per effect per car.
8. **`EffectsConfig`:** the field table with placeholders.
9. **Debug menus:** the ten `CarEffects` context menu entries and `logEffects`.
10. **For later sub-projects:**
    - weapons apply through `IEffectReceiver` only
    - a stun cancels a maneuver via `Has(Fire)`
    - adding an 11th effect means a new enum value, an `EffectInfo` update, a behaviour and a `Stacks` field
11. **Tests:** a fixture/count table for `EffectInfoTests` 4, `EffectRulesTests` 12, `EffectSetTests` 6, `EffectsConfigTests` 2, `CarEffectsTests` 23.

- [ ] **Step 3: Update the existing docs**

- `docs/combat.md`:
  - Replace the "**Respawn is not built.**" paragraph and its bullet list with a `## Respawn` section: `CarRespawn.Respawn` steps (spec §1.1); the `IRespawnable` reset table (§1.2); the `RespawnRule` placeholder and `RespawnConfig` table (§1.3); no spawn protection.
  - In the damage path, add `attack` to the `DamageRequest` field list, and change the Formula sentence to say `attack` is the request's snapshot when set, otherwise the source's effective attack.
  - In the ability-switch table, change the "Ram reel (victim)" row's Duration to `reelSeconds (Reeling effect, see effects.md)` and its Refresh to `effect stacking (Reeling stacks by default)`.
  - In the opening paragraph, say effects now use these seams and link `effects.md`.
  - Tests table: `HealthStateTests` 15, `HealthTests` 9, and add `CarRespawnTests` 4 and `RespawnRulesTests` 5.
- `docs/ramming.md`:
  - Where the reel is described as a block with `ReelBlock` and a `spinDecayRate`, say the victim receives the **Reeling effect** for `reelSeconds` (source = attacker, tag `"ram"`). Its abilities are unchanged, spin decays at `EffectsConfig.reelingSpinDecayRate`, and a second ram restarts it because `reelingStacks` is on.
  - Note the one-step (0.02 s) later end.
  - Keep `LockBlock` as the only ramming block, not an effect.
- `docs/tuning.md`:
  - RamConfig table: delete the `spinDecayRate` row, and change `reelSeconds` Notes to "How long a flank or rear victim Reels (the Reeling effect)".
  - In the paragraph under the table, no change is needed except any mention of `spinDecayRate`.
  - Add an `## EffectsConfig` table and a `## RespawnConfig` table, using the placeholders and meanings from spec §3.3 and §1.3.
  - CarDefinition table: add `effectsConfig` to the references row.
- `docs/hud.md`: add an "Effect chips" section covering `SelfEffectsWidget` placement, enemy chips under each bar, labels/colours/rounding, pooling, and the chip placeholders.
- `docs/architecture.md`: the assembly count 14 → 15; add `Effects` (references Core) to the assembly diagram and list. `Cars`, `Bootstrap` and `EditorTools` now reference `Effects`.
- `docs/workflow.md`:
  - Test count 206 → 272 in the sentence and table: `RamRulesTests` 36, `CarFactoryTests` 20, `HealthStateTests` 15, `HealthTests` 9, plus rows `CarRespawnTests` 4, `RespawnRulesTests` 5, `EffectInfoTests` 4, `EffectRulesTests` 12, `EffectSetTests` 6, `EffectsConfigTests` 2, `CarEffectsTests` 23, `EffectChipLayoutTests` 4, `EffectChipRowTests` 1.
  - Checklist intro: "rows 21–37 are not yet walked".
  - Change row 19's Expected to "Second ram applies; its reel restarts (the REEL chip jumps back up)".
  - Add these rows:

| # | Check | Expected |
|---|---|---|
| 27 | Dummy's Health → Debug: destroy, then wait | After the fade it disappears; about 3 s after destruction it reappears at its start: full bar, upright, normal colour and shadow |
| 28 | Park on the dummy's start, destroy it, wait 5 s, then drive off | It does not reappear while you are on the spot; it appears the moment you leave, and nothing is launched |
| 29 | PlayerCar's Health → Debug: destroy | You reappear at your start ~3 s later, can drive at once, HP reads 1000 / 1000 |
| 30 | While driving, PlayerCar's CarEffects → Debug: apply Stunned | You stop dead; W/A/D do nothing for 3 s; mouse aim still works; a STUN chip counts down above your HP bar |
| 31 | Dummy → Debug: apply Stunned, then flank-ram it | It is still shoved and reels (REEL chip under its bar) |
| 32 | PlayerCar → Debug: apply Spiked, then hold W | Top speed is visibly lower until the SPIK chip ends |
| 33 | Dummy → Debug: apply Overheated | Its bar drops at once and twice more, a second apart (3 ticks); HEAT chip under its bar |
| 34 | Dummy → Debug: apply Armored, then Overheated | No HP lost while ARMR shows |
| 35 | Dummy → Corroded, Fortified, Exhausted, then Overhauled | Chips appear in a fixed order; Overhauled clears them all at once |
| 36 | PlayerCar → Debug: apply Reeling while turning | The car slides and spins down; no steering until REEL ends |
| 37 | Re-walk ram rows 13–20 | Unchanged — the reel is now an effect with the same abilities |

- `CLAUDE.md`:
  - First paragraph: "Ramming, health, destruction, respawn and effects work; no weapons, no menus, no netcode."
  - Doc table: add a row `[docs/effects.md](docs/effects.md)` | "Anything about effects (Stunned, Corroded, …), stacking or the effect chips".
  - "Not built yet": remove "effects, respawn" so it starts "Weapons. Menus, …".

- [ ] **Step 4: Run the tests to verify nothing moved**

Run: `rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml`
Expected: `total="272"`, `failed="0"`.

Check no stale mentions remain (the output should be empty):

```bash
grep -rn "spinDecayRate\|ReelBlock\|Respawn is not built" docs/*.md CLAUDE.md Assets/_Project/Scripts Assets/_Project/Configs
```

- [ ] **Step 5: Commit**

```bash
git checkout -- ProjectSettings/DynamicsManager.asset
git add Assets/_Project docs CLAUDE.md
git status --porcelain
git commit -m "Generate effects and respawn assets; document effects and respawn

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
