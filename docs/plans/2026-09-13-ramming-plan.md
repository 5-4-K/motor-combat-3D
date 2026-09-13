# Ramming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Car-vs-car rams that classify head-on / flank / rear from box regions and heading angle, shove the victim with a stat-based velocity change at the contact point, stop and lock the attacker, and make the victim reel.

**Architecture:** Pure static rules (`RamRules`) in the Ramming assembly, a thin `RammingModule` adapter that reads pre-contact velocities and writes Rigidbodies, and a `CarStatus` timer object in Core that `DrivingModule` reads — so Driving and Ramming never reference each other.

**Tech Stack:** Unity 6 (`6000.6.0f1`), C#, PhysX Rigidbody, NUnit EditMode tests via the `unity` CLI.

**Spec:** `docs/specs/2026-09-13-ramming-design.md`

## Global Constraints

- Every script belongs to an asmdef folder under `Assets/_Project/Scripts/`. Gameplay assemblies reference `MotorCombat.Core` only; only `Cars`, `Bootstrap`, `EditorTools` compose across modules. **Driving must not reference Ramming.**
- Decisions live in pure statics; MonoBehaviours stay thin.
- Every decay is a rate in 1/s applied as `value * Mathf.Exp(-rate * dt)`. Never `value *= (1 - rate)`.
- `DrivePhysics` stays the only source of driving resistance; do not touch Rigidbody damping or the frictionless material.
- No ram ever writes vertical velocity; keep each body's current `linearVelocity.y`.
- Mass is ignored by rams. Shove = `flatForward × (attack / max(0.01, defense)) × forwardSpeed × typeScale`.
- Tuning numbers are placeholders owned by the user: headOnScale 0.2, flankScale 1.5, rearScale 1.2, attackerLockSeconds 0.5, reelSeconds 1.0, minRamSpeed 3, spinScale 1, spinDecayRate 2, headOnAngleDegrees 45, cornerBandMetres 0.3. `CarDefinition.attack = 1`, `defense = 1`.
- Match the surrounding code style: XML `<summary>` comments that explain *why*, `[Tooltip]` on config fields, `Debug.LogError("[MotorCombat] ...", this)` for misconfiguration checked in `Start`.
- **The Unity Editor must be closed** to run the CLI. Test command, always in this form (a failed run leaves the old file in place, so it is deleted first):
  ```bash
  rm -f test-results.xml && unity test . --mode EditMode; grep -o '<test-run[^>]*>' test-results.xml
  ```
  Read `total`, `passed` and `failed` from the printed `<test-run>` tag. If `test-results.xml` is missing, the run failed — read the CLI output (compile errors appear there).
- Unity generates `.meta` files on import (including for new folders). After a test run, stage `Assets/_Project` wholesale and check `git status --porcelain` so no `.meta` is left out. Never commit `test-results.xml` (it is ignored).
- Work on `main`. Commit per task. **Do not push.** End commit messages with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

---

## File map

| File | Task | Responsibility |
|---|---|---|
| `Scripts/Core/CarStatus.cs` (new) | 1 | Lock / reel timers, `CanDrive`, `CanAttack` |
| `Scripts/Core/CarController.cs` | 1 | Owns `Status`, advances it, snapshots pre-step velocity |
| `Tests/EditMode/CarStatusTests.cs` (new) | 1 | Timer behaviour |
| `Scripts/Ramming/RamConfig.cs` (new) | 2 | Global ram tuning asset |
| `Scripts/Ramming/RamRules.cs` (new) | 2 | Regions, classification, resolve, shove, spin, decay |
| `Tests/EditMode/RamRulesTests.cs` (new) | 2 | All rules |
| `Tests/EditMode/MotorCombat.Tests.EditMode.asmdef` | 2 | + `MotorCombat.Ramming` |
| `Scripts/Driving/DrivingModule.cs` | 3 | Input gate; reel skips yaw and grip |
| `Scripts/Ramming/RamReport.cs` (new) | 3 | Event payload |
| `Scripts/Ramming/RammingModule.cs` | 3 | Collision → rules → Rigidbodies + Status; spin decay; gizmos |
| `Scripts/Cars/CarDefinition.cs`, `CarFactory.cs` | 3 | attack / defense / ramConfig wiring |
| `Scripts/Bootstrap/GameBootstrap.cs` | 3 | Validate ramConfig |
| `Editor/ConfigAssetBootstrap.cs` | 3 | Create RamConfig asset |
| `Tests/EditMode/CarFactoryTests.cs` | 3 | Wiring test |
| `Configs/RamConfig.asset`, `Configs/CarDefinition.asset` | 4 | Generated assets |
| `docs/ramming.md` (new), `CLAUDE.md`, `docs/*.md` | 4 | Documentation |

(All `Scripts/`, `Tests/`, `Editor/`, `Configs/` paths are under `Assets/_Project/`.)

---

### Task 1: CarStatus and CarController state

**Files:**
- Create: `Assets/_Project/Scripts/Core/CarStatus.cs`
- Modify: `Assets/_Project/Scripts/Core/CarController.cs`
- Test: `Assets/_Project/Tests/EditMode/CarStatusTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `MotorCombat.Core.CarStatus` with `float LockRemaining { get; }`, `float ReelRemaining { get; }`, `bool IsLocked`, `bool IsReeling`, `bool CanDrive`, `bool CanAttack`, `void Lock(float seconds)`, `void Reel(float seconds)`, `void Advance(float dt)`.
  - `CarController.Status` (`CarStatus`, never null), `CarController.PreStepVelocity` (`Vector3`), `CarController.PreStepAngularVelocity` (`Vector3`).

- [ ] **Step 1: Write the failing test**

`Assets/_Project/Tests/EditMode/CarStatusTests.cs`:
```csharp
using NUnit.Framework;
using MotorCombat.Core;

namespace MotorCombat.Tests
{
    public class CarStatusTests
    {
        [Test]
        public void NewStatus_CanDriveAndAttack()
        {
            var status = new CarStatus();
            Assert.IsTrue(status.CanDrive);
            Assert.IsTrue(status.CanAttack);
            Assert.IsFalse(status.IsLocked);
            Assert.IsFalse(status.IsReeling);
        }

        [Test]
        public void Lock_BlocksDrivingAndAttackingUntilItExpires()
        {
            var status = new CarStatus();
            status.Lock(0.5f);

            status.Advance(0.3f);
            Assert.IsTrue(status.IsLocked);
            Assert.IsFalse(status.CanDrive);
            Assert.IsFalse(status.CanAttack);

            status.Advance(0.3f);
            Assert.IsFalse(status.IsLocked);
            Assert.IsTrue(status.CanDrive);
        }

        [Test]
        public void Reel_BlocksDrivingAndAttackingUntilItExpires()
        {
            var status = new CarStatus();
            status.Reel(1f);

            status.Advance(0.9f);
            Assert.IsTrue(status.IsReeling);
            Assert.IsFalse(status.CanDrive);
            Assert.IsFalse(status.CanAttack);

            status.Advance(0.2f);
            Assert.IsFalse(status.IsReeling);
            Assert.IsTrue(status.CanAttack);
        }

