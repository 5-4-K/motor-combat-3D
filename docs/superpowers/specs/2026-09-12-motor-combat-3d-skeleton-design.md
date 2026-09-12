# Motor Combat 3D — Skeleton Design

**Date:** 2026-09-12
**Status:** Implemented. Two sections were revised after play-testing — see §5 steps 3 and 4,
which record where this design was wrong and why.
**Scope:** Create the Unity project and the in-game skeleton. No menus, no combat, no netcode.

## 1. Purpose

Stand up a Unity 6 project containing one playable Arena scene: a circular arena, a box
car, first- and third-person cameras, WASD driving with arcade physics, and mouse aiming
within a configurable cone. The point is to make driving and camera feel testable as early
as possible, and to place the module seams that combat will later fill in.

`E:\Work\motor-combat-MOBA` is a reference for screen flow and UX only. It is a
TypeScript/Colyseus/Phaser 2D web game; no code, tuning values, or gameplay rules are taken
from it. Its UI/UX (homepage, lobby, practice mode, car select) will be revisited in a
later session and is out of scope here.

## 2. Decisions

| Decision | Choice | Reason |
|---|---|---|
| Editor | 6000.6.0f1 | Only version installed; current Unity 6 |
| Template | `com.unity.template.urp-blank` | Cached locally; HDRP overkill, Built-in legacy |
| Input | Input System package | Required for clean mouse-delta sampling |
| Networking | Not implemented; input seam only | Keeps feel-tuning fast, makes netcode a drop-in later |
| Module wiring | MonoBehaviour modules with pure-function cores | Inspector-tunable *and* unit-testable |
| Physics substrate | `Rigidbody` + `AddForce`, no `WheelCollider` | Wheel colliders simulate suspension/tyre slip we do not want, and fight turn-in-place |
| Yaw | Direct angular velocity | Crisp arcade response; makes turn-in-place deliberate |
| Drift | Constant lateral grip rate | One knob; drift magnitude scales with speed naturally |
| Reverse | Brake, then reverse below a speed epsilon | Backing out of a wall pin matters in a brawler |
| Arena edge | Solid wall | Keeps play contained; gives ramming something to slam against |
| Aim mapping | Locked cursor, accumulate delta, clamp | Decouples cone angle from camera FOV; identical in both camera modes |
| Camera follow | Chassis heading | Satisfies the crosshair invariant; makes the cone limit visible on screen |

## 3. Repository layout

The Unity project lives at the repository root. `Assets/`, `Packages/` and
`ProjectSettings/` sit directly under `E:\Work\motor-combat-3D`; the existing `.gitignore`
is the root-level Unity template and already expects this.

```
Assets/_Project/
  Scenes/     Arena.unity                 <- the only scene
  Configs/    *.asset                     <- ScriptableObject tuning assets
  Prefabs/    Car.prefab
  Materials/  flat URP lit placeholder materials
  Scripts/
    Core/       CarInput, IInputProvider, ICarModule, CarController
    Controls/   LocalInputProvider, NullInputProvider
    Driving/    DrivingModule, DrivePhysics (pure), DriveConfig
    Aiming/     AimModule,     AimMath     (pure), AimConfig
    Ramming/    RammingModule, CarCollisionEvent      <- seam only
    Weapons/    IWeapon, WeaponModule                 <- seam only
    Cars/       CarDefinition, CarFactory
    Arena/      ArenaConfig, ArenaBuilder, ArenaMeshBuilder
    Cameras/    CameraRig, CameraConfig
    HUD/        CrosshairHUD
    Bootstrap/  GameBootstrap
  Editor/
    ArenaSceneBuilder.cs                   <- generates Arena.unity from code
  Tests/
    EditMode/   DrivePhysicsTests, AimMathTests
```

The `_Project` prefix keeps first-party code sorted above imported asset packages once real
car models arrive.

**One assembly definition per script folder.** This makes modularity enforced rather than
aspirational: a reference from `Driving` to `Weapons` becomes a compile error instead of
slow architectural drift. The cost is that each new script must live in the correct folder.

Two folder names differ from the obvious choice, both to avoid a
namespace-versus-type collision: `Cameras/` rather than `CameraRig/` (which would put a
`CameraRig` class inside a `MotorCombat.CameraRig` namespace), and `Controls/` rather than
`Input/` (where the identifier `Input` would resolve to the namespace).

Every gameplay assembly references `MotorCombat.Core` and nothing else, except
`MotorCombat.Cars` and `MotorCombat.Bootstrap`, which compose. `Editor/` and `Tests/` carry
their own asmdefs — the editor one marked Editor-platform-only, the test one referencing the
assemblies under test plus the `UnityEngine.TestRunner` / `UnityEditor.TestRunner`
precompiled references, with `nunit.framework.dll` as a precompiled reference and the
`UNITY_INCLUDE_TESTS` define constraint so tests never ship in a player build.

