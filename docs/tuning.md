# Tuning reference

Every number lives in a ScriptableObject under `Assets/_Project/Configs/`. Nothing needs a
code change. Values below are the current asset contents.

**All decay values are rates in 1/s**, applied as `exp(-rate × dt)` — see
[driving-physics.md](driving-physics.md#every-decay-value-is-a-rate-in-1s).

## DriveConfig

| Field | Default | Notes |
|---|---|---|
| `enginePower` | 30000 N | Top speed = `enginePower / (mass × linearDrag)` = 25 m/s |
| `linearDrag` | 1.0 /s | Rate, not a fraction |
| `brakeForce` | 40000 N | Applied against velocity when S is held above the epsilon |
| `reversePower` | 12000 N | Deliberately weaker than forward |
| `reverseEpsilon` | 0.5 m/s | Below this, S reverses instead of braking |
| `turnRate` | 90 °/s | At full steer, at any speed including zero |
| `flipSteeringInReverse` | true | Car-like reverse steering; false gives tank-style |
| `lateralGripStrength` | 6.0 /s | Lower drifts more. 0 is a hockey puck |

`flipSteeringInReverse` is not yet written into the asset — it takes the C# default
(`true`). Unity will serialise it on the next re-save; that one-line diff is expected, not a
change.

## AimConfig

| Field | Default | Notes |
|---|---|---|
| `coneAngleDegrees` | 100 | Total cone; aim clamps to ±50 |
| `mouseSensitivity` | 0.12 °/pixel | Never multiplied by deltaTime |

Against the 102° first-person view the crosshair travels almost exactly edge to edge. In
third person (92° view) it still stays on-screen, because the camera sits 8 m behind the car
and a point 40 m out appears nearer the centre than its aim angle suggests — edge to edge
there is about 108°. Because the horizontal view is fixed, both hold on every monitor shape.

## CameraConfig

| Field | Default | Notes |
|---|---|---|
| `mode` | `FirstPerson` (1) | `ThirdPerson` is 0 |
| `thirdPersonDistance` | 8 m | |
| `thirdPersonHeight` | 3.5 m | |
| `thirdPersonPitch` | 12° | |
| `thirdPersonHorizontalFov` | 92° | **Horizontal.** Equals a 60° vertical FOV at 16:9 |
| `firstPersonHorizontalFov` | 102° | **Horizontal.** Equals a 70° vertical FOV at 16:9 |

Both FOVs are **horizontal and identical for every monitor shape** — a competitive fairness
rule. Unity's camera takes a vertical FOV, so `CameraRig` derives it each frame from the
screen's aspect ratio. Wider screens see less top and bottom rather than more to the sides.
See [aiming-and-camera.md](aiming-and-camera.md#every-monitor-sees-the-same-width-of-world).

Yaw is rigid to the chassis in both modes and must stay that way — see
[aiming-and-camera.md](aiming-and-camera.md#camera-yaw-is-rigid-to-the-chassis-and-must-never-be-smoothed).

## ArenaConfig

| Field | Default | Notes |
|---|---|---|
| `radiusMetres` | 45 m | Absolute. Independent of car size |
| `wallHeight` | 2.5 m | |
| `segments` | 96 | Higher is smoother, more triangles |
| `groundMaterial` | *assigned* | Empty generates the calibration grid |
| `wallMaterial` | *assigned* | Empty generates stripes |
| `groundTileMetres` | 4 m | World size of one floor texture repeat |
| `wallTileMetres` | 2.5 m | Equal to `wallHeight` shows one full tile vertically |
| `gridCellInCarLengths` | 1 | Placeholder grid only; ignored once a material is assigned |

## CarDefinition

| Field | Default | Notes |
|---|---|---|
| `length` / `width` / `height` | 4.657 / 2.208 / 1.342 m | Measured from the Bastion model. Drives the BoxCollider |
| `mass` | 1200 kg | Feeds the terminal-speed formula |
| `driverAnchorOffset` | (0, 0.40, 0.15) | First-person eye, relative to car centre — about 1.07 m above ground. Move it rather than widening FOV if the dashboard shows |
| `visualPrefab` | Bastion | Empty falls back to the placeholder box |
| `visualOffset` | (0, −0.667, −0.245) | Centres the model on the collider; the model's origin is at its wheels |
| `visualYawOffset` | −90° | The model is authored nose-along-+X; Unity's forward is +Z |
| `tintedMaterials` | Body, Door | Which materials take the team colour. Empty tints every renderer |
| `driveConfig` / `aimConfig` | references | |

Changing `length` no longer resizes the arena — that coupling was removed deliberately.

The collider is **always** a box of the dimensions above, and any collider shipped inside
`visualPrefab` is stripped on spawn.

## Replacing the box with a real car model

The box is a **child** GameObject (`Body`); the `Rigidbody` and `BoxCollider` are on the
**root**. Swapping the visual touches no module.

What to check when sourcing a model:

| Thing | Want | Goes wrong |
|---|---|---|
| Format | `.fbx` or `.glb` | `.blend` needs Blender installed |
| Forward axis | **+Z** | Blender exports often land on −Z or +X; the car then drives backwards |
| Units | metres, ≈4.5 m long | Centimetre-authored models arrive 100× too big |
| Origin | centre or wheel contact — just know which | The root spawns at `y = height/2` |
| Materials | URP | Built-in materials render magenta; see [arena-and-rendering.md](arena-and-rendering.md#urp-the-traps) |

Worth filtering for at purchase time: **separate wheel objects** (baked-in wheels can never
be steered or spun) and **a paintable body material slot** (`CarFactory.Paint` currently
overwrites the whole visual with one flat colour, which would destroy a real texture).

**The collider stays a box.** A mesh collider from the car model is the obvious move and is
wrong: concave mesh colliders cannot be dynamic in PhysX, and a convex hull of a car body
rams worse and less predictably than a box. Physics keeps using `CarDefinition` dimensions;
the model is decoration inside it.