        [Test]
        public void Reel_RestartsWhenAppliedAgain()
        {
            var status = new CarStatus();
            status.Reel(1f);
            status.Advance(0.8f);
            status.Reel(1f);
            status.Advance(0.5f);

            Assert.IsTrue(status.IsReeling, "a second ram restarts the reel from full");
            Assert.AreEqual(0.5f, status.ReelRemaining, 1e-5f);
        }

        [Test]
        public void Lock_NeverShortensALongerLock()
        {
            var status = new CarStatus();
            status.Lock(1f);
            status.Lock(0.2f);
            status.Advance(0.5f);

            Assert.IsTrue(status.IsLocked);
            Assert.AreEqual(0.5f, status.LockRemaining, 1e-5f);
        }

        [Test]
        public void Advance_ClampsTimersAtZero()
        {
            var status = new CarStatus();
            status.Lock(0.1f);
            status.Reel(0.1f);
            status.Advance(5f);

            Assert.AreEqual(0f, status.LockRemaining);
            Assert.AreEqual(0f, status.ReelRemaining);
        }

        [Test]
        public void LockAndReel_AreIndependent()
        {
            var status = new CarStatus();
            status.Lock(0.5f);
            status.Reel(1f);
            status.Advance(0.6f);

            Assert.IsFalse(status.IsLocked);
            Assert.IsTrue(status.IsReeling);
            Assert.IsFalse(status.CanDrive);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`; the CLI output shows a compile error `The type or namespace name 'CarStatus' could not be found`.

- [ ] **Step 3: Implement `CarStatus`**

`Assets/_Project/Scripts/Core/CarStatus.cs`:
```csharp
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
```

- [ ] **Step 4: Add state to `CarController`**

In `Assets/_Project/Scripts/Core/CarController.cs`, after the `AimDirection` property add:
```csharp
        /// <summary>Lock and reel timers. Written by ramming, obeyed by driving.</summary>
        public CarStatus Status { get; } = new CarStatus();

        /// <summary>
        /// Velocity going INTO the last physics step, recorded after every module
        /// ticked. Collision callbacks run after the step, by which time PhysX has
        /// already applied its own response — this is the pre-contact value.
        /// </summary>
        public Vector3 PreStepVelocity { get; private set; }

        /// <summary>Angular counterpart of <see cref="PreStepVelocity"/>.</summary>
        public Vector3 PreStepAngularVelocity { get; private set; }
```

Replace the whole `FixedUpdate` method with:
```csharp
        void FixedUpdate()
        {
            // Reuses the cached struct; only the level fields (throttle, steer)
            // are meaningful here.
            float dt = Time.fixedDeltaTime;

            Status.Advance(dt);

            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Tick(in _current, dt);
            }

            // Last, so it reflects every module's writes. Forces added with
            // AddForce this step are integrated inside the physics step and are
            // absent here — at most enginePower / mass × dt, about 0.5 m/s.
            PreStepVelocity = Body.linearVelocity;
            PreStepAngularVelocity = Body.angularVelocity;
        }
```

- [ ] **Step 5: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="79"` (72 existing + 7 new).

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Add CarStatus lock/reel timers and pre-step velocity snapshot" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: RamConfig and RamRules

**Files:**
- Create: `Assets/_Project/Scripts/Ramming/RamConfig.cs`
- Create: `Assets/_Project/Scripts/Ramming/RamRules.cs`
- Modify: `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef` (add reference)
- Test: `Assets/_Project/Tests/EditMode/RamRulesTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces (namespace `MotorCombat.Ramming`):
  - `class RamConfig : ScriptableObject` with public float fields `headOnScale, flankScale, rearScale, attackerLockSeconds, reelSeconds, minRamSpeed, spinScale, spinDecayRate, headOnAngleDegrees, cornerBandMetres`.
  - `enum RamRegion { Front, FrontCorner, Side, RearCorner, Rear }`, `enum RamType { None, HeadOn, Flank, Rear }`.
  - `struct RamParticipant { RamRegion region; Vector3 flatForward; float forwardSpeed; bool canAttack; }` (public fields).
  - `struct RamOutcome { RamType type; int attacker; static RamOutcome None; }` — attacker 0 = a, 1 = b, −1 = none/head-on.
  - `static class RamRules`: `const float MinDefense = 0.01f`; `Vector3 FlatForward(Vector3 forward)`; `RamRegion Region(Vector3 localPoint, float width, float length, float cornerBand)`; `bool IsAttackRegion(RamRegion)`; `float ForwardSpeed(Vector3 velocity, Vector3 flatForward)`; `RamType Classify(RamRegion victimRegion, Vector3 attackerForward, Vector3 victimForward, float headOnAngle)`; `bool Qualifies(in RamParticipant participant, float minRamSpeed)`; `RamOutcome Resolve(in RamParticipant a, in RamParticipant b, float minRamSpeed, float headOnAngle)`; `float ScaleFor(RamType type, float headOnScale, float flankScale, float rearScale)`; `Vector3 ShoveDelta(Vector3 attackerFlatForward, float attack, float defense, float speed, float scale)`; `float SpinDelta(Vector3 contactPoint, Vector3 victimCentre, Vector3 shoveDelta, float width, float length, float spinScale)`; `float DecaySpin(float yawRate, float rate, float dt)`.

- [ ] **Step 1: Add the test assembly reference**

In `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, change the `references` array to:
```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "MotorCombat.Core",
        "MotorCombat.Aiming",
        "MotorCombat.Driving",
        "MotorCombat.Arena",
        "MotorCombat.Cars",
        "MotorCombat.Cameras",
        "MotorCombat.Ramming"
    ],
```

- [ ] **Step 2: Write the failing test**

`Assets/_Project/Tests/EditMode/RamRulesTests.cs` (36 tests):
```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Ramming;

namespace MotorCombat.Tests
{
    /// <summary>
    /// Every ram rule, without a scene. Footprint used throughout: 2 m wide,
    /// 4 m long, 0.3 m corner band.
    /// </summary>
    public class RamRulesTests
    {
        const float W = 2f;
        const float L = 4f;
        const float Band = 0.3f;
        const float MinSpeed = 3f;
        const float HeadOn = 45f;

        static RamRegion RegionAt(float x, float z) => RamRules.Region(new Vector3(x, 0f, z), W, L, Band);

        static Vector3 Yawed(Vector3 direction, float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * direction;

        static RamParticipant P(RamRegion region, Vector3 forward, float speed, bool canAttack = true)
        {
            return new RamParticipant { region = region, flatForward = forward, forwardSpeed = speed, canAttack = canAttack };
        }

        // --- Regions ------------------------------------------------------------

        [Test] public void Region_FrontFaceCentre_IsFront() => Assert.AreEqual(RamRegion.Front, RegionAt(0f, 2f));
        [Test] public void Region_RearFaceCentre_IsRear() => Assert.AreEqual(RamRegion.Rear, RegionAt(0f, -2f));
        [Test] public void Region_SideCentre_IsSide() => Assert.AreEqual(RamRegion.Side, RegionAt(1f, 0f));
        [Test] public void Region_FrontLeftCorner_IsFrontCorner() => Assert.AreEqual(RamRegion.FrontCorner, RegionAt(-0.9f, 1.9f));
        [Test] public void Region_RearRightCorner_IsRearCorner() => Assert.AreEqual(RamRegion.RearCorner, RegionAt(0.9f, -1.9f));
        [Test] public void Region_InsideTheBandOnTheFrontFace_IsFrontCorner() => Assert.AreEqual(RamRegion.FrontCorner, RegionAt(0.75f, 2f));
        [Test] public void Region_JustOutsideTheBandOnTheFrontFace_IsFront() => Assert.AreEqual(RamRegion.Front, RegionAt(0.65f, 2f));

        /// <summary>
        /// The T-bone-near-the-nose case: a side panel 0.5 m behind the bumper is
        /// the side, not the front. A volume-based front zone would get this wrong.
        /// </summary>
        [Test] public void Region_SidePanelNearTheNose_IsSide() => Assert.AreEqual(RamRegion.Side, RegionAt(1f, 1.5f));

        [Test] public void Region_PenetratingContactPastTheFrontFace_IsStillFront() => Assert.AreEqual(RamRegion.Front, RegionAt(0f, 2.05f));

        [Test]
        public void IsAttackRegion_OnlyFrontAndFrontCorner()
        {
            Assert.IsTrue(RamRules.IsAttackRegion(RamRegion.Front));
            Assert.IsTrue(RamRules.IsAttackRegion(RamRegion.FrontCorner));
            Assert.IsFalse(RamRules.IsAttackRegion(RamRegion.Side));
            Assert.IsFalse(RamRules.IsAttackRegion(RamRegion.RearCorner));
            Assert.IsFalse(RamRules.IsAttackRegion(RamRegion.Rear));
        }

        // --- Speed and heading -------------------------------------------------

        [Test]
        public void ForwardSpeed_IsTheComponentAlongTheNoseAndNeverNegative()
        {
            Assert.AreEqual(10f, RamRules.ForwardSpeed(new Vector3(0f, 0f, 10f), Vector3.forward), 1e-5f);
            Assert.AreEqual(4f, RamRules.ForwardSpeed(new Vector3(3f, 0f, 4f), Vector3.forward), 1e-5f);
            Assert.AreEqual(0f, RamRules.ForwardSpeed(new Vector3(0f, 0f, -5f), Vector3.forward), 1e-5f);
        }

        [Test]
        public void FlatForward_DropsPitchAndNormalises()
        {
            Vector3 flat = RamRules.FlatForward(new Vector3(0f, 0.5f, 2f));
            Assert.AreEqual(0f, flat.y);
            Assert.AreEqual(1f, flat.magnitude, 1e-5f);
        }

        // --- Classification ----------------------------------------------------

        [Test]
        public void Classify_SideIsAlwaysFlank()
        {
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.Side, Vector3.back, Vector3.forward, HeadOn));
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.Side, Vector3.forward, Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_FrontFaceNoseToNose_IsHeadOn()
        {
            Assert.AreEqual(RamType.HeadOn, RamRules.Classify(RamRegion.Front, Vector3.back, Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_FrontAt44Degrees_IsHeadOn_At46_IsFlank()
        {
            Assert.AreEqual(RamType.HeadOn, RamRules.Classify(RamRegion.FrontCorner, Yawed(Vector3.back, 44f), Vector3.forward, HeadOn));
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.FrontCorner, Yawed(Vector3.back, 46f), Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_GlancingHitOnTheFrontFace_IsFlank()
        {
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.Front, Yawed(Vector3.back, 60f), Vector3.forward, HeadOn));
        }

        [Test]
        public void Classify_RearAt44Degrees_IsRear_At46_IsFlank()
        {
            Assert.AreEqual(RamType.Rear, RamRules.Classify(RamRegion.Rear, Yawed(Vector3.forward, -44f), Vector3.forward, HeadOn));
            Assert.AreEqual(RamType.Flank, RamRules.Classify(RamRegion.RearCorner, Yawed(Vector3.forward, -46f), Vector3.forward, HeadOn));
        }

        // --- Resolve -----------------------------------------------------------

        [Test]
        public void Resolve_NoFrontContact_IsNone()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Side, Vector3.forward, 10f), P(RamRegion.Side, Vector3.right, 10f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.None, outcome.type);
            Assert.AreEqual(-1, outcome.attacker);
        }

        [Test]
        public void Resolve_BelowMinimumSpeed_IsNone()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 2.9f), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.None, outcome.type);
        }

        [Test]
        public void Resolve_AtMinimumSpeed_Rams()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 3f), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Flank, outcome.type);
        }

        [Test]
        public void Resolve_CarThatCannotAttack_IsNone()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 10f, canAttack: false), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.None, outcome.type);
        }

        [Test]
        public void Resolve_FrontIntoSide_IsFlankByTheFirstCar()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 10f), P(RamRegion.Side, Vector3.right, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Flank, outcome.type);
            Assert.AreEqual(0, outcome.attacker);
        }

        [Test]
        public void Resolve_SecondCarIntoFirstCarsRear_IsRearBySecondCar()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Rear, Vector3.forward, 0f), P(RamRegion.Front, Vector3.forward, 10f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Rear, outcome.type);
            Assert.AreEqual(1, outcome.attacker);
        }

        [Test]
        public void Resolve_IntoAParkedCarsNose_IsHeadOn()
        {
            var outcome = RamRules.Resolve(P(RamRegion.Front, Vector3.forward, 10f), P(RamRegion.Front, Vector3.back, 0f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.HeadOn, outcome.type);
            Assert.AreEqual(-1, outcome.attacker);
        }

        [Test]
        public void Resolve_BothNosesAtRightAngles_FasterCarAttacks()
        {
            var outcome = RamRules.Resolve(P(RamRegion.FrontCorner, Vector3.forward, 8f), P(RamRegion.FrontCorner, Vector3.right, 12f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.Flank, outcome.type);
            Assert.AreEqual(1, outcome.attacker);
        }

        [Test]
        public void Resolve_BothNosesAtRightAngles_ExactTie_IsHeadOn()
        {
            var outcome = RamRules.Resolve(P(RamRegion.FrontCorner, Vector3.forward, 8f), P(RamRegion.FrontCorner, Vector3.right, 8f), MinSpeed, HeadOn);
            Assert.AreEqual(RamType.HeadOn, outcome.type);
        }

        // --- Shove ------------------------------------------------------------

        [Test]
        public void ScaleFor_PicksTheScaleForEachType()
        {
            Assert.AreEqual(0.2f, RamRules.ScaleFor(RamType.HeadOn, 0.2f, 1.5f, 1.2f));
            Assert.AreEqual(1.5f, RamRules.ScaleFor(RamType.Flank, 0.2f, 1.5f, 1.2f));
            Assert.AreEqual(1.2f, RamRules.ScaleFor(RamType.Rear, 0.2f, 1.5f, 1.2f));
            Assert.AreEqual(0f, RamRules.ScaleFor(RamType.None, 0.2f, 1.5f, 1.2f));
        }

        [Test]
        public void ShoveDelta_IsAttackOverDefenseTimesSpeedTimesScale_AlongHeading()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, attack: 2f, defense: 1f, speed: 10f, scale: 1.5f);
            Assert.AreEqual(0f, shove.x, 1e-4f);
            Assert.AreEqual(0f, shove.y, 1e-4f);
            Assert.AreEqual(30f, shove.z, 1e-4f);
        }

        [Test]
        public void ShoveDelta_HigherDefenseShrinksItProportionally()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.right, attack: 1f, defense: 2f, speed: 10f, scale: 1f);
            Assert.AreEqual(5f, shove.x, 1e-4f);
        }

        [Test]
        public void ShoveDelta_IsNeverVertical()
        {
            Vector3 shove = RamRules.ShoveDelta(new Vector3(0f, 0.3f, 1f), 1f, 1f, 10f, 1f);
            Assert.AreEqual(0f, shove.y);
        }

        [Test]
        public void ShoveDelta_ZeroDefenseIsClampedInsteadOfDividingByZero()
        {
            Vector3 shove = RamRules.ShoveDelta(Vector3.forward, 1f, 0f, 1f, 1f);
            Assert.AreEqual(1f / RamRules.MinDefense, shove.z, 1e-2f);
        }

        // --- Spin -------------------------------------------------------------

        [Test]
        public void SpinDelta_ThroughTheCentre_IsZero()
        {
            Assert.AreEqual(0f, RamRules.SpinDelta(Vector3.zero, Vector3.zero, new Vector3(5f, 0f, 0f), W, L, 1f), 1e-5f);
        }

        [Test]
        public void SpinDelta_RearRamStraightAlongTheLength_IsZero()
        {
            Assert.AreEqual(0f, RamRules.SpinDelta(new Vector3(0f, 0f, -2f), Vector3.zero, new Vector3(0f, 0f, 10f), W, L, 1f), 1e-5f);
        }

        /// <summary>
        /// Tail pushed toward +X: the nose swings toward −X, which is negative yaw
        /// in Unity. k² = (2² + 4²) / 12 = 5/3, so (−2 × 1) / (5/3) = −1.2 rad/s.
        /// </summary>
        [Test]
        public void SpinDelta_TailPushedRight_SpinsNoseLeft()
        {
            Assert.AreEqual(-1.2f, RamRules.SpinDelta(new Vector3(0f, 0f, -2f), Vector3.zero, new Vector3(1f, 0f, 0f), W, L, 1f), 1e-4f);
        }

        [Test]
        public void SpinDelta_ScalesLinearlyWithSpinScale_AndIgnoresHeight()
        {
            float spin = RamRules.SpinDelta(new Vector3(0f, 5f, -2f), Vector3.zero, new Vector3(1f, 0f, 0f), W, L, 2f);
            Assert.AreEqual(-2.4f, spin, 1e-4f);
        }

        [Test]
        public void DecaySpin_IsTimestepIndependent()
        {
            float oneStep = RamRules.DecaySpin(3f, 2f, 0.02f);
            float twoSteps = RamRules.DecaySpin(RamRules.DecaySpin(3f, 2f, 0.01f), 2f, 0.01f);
            Assert.AreEqual(oneStep, twoSteps, 1e-5f);
            Assert.Less(oneStep, 3f);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`; compile error `The type or namespace name 'RamRegion' could not be found`.

- [ ] **Step 4: Implement `RamConfig`**

`Assets/_Project/Scripts/Ramming/RamConfig.cs`:
```csharp
using UnityEngine;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// Global ram tuning. Per-car stats (attack, defense) live on CarDefinition.
    /// Every value here is a placeholder — tuning belongs to the designer.
    /// </summary>
    [CreateAssetMenu(menuName = "Motor Combat/Ram Config", fileName = "RamConfig")]
    public class RamConfig : ScriptableObject
    {
        [Header("Shove scale per ram type")]
        [Tooltip("Head-on shove multiplier. Keep small: head-ons stop both cars and should not reward either.")]
        public float headOnScale = 0.2f;

        [Tooltip("Flank (side) shove multiplier.")]
        public float flankScale = 1.5f;

        [Tooltip("Rear shove multiplier.")]
        public float rearScale = 1.2f;

        [Header("States (seconds)")]
        [Tooltip("How long the attacker (and both cars in a head-on) ignore throttle and steer.")]
        public float attackerLockSeconds = 0.5f;

        [Tooltip("How long a flank or rear victim reels: no throttle, steer or grip; spins freely.")]
        public float reelSeconds = 1f;

        [Header("Thresholds")]
        [Tooltip("Minimum attacker forward speed in m/s. Slower front contacts are plain physics bumps.")]
        public float minRamSpeed = 3f;

        [Tooltip("Headings within this many degrees of opposite (front hit) are head-on, and of parallel (rear hit) are rear. Otherwise flank.")]
        [Range(0f, 90f)]
        public float headOnAngleDegrees = 45f;

        [Tooltip("Width in metres of the corner band where a front or rear face meets a side.")]
        public float cornerBandMetres = 0.3f;

        [Header("Spin")]
        [Tooltip("Multiplies the yaw a real impulse at the contact point would give. 1 = physical.")]
        public float spinScale = 1f;

        [Tooltip("Rate in 1/s at which a reeling car's spin decays, as exp(-rate × dt).")]
        public float spinDecayRate = 2f;
    }
}
```

- [ ] **Step 5: Implement `RamRules`**

`Assets/_Project/Scripts/Ramming/RamRules.cs`:
```csharp
using UnityEngine;

namespace MotorCombat.Ramming
{
    /// <summary>Which part of a car's collider box a contact landed on.</summary>
    public enum RamRegion { Front, FrontCorner, Side, RearCorner, Rear }

    public enum RamType { None, HeadOn, Flank, Rear }

    /// <summary>One car's side of a contact, reduced to plain values.</summary>
    public struct RamParticipant
    {
        public RamRegion region;

        /// <summary>Horizontal unit heading.</summary>
        public Vector3 flatForward;

        /// <summary>Pre-contact speed along the heading, never negative.</summary>
        public float forwardSpeed;

        /// <summary>False while locked or reeling.</summary>
        public bool canAttack;
    }

    public struct RamOutcome
    {
        public RamType type;

        /// <summary>0 = first participant, 1 = second, -1 = nobody (None) or both (HeadOn).</summary>
        public int attacker;

        public static RamOutcome None => new RamOutcome { type = RamType.None, attacker = -1 };
    }

    /// <summary>
    /// Every ram decision as a pure function. No Rigidbody, no scene — the
    /// adapter in RammingModule gathers values, calls these, and writes results.
    /// </summary>
    public static class RamRules
    {
        /// <summary>Floor for defense so a misconfigured zero cannot divide by zero.</summary>
        public const float MinDefense = 0.01f;

        public static Vector3 FlatForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        /// <summary>
        /// Region by which FACE of the box the local contact point is nearest,
        /// with a corner band where a front or rear face meets a side.
        ///
        /// Faces, not volumes: a front zone deep enough to catch corner hits would
        /// also swallow the side panel behind the bumper and call a T-bone a head-on.
        /// </summary>
        public static RamRegion Region(Vector3 localPoint, float width, float length, float cornerBand)
        {
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            float toFront = halfLength - localPoint.z;
            float toRear = halfLength + localPoint.z;
            float toSide = halfWidth - Mathf.Abs(localPoint.x);

            if (toSide <= cornerBand && toFront <= cornerBand) return RamRegion.FrontCorner;
            if (toSide <= cornerBand && toRear <= cornerBand) return RamRegion.RearCorner;

            if (toFront <= toRear && toFront <= toSide) return RamRegion.Front;
            if (toRear <= toSide) return RamRegion.Rear;
            return RamRegion.Side;
        }

        public static bool IsAttackRegion(RamRegion region)
        {
            return region == RamRegion.Front || region == RamRegion.FrontCorner;
        }

        public static float ForwardSpeed(Vector3 velocity, Vector3 flatForward)
        {
            return Mathf.Max(0f, Vector3.Dot(velocity, flatForward));
        }

        /// <summary>
        /// Type of ram from the region the victim was struck in and the angle
        /// between headings. A side hit is always a flank; a front or rear hit is
        /// head-on or rear only when the cars line up within <paramref name="headOnAngle"/>.
        /// </summary>
        public static RamType Classify(RamRegion victimRegion, Vector3 attackerForward, Vector3 victimForward, float headOnAngle)
        {
            switch (victimRegion)
            {
                case RamRegion.Front:
                case RamRegion.FrontCorner:
                    return Vector3.Angle(attackerForward, -victimForward) <= headOnAngle ? RamType.HeadOn : RamType.Flank;

                case RamRegion.Rear:
                case RamRegion.RearCorner:
                    return Vector3.Angle(attackerForward, victimForward) <= headOnAngle ? RamType.Rear : RamType.Flank;

                default:
                    return RamType.Flank;
            }
        }

        public static bool Qualifies(in RamParticipant participant, float minRamSpeed)
        {
            return participant.canAttack
                   && IsAttackRegion(participant.region)
                   && participant.forwardSpeed >= minRamSpeed;
        }

        /// <summary>
        /// Resolves one contact between two cars. Each qualifying car is evaluated
        /// as attacker against the other. Any head-on evaluation makes it a head-on
        /// for both — including a parked car. Two non-head-on attackers: the faster
        /// one wins; an exact tie is treated as a head-on.
        /// </summary>
        public static RamOutcome Resolve(in RamParticipant a, in RamParticipant b, float minRamSpeed, float headOnAngle)
        {
            bool aQualifies = Qualifies(a, minRamSpeed);
            bool bQualifies = Qualifies(b, minRamSpeed);

            if (!aQualifies && !bQualifies) return RamOutcome.None;

            RamType aType = aQualifies ? Classify(b.region, a.flatForward, b.flatForward, headOnAngle) : RamType.None;
            RamType bType = bQualifies ? Classify(a.region, b.flatForward, a.flatForward, headOnAngle) : RamType.None;

            if (aType == RamType.HeadOn || bType == RamType.HeadOn)
            {
                return new RamOutcome { type = RamType.HeadOn, attacker = -1 };
            }

            if (aQualifies && bQualifies)
            {
                if (a.forwardSpeed > b.forwardSpeed) return new RamOutcome { type = aType, attacker = 0 };
                if (b.forwardSpeed > a.forwardSpeed) return new RamOutcome { type = bType, attacker = 1 };
                return new RamOutcome { type = RamType.HeadOn, attacker = -1 };
            }

            return aQualifies
                ? new RamOutcome { type = aType, attacker = 0 }
                : new RamOutcome { type = bType, attacker = 1 };
        }

        public static float ScaleFor(RamType type, float headOnScale, float flankScale, float rearScale)
        {
            switch (type)
            {
                case RamType.HeadOn: return headOnScale;
                case RamType.Flank: return flankScale;
                case RamType.Rear: return rearScale;
                default: return 0f;
            }
        }

        /// <summary>
        /// Velocity change for the victim. Mass is deliberately ignored: attack and
        /// defense are the only balance levers. With equal stats and scale 1 the
        /// victim leaves at the attacker's speed. Never vertical.
        /// </summary>
        /// <param name="attackerFlatForward">Horizontal unit heading of the attacker.</param>
        public static Vector3 ShoveDelta(Vector3 attackerFlatForward, float attack, float defense, float speed, float scale)
        {
            Vector3 direction = attackerFlatForward;
            direction.y = 0f;
            return direction * (attack / Mathf.Max(MinDefense, defense) * speed * scale);
        }

        /// <summary>
        /// Yaw-rate change (rad/s, about world up) from applying the shove at the
        /// contact point. With <paramref name="spinScale"/> 1 this equals what a
        /// real impulse m·Δv at that offset gives a solid box of this footprint:
        /// Δω = (r × Δv)·up / k², with k² = (width² + length²) / 12.
        /// </summary>
        public static float SpinDelta(Vector3 contactPoint, Vector3 victimCentre, Vector3 shoveDelta, float width, float length, float spinScale)
        {
            Vector3 offset = contactPoint - victimCentre;
            offset.y = 0f;

            float radiusOfGyrationSquared = (width * width + length * length) / 12f;
            if (radiusOfGyrationSquared <= 0f) return 0f;

            return spinScale * Vector3.Cross(offset, shoveDelta).y / radiusOfGyrationSquared;
        }

        /// <summary>Exponential spin decay. <paramref name="rate"/> is in 1/s.</summary>
        public static float DecaySpin(float yawRate, float rate, float dt)
        {
            return yawRate * Mathf.Exp(-rate * dt);
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="115"` (79 + 36 new).

If a test fails, fix the implementation to match the spec, not the test — the test values come from the spec. Report any test you believe contradicts the spec instead of editing it.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Add RamRules: region, classification, resolve, shove and spin" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Driving carve-outs, RammingModule, and wiring

**Files:**
- Modify: `Assets/_Project/Scripts/Driving/DrivingModule.cs` (the `Tick` method)
- Create: `Assets/_Project/Scripts/Ramming/RamReport.cs`
- Modify (full rewrite): `Assets/_Project/Scripts/Ramming/RammingModule.cs`
- Modify: `Assets/_Project/Scripts/Cars/CarDefinition.cs`
- Modify: `Assets/_Project/Scripts/Cars/CarFactory.cs` (the `AddComponent<RammingModule>()` line)
- Modify: `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs` (`Validate`)
- Modify: `Assets/_Project/Editor/ConfigAssetBootstrap.cs`
- Test: `Assets/_Project/Tests/EditMode/CarFactoryTests.cs`

**Interfaces:**
- Consumes: Task 1 `CarController.Status`, `PreStepVelocity`, `PreStepAngularVelocity`, `CarStatus.CanDrive/IsReeling/CanAttack/Lock/Reel`; Task 2 `RamConfig`, `RamRules.*`, `RamParticipant`, `RamOutcome`, `RamType`.
- Produces: `RammingModule` public fields `RamConfig config`, `float attack`, `float defense`, `bool logImpacts`; events `Collided` (`Action<CarCollisionEvent>`, unchanged) and `Rammed` (`Action<RamReport>`); `CarDefinition.attack`, `CarDefinition.defense`, `CarDefinition.ramConfig`.

- [ ] **Step 1: Write the failing test**

In `Assets/_Project/Tests/EditMode/CarFactoryTests.cs`:

Add `using MotorCombat.Ramming;` below `using MotorCombat.Aiming;`.

In `SetUp`, before the `_car = CarFactory.Spawn(` line, add:
```csharp
            _definition.ramConfig = ScriptableObject.CreateInstance<RamConfig>();
            _definition.attack = 2f;
            _definition.defense = 3f;
```

In `TearDown`, before `Object.DestroyImmediate(_definition);`, add:
```csharp
            Object.DestroyImmediate(_definition.ramConfig);
```

After the `Spawn_WiresModuleConfigsFromTheDefinition` test, add:
```csharp
        [Test]
        public void Spawn_WiresRamConfigAndStatsFromTheDefinition()
        {
            var ramming = _car.GetComponent<RammingModule>();
            Assert.AreSame(_definition.ramConfig, ramming.config);
            Assert.AreEqual(2f, ramming.attack);
            Assert.AreEqual(3f, ramming.defense);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run the Global Constraints test command.
Expected: no `test-results.xml`; compile error `'CarDefinition' does not contain a definition for 'ramConfig'`.

- [ ] **Step 3: Add stats to `CarDefinition`**

In `Assets/_Project/Scripts/Cars/CarDefinition.cs`, add `using MotorCombat.Ramming;` below `using MotorCombat.Aiming;`, and replace the `[Header("Behaviour")]` block at the end with:
```csharp
        [Header("Ramming")]
        [Tooltip("Multiplies the shove this car deals when it rams. Balanced against the victim's defense as a ratio.")]
        public float attack = 1f;