### .gitignore

The standard Unity template already covers `Library/`, `Temp/`, `Logs/`, `obj/`, and the
generated `*.csproj`/`*.sln` files, and nothing in it excludes `.claude/`. Explicit
negations are added anyway as a guard against a future template refresh:

```
!.claude/skills/
!.claude/settings.json
!.claude/hooks/
```

## 4. Architecture

```
LocalInputProvider  -- implements IInputProvider
        |
        |  CarInput { throttle, steer, aimDeltaX }
        v
CarController -- owns Rigidbody, owns aimYaw, ticks its ICarModules
        |
        +-- AimModule      (Update)       aimYaw += delta * sens, clamp +/- cone/2
        +-- DrivingModule  (FixedUpdate)  thrust | brake/reverse | yaw | grip
        +-- RammingModule  (collision)    OnCollisionEnter -> CarCollisionEvent   [stub]
        +-- WeaponModule   (--)           reads CarController.AimDirection        [stub]

CameraRig    (LateUpdate) <- car transform + CameraMode
CrosshairHUD (LateUpdate) <- CarController.AimDirection, projected through the camera
```

### CarInput

```csharp
public struct CarInput
{
    public float throttle;    // -1 .. +1   W = +1, S = -1
    public float steer;       // -1 .. +1   D = +1 (clockwise seen from above), A = -1
    public float aimDeltaX;   // raw mouse delta in pixels since the last Update
}
```

### CarController

Deliberately dumb. It owns the `Rigidbody`, holds `aimYaw`, exposes `AimDirection`, and
hands a `CarInput` to every attached `ICarModule`. It contains no physics and no game rules.
Deleting `DrivingModule` and dropping in a replacement must require no change to
`CarController`.

**`Sample()` is called exactly once per `Update`, and the result is cached.** This matters:
`aimDeltaX` is a per-frame accumulation that must be consumed once and only once, while
`throttle` and `steer` are level values that any number of readers can read harmlessly.
Sampling again inside `FixedUpdate` would either double-consume or silently drop mouse
movement, depending on how many physics steps fell in the frame. So:

- `Update` — sample once, cache, call `FrameTick` on every module (this is where aim
  consumes `aimDeltaX`)
- `FixedUpdate` — call `Tick` on every module with the **cached** struct, reading only the
  level fields

A frame with two physics steps therefore applies throttle twice (correct — it is a force
applied over time) and consumes the mouse delta once (also correct).

### ICarModule

```csharp
public interface ICarModule
{
    void Tick(in CarInput input, float dt);       // called from FixedUpdate
    void FrameTick(in CarInput input, float dt);  // called from Update
}
```

Modules that need only one rate implement the other as an empty body.

### Tick rates

Aim runs in `Update`, because mouse delta is reported per frame. **The delta must not be
multiplied by `deltaTime`** — a mouse delta is already a displacement, not a rate; scaling
it by frame time makes sensitivity frame-rate dependent.

Driving runs in `FixedUpdate`, because it applies forces to a `Rigidbody`. The `Rigidbody`
uses `RigidbodyInterpolation.Interpolate` so the camera, reading it in `LateUpdate`, sees
smooth motion rather than physics-step judder.

### The netcode seam

`IInputProvider` exposes one method, `CarInput Sample()`. Today the only real implementation
reads keyboard and mouse; `NullInputProvider` returns a zeroed struct for the dummy car.
Later, a `NetworkInputProvider` returns what the server sent and a `BotInputProvider` returns
what an AI decided — neither requires the driving code to know the difference.

### Ramming and Weapons

Both ship as real files with real interfaces and no behaviour. `RammingModule` catches
collisions and raises a `CarCollisionEvent` carrying impact normal, relative velocity and
the other car, then does nothing with it. `WeaponModule` exposes a fire entry point that
no-ops. They exist so that implementing combat means filling in a body rather than
re-architecting.

## 5. Driving physics

Per `FixedUpdate`, in order:

1. **Thrust** — `AddForce(transform.forward * throttle * enginePower)` when throttle > 0.
2. **Brake / reverse** — when throttle < 0: if forward speed exceeds an epsilon, apply
   `brakeForce` opposing velocity; otherwise apply `reversePower` backward.
