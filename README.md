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
Sizes are absolute metres: `ArenaConfig.radiusMetres` sets the arena, and
`groundTileMetres` / `wallTileMetres` set how often each texture repeats. The arena does
not move when `CarDefinition.length` changes — a texture's scale is a property of the
material, not of whatever drives over it.

The one deliberate exception is `ArenaConfig.gridCellInCarLengths`. That sizes the
generated placeholder grid, which is a measuring tool: its cells are one car long so
speed, braking distance and drift are countable while play-testing. It is ignored the
moment a real ground material is assigned.

Leave `groundMaterial` or `wallMaterial` empty and the arena generates its own placeholder
texture. Assign a material and it tiles correctly with no further setup — tiling is baked
into the mesh UVs, not set on the material.

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
