# Motor Combat 3D Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a Unity 6 project with one playable Arena scene — circular arena, box car, first/third-person cameras, arcade driving, cone-clamped mouse aiming — plus the module seams combat will later fill.

**Architecture:** A thin `CarController` MonoBehaviour owns the `Rigidbody` and hands a `CarInput` struct to swappable `ICarModule` components. Each module is a thin MonoBehaviour adapter over a pure static class that does the actual math, so the physics is unit-testable without a scene. Assembly definitions per folder make the module boundaries compile-enforced.

**Tech Stack:** Unity 6000.6.0f1, URP (`com.unity.template.urp-blank`), Input System package, Unity Test Framework (NUnit), C# / .NET Standard 2.1.

**Spec:** [`docs/superpowers/specs/2026-09-12-motor-combat-3d-skeleton-design.md`](../specs/2026-09-12-motor-combat-3d-skeleton-design.md)

## Global Constraints

- Unity editor version is exactly `6000.6.0f1`. Do not upgrade the project.
- Render pipeline is URP. Template is `com.unity.template.urp-blank`.
- Input uses the Input System package only. Active Input Handling must be **Input System Package (New)**. Never call legacy `UnityEngine.Input`.
- All first-party code lives under `Assets/_Project/`. Root namespace prefix is `MotorCombat`.
- **Every decay must be exponential and timestep-independent:** `value *= Mathf.Exp(-rate * dt)`. Never `value *= (1 - fraction)` per `FixedUpdate`. All decay config fields are **rates in 1/s**.
- `rb.linearDamping` and `rb.angularDamping` must both be `0`. Drag is applied manually so the terminal-speed formula holds.
- No `WheelCollider`. No netcode. No combat, damage, health, respawn, menus, lobby, audio, or real art.
- Every tuning number lives in a ScriptableObject under `Assets/_Project/Configs/`. No magic numbers in MonoBehaviours.
- `IInputProvider.Sample()` is called exactly once per `Update`, and the result is cached for `FixedUpdate`.
- Assembly names: `MotorCombat.Core`, `.Controls`, `.Driving`, `.Aiming`, `.Ramming`, `.Weapons`, `.Cars`, `.Arena`, `.Cameras`, `.HUD`, `.Bootstrap`, `.EditorTools`, `.Tests.EditMode`.
  Note the two deliberate renames from the spec's folder sketch: `Cameras` (not `CameraRig`, which would collide with the `CameraRig` class) and `Controls` (not `Input`, which would shadow the `Input` identifier).

---

## File Structure

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/Core/CarInput.cs` | The input struct passed to every module |
| `Assets/_Project/Scripts/Core/IInputProvider.cs` | One method: `CarInput Sample()` — the netcode seam |
| `Assets/_Project/Scripts/Core/ICarModule.cs` | `Tick` (fixed) + `FrameTick` (frame) |
| `Assets/_Project/Scripts/Core/CarController.cs` | Owns Rigidbody + aimYaw, samples once per frame, ticks modules |
| `Assets/_Project/Scripts/Controls/LocalInputProvider.cs` | Keyboard + mouse via Input System |
| `Assets/_Project/Scripts/Controls/NullInputProvider.cs` | Zeroed input, for the dummy car |
| `Assets/_Project/Scripts/Aiming/AimMath.cs` | **Pure.** Accumulate + clamp aim yaw |
| `Assets/_Project/Scripts/Aiming/AimModule.cs` | Adapter: reads config, writes `CarController.AimYaw` |
| `Assets/_Project/Scripts/Aiming/AimConfig.cs` | Cone angle, sensitivity |
| `Assets/_Project/Scripts/Driving/DrivePhysics.cs` | **Pure.** Drive force, drag, grip, yaw rate |
| `Assets/_Project/Scripts/Driving/DrivingModule.cs` | Adapter: applies `DrivePhysics` output to the Rigidbody |
| `Assets/_Project/Scripts/Driving/DriveConfig.cs` | Engine power, drag, brake, reverse, turn rate, grip |
| `Assets/_Project/Scripts/Ramming/CarCollisionEvent.cs` | Impact data struct |
| `Assets/_Project/Scripts/Ramming/RammingModule.cs` | Raises collision events. **Seam only.** |
| `Assets/_Project/Scripts/Weapons/IWeapon.cs` | Weapon contract |
| `Assets/_Project/Scripts/Weapons/WeaponModule.cs` | Holds weapons, exposes Fire(). **Seam only.** |
| `Assets/_Project/Scripts/Cars/CarDefinition.cs` | Dimensions, mass, refs to drive/aim configs |
| `Assets/_Project/Scripts/Cars/CarFactory.cs` | Builds a car GameObject from a definition |
| `Assets/_Project/Scripts/Arena/ArenaMeshBuilder.cs` | Disc + ring mesh generation |
| `Assets/_Project/Scripts/Arena/ArenaBuilder.cs` | Instantiates ground + wall GameObjects |
| `Assets/_Project/Scripts/Arena/ArenaConfig.cs` | Radius in car lengths, wall height, segments |
| `Assets/_Project/Scripts/Cameras/CameraRig.cs` | First/third person follow |
| `Assets/_Project/Scripts/Cameras/CameraConfig.cs` | Mode enum + per-mode placement |
| `Assets/_Project/Scripts/HUD/CrosshairHUD.cs` | Projects aim ray to screen, draws crosshair |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs` | Builds arena, spawns cars, binds camera |
| `Assets/_Project/Editor/ArenaSceneBuilder.cs` | Generates `Arena.unity` headlessly |
| `Assets/_Project/Tests/EditMode/AimMathTests.cs` | Cone clamping |
| `Assets/_Project/Tests/EditMode/DrivePhysicsTests.cs` | Timestep independence, terminal speed, brake/reverse |
| `Assets/_Project/Tests/EditMode/ArenaMeshBuilderTests.cs` | Mesh topology and rim radius |

---

### Task 1: Create the Unity project at the repository root

**Files:**
- Create: `Assets/`, `Packages/`, `ProjectSettings/` (from template)
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing
- Produces: a Unity 6000.6.0f1 URP project rooted at the repo, with Input System active and the Test Framework available.

`unity projects new` creates a *new folder*, and our repo root already exists with git history in it. So we create into a temp directory and move the three project folders in.

- [ ] **Step 1: Create the project in a temp directory**

```bash
TMPROOT=$(mktemp -d)
unity projects new motor-combat-3D \
  --path "$TMPROOT" \
  --editor-version 6000.6.0f1 \
  --template com.unity.template.urp-blank \
  --non-interactive
ls "$TMPROOT/motor-combat-3D"
```

Expected: `Assets`, `Packages`, `ProjectSettings` listed.

- [ ] **Step 2: Move the project folders into the repo root**

```bash
mv "$TMPROOT/motor-combat-3D/Assets" \
   "$TMPROOT/motor-combat-3D/Packages" \
   "$TMPROOT/motor-combat-3D/ProjectSettings" .
rm -rf "$TMPROOT"
ls -d Assets Packages ProjectSettings
```

- [ ] **Step 3: Re-register the project with the Hub at its real path**

```bash
unity projects add "$(pwd -W 2>/dev/null || pwd)"
unity projects info .
```

Expected: `info` reports editor version `6000.6.0f1`.

- [ ] **Step 4: Ensure the Input System and Test Framework packages are present**

