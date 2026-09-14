# motor-combat-3D

3D car brawl game. Unity 6 (`6000.6.0f1`), URP. Currently a playable skeleton: one arena,
one drivable car, one dummy to ram. Ramming, health, destruction, respawn and effects work;
no weapons, no menus, no netcode.

## Documentation

| Doc | Read it when |
|---|---|
| [docs/architecture.md](docs/architecture.md) | Adding a module, touching `CarController`, or wondering why the folders are split the way they are |
| [docs/driving-physics.md](docs/driving-physics.md) | Anything about how the car moves |
| [docs/aiming-and-camera.md](docs/aiming-and-camera.md) | Anything about the crosshair or the camera — they are one subject |
| [docs/ramming.md](docs/ramming.md) | Anything about car-vs-car collisions, lock or reel |
| [docs/combat.md](docs/combat.md) | Health, damage, stats, ability switches, destruction, respawn, physics layers |
| [docs/effects.md](docs/effects.md) | Anything about effects (Stunned, Corroded, …), stacking or the effect chips |
| [docs/hud.md](docs/hud.md) | Anything drawn on screen besides the crosshair |
| [docs/arena-and-rendering.md](docs/arena-and-rendering.md) | Arena geometry, textures, materials, or a URP problem |
| [docs/tuning.md](docs/tuning.md) | Changing a number, or replacing the box with a real car model |
| [docs/workflow.md](docs/workflow.md) | Running it, testing it, the acceptance checklist, git |

## Five things that will bite you

Each is a real defect that shipped or nearly shipped here.

1. **Every decay value is a rate in 1/s**, applied as `exp(-rate × dt)`. Never
   `value *= (1 - rate)` — that couples handling to the physics timestep.
   → [driving-physics](docs/driving-physics.md#every-decay-value-is-a-rate-in-1s)

2. **`DrivePhysics` is the only source of resistance.** Rigidbody damping and PhysX contact
   friction are both pinned to zero. Anything else that slows the car breaks the
   terminal-speed formula.
   → [driving-physics](docs/driving-physics.md#drivephysics-is-the-only-source-of-resistance)

3. **Aiming moves the crosshair; steering does not.** Camera yaw is rigid to the chassis and
   must never be smoothed.
   → [aiming-and-camera](docs/aiming-and-camera.md#the-invariant)

4. **Procedural meshes need `RecalculateTangents()`** if they will ever carry a normal map.
   Without it, lighting breaks per-triangle and the geometry looks fine.
   → [arena-and-rendering](docs/arena-and-rendering.md#tangents-are-mandatory)

5. **The Editor must be closed** for `unity test` and `unity run`. A failed run leaves the
   previous `test-results.xml` in place, so a stale pass reads as a real one.
   → [workflow](docs/workflow.md#two-cli-gotchas-that-cost-real-time)

## Commands

```bash
unity test . --mode EditMode
```

```bash
unity projects verify .
```

## Working agreements

**`E:\Work\motor-combat-MOBA` is a UI/UX reference only.** It is a TypeScript/Colyseus/Phaser
2D web game. Its screen flow — homepage, lobby, practice mode, car select — will be
revisited later. **No code, tuning values, or gameplay rules are taken from it.** Gameplay
decisions come from the user.

**Keep modules separable.** Driving, ramming, weapons, cars and arena each own an assembly so
a cross-module reference is a compile error. Put decisions in pure statics and keep
MonoBehaviours dumb.

**Do not assume — ask.** Ambiguity gets a question, not a guess.

**Work happens on `main`, and nothing is pushed.** Pushing is a deliberate manual step by the
user.

**Tuning numbers belong to the user.** Report what feels wrong; do not quietly retune.

**Ask before starting the "Iterative implementation workflow".** When brainstorming an idea
reaches the point where the design would be written, always ask the user whether to start the
"Iterative implementation workflow". Only on a yes, run it end to end: write the design in
sections → self-review → write the implementation plan → self-review → implement with
subagent-driven development (SDD).

## Not built yet

Weapons. Menus, lobby, practice mode, car select. Netcode — only the `IInputProvider` seam
exists. Audio. The car is one model (Bastion); there is no car select.

`WeaponModule` is a deliberate empty seam, not an oversight.