        [Tooltip("Divides the shove this car receives when rammed. Must be above zero.")]
        [Min(0.01f)]
        public float defense = 1f;

        [Header("Behaviour")]
        public DriveConfig driveConfig;
        public AimConfig aimConfig;
        public RamConfig ramConfig;
```

- [ ] **Step 4: Wire the factory**

In `Assets/_Project/Scripts/Cars/CarFactory.cs`, replace the line `car.AddComponent<RammingModule>();` with:
```csharp
            var ramming = car.AddComponent<RammingModule>();
            ramming.config = definition.ramConfig;
            ramming.attack = definition.attack;
            ramming.defense = definition.defense;
```

- [ ] **Step 5: Add `RamReport`**

`Assets/_Project/Scripts/Ramming/RamReport.cs`:
```csharp
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// One resolved ram. Raised for future consumers (damage, HUD, audio) and
    /// logged while tuning. For a head-on, attacker is the car whose callback
    /// resolved the pair and victim is the other; both were locked.
    /// </summary>
    public struct RamReport
    {
        public RamType type;
        public CarController attacker;
        public CarController victim;

        /// <summary>Speed change given to the victim, in m/s.</summary>
        public float shoveSpeed;

        /// <summary>Yaw-rate change given to the victim, in rad/s. Zero for head-ons.</summary>
        public float spin;
    }
}
```

- [ ] **Step 6: Rewrite `RammingModule`**

Replace the entire contents of `Assets/_Project/Scripts/Ramming/RammingModule.cs` with:
```csharp
using System;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// Thin adapter for rams. On a new car-vs-car contact it reduces both cars to
    /// plain values, asks RamRules what happened, and writes velocities and
    /// status. All decisions live in RamRules.
    ///
    /// PhysX has already applied its own collision response by the time
    /// OnCollisionEnter runs. A ram overwrites both cars' horizontal velocity
    /// from the pre-step snapshot, so that response never shows — the attacker
    /// feels no opposing impulse. Vertical velocity is never touched.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RammingModule : MonoBehaviour, ICarModule
    {
        public RamConfig config;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float attack = 1f;

        [Tooltip("Set from CarDefinition by CarFactory.")]
        public float defense = 1f;

        [Tooltip("Log impacts and rams to the console. Useful while tuning.")]
        public bool logImpacts;

        /// <summary>Every impact, including walls and plain bumps.</summary>
        public event Action<CarCollisionEvent> Collided;

        /// <summary>Resolved rams only.</summary>
        public event Action<RamReport> Rammed;

        CarController _car;
        BoxCollider _box;

        // Both cars receive OnCollisionEnter for the same contact in the same
        // step. Whichever runs first resolves the pair and marks both modules.
        RammingModule _resolvedPartner;
        float _resolvedAt = -1f;

        // Looked up lazily: collision callbacks are not guaranteed to wait for Start.
        CarController Car => _car != null ? _car : (_car = GetComponent<CarController>());
        BoxCollider Box => _box != null ? _box : (_box = GetComponent<BoxCollider>());

        void Start()
        {
            // Checked in Start, not Awake — CarFactory assigns config after AddComponent.
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] RammingModule on '{name}' has no RamConfig assigned — this car will neither ram nor be rammed.", this);
            }
        }

        public void Tick(in CarInput input, float dt)
        {
            if (config == null || !Car.Status.IsReeling) return;

            // DrivingModule does not write yaw while reeling; the spin winds down here.
            Rigidbody body = Car.Body;
            body.angularVelocity = Vector3.up * RamRules.DecaySpin(body.angularVelocity.y, config.spinDecayRate, dt);
        }

        public void FrameTick(in CarInput input, float dt) { }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;

            ContactPoint first = collision.GetContact(0);
            var other = collision.collider.GetComponentInParent<CarController>();

            Collided?.Invoke(new CarCollisionEvent
            {
                other = other,
                point = first.point,
                normal = first.normal,
                relativeSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, first.normal))
            });

            if (other == null) return;

            var partner = other.GetComponent<RammingModule>();
            if (partner == null || config == null || partner.config == null) return;

            float now = Time.fixedTime;
            if (_resolvedPartner == partner && _resolvedAt == now) return;
            MarkResolved(partner, now);
            partner.MarkResolved(this, now);

            Vector3 contact = MeanContact(collision);
            RamParticipant self = Participant(contact, config.cornerBandMetres);
            RamParticipant them = partner.Participant(contact, config.cornerBandMetres);

            RamOutcome outcome = RamRules.Resolve(self, them, config.minRamSpeed, config.headOnAngleDegrees);

            switch (outcome.type)
            {
                case RamType.None:
                    if (logImpacts) Debug.Log($"[Ram] {name} bumped {other.name} (no ram)");
                    break;
                case RamType.HeadOn:
                    ApplyHeadOn(partner, self, them);
                    break;
                default:
                    if (outcome.attacker == 0) ApplyRam(this, partner, self, contact, outcome.type);
                    else ApplyRam(partner, this, them, contact, outcome.type);
                    break;
            }
        }

        void MarkResolved(RammingModule partner, float time)
        {
            _resolvedPartner = partner;
            _resolvedAt = time;
        }

        static Vector3 MeanContact(Collision collision)
        {
            Vector3 sum = Vector3.zero;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                sum += collision.GetContact(i).point;
            }
            return sum / count;
        }

        RamParticipant Participant(Vector3 worldContact, float cornerBand)
        {
            Vector3 forward = RamRules.FlatForward(transform.forward);
            Vector3 local = transform.InverseTransformPoint(worldContact) - Box.center;

            return new RamParticipant
            {
                region = RamRules.Region(local, Box.size.x, Box.size.z, cornerBand),
                flatForward = forward,
                forwardSpeed = RamRules.ForwardSpeed(Car.PreStepVelocity, forward),
                canAttack = Car.Status.CanAttack
            };
        }

        void ApplyRam(RammingModule attacker, RammingModule victim, in RamParticipant attackerSide, Vector3 contact, RamType type)
        {
            float scale = RamRules.ScaleFor(type, config.headOnScale, config.flankScale, config.rearScale);
            Vector3 shove = RamRules.ShoveDelta(attackerSide.flatForward, attacker.attack, victim.defense, attackerSide.forwardSpeed, scale);
            float spin = RamRules.SpinDelta(contact, victim.transform.position, shove, victim.Box.size.x, victim.Box.size.z, config.spinScale);

            attacker.SetHorizontalVelocity(Vector3.zero, 0f);
            attacker.Car.Status.Lock(config.attackerLockSeconds);

            Vector3 preVelocity = victim.Car.PreStepVelocity;
            victim.SetHorizontalVelocity(preVelocity + shove, victim.Car.PreStepAngularVelocity.y + spin);
            victim.Car.Status.Reel(config.reelSeconds);

            Report(type, attacker, victim, shove.magnitude, spin);
        }

        void ApplyHeadOn(RammingModule partner, in RamParticipant self, in RamParticipant them)
        {
            // Each car is shoved by the OTHER car's attack, speed and heading, and
            // resists with its own defense. Through the centre: no spin.
            Vector3 toSelf = RamRules.ShoveDelta(them.flatForward, partner.attack, defense, them.forwardSpeed, config.headOnScale);
            Vector3 toPartner = RamRules.ShoveDelta(self.flatForward, attack, partner.defense, self.forwardSpeed, config.headOnScale);

            SetHorizontalVelocity(toSelf, 0f);
            partner.SetHorizontalVelocity(toPartner, 0f);

            Car.Status.Lock(config.attackerLockSeconds);
            partner.Car.Status.Lock(config.attackerLockSeconds);

            Report(RamType.HeadOn, this, partner, toPartner.magnitude, 0f);
        }

        /// <summary>Writes horizontal velocity and yaw rate; keeps vertical velocity.</summary>
        void SetHorizontalVelocity(Vector3 horizontal, float yawRate)
        {
            Rigidbody body = Car.Body;
            body.linearVelocity = new Vector3(horizontal.x, body.linearVelocity.y, horizontal.z);
            body.angularVelocity = Vector3.up * yawRate;
        }

        void Report(RamType type, RammingModule attacker, RammingModule victim, float shoveSpeed, float spin)
        {
            if (logImpacts)
            {
                Debug.Log($"[Ram] {type}: {attacker.name} → {victim.name}, shove {shoveSpeed:F1} m/s, spin {spin:F2} rad/s");
            }

            Rammed?.Invoke(new RamReport
            {
                type = type,
                attacker = attacker.Car,
                victim = victim.Car,
                shoveSpeed = shoveSpeed,
                spin = spin
            });
        }

        /// <summary>
        /// Illustrates the regions on the collider's top face: front red, rear
        /// blue, sides yellow, corner bands magenta. Region membership is decided
        /// by nearest face, so the strips show boundaries, not exact volumes.
        /// </summary>
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null || config == null) return;

            float band = config.cornerBandMetres;
            float halfWidth = box.size.x * 0.5f;
            float halfLength = box.size.z * 0.5f;
            float y = box.center.y + box.size.y * 0.5f + 0.02f;
            const float thin = 0.02f;

            Gizmos.matrix = transform.localToWorldMatrix;

            DrawZone(Color.red, new Vector3(0f, y, halfLength - band * 0.5f), new Vector3(box.size.x - 2f * band, thin, band));
            DrawZone(Color.blue, new Vector3(0f, y, -halfLength + band * 0.5f), new Vector3(box.size.x - 2f * band, thin, band));
            DrawZone(Color.yellow, new Vector3(halfWidth - band * 0.5f, y, 0f), new Vector3(band, thin, box.size.z - 2f * band));
            DrawZone(Color.yellow, new Vector3(-halfWidth + band * 0.5f, y, 0f), new Vector3(band, thin, box.size.z - 2f * band));

            Color corner = Color.magenta;
            DrawZone(corner, new Vector3(halfWidth - band * 0.5f, y, halfLength - band * 0.5f), new Vector3(band, thin, band));
            DrawZone(corner, new Vector3(-halfWidth + band * 0.5f, y, halfLength - band * 0.5f), new Vector3(band, thin, band));
            DrawZone(corner, new Vector3(halfWidth - band * 0.5f, y, -halfLength + band * 0.5f), new Vector3(band, thin, band));
            DrawZone(corner, new Vector3(-halfWidth + band * 0.5f, y, -halfLength + band * 0.5f), new Vector3(band, thin, band));
        }

        static void DrawZone(Color colour, Vector3 centre, Vector3 size)
        {
            Gizmos.color = colour;
            Gizmos.DrawCube(centre, size);
        }
    }
}
```

- [ ] **Step 7: Driving carve-outs**

In `Assets/_Project/Scripts/Driving/DrivingModule.cs`, replace the whole `Tick` method with:
```csharp
        public void Tick(in CarInput input, float dt)
        {
            if (config == null) return;

            Rigidbody body = _car.Body;
            Vector3 forward = FlatForward();
            CarStatus status = _car.Status;

            // Locked or reeling: throttle and steer are ignored. Aim is untouched —
            // it runs in AimModule on the frame tick.
            float throttle = status.CanDrive ? input.throttle : 0f;
            float steer = status.CanDrive ? input.steer : 0f;

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

            // A reeling car slides and spins freely: no yaw write (the ram's spin
            // survives and decays in RammingModule) and no grip (the shove survives).
            if (status.IsReeling) return;

            // 3. Yaw, set directly. Never gated on speed MAGNITUDE, so turning on
            //    the spot works; the SIGN of travel can invert the steering sense
            //    so reversing handles like a real car.
            float forwardSpeed = Vector3.Dot(body.linearVelocity, forward);
            float yawRate = DrivePhysics.YawRate(
                steer,
                config.turnRate,
                forwardSpeed,
                config.flipSteeringInReverse,
                config.reverseEpsilon);
            body.angularVelocity = Vector3.up * (yawRate * Mathf.Deg2Rad);

            // 4. Grip. Whatever sideways velocity survives is the drift.
            body.linearVelocity = DrivePhysics.ApplyGrip(
                body.linearVelocity, forward, config.lateralGripStrength, dt);
        }