Read `Packages/manifest.json`. It must contain both of these entries inside `dependencies`; add any that are missing (keep the file's existing ordering):

```json
"com.unity.inputsystem": "1.14.2",
"com.unity.test-framework": "1.5.1",
```

If the manifest already pins different versions of these two packages, leave the existing versions alone — any 1.x satisfies this plan.

- [ ] **Step 5: Set Active Input Handling to the new Input System**

In `ProjectSettings/ProjectSettings.asset`, find the `activeInputHandler:` line and set it to `1`.

```
  activeInputHandler: 1
```

Values are `0` = legacy only, `1` = new only, `2` = both. We want `1`.

- [ ] **Step 6: Add the .gitignore rules**

Append to `.gitignore`:

```gitignore
# Unity CLI test reports
/test-results.xml
/CodeCoverage/

# Claude Code project configuration is tracked on purpose
!.claude/skills/
!.claude/settings.json
!.claude/hooks/
```

- [ ] **Step 7: Verify project integrity**

Run: `unity projects verify .`
Expected: no integrity problems reported.

- [ ] **Step 8: Verify it compiles and the test runner starts**

Run: `unity test . --mode EditMode --timeout 900`
Expected: the editor launches, compiles with no errors, and reports 0 tests run (no test assemblies exist yet). A non-zero exit caused by "no tests matched" is acceptable here; **compile errors are not**.

- [ ] **Step 9: Commit**

```bash
git add Assets Packages ProjectSettings .gitignore
git commit -m "$(printf 'Create Unity 6 URP project at repo root\n\nTemplate com.unity.template.urp-blank on editor 6000.6.0f1, Input System\nset as the active input handler, Test Framework available.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 2: Core contracts and assembly layout

**Files:**
- Create: `Assets/_Project/Scripts/Core/CarInput.cs`
- Create: `Assets/_Project/Scripts/Core/IInputProvider.cs`
- Create: `Assets/_Project/Scripts/Core/ICarModule.cs`
- Create: `Assets/_Project/Scripts/Core/MotorCombat.Core.asmdef`

**Interfaces:**
- Consumes: nothing
- Produces: `MotorCombat.Core.CarInput` (fields `throttle`, `steer`, `aimDeltaX`), `MotorCombat.Core.IInputProvider.Sample() -> CarInput`, `MotorCombat.Core.ICarModule.Tick(in CarInput, float)` and `.FrameTick(in CarInput, float)`.

- [ ] **Step 1: Create the Core assembly definition**

`Assets/_Project/Scripts/Core/MotorCombat.Core.asmdef`:

```json
{
    "name": "MotorCombat.Core",
    "rootNamespace": "MotorCombat.Core",
    "references": [],
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

- [ ] **Step 2: Create CarInput**

`Assets/_Project/Scripts/Core/CarInput.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>
    /// One frame of intent for a car. Produced by an <see cref="IInputProvider"/>,
    /// consumed by every <see cref="ICarModule"/>.
    /// </summary>
    public struct CarInput
    {
        /// <summary>-1 (S, brake/reverse) .. +1 (W, accelerate).</summary>
        public float throttle;

        /// <summary>-1 (A, left) .. +1 (D, right).</summary>
        public float steer;

        /// <summary>
        /// Raw horizontal mouse movement in pixels since the last Update.
        /// This is a displacement, NOT a rate — never multiply it by deltaTime.
        /// </summary>
        public float aimDeltaX;

        public static CarInput None => new CarInput();
    }
}
```

- [ ] **Step 3: Create IInputProvider**

`Assets/_Project/Scripts/Core/IInputProvider.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>
    /// The seam that keeps driving code ignorant of where input came from.
    /// Local keyboard today; a network or bot provider later, with no change
    /// to any module.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// Called exactly once per Update by <see cref="CarController"/>.
        /// Implementations that accumulate deltas may clear them here.
        /// </summary>
        CarInput Sample();
    }
}
```

- [ ] **Step 4: Create ICarModule**

`Assets/_Project/Scripts/Core/ICarModule.cs`:

```csharp
namespace MotorCombat.Core
{
    /// <summary>
    /// A swappable behaviour attached to a car. Modules never talk to each
    /// other; they read the input struct and the controller's public state.
    /// </summary>
    public interface ICarModule
    {
        /// <summary>Called from FixedUpdate. Use for anything touching the Rigidbody.</summary>
        void Tick(in CarInput input, float dt);

        /// <summary>Called from Update. Use for anything consuming per-frame deltas.</summary>
        void FrameTick(in CarInput input, float dt);
    }
}
```

- [ ] **Step 5: Verify it compiles**

Run: `unity test . --mode EditMode --timeout 900`
Expected: no compile errors in the output.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Core
git commit -m "$(printf 'Add core car contracts\n\nCarInput, IInputProvider (the netcode seam) and ICarModule, in their own\nassembly so gameplay modules can depend on contracts without depending on\neach other.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 3: AimMath (pure) with tests

**Files:**
- Create: `Assets/_Project/Scripts/Aiming/AimMath.cs`
- Create: `Assets/_Project/Scripts/Aiming/MotorCombat.Aiming.asmdef`
- Test: `Assets/_Project/Tests/EditMode/AimMathTests.cs`
- Test: `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`

**Interfaces:**
- Consumes: nothing
- Produces: `MotorCombat.Aiming.AimMath.Accumulate(float currentYaw, float deltaPixels, float sensitivity, float coneAngleDegrees) -> float`

- [ ] **Step 1: Create the Aiming assembly definition**

`Assets/_Project/Scripts/Aiming/MotorCombat.Aiming.asmdef`:

```json
{
    "name": "MotorCombat.Aiming",
    "rootNamespace": "MotorCombat.Aiming",
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

- [ ] **Step 2: Create the EditMode test assembly definition**

`Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`:

```json
{
    "name": "MotorCombat.Tests.EditMode",
    "rootNamespace": "MotorCombat.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "MotorCombat.Core",
        "MotorCombat.Aiming"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Later tasks add `MotorCombat.Driving` and `MotorCombat.Arena` to the `references` array.

- [ ] **Step 3: Write the failing tests**

`Assets/_Project/Tests/EditMode/AimMathTests.cs`:

```csharp
using NUnit.Framework;
using MotorCombat.Aiming;

namespace MotorCombat.Tests
{
    public class AimMathTests
    {
        const float Cone = 90f;          // +/- 45 degrees
        const float Sensitivity = 0.12f; // degrees per pixel

        [Test]
        public void ZeroDelta_LeavesYawUnchanged()
        {
            float result = AimMath.Accumulate(12.5f, 0f, Sensitivity, Cone);
            Assert.AreEqual(12.5f, result, 1e-5f);
        }

        [Test]
        public void SmallDelta_AccumulatesAtSensitivity()
        {
            float result = AimMath.Accumulate(0f, 100f, Sensitivity, Cone);
            Assert.AreEqual(12f, result, 1e-5f);
        }

        [Test]
        public void LargePositiveDelta_ClampsAtHalfCone()
        {
            float result = AimMath.Accumulate(0f, 10000f, Sensitivity, Cone);
            Assert.AreEqual(45f, result, 1e-5f);
        }

        [Test]
        public void LargeNegativeDelta_ClampsAtNegativeHalfCone()
        {
            float result = AimMath.Accumulate(0f, -10000f, Sensitivity, Cone);
            Assert.AreEqual(-45f, result, 1e-5f);
        }

        [Test]
        public void PushingPastTheEdge_DoesNotWrapAround()
        {
            float atEdge = AimMath.Accumulate(0f, 10000f, Sensitivity, Cone);
            float stillAtEdge = AimMath.Accumulate(atEdge, 10000f, Sensitivity, Cone);
            Assert.AreEqual(45f, stillAtEdge, 1e-5f);
        }

        [Test]
        public void ReversingFromTheEdge_MovesBackImmediately()
        {
            float atEdge = AimMath.Accumulate(0f, 10000f, Sensitivity, Cone);
            float backOff = AimMath.Accumulate(atEdge, -100f, Sensitivity, Cone);
            Assert.AreEqual(33f, backOff, 1e-5f);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `unity test . --mode EditMode --filter AimMathTests --timeout 900`
Expected: FAIL — compile error, `AimMath` does not exist.

- [ ] **Step 5: Write the implementation**

`Assets/_Project/Scripts/Aiming/AimMath.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Aiming
{
    /// <summary>
    /// Pure aim maths. No Unity objects, no state — unit-testable without a scene.
    /// </summary>
    public static class AimMath
    {
        /// <summary>
        /// Advance a car-relative aim yaw by one frame of mouse movement and clamp
        /// it into the aiming cone.
        /// </summary>
        /// <param name="currentYaw">Current aim yaw in degrees, relative to the car's forward.</param>
        /// <param name="deltaPixels">Raw horizontal mouse displacement this frame. NOT scaled by dt.</param>
        /// <param name="sensitivity">Degrees of aim per pixel of mouse movement.</param>
        /// <param name="coneAngleDegrees">Total cone width; the yaw is clamped to half of it either side.</param>
        public static float Accumulate(float currentYaw, float deltaPixels, float sensitivity, float coneAngleDegrees)
        {
            float halfCone = coneAngleDegrees * 0.5f;
            float next = currentYaw + deltaPixels * sensitivity;
            return Mathf.Clamp(next, -halfCone, halfCone);
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `unity test . --mode EditMode --filter AimMathTests --timeout 900`
Expected: PASS, 6 tests.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Aiming Assets/_Project/Tests
git commit -m "$(printf 'Add AimMath with cone clamping tests\n\nCar-relative aim yaw accumulated from raw mouse delta and clamped to the\nconfigured cone. Pure static, no scene required to test.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 4: DrivePhysics (pure) with tests

**Files:**
- Create: `Assets/_Project/Scripts/Driving/DrivePhysics.cs`
- Create: `Assets/_Project/Scripts/Driving/MotorCombat.Driving.asmdef`
- Modify: `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef` (add `MotorCombat.Driving` to references)
- Test: `Assets/_Project/Tests/EditMode/DrivePhysicsTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces, all on `MotorCombat.Driving.DrivePhysics`:
  - `DriveForce(Vector3 forward, Vector3 velocity, float throttle, float enginePower, float brakeForce, float reversePower, float reverseEpsilon) -> Vector3`
  - `ApplyDrag(Vector3 velocity, float dragRate, float dt) -> Vector3`
  - `ApplyGrip(Vector3 velocity, Vector3 forward, float gripStrength, float dt) -> Vector3`
  - `YawRate(float steer, float turnRateDegPerSec) -> float`
  - `TerminalSpeed(float enginePower, float mass, float dragRate) -> float`

- [ ] **Step 1: Create the Driving assembly definition**

`Assets/_Project/Scripts/Driving/MotorCombat.Driving.asmdef`:

```json
{
    "name": "MotorCombat.Driving",
    "rootNamespace": "MotorCombat.Driving",
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

- [ ] **Step 2: Add Driving to the test assembly references**

In `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, change the `references` array to:

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "MotorCombat.Core",
        "MotorCombat.Aiming",
        "MotorCombat.Driving"
    ],
```

- [ ] **Step 3: Write the failing tests**

`Assets/_Project/Tests/EditMode/DrivePhysicsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Driving;

namespace MotorCombat.Tests
{
    public class DrivePhysicsTests
    {
        static readonly Vector3 Forward = Vector3.forward;

        // --- Grip -------------------------------------------------------------

        [Test]
        public void Grip_LeavesLongitudinalVelocityUntouched()
        {
            var velocity = new Vector3(3f, 0f, 10f);   // 3 sideways, 10 forward
            var result = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);
            Assert.AreEqual(10f, Vector3.Dot(result, Forward), 1e-4f);
        }

        [Test]
        public void Grip_BleedsLateralVelocity()
        {
            var velocity = new Vector3(3f, 0f, 10f);
            var result = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);
            float lateral = Vector3.Dot(result, Vector3.right);
            Assert.Less(lateral, 3f);
            Assert.Greater(lateral, 0f);
        }

        /// <summary>
        /// The property that matters most: handling must not change when the
        /// physics timestep changes. One 0.02s step must equal two 0.01s steps.
        /// </summary>
        [Test]
        public void Grip_IsTimestepIndependent()
        {
            var velocity = new Vector3(5f, 0f, 12f);

            var oneBigStep = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.02f);

            var twoSmallSteps = DrivePhysics.ApplyGrip(velocity, Forward, 6f, 0.01f);
            twoSmallSteps = DrivePhysics.ApplyGrip(twoSmallSteps, Forward, 6f, 0.01f);

            Assert.AreEqual(Vector3.Dot(oneBigStep, Vector3.right),
                            Vector3.Dot(twoSmallSteps, Vector3.right), 1e-5f);
        }

        // --- Drag -------------------------------------------------------------

        [Test]
        public void Drag_IsTimestepIndependent()
        {
            var velocity = new Vector3(0f, 0f, 20f);

            var oneBigStep = DrivePhysics.ApplyDrag(velocity, 1f, 0.02f);

            var twoSmallSteps = DrivePhysics.ApplyDrag(velocity, 1f, 0.01f);
            twoSmallSteps = DrivePhysics.ApplyDrag(twoSmallSteps, 1f, 0.01f);

            Assert.AreEqual(oneBigStep.z, twoSmallSteps.z, 1e-5f);
        }

        [Test]
        public void TerminalSpeed_MatchesTheConfiguredFormula()
        {
            // 30000 N on a 1200 kg car with drag rate 1.0 /s  ->  25 m/s
            Assert.AreEqual(25f, DrivePhysics.TerminalSpeed(30000f, 1200f, 1f), 1e-4f);
        }

        [Test]
        public void IntegratingThrustAgainstDrag_ConvergesOnTerminalSpeed()
        {
            const float mass = 1200f, power = 30000f, drag = 1f, dt = 0.02f;
            var velocity = Vector3.zero;

            for (int i = 0; i < 1500; i++)   // 30 simulated seconds
            {
                var force = DrivePhysics.DriveForce(Forward, velocity, 1f, power, 40000f, 12000f, 0.5f);
                velocity += force / mass * dt;
                velocity = DrivePhysics.ApplyDrag(velocity, drag, dt);
            }

            // Discrete stepping settles a little under the continuous limit:
            // applying drag after the force gives v = a*dt*k / (1-k) with
            // k = exp(-drag*dt), which is ~24.75 rather than exactly 25.
            // The tolerance covers that without hiding a real regression.
            Assert.AreEqual(DrivePhysics.TerminalSpeed(power, mass, drag), velocity.z, 0.5f);
        }

        // --- Throttle, brake, reverse -----------------------------------------

        [Test]
        public void PositiveThrottle_PushesForward()
        {
            var force = DrivePhysics.DriveForce(Forward, Vector3.zero, 1f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(30000f, force.z, 1e-3f);
        }

        [Test]
        public void NegativeThrottle_WhileMovingForward_Brakes()
        {
            var velocity = new Vector3(0f, 0f, 10f);
            var force = DrivePhysics.DriveForce(Forward, velocity, -1f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(-40000f, force.z, 1e-3f, "should oppose motion with brakeForce");
        }

        [Test]
        public void NegativeThrottle_WhenNearlyStopped_Reverses()
        {
            var velocity = new Vector3(0f, 0f, 0.1f);   // below the 0.5 epsilon
            var force = DrivePhysics.DriveForce(Forward, velocity, -1f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(-12000f, force.z, 1e-3f, "should apply reversePower, not brakeForce");
        }

        [Test]
        public void NoThrottle_ProducesNoForce()
        {
            var force = DrivePhysics.DriveForce(Forward, new Vector3(0f, 0f, 10f), 0f, 30000f, 40000f, 12000f, 0.5f);
            Assert.AreEqual(Vector3.zero, force);
        }

        // --- Yaw --------------------------------------------------------------

        /// <summary>
        /// Turn-in-place works because YawRate never consults speed. This test
        /// pins that: full steer yields the full configured rate, and the
        /// signature offers nowhere for a speed term to creep in later.
        /// </summary>
        [Test]
        public void YawRate_IsFullRateAtFullSteer()
        {
            Assert.AreEqual(90f, DrivePhysics.YawRate(1f, 90f), 1e-4f);
            Assert.AreEqual(-90f, DrivePhysics.YawRate(-1f, 90f), 1e-4f);
            Assert.AreEqual(0f, DrivePhysics.YawRate(0f, 90f), 1e-4f);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `unity test . --mode EditMode --filter DrivePhysicsTests --timeout 900`
Expected: FAIL — compile error, `DrivePhysics` does not exist.

- [ ] **Step 5: Write the implementation**

`Assets/_Project/Scripts/Driving/DrivePhysics.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Driving
{
    /// <summary>
    /// Pure arcade driving maths. No Rigidbody, no MonoBehaviour, no state —
    /// every function is a plain transformation, so the feel of the car can be
    /// pinned by unit tests without opening a scene.
    /// </summary>
    public static class DrivePhysics
    {
        /// <summary>
        /// Engine, brake and reverse resolved into a single world-space force.
        /// Braking and reversing are the same key: brake while actually moving
        /// forward, reverse once nearly stopped.
        /// </summary>
        public static Vector3 DriveForce(
            Vector3 forward,
            Vector3 velocity,
            float throttle,
            float enginePower,
            float brakeForce,
            float reversePower,
            float reverseEpsilon)
        {
            if (throttle > 0f)
            {
                return forward * (throttle * enginePower);
            }

            if (throttle < 0f)
            {
                float forwardSpeed = Vector3.Dot(velocity, forward);
                if (forwardSpeed > reverseEpsilon)
                {
                    return -forward * brakeForce;
                }

                // throttle is negative, so this pushes backwards.
                return forward * (throttle * reversePower);
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Exponential velocity decay. Exponential rather than a per-tick
        /// multiply so that changing Fixed Timestep does not change how the car
        /// handles. <paramref name="dragRate"/> is in 1/s.
        /// </summary>
        public static Vector3 ApplyDrag(Vector3 velocity, float dragRate, float dt)
        {
            return velocity * Mathf.Exp(-dragRate * dt);
        }

        /// <summary>
        /// Bleed the sideways component of velocity. The gap this leaves between
        /// where the nose points and where the car is actually travelling is the
        /// drift. <paramref name="gripStrength"/> is in 1/s; higher is grippier.
        /// </summary>
        public static Vector3 ApplyGrip(Vector3 velocity, Vector3 forward, float gripStrength, float dt)
        {
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float longitudinal = Vector3.Dot(velocity, forward);
            float lateral = Vector3.Dot(velocity, right);
            float vertical = Vector3.Dot(velocity, Vector3.up);

            lateral *= Mathf.Exp(-gripStrength * dt);

            return forward * longitudinal + right * lateral + Vector3.up * vertical;
        }

        /// <summary>
        /// Degrees per second of yaw for a given steer input. Deliberately has no
        /// speed parameter: that is what makes turning on the spot work.
        /// </summary>
        public static float YawRate(float steer, float turnRateDegPerSec)
        {
            return steer * turnRateDegPerSec;
        }

        /// <summary>
        /// The speed at which engine force and drag balance. Only holds while
        /// Rigidbody.linearDamping is zero and drag is applied by ApplyDrag.
        /// </summary>
        public static float TerminalSpeed(float enginePower, float mass, float dragRate)
        {
            return enginePower / (mass * dragRate);
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `unity test . --mode EditMode --filter DrivePhysicsTests --timeout 900`
Expected: PASS, 11 tests.

- [ ] **Step 7: Run the whole suite**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 17 tests.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Driving Assets/_Project/Tests
git commit -m "$(printf 'Add DrivePhysics with timestep-independence tests\n\nThrust, brake/reverse, exponential drag and lateral grip as pure functions.\nTests pin that one 0.02s step equals two 0.01s steps, so handling cannot\ndrift with the physics timestep.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 5: Configuration ScriptableObjects and asset instances

**Files:**
- Create: `Assets/_Project/Scripts/Driving/DriveConfig.cs`
- Create: `Assets/_Project/Scripts/Aiming/AimConfig.cs`
- Create: `Assets/_Project/Scripts/Arena/ArenaConfig.cs`
- Create: `Assets/_Project/Scripts/Arena/MotorCombat.Arena.asmdef`
- Create: `Assets/_Project/Scripts/Cameras/CameraConfig.cs`
- Create: `Assets/_Project/Scripts/Cameras/MotorCombat.Cameras.asmdef`
- Create: `Assets/_Project/Scripts/Cars/CarDefinition.cs`
- Create: `Assets/_Project/Scripts/Cars/MotorCombat.Cars.asmdef`
- Create: `Assets/_Project/Editor/ConfigAssetBootstrap.cs`
- Create: `Assets/_Project/Editor/MotorCombat.EditorTools.asmdef`

**Interfaces:**
- Consumes: nothing
- Produces: `DriveConfig` (`enginePower`, `linearDrag`, `brakeForce`, `reversePower`, `reverseEpsilon`, `turnRate`, `lateralGripStrength`), `AimConfig` (`coneAngleDegrees`, `mouseSensitivity`), `ArenaConfig` (`radiusInCarLengths`, `wallHeight`, `segments`), `CameraConfig` (`mode`, `thirdPersonDistance`, `thirdPersonHeight`, `thirdPersonPitch`, `thirdPersonFov`, `firstPersonFov`), `CarDefinition` (`length`, `width`, `height`, `mass`, `driverAnchorOffset`, `driveConfig`, `aimConfig`), and the enum `CameraMode { ThirdPerson, FirstPerson }`. Asset instances at `Assets/_Project/Configs/`.

- [ ] **Step 1: Create the Arena, Cameras and Cars assembly definitions**

`Assets/_Project/Scripts/Arena/MotorCombat.Arena.asmdef`:

```json
{
    "name": "MotorCombat.Arena",
    "rootNamespace": "MotorCombat.Arena",
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

`Assets/_Project/Scripts/Cameras/MotorCombat.Cameras.asmdef`:

```json
{
    "name": "MotorCombat.Cameras",
    "rootNamespace": "MotorCombat.Cameras",
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

`Assets/_Project/Scripts/Cars/MotorCombat.Cars.asmdef`:

```json
{
    "name": "MotorCombat.Cars",
    "rootNamespace": "MotorCombat.Cars",
    "references": [
        "MotorCombat.Core",
        "MotorCombat.Driving",
        "MotorCombat.Aiming"
    ],
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

- [ ] **Step 2: Create DriveConfig**

`Assets/_Project/Scripts/Driving/DriveConfig.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Driving
{
    [CreateAssetMenu(menuName = "Motor Combat/Drive Config", fileName = "DriveConfig")]
    public class DriveConfig : ScriptableObject
    {
        [Header("Thrust")]
        [Tooltip("Newtons of forward force at full throttle.")]
        public float enginePower = 30000f;

        [Tooltip("Velocity decay RATE in 1/s. Terminal speed = enginePower / (mass * linearDrag).")]
        public float linearDrag = 1f;

        [Header("Braking and reverse")]
        public float brakeForce = 40000f;
        public float reversePower = 12000f;

        [Tooltip("Forward speed (m/s) below which S reverses instead of braking.")]
        public float reverseEpsilon = 0.5f;

        [Header("Turning")]
        [Tooltip("Degrees per second at full steer. Not gated on speed — this is what allows turning on the spot.")]
        public float turnRate = 90f;

        [Header("Grip")]
        [Tooltip("Sideways velocity decay RATE in 1/s. Lower drifts more. 0 is a hockey puck.")]
        public float lateralGripStrength = 6f;
    }
}
```

- [ ] **Step 3: Create AimConfig**

`Assets/_Project/Scripts/Aiming/AimConfig.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Aiming
{
    [CreateAssetMenu(menuName = "Motor Combat/Aim Config", fileName = "AimConfig")]
    public class AimConfig : ScriptableObject
    {
        [Tooltip("Total width of the aiming cone. Aim is clamped to half of this either side of the car's forward.")]
        [Range(10f, 180f)]
        public float coneAngleDegrees = 90f;

        [Tooltip("Degrees of aim per pixel of mouse movement.")]
        public float mouseSensitivity = 0.12f;
    }
}
```

- [ ] **Step 4: Create ArenaConfig**

`Assets/_Project/Scripts/Arena/ArenaConfig.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Arena
{
    [CreateAssetMenu(menuName = "Motor Combat/Arena Config", fileName = "ArenaConfig")]
    public class ArenaConfig : ScriptableObject
    {
        [Tooltip("Arena radius expressed in car lengths. Actual radius = this * CarDefinition.length.")]
        public float radiusInCarLengths = 10f;

        public float wallHeight = 2.5f;

        [Tooltip("Segments around the circle. Higher is smoother and costs more triangles.")]
        [Range(12, 256)]
        public int segments = 96;
    }
}
```

- [ ] **Step 5: Create CameraConfig**

`Assets/_Project/Scripts/Cameras/CameraConfig.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Cameras
{
    public enum CameraMode
    {
        ThirdPerson = 0,
        FirstPerson = 1
    }

    [CreateAssetMenu(menuName = "Motor Combat/Camera Config", fileName = "CameraConfig")]
    public class CameraConfig : ScriptableObject
    {
        [Tooltip("Switch this in the Inspector to compare the two views.")]
        public CameraMode mode = CameraMode.ThirdPerson;

        [Header("Third person")]
        public float thirdPersonDistance = 8f;
        public float thirdPersonHeight = 3.5f;
        public float thirdPersonPitch = 12f;
        public float thirdPersonFov = 60f;

        [Header("First person")]
        public float firstPersonFov = 70f;
    }
}
```

- [ ] **Step 6: Create CarDefinition**

`Assets/_Project/Scripts/Cars/CarDefinition.cs`:

```csharp
using UnityEngine;
using MotorCombat.Driving;
using MotorCombat.Aiming;

namespace MotorCombat.Cars
{
    [CreateAssetMenu(menuName = "Motor Combat/Car Definition", fileName = "CarDefinition")]
    public class CarDefinition : ScriptableObject
    {
        [Header("Dimensions (metres)")]
        [Tooltip("Length also sets the arena size, via ArenaConfig.radiusInCarLengths.")]
        public float length = 4.5f;
        public float width = 2f;
        public float height = 1.2f;

        public float mass = 1200f;

        [Header("Driver eye position, relative to the car's centre")]
        public Vector3 driverAnchorOffset = new Vector3(0f, 1f, 0.4f);

        [Header("Behaviour")]
        public DriveConfig driveConfig;
        public AimConfig aimConfig;
    }
}
```

- [ ] **Step 7: Create the EditorTools assembly definition**

`Assets/_Project/Editor/MotorCombat.EditorTools.asmdef`:

```json
{
    "name": "MotorCombat.EditorTools",
    "rootNamespace": "MotorCombat.EditorTools",
    "references": [
        "MotorCombat.Core",
        "MotorCombat.Driving",
        "MotorCombat.Aiming",
        "MotorCombat.Arena",
        "MotorCombat.Cameras",
        "MotorCombat.Cars"
    ],
    "includePlatforms": ["Editor"],
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

`MotorCombat.Bootstrap`, `MotorCombat.Controls`, `MotorCombat.Ramming`, `MotorCombat.Weapons` and `MotorCombat.HUD` are added to this list in Task 11.

- [ ] **Step 8: Create the config asset generator**

`Assets/_Project/Editor/ConfigAssetBootstrap.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.Cars;

namespace MotorCombat.EditorTools
{
    /// <summary>
    /// Creates the default tuning assets. Idempotent: existing assets are left
    /// alone, so re-running never clobbers hand-tuned values.
    /// </summary>
    public static class ConfigAssetBootstrap
    {
        const string ConfigDir = "Assets/_Project/Configs";

        [MenuItem("Motor Combat/Create Default Configs")]
        public static void CreateDefaults()
        {
            Directory.CreateDirectory(ConfigDir);
            AssetDatabase.Refresh();

            var drive = GetOrCreate<DriveConfig>("DriveConfig");
            var aim = GetOrCreate<AimConfig>("AimConfig");
            GetOrCreate<ArenaConfig>("ArenaConfig");
            GetOrCreate<CameraConfig>("CameraConfig");

            var car = GetOrCreate<CarDefinition>("CarDefinition");
            if (car.driveConfig == null) car.driveConfig = drive;
            if (car.aimConfig == null) car.aimConfig = aim;
            EditorUtility.SetDirty(car);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MotorCombat] Default configs ready in " + ConfigDir);
        }

        static T GetOrCreate<T>(string assetName) where T : ScriptableObject
        {
            string path = $"{ConfigDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
```

- [ ] **Step 9: Generate the config assets headlessly**

```bash
unity run . -- -batchmode -nographics -quit \
  -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
ls Assets/_Project/Configs
```

Expected: `AimConfig.asset`, `ArenaConfig.asset`, `CameraConfig.asset`, `CarDefinition.asset`, `DriveConfig.asset` (plus `.meta` files).

- [ ] **Step 10: Run the test suite to confirm nothing broke**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 17 tests.

- [ ] **Step 11: Commit**

```bash
git add Assets/_Project/Scripts Assets/_Project/Editor Assets/_Project/Configs
git commit -m "$(printf 'Add tuning configs as ScriptableObjects\n\nDrive, aim, arena, camera and car definition assets, generated idempotently\nby an editor menu item so defaults are reproducible without hand-authoring\nasset YAML.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 6: CarController and input providers

**Files:**
- Create: `Assets/_Project/Scripts/Core/CarController.cs`
- Create: `Assets/_Project/Scripts/Controls/LocalInputProvider.cs`
- Create: `Assets/_Project/Scripts/Controls/NullInputProvider.cs`
- Create: `Assets/_Project/Scripts/Controls/MotorCombat.Controls.asmdef`

**Interfaces:**
- Consumes: `CarInput`, `IInputProvider`, `ICarModule` from Task 2.
- Produces: `MotorCombat.Core.CarController` with `Rigidbody Body`, `float AimYaw` (get/set), `Vector3 AimDirection`, `void Bind(IInputProvider)`; `MotorCombat.Controls.LocalInputProvider` and `NullInputProvider`, both MonoBehaviours implementing `IInputProvider`.

- [ ] **Step 1: Create the Controls assembly definition**

`Assets/_Project/Scripts/Controls/MotorCombat.Controls.asmdef`:

```json
{
    "name": "MotorCombat.Controls",
    "rootNamespace": "MotorCombat.Controls",
    "references": [
        "MotorCombat.Core",
        "Unity.InputSystem"
    ],
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

- [ ] **Step 2: Create CarController**

`Assets/_Project/Scripts/Core/CarController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace MotorCombat.Core
{
    /// <summary>
    /// Deliberately dumb. Owns the Rigidbody and the aim yaw, samples input once
    /// per frame, and forwards it to whatever modules are attached. Contains no
    /// physics and no game rules, so any module can be swapped without touching
    /// this file.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }

        /// <summary>Aim angle in degrees, relative to the car's forward. Written by the aim module.</summary>
        public float AimYaw { get; set; }

        /// <summary>World-space direction the weapons point.</summary>
        public Vector3 AimDirection => Quaternion.AngleAxis(AimYaw, Vector3.up) * transform.forward;

        IInputProvider _input;
        readonly List<ICarModule> _modules = new List<ICarModule>();

        // Sampled once per Update and reused by FixedUpdate. See Update's comment.
        CarInput _current;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();

            // Drag and angular damping are applied by the driving module using
            // exponential decay, so PhysX must not also apply its own.
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void Start()
        {
            // Awake runs the moment AddComponent is called, so modules attached
            // after the controller would be missed. Collect once the object is
            // fully assembled.
            _modules.Clear();
            GetComponents(_modules);
        }

        public void Bind(IInputProvider provider)
        {
            _input = provider;
        }

        void Update()
        {
            if (_input == null) return;

            // Sampled EXACTLY once per frame. aimDeltaX is a per-frame
            // accumulation that must be consumed once; sampling again in
            // FixedUpdate would double-consume or drop it depending on how many
            // physics steps landed in this frame.
            _current = _input.Sample();

            float dt = Time.deltaTime;
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].FrameTick(in _current, dt);
            }
        }

        void FixedUpdate()
        {
            // Reuses the cached struct; only the level fields (throttle, steer)
            // are meaningful here.
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Tick(in _current, dt);
            }
        }
    }
}
```

- [ ] **Step 3: Create LocalInputProvider**

`Assets/_Project/Scripts/Controls/LocalInputProvider.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using MotorCombat.Core;

namespace MotorCombat.Controls
{
    /// <summary>
    /// Keyboard and mouse. Reads devices directly rather than through an action
    /// asset — the control set is three axes and will be replaced by a network
    /// provider for real play, so an .inputactions asset would be ceremony.
    /// </summary>
    public class LocalInputProvider : MonoBehaviour, IInputProvider
    {
        [Tooltip("Hide and lock the cursor while playing.")]
        public bool lockCursor = true;

        void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public CarInput Sample()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            var input = new CarInput();

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.throttle += 1f;
                if (keyboard.sKey.isPressed) input.throttle -= 1f;
                if (keyboard.dKey.isPressed) input.steer += 1f;
                if (keyboard.aKey.isPressed) input.steer -= 1f;
            }

            if (mouse != null)
            {
                // Already a per-frame displacement in pixels. Do not scale by dt.
                input.aimDeltaX = mouse.delta.ReadValue().x;
            }

            return input;
        }
    }
}
```

- [ ] **Step 4: Create NullInputProvider**

`Assets/_Project/Scripts/Controls/NullInputProvider.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Controls
{
    /// <summary>Always idle. Used by the stationary dummy car.</summary>
    public class NullInputProvider : MonoBehaviour, IInputProvider
    {
        public CarInput Sample() => CarInput.None;
    }
}
```

- [ ] **Step 5: Verify it compiles**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 17 tests, no compile errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Core Assets/_Project/Scripts/Controls
git commit -m "$(printf 'Add CarController and input providers\n\nController samples input exactly once per Update and caches it for\nFixedUpdate, so the per-frame mouse delta is consumed once regardless of how\nmany physics steps land in a frame.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 7: DrivingModule

**Files:**
- Create: `Assets/_Project/Scripts/Driving/DrivingModule.cs`

**Interfaces:**
- Consumes: `CarController.Body`, `DrivePhysics` (Task 4), `DriveConfig` (Task 5), `ICarModule` (Task 2).
- Produces: `MotorCombat.Driving.DrivingModule`, a MonoBehaviour with a public `DriveConfig config` field.

- [ ] **Step 1: Write the module**

`Assets/_Project/Scripts/Driving/DrivingModule.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Driving
{
    /// <summary>
    /// Thin adapter: reads config, calls DrivePhysics, writes the result to the
    /// Rigidbody. All the maths worth testing lives in DrivePhysics.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class DrivingModule : MonoBehaviour, ICarModule
    {
        public DriveConfig config;

        CarController _car;

        void Awake()
        {
            _car = GetComponent<CarController>();
        }

        public void Tick(in CarInput input, float dt)
        {
            if (config == null) return;

            Rigidbody body = _car.Body;
            Vector3 forward = FlatForward();

            // 1. Thrust, brake and reverse.
            Vector3 force = DrivePhysics.DriveForce(
                forward,
                body.linearVelocity,
                input.throttle,
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

            // 3. Yaw, set directly. No speed gate, so turning on the spot works.
            float yawRate = DrivePhysics.YawRate(input.steer, config.turnRate);
            body.angularVelocity = Vector3.up * (yawRate * Mathf.Deg2Rad);

            // 4. Grip. Whatever sideways velocity survives is the drift.
            body.linearVelocity = DrivePhysics.ApplyGrip(
                body.linearVelocity, forward, config.lateralGripStrength, dt);
        }

        public void FrameTick(in CarInput input, float dt)
        {
            // Driving is fixed-step only.
        }

        /// <summary>
        /// The car's forward flattened onto the ground plane, so a slight pitch
        /// from a collision never leaks into steering or grip.
        /// </summary>
        Vector3 FlatForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 17 tests.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Driving/DrivingModule.cs
git commit -m "$(printf 'Add DrivingModule\n\nAdapter from DrivePhysics onto the Rigidbody: thrust, manual exponential\ndrag, direct yaw, then lateral grip. Rigidbody damping stays zero so PhysX\ndoes not stack a second decay.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 8: AimModule

**Files:**
- Create: `Assets/_Project/Scripts/Aiming/AimModule.cs`

**Interfaces:**
- Consumes: `AimMath` (Task 3), `AimConfig` (Task 5), `CarController.AimYaw` (Task 6).
- Produces: `MotorCombat.Aiming.AimModule`, a MonoBehaviour with a public `AimConfig config` field.

- [ ] **Step 1: Write the module**

`Assets/_Project/Scripts/Aiming/AimModule.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Aiming
{
    /// <summary>
    /// Turns per-frame mouse movement into the car-relative aim yaw. Runs on the
    /// frame tick because the mouse delta is a per-frame quantity.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class AimModule : MonoBehaviour, ICarModule
    {
        public AimConfig config;

        CarController _car;

        void Awake()
        {
            _car = GetComponent<CarController>();
        }

        public void FrameTick(in CarInput input, float dt)
        {
            if (config == null) return;

            _car.AimYaw = AimMath.Accumulate(
                _car.AimYaw,
                input.aimDeltaX,
                config.mouseSensitivity,
                config.coneAngleDegrees);
        }

        public void Tick(in CarInput input, float dt)
        {
            // Aiming is frame-rate driven, not fixed-step.
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 17 tests.

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Aiming/AimModule.cs
git commit -m "$(printf 'Add AimModule\n\nConsumes the per-frame mouse delta on FrameTick and writes the clamped,\ncar-relative aim yaw back to the controller.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 9: Ramming and Weapon seams

**Files:**
- Create: `Assets/_Project/Scripts/Ramming/CarCollisionEvent.cs`
- Create: `Assets/_Project/Scripts/Ramming/RammingModule.cs`
- Create: `Assets/_Project/Scripts/Ramming/MotorCombat.Ramming.asmdef`
- Create: `Assets/_Project/Scripts/Weapons/IWeapon.cs`
- Create: `Assets/_Project/Scripts/Weapons/WeaponModule.cs`
- Create: `Assets/_Project/Scripts/Weapons/MotorCombat.Weapons.asmdef`

**Interfaces:**
- Consumes: `ICarModule`, `CarController` (Tasks 2, 6).
- Produces: `MotorCombat.Ramming.CarCollisionEvent` (fields `other`, `point`, `normal`, `relativeSpeed`), `RammingModule` with `event System.Action<CarCollisionEvent> Collided`; `MotorCombat.Weapons.IWeapon` with `void Fire(Vector3 origin, Vector3 direction)`, `WeaponModule` with `void Equip(IWeapon)` and `void Fire()`.

These ship with real interfaces and no behaviour. Implementing combat later means filling in a body, not re-architecting.

- [ ] **Step 1: Create the Ramming assembly definition**

`Assets/_Project/Scripts/Ramming/MotorCombat.Ramming.asmdef`:

```json
{
    "name": "MotorCombat.Ramming",
    "rootNamespace": "MotorCombat.Ramming",
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

- [ ] **Step 2: Create CarCollisionEvent**

`Assets/_Project/Scripts/Ramming/CarCollisionEvent.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// Everything a future damage model needs about one impact. Raised today,
    /// consumed by nobody.
    /// </summary>
    public struct CarCollisionEvent
    {
        /// <summary>The other car, or null when the impact was with the wall or ground.</summary>
        public CarController other;

        public Vector3 point;
        public Vector3 normal;

        /// <summary>Closing speed along the contact normal, in m/s. Always positive.</summary>
        public float relativeSpeed;
    }
}
```

- [ ] **Step 3: Create RammingModule**

`Assets/_Project/Scripts/Ramming/RammingModule.cs`:

```csharp
using System;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Ramming
{
    /// <summary>
    /// SEAM ONLY. Detects impacts and raises them. Applies no damage, no
    /// knockback and no status — Unity's own collision response is the entire
    /// behaviour for now.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class RammingModule : MonoBehaviour, ICarModule
    {
        [Tooltip("Log impacts to the console. Useful while tuning collision feel.")]
        public bool logImpacts;

        public event Action<CarCollisionEvent> Collided;

        void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;

            ContactPoint contact = collision.GetContact(0);

            var impact = new CarCollisionEvent
            {
                other = collision.collider.GetComponentInParent<CarController>(),
                point = contact.point,
                normal = contact.normal,
                relativeSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal))
            };

            if (logImpacts)
            {
                string target = impact.other != null ? impact.other.name : "environment";
                Debug.Log($"[Ram] {name} hit {target} at {impact.relativeSpeed:F1} m/s");
            }

            Collided?.Invoke(impact);
        }

        public void Tick(in CarInput input, float dt) { }
        public void FrameTick(in CarInput input, float dt) { }
    }
}
```

- [ ] **Step 4: Create the Weapons assembly definition**

`Assets/_Project/Scripts/Weapons/MotorCombat.Weapons.asmdef`:

```json
{
    "name": "MotorCombat.Weapons",
    "rootNamespace": "MotorCombat.Weapons",
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

- [ ] **Step 5: Create IWeapon**

`Assets/_Project/Scripts/Weapons/IWeapon.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Weapons
{
    /// <summary>
    /// SEAM ONLY. Nothing implements this yet.
    /// </summary>
    public interface IWeapon
    {
        string DisplayName { get; }

        /// <summary>Fire along a world-space direction from a world-space origin.</summary>
        void Fire(Vector3 origin, Vector3 direction);
    }
}
```

- [ ] **Step 6: Create WeaponModule**

`Assets/_Project/Scripts/Weapons/WeaponModule.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.Weapons
{
    /// <summary>
    /// SEAM ONLY. Holds weapons and knows where the car is aiming. Fires nothing,
    /// because no IWeapon exists yet.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class WeaponModule : MonoBehaviour, ICarModule
    {
        readonly List<IWeapon> _weapons = new List<IWeapon>();

        CarController _car;

        void Awake()
        {
            _car = GetComponent<CarController>();
        }

        public void Equip(IWeapon weapon)
        {
            if (weapon != null) _weapons.Add(weapon);
        }

        /// <summary>Fires every equipped weapon along the car's current aim.</summary>
        public void Fire()
        {
            Vector3 origin = transform.position;
            Vector3 direction = _car.AimDirection;

            for (int i = 0; i < _weapons.Count; i++)
            {
                _weapons[i].Fire(origin, direction);
            }
        }

        public void Tick(in CarInput input, float dt) { }
        public void FrameTick(in CarInput input, float dt) { }
    }
}
```

- [ ] **Step 7: Verify it compiles**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 17 tests.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Ramming Assets/_Project/Scripts/Weapons
git commit -m "$(printf 'Add ramming and weapon seams\n\nReal interfaces, no behaviour. RammingModule raises CarCollisionEvent with\nimpact data and nothing consumes it; WeaponModule holds IWeapons and fires\nalong the aim direction.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 10: Arena mesh generation with tests

**Files:**
- Create: `Assets/_Project/Scripts/Arena/ArenaMeshBuilder.cs`
- Create: `Assets/_Project/Scripts/Arena/ArenaBuilder.cs`
- Modify: `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef` (add `MotorCombat.Arena`)
- Test: `Assets/_Project/Tests/EditMode/ArenaMeshBuilderTests.cs`

**Interfaces:**
- Consumes: `ArenaConfig` (Task 5).
- Produces: `ArenaMeshBuilder.BuildDisc(float radius, int segments) -> Mesh`, `ArenaMeshBuilder.BuildRing(float radius, float height, int segments) -> Mesh`, `ArenaBuilder.RadiusFor(ArenaConfig, float carLength) -> float`, `ArenaBuilder.Build(ArenaConfig config, float carLength, Transform parent) -> GameObject`.

- [ ] **Step 1: Add Arena to the test assembly references**

In `Assets/_Project/Tests/EditMode/MotorCombat.Tests.EditMode.asmdef`, change `references` to:

```json
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "MotorCombat.Core",
        "MotorCombat.Aiming",
        "MotorCombat.Driving",
        "MotorCombat.Arena"
    ],
```

- [ ] **Step 2: Write the failing tests**

`Assets/_Project/Tests/EditMode/ArenaMeshBuilderTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using MotorCombat.Arena;

namespace MotorCombat.Tests
{
    public class ArenaMeshBuilderTests
    {
        [Test]
        public void Disc_HasCentreVertexPlusOnePerSegment()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32);
            Assert.AreEqual(33, mesh.vertexCount, "one centre vertex plus one per segment");
            Assert.AreEqual(32 * 3, mesh.triangles.Length, "one triangle per segment");
        }

        [Test]
        public void Disc_RimVerticesSitOnTheRadius()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 32);
            var vertices = mesh.vertices;

            // vertex 0 is the centre; the rest are the rim
            for (int i = 1; i < vertices.Length; i++)
            {
                float distance = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                Assert.AreEqual(45f, distance, 1e-3f, $"vertex {i} off the rim");
            }
        }

        [Test]
        public void Disc_IsFlatAtYZero()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 16);
            foreach (var v in mesh.vertices)
            {
                Assert.AreEqual(0f, v.y, 1e-5f);
            }
        }

        [Test]
        public void Ring_HasABottomAndTopVertexPerColumn()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32);
            Assert.AreEqual((32 + 1) * 2, mesh.vertexCount, "a bottom and top vertex per column, seam column duplicated");
            Assert.AreEqual(32 * 6, mesh.triangles.Length, "two triangles per segment");
        }

        [Test]
        public void Ring_SpansFromGroundToWallHeight()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 32);
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var v in mesh.vertices)
            {
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }

            Assert.AreEqual(0f, minY, 1e-5f);
            Assert.AreEqual(2.5f, maxY, 1e-5f);
        }

        [Test]
        public void Ring_VerticesSitOnTheRadius()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 24);
            foreach (var v in mesh.vertices)
            {
                float distance = new Vector2(v.x, v.z).magnitude;
                Assert.AreEqual(45f, distance, 1e-3f);
            }
        }

        /// <summary>
        /// Winding, not just geometry. Unity front faces are clockwise seen from
        /// the front, which makes cross(b-a, c-a) point along the face normal. A
        /// wall wound the wrong way still collides correctly, so nothing else in
        /// this suite would catch it — you would only notice the arena looking
        /// hollow at runtime.
        /// </summary>
        [Test]
        public void Ring_TrianglesFaceInward()
        {
            var mesh = ArenaMeshBuilder.BuildRing(45f, 2.5f, 24);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                Vector3 outward = new Vector3(a.x, 0f, a.z);

                Assert.Less(Vector3.Dot(faceNormal, outward), 0f,
                    $"triangle at index {i} faces away from the arena centre");
            }
        }

        [Test]
        public void Disc_TrianglesFaceUp()
        {
            var mesh = ArenaMeshBuilder.BuildDisc(45f, 24);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);

                Assert.Greater(Vector3.Dot(faceNormal, Vector3.up), 0f,
                    $"triangle at index {i} faces downward");
            }
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `unity test . --mode EditMode --filter ArenaMeshBuilderTests --timeout 900`
Expected: FAIL — compile error, `ArenaMeshBuilder` does not exist.

- [ ] **Step 4: Write ArenaMeshBuilder**

`Assets/_Project/Scripts/Arena/ArenaMeshBuilder.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Generates the arena's geometry. A real circular mesh rather than a ring of
    /// box colliders, because a car slides along this wall constantly and box
    /// seams catch.
    /// </summary>
    public static class ArenaMeshBuilder
    {
        /// <summary>Flat disc on the XZ plane at y = 0, normals up.</summary>
        public static Mesh BuildDisc(float radius, int segments)
        {
            segments = Mathf.Max(3, segments);

            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices[i + 1] = new Vector3(x * radius, 0f, z * radius);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2((x + 1f) * 0.5f, (z + 1f) * 0.5f);

                int next = (i + 1) % segments;
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "ArenaGround" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Open-ended cylinder wall, inward facing, from y = 0 to y = height.
        /// The seam column is duplicated so UVs do not wrap.
        /// </summary>
        public static Mesh BuildRing(float radius, float height, int segments)
        {
            segments = Mathf.Max(3, segments);

            int columns = segments + 1;   // duplicate the seam column
            var vertices = new Vector3[columns * 2];
            var normals = new Vector3[columns * 2];
            var uvs = new Vector2[columns * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < columns; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                int bottom = i * 2;
                int top = bottom + 1;

                vertices[bottom] = new Vector3(x * radius, 0f, z * radius);
                vertices[top] = new Vector3(x * radius, height, z * radius);

                // Inward facing: the players are inside the cylinder.
                var inward = new Vector3(-x, 0f, -z);
                normals[bottom] = inward;
                normals[top] = inward;

                uvs[bottom] = new Vector2(t, 0f);
                uvs[top] = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int bottom = i * 2;
                int top = bottom + 1;
                int nextBottom = bottom + 2;
                int nextTop = bottom + 3;

                // Wound so the front face points at the arena centre. Unity
                // front faces are clockwise seen from the front, so reversing
                // these two triples would leave the wall invisible from inside
                // (collision would still work, which makes it easy to miss).
                triangles[i * 6 + 0] = bottom;
                triangles[i * 6 + 1] = nextTop;
                triangles[i * 6 + 2] = top;

                triangles[i * 6 + 3] = bottom;
                triangles[i * 6 + 4] = nextBottom;
                triangles[i * 6 + 5] = nextTop;
            }

            var mesh = new Mesh { name = "ArenaWall" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `unity test . --mode EditMode --filter ArenaMeshBuilderTests --timeout 900`
Expected: PASS, 8 tests.

- [ ] **Step 6: Write ArenaBuilder**

`Assets/_Project/Scripts/Arena/ArenaBuilder.cs`:

```csharp
using UnityEngine;

namespace MotorCombat.Arena
{
    /// <summary>
    /// Turns an ArenaConfig plus a car length into actual GameObjects.
    /// </summary>
    public static class ArenaBuilder
    {
        public static float RadiusFor(ArenaConfig config, float carLength)
        {
            return config.radiusInCarLengths * carLength;
        }

        /// <summary>
        /// Builds ground and wall under a new "Arena" GameObject and returns it.
        /// </summary>
        public static GameObject Build(ArenaConfig config, float carLength, Transform parent = null)
        {
            float radius = RadiusFor(config, carLength);

            var root = new GameObject("Arena");
            if (parent != null) root.transform.SetParent(parent, false);

            CreatePiece(
                "Ground",
                ArenaMeshBuilder.BuildDisc(radius, config.segments),
                new Color(0.22f, 0.24f, 0.27f),
                root.transform);

            CreatePiece(
                "Wall",
                ArenaMeshBuilder.BuildRing(radius, config.wallHeight, config.segments),
                new Color(0.35f, 0.30f, 0.28f),
                root.transform);

            return root;
        }

        static void CreatePiece(string name, Mesh mesh, Color colour, Transform parent)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);

            piece.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(colour);

            // Non-convex mesh colliders are legal on static geometry, which this is.
            var collider = piece.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
        }

        static Material CreateMaterial(Color colour)
        {
            // URP's lit shader. Falls back to the built-in standard shader if the
            // project is ever moved off URP.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "ArenaMaterial" };
            material.color = colour;
            return material;
        }
    }
}
```

- [ ] **Step 7: Run the whole suite**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 25 tests.

- [ ] **Step 8: Commit**

```bash
git add Assets/_Project/Scripts/Arena Assets/_Project/Tests
git commit -m "$(printf 'Add procedural circular arena\n\nDisc ground and inward-facing ring wall generated from ArenaConfig, with\ntests pinning topology and rim radius. Mesh rather than box segments so a\nsliding car does not catch on seams.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 11: CarFactory, CameraRig, CrosshairHUD and GameBootstrap

**Files:**
- Create: `Assets/_Project/Scripts/Cars/CarFactory.cs`
- Create: `Assets/_Project/Scripts/Cameras/CameraRig.cs`
- Create: `Assets/_Project/Scripts/HUD/CrosshairHUD.cs`
- Create: `Assets/_Project/Scripts/HUD/MotorCombat.HUD.asmdef`
- Create: `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`
- Create: `Assets/_Project/Scripts/Bootstrap/MotorCombat.Bootstrap.asmdef`
- Modify: `Assets/_Project/Scripts/Cars/MotorCombat.Cars.asmdef`
- Modify: `Assets/_Project/Scripts/Cameras/MotorCombat.Cameras.asmdef`
- Modify: `Assets/_Project/Editor/MotorCombat.EditorTools.asmdef`

**Interfaces:**
- Consumes: everything from Tasks 5–10.
- Produces: `CarFactory.Spawn(CarDefinition definition, Vector3 position, Quaternion rotation, IInputProvider provider, Color colour) -> CarController` and `CarFactory.DriverAnchorName`; `CameraRig` with `void Follow(CarController)` and public `CameraConfig config`; `CrosshairHUD` with public `CarController target` and `Camera view`; `GameBootstrap` MonoBehaviour with public `arenaConfig`, `carDefinition`, `cameraRig`.

- [ ] **Step 1: Update the Cars assembly to reference Ramming and Weapons**

In `Assets/_Project/Scripts/Cars/MotorCombat.Cars.asmdef`, change `references` to:

```json
    "references": [
        "MotorCombat.Core",
        "MotorCombat.Driving",
        "MotorCombat.Aiming",
        "MotorCombat.Ramming",
        "MotorCombat.Weapons"
    ],
```

- [ ] **Step 2: Write CarFactory**

`Assets/_Project/Scripts/Cars/CarFactory.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Driving;
using MotorCombat.Aiming;
using MotorCombat.Ramming;
using MotorCombat.Weapons;

namespace MotorCombat.Cars
{
    /// <summary>
    /// Builds a car from a definition. A scaled box today; swap the visual for a
    /// real model later without touching any module.
    /// </summary>
    public static class CarFactory
    {
        public const string DriverAnchorName = "DriverAnchor";

        public static CarController Spawn(
            CarDefinition definition,
            Vector3 position,
            Quaternion rotation,
            IInputProvider provider,
            Color colour)
        {
            var car = new GameObject(definition.name);
            car.transform.SetPositionAndRotation(position, rotation);

            // Body: a box of the configured dimensions. Z is length (forward).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(car.transform, false);
            body.transform.localScale = new Vector3(definition.width, definition.height, definition.length);
            Paint(body, colour);

            // The primitive brings its own collider on the child. Collision must
            // live on the root next to the Rigidbody, so drop the child's.
            Object.DestroyImmediate(body.GetComponent<BoxCollider>());
            var collider = car.AddComponent<BoxCollider>();
            collider.size = new Vector3(definition.width, definition.height, definition.length);

            // Nose marker, so facing is readable on a featureless box.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(car.transform, false);
            nose.transform.localScale = new Vector3(
                definition.width * 0.25f, definition.height * 0.25f, definition.length * 0.1f);
            nose.transform.localPosition = new Vector3(
                0f, definition.height * 0.4f, definition.length * 0.5f);
            Object.DestroyImmediate(nose.GetComponent<BoxCollider>());
            Paint(nose, Color.white);

            var anchor = new GameObject(DriverAnchorName);
            anchor.transform.SetParent(car.transform, false);
            anchor.transform.localPosition = definition.driverAnchorOffset;

            var rigidbody = car.AddComponent<Rigidbody>();
            rigidbody.mass = definition.mass;

            var controller = car.AddComponent<CarController>();

            car.AddComponent<DrivingModule>().config = definition.driveConfig;
            car.AddComponent<AimModule>().config = definition.aimConfig;
            car.AddComponent<RammingModule>();
            car.AddComponent<WeaponModule>();

            controller.Bind(provider);
            return controller;
        }

        static void Paint(GameObject target, Color colour)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.color = colour;
            target.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
```

`CarController` collects its modules in `Start`, which runs after this whole method has finished, so the modules added on the lines above are all picked up.

- [ ] **Step 3: Update the Cameras assembly to reference Cars**

In `Assets/_Project/Scripts/Cameras/MotorCombat.Cameras.asmdef`, change `references` to:

```json
    "references": ["MotorCombat.Core", "MotorCombat.Cars"],
```

- [ ] **Step 4: Write CameraRig**

`Assets/_Project/Scripts/Cameras/CameraRig.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;
using MotorCombat.Cars;

namespace MotorCombat.Cameras
{
    /// <summary>
    /// First or third person, chosen by config. Camera YAW is rigid to the
    /// chassis in both modes and is never smoothed: lagging it would drag the
    /// crosshair across the screen during turns, which is exactly the thing the
    /// aiming design guarantees will not happen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public CameraConfig config;

        CarController _target;
        Transform _driverAnchor;
        Camera _camera;

        void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void Follow(CarController target)
        {
            _target = target;
            _driverAnchor = target != null
                ? target.transform.Find(CarFactory.DriverAnchorName)
                : null;
        }

        void LateUpdate()
        {
            if (_target == null || config == null) return;

            if (config.mode == CameraMode.FirstPerson)
            {
                ApplyFirstPerson();
            }
            else
            {
                ApplyThirdPerson();
            }
        }

        void ApplyFirstPerson()
        {
            Transform anchor = _driverAnchor != null ? _driverAnchor : _target.transform;

            _camera.fieldOfView = config.firstPersonFov;
            transform.position = anchor.position;
            transform.rotation = Quaternion.Euler(0f, ChassisYaw(), 0f);
        }

        void ApplyThirdPerson()
        {
            float yaw = ChassisYaw();
            Quaternion flatRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 offset = flatRotation * new Vector3(0f, 0f, -config.thirdPersonDistance)
                             + Vector3.up * config.thirdPersonHeight;

            _camera.fieldOfView = config.thirdPersonFov;
            transform.position = _target.transform.position + offset;
            transform.rotation = Quaternion.Euler(config.thirdPersonPitch, yaw, 0f);
        }

        float ChassisYaw()
        {
            return _target.transform.eulerAngles.y;
        }
    }
}
```

- [ ] **Step 5: Create the HUD assembly definition**

`Assets/_Project/Scripts/HUD/MotorCombat.HUD.asmdef`:

```json
{
    "name": "MotorCombat.HUD",
    "rootNamespace": "MotorCombat.HUD",
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

- [ ] **Step 6: Write CrosshairHUD**

`Assets/_Project/Scripts/HUD/CrosshairHUD.cs`:

```csharp
using UnityEngine;
using MotorCombat.Core;

namespace MotorCombat.HUD
{
    /// <summary>
    /// Draws the crosshair by projecting the aim ray through the camera, NOT by
    /// lerping across screen X. Screen position is a tangent function of angle,
    /// so a linear mapping would visibly drift from where shots actually go near
    /// the screen edges.
    /// </summary>
    public class CrosshairHUD : MonoBehaviour
    {
        public CarController target;
        public Camera view;

        [Tooltip("How far along the aim ray the crosshair is projected, in metres.")]
        public float projectionDistance = 40f;

        [Tooltip("Crosshair arm length in pixels.")]
        public float size = 10f;

        [Tooltip("Pixels of margin when the aim leaves the viewport.")]
        public float edgeMargin = 24f;

        Texture2D _pixel;

        void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        void OnGUI()
        {
            if (target == null || view == null) return;

            Vector3 worldPoint = target.transform.position + target.AimDirection * projectionDistance;
            Vector3 screenPoint = view.WorldToScreenPoint(worldPoint);

            bool behindCamera = screenPoint.z < 0f;
            if (behindCamera)
            {
                // Mirror it so a point behind the camera pins to the correct side.
                screenPoint.x = Screen.width - screenPoint.x;
            }

            float clampedX = Mathf.Clamp(screenPoint.x, edgeMargin, Screen.width - edgeMargin);
            float clampedY = Mathf.Clamp(screenPoint.y, edgeMargin, Screen.height - edgeMargin);

            bool clamped = behindCamera
                           || !Mathf.Approximately(clampedX, screenPoint.x)
                           || !Mathf.Approximately(clampedY, screenPoint.y);

            // GUI space has y growing downward; screen space has it growing up.
            float x = clampedX;
            float y = Screen.height - clampedY;

            GUI.color = clamped ? new Color(1f, 0.6f, 0.2f) : Color.white;

            // Horizontal arm
            GUI.DrawTexture(new Rect(x - size, y - 1f, size * 2f, 2f), _pixel);
            // Vertical arm
            GUI.DrawTexture(new Rect(x - 1f, y - size, 2f, size * 2f), _pixel);

            GUI.color = Color.white;
        }
    }
}
```

- [ ] **Step 7: Create the Bootstrap assembly definition**

`Assets/_Project/Scripts/Bootstrap/MotorCombat.Bootstrap.asmdef`:

```json
{
    "name": "MotorCombat.Bootstrap",
    "rootNamespace": "MotorCombat.Bootstrap",
    "references": [
        "MotorCombat.Core",
        "MotorCombat.Controls",
        "MotorCombat.Driving",
        "MotorCombat.Aiming",
        "MotorCombat.Ramming",
        "MotorCombat.Weapons",
        "MotorCombat.Cars",
        "MotorCombat.Arena",
        "MotorCombat.Cameras",
        "MotorCombat.HUD"
    ],
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

- [ ] **Step 8: Write GameBootstrap**

`Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs`:

```csharp
using UnityEngine;
using MotorCombat.Controls;
using MotorCombat.Cars;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.HUD;

namespace MotorCombat.Bootstrap
{
    /// <summary>
    /// The only thing authored into the Arena scene besides a light and a camera.
    /// Builds the arena from config, spawns the cars, and wires the camera.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Configs")]
        public ArenaConfig arenaConfig;
        public CarDefinition carDefinition;

        [Header("Scene references")]
        public CameraRig cameraRig;

        [Header("Dummy car")]
        [Tooltip("How far ahead of the player the stationary dummy spawns, in car lengths.")]
        public float dummyDistanceInCarLengths = 4f;

        void Start()
        {
            if (!Validate()) return;

            ArenaBuilder.Build(arenaConfig, carDefinition.length);

            float halfHeight = carDefinition.height * 0.5f;

            var playerInput = gameObject.AddComponent<LocalInputProvider>();
            var player = CarFactory.Spawn(
                carDefinition,
                new Vector3(0f, halfHeight, 0f),
                Quaternion.identity,
                playerInput,
                new Color(0.20f, 0.55f, 0.90f));
            player.name = "PlayerCar";

            var dummyInput = gameObject.AddComponent<NullInputProvider>();
            var dummy = CarFactory.Spawn(
                carDefinition,
                new Vector3(0f, halfHeight, dummyDistanceInCarLengths * carDefinition.length),
                Quaternion.identity,
                dummyInput,
                new Color(0.85f, 0.35f, 0.25f));
            dummy.name = "DummyCar";

            cameraRig.Follow(player);

            var crosshair = cameraRig.gameObject.GetComponent<CrosshairHUD>();
            if (crosshair == null)
            {
                crosshair = cameraRig.gameObject.AddComponent<CrosshairHUD>();
            }
            crosshair.target = player;
            crosshair.view = cameraRig.GetComponent<Camera>();
        }

        bool Validate()
        {
            if (arenaConfig == null) { Debug.LogError("[MotorCombat] GameBootstrap.arenaConfig is not assigned."); return false; }
            if (carDefinition == null) { Debug.LogError("[MotorCombat] GameBootstrap.carDefinition is not assigned."); return false; }
            if (carDefinition.driveConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.driveConfig is not assigned."); return false; }
            if (carDefinition.aimConfig == null) { Debug.LogError("[MotorCombat] CarDefinition.aimConfig is not assigned."); return false; }
            if (cameraRig == null) { Debug.LogError("[MotorCombat] GameBootstrap.cameraRig is not assigned."); return false; }
            return true;
        }
    }
}
```

- [ ] **Step 9: Update the EditorTools assembly references**

In `Assets/_Project/Editor/MotorCombat.EditorTools.asmdef`, change `references` to:

```json
    "references": [
        "MotorCombat.Core",
        "MotorCombat.Controls",
        "MotorCombat.Driving",
        "MotorCombat.Aiming",
        "MotorCombat.Ramming",
        "MotorCombat.Weapons",
        "MotorCombat.Arena",
        "MotorCombat.Cameras",
        "MotorCombat.Cars",
        "MotorCombat.HUD",
        "MotorCombat.Bootstrap"
    ],
```

- [ ] **Step 10: Verify it compiles and the suite still passes**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 25 tests.

- [ ] **Step 11: Commit**

```bash
git add Assets/_Project/Scripts Assets/_Project/Editor
git commit -m "$(printf 'Add car factory, camera rig, crosshair and bootstrap\n\nCamera yaw is rigid to the chassis in both modes so the crosshair holds its\nscreen position while steering. Crosshair is projected through the camera\nrather than lerped across screen X.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 12: Generate the Arena scene

**Files:**
- Create: `Assets/_Project/Editor/ArenaSceneBuilder.cs`
- Create: `Assets/_Project/Scenes/Arena.unity` (generated)
- Modify: `ProjectSettings/EditorBuildSettings.asset` (generated)

**Interfaces:**
- Consumes: `GameBootstrap`, `CameraRig`, and the config assets.
- Produces: a scene that plays.

- [ ] **Step 1: Write ArenaSceneBuilder**

`Assets/_Project/Editor/ArenaSceneBuilder.cs`:

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MotorCombat.Arena;
using MotorCombat.Cameras;
using MotorCombat.Cars;
using MotorCombat.Bootstrap;

namespace MotorCombat.EditorTools
{
    /// <summary>
    /// Generates Arena.unity from code, so the scene is reproducible and its
    /// construction is reviewable in a diff rather than as opaque YAML.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        const string SceneDir = "Assets/_Project/Scenes";
        const string ScenePath = SceneDir + "/Arena.unity";
        const string ConfigDir = "Assets/_Project/Configs";

        [MenuItem("Motor Combat/Rebuild Arena Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(SceneDir);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Light ---
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, 138f, 0f);

            // --- Camera ---
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<AudioListener>();

            var rig = cameraObject.AddComponent<CameraRig>();
            rig.config = Load<CameraConfig>("CameraConfig");

            // --- Bootstrap ---
            var bootstrapObject = new GameObject("GameBootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.arenaConfig = Load<ArenaConfig>("ArenaConfig");
            bootstrap.carDefinition = Load<CarDefinition>("CarDefinition");
            bootstrap.cameraRig = rig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log("[MotorCombat] Arena scene written to " + ScenePath);
        }

        static T Load<T>(string assetName) where T : ScriptableObject
        {
            string path = $"{ConfigDir}/{assetName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"[MotorCombat] Missing config asset at {path}. " +
                               "Run Motor Combat > Create Default Configs first.");
            }
            return asset;
        }

        static void RegisterInBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
```

- [ ] **Step 2: Generate the scene headlessly**

```bash
unity run . -- -batchmode -nographics -quit \
  -executeMethod MotorCombat.EditorTools.ArenaSceneBuilder.BuildScene
ls Assets/_Project/Scenes
```

Expected: `Arena.unity` and `Arena.unity.meta` exist.

- [ ] **Step 3: Verify project integrity**

Run: `unity projects verify .`
Expected: no integrity problems.

- [ ] **Step 4: Run the full test suite**

Run: `unity test . --mode EditMode --timeout 900`
Expected: PASS, 25 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Editor Assets/_Project/Scenes ProjectSettings/EditorBuildSettings.asset
git commit -m "$(printf 'Generate the Arena scene from code\n\nArenaSceneBuilder writes Arena.unity with a light, a CameraRig camera and a\nGameBootstrap wired to the config assets. Everything else is built at\nruntime from ArenaConfig.\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

### Task 13: Manual play verification and documentation

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: the finished skeleton.
- Produces: a verified, documented project.

- [ ] **Step 1: Open the project**

```bash
unity project open .
```

Wait for the editor to finish importing, then open `Assets/_Project/Scenes/Arena.unity` and press Play.

- [ ] **Step 2: Walk the acceptance checklist**

Confirm each of these by playing. Report the result of every line — do not summarise as "works".

| # | Check | Expected |
|---|---|---|
| 1 | Press W | Car accelerates forward, settles at a top speed |
| 2 | Release W | Car coasts to a stop rather than stopping dead |
| 3 | Press S while moving forward | Car brakes noticeably harder than coasting |
| 4 | Hold S after stopping | Car reverses, slower than it drives forward |
| 5 | Press A / D while stationary | Car rotates on the spot |
| 6 | Turn hard at speed | Car's nose leads its travel direction — visible drift |
| 7 | Move mouse right | Crosshair moves right, stops at the cone edge |
| 8 | Move mouse left | Crosshair moves left, stops at the cone edge |
| 9 | Steer while holding the mouse still | Crosshair does NOT move on screen |
| 10 | Drive into the wall | Car is stopped and slides along it without catching |
| 11 | Drive into the dummy car | Dummy is shoved, no damage or destruction |
| 12 | Set `CameraConfig.mode` to `FirstPerson`, press Play | View is from inside the car; all of the above still holds |

- [ ] **Step 3: Record the tuning observations**

Note in your report which of the default numbers felt wrong (top speed, turn rate, grip, sensitivity, camera distance). Do NOT change them — they are the user's to tune. Just report.

- [ ] **Step 4: Update the README**

Replace the contents of `README.md` with the following. Note the nested fence: write the shell blocks below as ordinary triple-backtick blocks in the final file.

    # motor-combat-3D

    3D car brawl game, Unity 6 (`6000.6.0f1`, URP).

    ## Running it

    Open the project in Unity 6000.6.0f1, open `Assets/_Project/Scenes/Arena.unity`, press Play.

    - **WASD** — drive. S brakes while moving forward, reverses once stopped. A/D turn, including on the spot.
    - **Mouse** — aim within a cone. The cursor is locked; the crosshair holds its screen position while you steer.
    - Switch first/third person by changing `mode` on `Assets/_Project/Configs/CameraConfig.asset`.

    ## Tuning

    Every number lives in a ScriptableObject under `Assets/_Project/Configs/`:
    `DriveConfig`, `AimConfig`, `ArenaConfig`, `CameraConfig`, `CarDefinition`.
    Arena radius is `ArenaConfig.radiusInCarLengths * CarDefinition.length`, so changing the
    car length rescales the arena.

    All decay values are **rates in 1/s**, applied as `exp(-rate * dt)` so handling never
    changes with the physics timestep.

    ## Tests

    ```bash
    unity test . --mode EditMode
    ```

    ## Regenerating

    ```bash
    unity run . -- -batchmode -nographics -quit -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
    ```

    ```bash
    unity run . -- -batchmode -nographics -quit -executeMethod MotorCombat.EditorTools.ArenaSceneBuilder.BuildScene
    ```

    ## Status

    Skeleton only. No combat, damage, health, respawn, menus, lobby, netcode, audio or real art.
    `RammingModule` and `WeaponModule` are empty seams.

    - Design: [`docs/superpowers/specs/2026-09-12-motor-combat-3d-skeleton-design.md`](docs/superpowers/specs/2026-09-12-motor-combat-3d-skeleton-design.md)
    - Plan: [`docs/superpowers/plans/2026-09-12-motor-combat-3d-skeleton.md`](docs/superpowers/plans/2026-09-12-motor-combat-3d-skeleton.md)

    `E:\Work\motor-combat-MOBA` is a UX reference only (a separate 2D web game). No code,
    tuning values, or gameplay rules are taken from it.

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "$(printf 'Document how to run and tune the skeleton\n\nCo-Authored-By: Claude Opus 5 <noreply@anthropic.com>')"
```

---

## Verification Summary

At the end of every task: `unity test . --mode EditMode --timeout 900` must pass.

Final expected state:

| Signal | Expected |
|---|---|
| `unity projects verify .` | No integrity problems |
| `unity test . --mode EditMode` | 25 tests, all passing |
| Play in `Arena.unity` | All 12 checklist rows in Task 13 pass |
| `git status` | Clean |
