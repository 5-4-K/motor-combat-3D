# Workflow

Unity 6 (`6000.6.0f1`), URP. The Unity project lives at the repository root — `Assets/`,
`Packages/` and `ProjectSettings/` sit directly under it.

## Running it

Open the project, open `Assets/_Project/Scenes/Arena.unity`, press Play.

- **WASD** — drive. S brakes while moving forward, reverses once stopped. A/D turn,
  including on the spot.
- **Mouse** — aim within a cone. Cursor is locked; the crosshair holds its screen position
  while you steer.
- First/third person switches on `Assets/_Project/Configs/CameraConfig.asset`.

## Commands

```bash
unity test . --mode EditMode
```

```bash
unity projects verify .
```

Regenerate the config assets, or the scene, from code:

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ConfigAssetBootstrap.CreateDefaults
```

```bash
unity run . -- -executeMethod MotorCombat.EditorTools.ArenaSceneBuilder.BuildScene
```

## Two CLI gotchas that cost real time

**The Editor must be closed.** `unity test` and `unity run` cannot attach while the Editor
holds the project — they fail with *"already open in a running Editor (PID …)"*. If a test
run reports a suspiciously familiar number, check that `test-results.xml` was actually
rewritten; a failed run leaves the previous one in place and it is easy to read a stale
result as a pass.

**Batch flags are reserved.** `unity run . -- -batchmode -nographics -quit -executeMethod …`
is rejected with *"conflicts with a reserved Unity flag managed by this command."* Pass only
`-executeMethod`.

## Tests

72 EditMode tests, sub-second. Nearly all test pure statics; `CarFactoryTests` builds throwaway GameObjects but loads no scene.

| Fixture | Count |
|---|---|
| `DrivePhysicsTests` | 16 |
| `ArenaMeshBuilderTests` | 15 |
| `ArenaTextureBuilderTests` | 9 |
| `CarFactoryTests` | 15 |
| `AimMathTests` | 6 |
| `CameraFovTests` | 5 |
| `ArenaBuilderTests` | 6 |

The test assembly carries the `UNITY_INCLUDE_TESTS` define constraint, so tests never ship
in a player build.

## Acceptance checklist

Behaviour that cannot be unit-tested. All twelve currently pass. Re-walk them after any
change to driving, aiming or the camera.

| # | Check | Expected |
|---|---|---|
| 1 | Press W | Accelerates, settles at a top speed |
| 2 | Release W | Coasts rather than stopping dead |
| 3 | S while moving forward | Brakes noticeably harder than coasting |
| 4 | Hold S after stopping | Reverses, slower than forward |
| 5 | A/D while stationary | Rotates on the spot |
| 6 | Turn hard at speed | Nose leads travel direction — visible drift |
| 7 | Mouse right | Crosshair moves right, stops at the cone edge |
| 8 | Mouse left | Crosshair moves left, stops at the cone edge |
| 9 | **Steer, mouse held still** | **Crosshair does NOT move on screen** |
| 10 | Drive into the wall | Stopped, slides along without catching |
| 11 | Drive into the dummy car | Dummy is shoved, no damage |
| 12 | `CameraConfig.mode` → `FirstPerson` | View from inside; all the above still holds |

Row 9 is the one that matters. It is the reason the aiming and camera are built the way they
are, and the hardest to judge by eye — pick a floor grid line and watch the crosshair
against it.

## Git

Work happens directly on `main`. Nothing is pushed; `origin` is
`git@github.com:5-4-K/motor-combat-3D.git` and pushing is a deliberate manual step.

`.gitignore` is the Unity template plus explicit negations, so these stay tracked even if
the template is refreshed:

```
!.claude/skills/
!.claude/settings.json
!.claude/hooks/
```

Unity generates `.meta` files for **folders** as well as files, and a folder's `.meta` sits
one level *above* the folder. Staging a narrow path therefore misses them. Stage
`Assets/_Project` wholesale and check `git status --porcelain` afterwards.

## Prerequisites

Unity CLI, signed in, editor `6000.6.0f1`. The only extra platform module installed is Web;
Windows standalone support ships with the Windows Editor. For Android:

```bash
unity editors install-modules --version 6000.6.0f1 --module android
```