```

- [ ] **Step 8: Validate and bootstrap the config**

In `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs` `Validate()`, after the `aimConfig` line add:
```csharp
            if (carDefinition.ramConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.ramConfig is not assigned."); return false; }
```

In `Assets/_Project/Editor/ConfigAssetBootstrap.cs`, add `using MotorCombat.Ramming;` below `using MotorCombat.Cars;`. After `GetOrCreate<CameraConfig>("CameraConfig");` add:
```csharp
            var ram = GetOrCreate<RamConfig>("RamConfig");
```
and after `if (car.aimConfig == null) car.aimConfig = aim;` add:
```csharp
            if (car.ramConfig == null) car.ramConfig = ram;
```

- [ ] **Step 9: Run tests to verify they pass**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="116"`. Also confirm the CLI output shows no new compiler warnings from the changed files.

- [ ] **Step 10: Commit**

```bash
git add Assets/_Project
git status --porcelain
git commit -m "Implement rams: RammingModule, driving lock/reel carve-outs, stat wiring" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Generate assets and document

**Files:**
- Generate: `Assets/_Project/Configs/RamConfig.asset` (+ `.meta`); modifies `Assets/_Project/Configs/CarDefinition.asset`
- Create: `docs/ramming.md`
- Modify: `CLAUDE.md`, `docs/architecture.md`, `docs/driving-physics.md`, `docs/tuning.md`, `docs/workflow.md`

**Interfaces:**
- Consumes: everything from Tasks 1–3.
- Produces: a playable build where the scene's `CarDefinition` has a `RamConfig`.

- [ ] **Step 1: Generate the asset**

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
```

- [ ] **Step 2: Verify the generated assets**

```bash
git status --porcelain
git diff Assets/_Project/Configs/CarDefinition.asset
cat Assets/_Project/Configs/RamConfig.asset
```
Expected:
- `RamConfig.asset` and its `.meta` are new, with `headOnScale: 0.2`, `flankScale: 1.5`, `rearScale: 1.2`, `attackerLockSeconds: 0.5`, `reelSeconds: 1`, `minRamSpeed: 3`, `headOnAngleDegrees: 45`, `cornerBandMetres: 0.3`, `spinScale: 1`, `spinDecayRate: 2`.
- `CarDefinition.asset` diff **only adds** `attack: 1`, `defense: 1`, and `ramConfig: {fileID: 11400000, guid: <RamConfig guid>, type: 2}`. No existing value (length, mass, visualPrefab, visualOffset, tintedMaterials, driveConfig, aimConfig…) may change. If any does, stop and report.
- No other asset changed, except `DriveConfig.asset` gaining `flipSteeringInReverse: 1`, which `docs/tuning.md` already documents as expected.

- [ ] **Step 3: Write `docs/ramming.md`**

Create `docs/ramming.md` as the current-state reference, in the style of `docs/driving-physics.md` (short sections, tables, explain *why*). It must contain these sections, with content drawn from `docs/specs/2026-09-13-ramming-design.md`:
1. `# Ramming` — one paragraph: car-vs-car only; attacker stops, victim is shoved and reels; head-on stops both.
2. `## Regions are faces, not volumes` — the region table (§1.2) and the T-bone-near-the-nose reason.
3. `## Type` — the classification table (§1.3) and resolve order (§1.4), including the three decisions (locked/reeling cannot attack, faster wins, tie = head-on).
4. `## The shove` — the shove and spin formulas (§1.5), mass ignored, never vertical.
5. `## Locked and reeling` — the states table (§1.6).
6. `## Why velocities are overwritten, not added` — PhysX has already responded by `OnCollisionEnter`; `PreStepVelocity` snapshot; the ≈0.5 m/s approximation; pair guard by `Time.fixedTime`; and the known depenetration risk from the spec §2.6 (cars that overlapped get separated at up to `maxDepenetrationVelocity`, 10 m/s by default — watch head-ons; not yet fixed).
7. `## Tests` — `RamRulesTests` 36, `CarStatusTests` 7.

- [ ] **Step 4: Update the other docs**

- `CLAUDE.md`: add a row to the Documentation table after `aiming-and-camera.md`: `| [docs/ramming.md](docs/ramming.md) | Anything about car-vs-car collisions, lock or reel |`. In "Not built yet", change `Combat, damage, health, respawn.` to `Weapons, damage, health, respawn.` and replace the sentence `` `RammingModule` and `WeaponModule` are deliberate empty seams, not oversights. `` with `` `WeaponModule` is a deliberate empty seam, not an oversight. ``
- `docs/architecture.md`: in the diagram replace the `RammingModule` line with `├── RammingModule  (collision)    region + angle → shove victim, stop attacker; spin decay while reeling` and add below the module lines `CarStatus (Core) ← lock / reel timers; written by Ramming, read by Driving`. Rename section `## Ramming and Weapons are seams, not features` to `## Weapons is a seam, not a feature` and rewrite it to describe only `WeaponModule`, adding: "Ramming was the other seam; it is now built — see [ramming.md](ramming.md). It reaches driving only through `CarController.Status`, so the two assemblies still never reference each other." Change the `59 EditMode tests` phrase to `116 EditMode tests`.
- `docs/driving-physics.md`: in "Order of operations" add after the numbered list: `While the car is **locked or reeling**, throttle and steer read as zero. While **reeling**, yaw and grip are skipped — see [ramming.md](ramming.md#locked-and-reeling).` Replace the "Known behaviour that looks like a bug" section body with: `**Plain bumps barely move a car and never spin it.** A contact that is not a ram (below minRamSpeed, or not front-first) gets only PhysX's response, and DrivingModule's yaw write and grip absorb it. Rams bypass both — see [ramming.md](ramming.md).`
- `docs/tuning.md`: add a `## RamConfig` section after `## CameraConfig` with the ten fields, placeholder values, units and one-line notes from the RamConfig tooltips. In the `## CarDefinition` table add rows `| attack | 1 | Multiplies shove dealt |` and `| defense | 1 | Divides shove received; must be > 0 |`, and change the `driveConfig / aimConfig` row to `driveConfig / aimConfig / ramConfig`.
- `docs/workflow.md`: change `72 EditMode tests` to `116 EditMode tests`; add `| RamRulesTests | 36 |` and `| CarStatusTests | 7 |` rows and change `CarFactoryTests` to 16; change the checklist intro to "Behaviour that cannot be unit-tested. Rows 1–12 passed before ramming; rows 13–20 are new and not yet walked." and change row 11 to `| 11 | Drive slowly (under 3 m/s) into the dummy | Plain bump, no ram |`. Append rows:

```
| 13 | Drive into the parked dummy's rear at speed | Rear ram: you stop dead; dummy shoots forward and slides |
| 14 | Hit the dummy's side mid-panel | Flank: dummy slides sideways, little or no spin |
| 15 | Hit the dummy's side near its tail | Flank with spin; spin winds down, then it settles |
| 16 | Drive into the dummy's nose, head-on | Both nudged apart, no spin, you are locked ~0.5 s. A violent bounce here is PhysX depenetration — see ramming.md |
| 17 | After any ram, press W and A/D immediately | No response for ~0.5 s; mouse aim still moves the crosshair |
| 18 | Hit the dummy's front corner at a shallow vs steep angle | Shallow (< 45°) is head-on; steep is flank |
| 19 | Ram the dummy again while it is still sliding | Second ram applies; its reel restarts |
| 20 | Any ram at top speed | No car leaves the ground or tips |
```

- [ ] **Step 5: Run tests once more**

Run the Global Constraints test command.
Expected: `failed="0"`, `total="116"`.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project docs CLAUDE.md
git status --porcelain
git commit -m "Generate RamConfig asset and document ramming" -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
