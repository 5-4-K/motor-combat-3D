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

The same two operations are also available in the Unity editor menu as
`Motor Combat > Create Default Configs` and `Motor Combat > Rebuild Arena Scene`.

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
```

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ArenaSceneBuilder.BuildScene
```

## Status

Skeleton only. No combat, damage, health, respawn, menus, lobby, netcode, audio or real art.
The controls above describe intended behaviour implemented in code and covered by unit tests
at the maths level; the manual play-test checklist in the plan has not been run yet, so
nothing here has been verified in Play mode.
`RammingModule` and `WeaponModule` are empty seams.

- Design: [`docs/superpowers/specs/2026-09-12-motor-combat-3d-skeleton-design.md`](docs/superpowers/specs/2026-09-12-motor-combat-3d-skeleton-design.md)
- Plan: [`docs/superpowers/plans/2026-09-12-motor-combat-3d-skeleton.md`](docs/superpowers/plans/2026-09-12-motor-combat-3d-skeleton.md)

`E:\Work\motor-combat-MOBA` is a UX reference only (a separate 2D web game). No code,
tuning values, or gameplay rules are taken from it.