3. **Drag** — velocity decays exponentially at `linearDrag` per second, giving a terminal
   speed of `enginePower / (mass * linearDrag)`. **Implemented manually in `DrivePhysics`,
   with `rb.linearDamping = 0`.** PhysX applies its own built-in damping with a different
   formulation, so leaving it non-zero would stack a second, differently-shaped decay on top
   and make the terminal-speed formula above wrong.

   **The same rule extends to CONTACT FRICTION, which the original spec missed.** PhysX's
   default material has friction 0.6, which on a 1200 kg car subtracts a flat
   `mu * m * g` — about 7 kN — from every drive force. Because it is flat rather than
   proportional it distorts the tuning curve rather than scaling it, and it hits the weaker
   reverse thrust far harder than forward: measured in play, forward lost 24% of its thrust
   and reverse 59%, which made reversing feel broken and put actual top speed 24% below what
   the formula promises. `ApplyGrip` IS this model's tyre-friction, so PhysX friction was
   both double-counting sideways resistance and adding unmodelled longitudinal resistance.
   `CarFactory` therefore assigns every car a zero-friction `PhysicsMaterial` with
   `frictionCombine = Minimum`, which wins over the ground's and wall's defaults so neither
   needs its own. With it, the terminal-speed formula holds.
4. **Yaw** — `rb.angularVelocity = Vector3.up * steer * turnRate * sense`. Never gated on
   speed MAGNITUDE, so turning while stationary works with no special case. It does consult
   the SIGN of travel: `sense` is `-1` while the car is travelling backwards faster than
   `reverseEpsilon`, and `+1` otherwise.

   **Revised 2026-09-12, after play-testing.** This originally read "not gated on speed" and
   took no speed parameter at all, which conflated two different things. Ignoring speed
   *magnitude* is correct and is what turn-in-place depends on. Ignoring the *sign* of travel
   is not: a real car's steering sense inverts in reverse, because yaw rate goes as
   `v/L * tan(delta)` and a negative `v` flips it — turn the wheel right while backing up and
   the rear swings right. Without the flip, reversing steered the opposite way from every car
   the player has ever driven. `DriveConfig.flipSteeringInReverse` selects between car-like
   (`true`, default) and tank-like absolute steering (`false`); turn-in-place is identical
   either way, since the flip only engages past `reverseEpsilon`.
5. **Grip** — decompose velocity into the car's forward and right axes; decay the right
   component. The gap between where the nose points and where the velocity points is the
   drift.

### Grip and drag must decay exponentially

`lateralVel *= (1 - grip)` once per `FixedUpdate` silently couples handling to the physics
timestep: change Fixed Timestep and the car handles differently. The correct form is

```
lateralVel *= Mathf.Exp(-gripStrength * dt)
```

which is why the config field is a **rate in 1/s**, not a 0–1 fraction. The same reasoning
applies to `linearDrag`. This is the single most important correctness property of the
driving module and is covered by an EditMode test.

## 6. Aiming

`aimYaw` is an angle **relative to the car's forward direction**, clamped to
`+/- coneAngleDegrees / 2`. World aim direction is `car.forward` rotated by `aimYaw`.

```
aimYaw += mouse.deltaX * mouseSensitivity
aimYaw  = Mathf.Clamp(aimYaw, -cone/2, +cone/2)
```

The cursor is hidden and locked (`CursorLockMode.Locked`).

Because `aimYaw` is car-relative, steering rotates the aim with the chassis and the
crosshair holds its screen position with no extra code — this is what satisfies the
requirement that the crosshair keeps its relative viewport position as the car moves.
Aiming moves the crosshair; steering does not.

`CrosshairHUD` draws the crosshair at `Camera.WorldToScreenPoint` of a point along the aim
ray — **not** at a linear lerp across screen X. Screen position is a tangent function of
angle, so a linear mapping would make the crosshair visibly drift from where shots land near
the screen edges. If the cone ever exceeds the camera's horizontal FOV, the crosshair clamps
to the screen edge and shows a direction arrow.

## 7. Camera

A single `Camera` with a `CameraRig` component, switched by `CameraConfig.mode`:

- **ThirdPerson** — positioned `distance` behind and `height` above the car, pitched down by
  `pitch`, yaw locked to the car's yaw.
- **FirstPerson** — positioned at the car's `DriverAnchor` child, yaw locked to the car's yaw.

Camera **yaw is rigid to the chassis in both modes, never smoothed**. Lagging the yaw would
make the crosshair drift across the screen during turns, breaking the invariant in section 6.
Positional smoothing is permissible later; yaw smoothing is not.

## 8. Scene composition

`Arena.unity` holds a directional light, one `Camera` with `CameraRig`, and one
`GameBootstrap` GameObject referencing the config assets. Everything else is built at
runtime, because a circular arena parameterised in car lengths is a formula rather than
hand-placed geometry.

```
GameBootstrap.Start()
  radius = arenaConfig.radiusInCarLengths * carDef.length
  ArenaBuilder.Build(radius)
      ground : procedural disc mesh + MeshCollider
      wall   : procedural ring mesh + MeshCollider, height = wallHeight
  CarFactory.Spawn(carDef, LocalInputProvider)   -> player car at origin, facing +Z
  CarFactory.Spawn(carDef, NullInputProvider)    -> dummy car at +Z * 4 * carDef.length
  cameraRig.Follow(playerCar)
```

