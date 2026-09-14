# Architecture

## The shape of it

```
LocalInputProvider ── implements IInputProvider
        │
        │  CarInput { throttle, steer, aimDeltaX }
        ▼
CarController ── owns the Rigidbody and aimYaw, ticks its ICarModules
        │
        ├── AimModule      (Update)       aimYaw += delta × sens, clamped to ±cone/2
        ├── DrivingModule  (FixedUpdate)  thrust │ brake/reverse │ yaw │ grip
        ├── RammingModule  (collision)    region + angle → shove victim, stop attacker; spin decay while reeling
        └── WeaponModule   (—)            reads CarController.AimDirection       [stub]

CarAbilities (Core) ← ability blocks; written by Ramming and Health (the wreck block); read by Driving, Ramming
CarStats     (Core) ← base stats + modifiers; read by Combat, Ramming, Driving

Health        (Combat) ← IDamageable; runs the damage path, raises Damaged / Destroyed
WreckSequence (Combat) ← on Destroyed: layer change, roll, fade, deactivate

CameraRig    (LateUpdate) ← car transform + CameraMode
CrosshairHUD (LateUpdate) ← CarController.AimDirection, projected through the camera
```

## Modularity is compile-enforced, not aspirational

**One assembly definition per script folder** — 14 of them. A reference from `Driving` to
`Weapons` is a compile error, not slow architectural drift. Every gameplay assembly
references `MotorCombat.Core` and nothing else; only `MotorCombat.Cars` and
`MotorCombat.Bootstrap` compose across modules.

```
MotorCombat.Core      Aiming    Arena     Bootstrap  Cameras   Cars
             Combat    Controls Driving   HUD       Ramming    Weapons
             EditorTools        Tests.EditMode
```

The cost is that a new script must go in the right folder. That is the point.

Two folder names avoid a namespace-versus-type collision: `Cameras/` rather than
`CameraRig/` (which would put a `CameraRig` class inside a `MotorCombat.CameraRig`
namespace), and `Controls/` rather than `Input/` (where the identifier `Input` would
resolve to the namespace).

### Where it stops

`CarFactory.Spawn` hard-codes the module set, so a car with a different *loadout* means
editing that file. Tuning is data; composition is not, yet.

## Core holds the contracts

`MotorCombat.Core` doesn't just hold `CarController` — it holds every small contract that a
future sub-project needs to plug into without touching the code that came before it:
`CarAbilities`/`CarAbility` (ability switches), `CarStats`/`CarStat` (base stats and
modifiers), `IDamageable`/`DamageRequest` (the one damage path), `Hostility` (who's an enemy of
whom), `TickSchedule` (periodic damage), `CarRegistry` (every live car) and `PhysicsLayers`
(layer names and their collision rules).

This is the **closed-spec rule**: a later sub-project only *adds* — new files, new config
types, new implementations of an existing interface, new registrations against an existing
static. It never edits an earlier sub-project's code, so weapons, effects, projectiles and
zones (sub-projects 2–6) are built entirely by calling into Core, not by changing it.

## Thin adapters over pure cores

Each module is a MonoBehaviour that reads config, calls a static pure function, and writes
the result to the Rigidbody. The maths lives in `DrivePhysics` and `AimMath` — no
`GameObject`, no scene, testable directly. This is why 202 EditMode tests run in under a
second with nothing instantiated.

When adding a module, put the decision in a pure static and keep the MonoBehaviour dumb.

## CarController is deliberately dumb

It owns the `Rigidbody`, holds `aimYaw`, exposes `AimDirection`, and hands a `CarInput` to
every attached `ICarModule`. No physics, no game rules. Deleting `DrivingModule` and
dropping in a replacement must require no change to `CarController`.

Modules are collected in `Start()`, not `Awake()`. `Awake` fires the instant `AddComponent`
returns — before `CarFactory` has finished assembling the object on the same line. A guard
placed in `Awake` reported every factory-built car as misconfigured. Anything that depends
on the object being complete belongs in `Start`.

