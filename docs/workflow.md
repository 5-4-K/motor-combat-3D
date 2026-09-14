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

273 EditMode tests, sub-second. Nearly all test pure statics; `CarFactoryTests` builds throwaway GameObjects but loads no scene.

| Fixture | Count |
|---|---|
| `DrivePhysicsTests` | 16 |
| `ArenaMeshBuilderTests` | 15 |
| `ArenaTextureBuilderTests` | 9 |
| `CarFactoryTests` | 20 |
| `AimMathTests` | 6 |
| `CameraFovTests` | 5 |
| `ArenaBuilderTests` | 7 |
| `RamRulesTests` | 36 |
| `CarAbilitiesTests` | 15 |
| `CarStatsTests` | 10 |
| `DamageRulesTests` | 9 |
| `HealthStateTests` | 15 |
| `TickScheduleTests` | 8 |
| `HostilityTests` | 4 |
| `HealthTests` | 9 |
| `PhysicsLayersTests` | 3 |
| `WreckMathTests` | 11 |
| `WreckMaterialsTests` | 1 |
| `HealthBarLayoutTests` | 7 |
| `HudRootTests` | 1 |
| `CarRegistryTests` | 3 |
| `SourceKeyIdentityTests` | 1 |
| `CarRespawnTests` | 4 |
| `RespawnRulesTests` | 5 |
| `EffectInfoTests` | 4 |
| `EffectRulesTests` | 12 |
| `EffectSetTests` | 6 |
| `EffectsConfigTests` | 2 |
| `CarEffectsTests` | 24 |
| `EffectChipLayoutTests` | 4 |
| `EffectChipRowTests` | 1 |

The test assembly carries the `UNITY_INCLUDE_TESTS` define constraint, so tests never ship
in a player build.

## Acceptance checklist

Behaviour that cannot be unit-tested. Rows 1–20 passed before combat and the HUD; rows 21–37
are not yet walked.

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
| 11 | Tap W from about a metre away from the dummy (under 3 m/s at contact) | Plain bump, no ram; with logImpacts on, the log says (no ram) |
| 12 | `CameraConfig.mode` → `FirstPerson` | View from inside; all the above still holds |
| 13 | Drive into the parked dummy's rear at speed | Rear ram: you stop dead; dummy shoots forward and slides |
| 14 | Hit the dummy's side mid-panel | Flank: dummy slides sideways, little or no spin |
| 15 | Hit the dummy's side near its tail | Flank with spin; spin winds down, then any remaining spin stops abruptly when the reel ends — known, see ramming.md |
| 16 | Drive into the parked dummy's nose, head-on | You stop dead and are locked ~0.5 s; the dummy is pushed backwards at about a fifth of your speed, no spin. Your own car must not move backwards — if it does, that is PhysX depenetration (see ramming.md) |
| 17 | After any ram, press W and A/D immediately | No response for ~0.5 s; mouse aim still moves the crosshair |
| 18 | Hit the dummy's front corner at a shallow vs steep angle | Nearly nose-to-nose (headings within 45° of opposite) is head-on; more than 45° off is flank |
| 19 | Ram the dummy again while it is still sliding | Second ram applies; its reel restarts (the REEL chip jumps back up) |
| 20 | Any ram at top speed | No car leaves the ground or tips |
| 21 | Play | Your HP bar bottom-centre reads 1000 / 1000; the dummy has a red bar and its name above it |
| 22 | Drive around the dummy and away from it | Its bar faces you from every side and shrinks with distance, never below a readable size |
| 23 | Dummy's Health component → right-click → Debug: take 25% max HP | Its bar shortens to three quarters |
| 24 | Temporarily set RamConfig.flankDamage to 100, flank-ram the dummy, then head-on it; set it back to 0 | Flank shortens the bar; head-on does not |
| 25 | Dummy's Health → Debug: destroy | Bar vanishes at once; dummy slides to a stop, rolls sideways, fades, disappears at ~1.5 s; you can drive through it but it never sinks or passes the wall |
| 26 | Re-walk ram rows 13–20 | Unchanged — the ability re-expression preserves them |
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