Ground and wall are **procedural meshes**, not rings of box colliders. Box segments are
simpler but leave seams that catch a car sliding along the wall, and sliding along the wall
is something a brawler does constantly.

`Arena.unity` itself is generated by an editor method, `ArenaSceneBuilder.BuildScene()`,
invoked headlessly. This keeps the scene reproducible from code and its construction
reviewable in a diff rather than as opaque YAML.

The **car** is a `Cube` scaled to the configured dimensions, with an interpolated
`Rigidbody`, a `BoxCollider`, the four modules, a small child cube at the nose so facing is
visible, and an empty `DriverAnchor` child marking the first-person eye position.

The **dummy car** is a second instance driven by `NullInputProvider`. It gives something to
ram, which is how collision feel gets judged before combat exists.

## 9. Configuration

Five ScriptableObject assets under `Assets/_Project/Configs/`.

| Asset | Field | Default |
|---|---|---|
| `ArenaConfig` | `radiusInCarLengths` | 10 |
| | `wallHeight` | 2.5 m |
| `CarDefinition` | `length` / `width` / `height` | 4.5 / 2.0 / 1.2 m |
| | `mass` | 1200 kg |
| | `driverAnchorOffset` (first-person eye) | (0, 1.0, 0.4) |
| | references to `DriveConfig`, `AimConfig` | — |
| `DriveConfig` | `enginePower` | 30000 N (top speed ~25 m/s) |
| | `linearDrag` | 1.0 /s |
| | `brakeForce` | 40000 N |
| | `reversePower` | 12000 N |
| | `turnRate` | 90 deg/s |
| | `flipSteeringInReverse` | true (car-like reverse steering) |
| | `lateralGripStrength` | 6.0 /s |
| `AimConfig` | `coneAngleDegrees` | 90 |
| | `mouseSensitivity` | 0.12 deg/pixel |
| `CameraConfig` | `mode` | `ThirdPerson` \| `FirstPerson` |
| | `distance` / `height` / `pitch` / `fov` (TP) | 8 m / 3.5 m / 12 deg / 60 deg |
| | `fov` (FP) | 70 deg |

Arena radius derives from `radiusInCarLengths * carDef.length`, so changing the car length
rescales the arena automatically when a real model replaces the box.

At these defaults a 60 deg vertical FOV at 16:9 is roughly 91.5 deg horizontal, against a
90 deg cone — the crosshair travels almost exactly to the screen edges. This is convenient,
not a constraint; the edge-clamp behaviour in section 6 covers wider cones.

## 10. Testing

EditMode tests cover the pure cores only. They are few on purpose — they pin the two things
that are easy to get subtly wrong and expensive to find later.

- `DrivePhysicsTests`
  - grip decay produces the same result over one second regardless of timestep
    (one 0.02 s step vs. two 0.01 s steps, within tolerance)
  - terminal speed converges to `enginePower / (mass * linearDrag)`
  - yaw is applied at full rate when speed is zero (turn-in-place)
  - yaw inverts past `reverseEpsilon` when travelling backwards, does not invert below it,
    and never inverts when `flipSteeringInReverse` is false
- `AimMathTests`
  - accumulated delta clamps at both cone edges and does not wrap
  - zero delta leaves `aimYaw` unchanged

## 11. Definition of done

Verifiable by playing:

- Project opens in Unity 6000.6.0f1 and compiles with zero errors
- Press Play in `Arena.unity`: WASD drives, S brakes then reverses, the car turns while
  stationary, and cornering at speed drifts
- The mouse sweeps the crosshair across the cone and it stops at both limits; steering does
  not move the crosshair
- Driving into the wall stops the car; driving into the dummy car shoves it
- Flipping `CameraConfig.mode` in the Inspector switches first/third person

Verifiable headlessly, with output shown rather than asserted:

- `unity project verify` reports no integrity problems
- `unity test` runs the EditMode suite green

## 12. Out of scope

Menus, lobby, practice mode, car select, any combat or damage or health, respawn, netcode,
audio, and real art. `RammingModule` and `WeaponModule` exist as empty seams only.

## 13. Prerequisites

Already satisfied on this machine: Unity CLI 1.0.0-beta.8, signed in, Unity Personal license
active, editor 6000.6.0f1 installed.

The only extra platform module installed is Web. Windows standalone build support ships with
the Windows Editor, so desktop play and builds are unaffected. If Android ever becomes a
target, install the module first:

```
unity editors install-modules --version 6000.6.0f1 --module android
```