## Input is sampled exactly once per Update

`IInputProvider.Sample()` is called once per `Update` and the result cached for
`FixedUpdate`. This is load-bearing.

`aimDeltaX` is a per-frame accumulation that must be consumed once and only once.
`throttle` and `steer` are level values any number of readers can read harmlessly. Sampling
again inside `FixedUpdate` would either double-consume the mouse delta or silently drop it,
depending on how many physics steps fell inside the frame.

- **`Update`** — sample once, cache, call `FrameTick` on every module. Aim consumes
  `aimDeltaX` here.
- **`FixedUpdate`** — call `Tick` with the **cached** struct, reading only level fields.

A frame containing two physics steps therefore applies throttle twice — correct, it is a
force applied over time — and consumes the mouse delta once, also correct.

### Why aim runs in Update and driving in FixedUpdate

Mouse delta is reported per frame, so aim must run per frame. **The delta must never be
multiplied by `deltaTime`**: a mouse delta is already a displacement, not a rate, and
scaling it by frame time makes sensitivity frame-rate dependent.

Driving applies forces to a `Rigidbody`, so it runs in `FixedUpdate`. The Rigidbody uses
`RigidbodyInterpolation.Interpolate` so the camera, reading it in `LateUpdate`, sees smooth
motion rather than physics-step judder.

## The netcode seam

`IInputProvider` exposes one method, `CarInput Sample()`.

| Implementation | Returns |
|---|---|
| `LocalInputProvider` | keyboard and mouse |
| `NullInputProvider` | a zeroed struct — the dummy car |
| *future* `NetworkInputProvider` | what the server sent |
| *future* `BotInputProvider` | what an AI decided |

None of them require the driving code to know the difference. Networking is not
implemented; only this seam exists.

## Weapons is a seam, not a feature

`WeaponModule` ships as a real file with a real interface and no behaviour: it exposes a
fire entry point, reading `CarController.AimDirection`, that no-ops. It exists so
implementing weapons means filling in a body rather than re-architecting.

Ramming was the other seam; it is now built — see [ramming.md](ramming.md). It reaches
driving only through `CarController.Status`, so the two assemblies still never reference
each other.

## Scene composition

`Arena.unity` holds a directional light, one Camera with `CameraRig`, and one
`GameBootstrap` referencing the config assets. Everything else is built at runtime, because
a parameterised circular arena is a formula rather than hand-placed geometry.

```
GameBootstrap.Start()
  ArenaBuilder.Build(arenaConfig, carDef.length)
      Ground : procedural disc mesh + MeshCollider
      Wall   : procedural ring mesh + MeshCollider
  CarFactory.Spawn(carDef, LocalInputProvider)  → player car at origin, facing +Z
  CarFactory.Spawn(carDef, NullInputProvider)   → dummy car ahead on +Z
  cameraRig.Follow(playerCar)
```

The scene itself is generated by `ArenaSceneBuilder.BuildScene()`, so its construction is
reviewable as code rather than opaque YAML. See [workflow.md](workflow.md).

## Standing design decisions

| Decision | Choice | Reason |
|---|---|---|
| Physics substrate | `Rigidbody` + `AddForce`, no `WheelCollider` | Wheel colliders simulate suspension and tyre slip we do not want, and fight turn-in-place |
| Yaw | Direct angular velocity | Crisp arcade response; makes turn-in-place deliberate |
| Drift | Constant lateral grip rate | One knob; drift magnitude scales with speed naturally |
| Reverse | Brake, then reverse below a speed epsilon | Backing out of a wall pin matters in a brawler |
| Arena edge | Solid wall | Contains play; gives ramming something to slam against |
| Aim mapping | Locked cursor, accumulate delta, clamp | Decouples cone angle from camera FOV; identical in both camera modes |
| Camera follow | Chassis heading | Satisfies the crosshair invariant; makes the cone limit visible |
| Module wiring | MonoBehaviour adapters over pure cores | Inspector-tunable *and* unit-testable |
