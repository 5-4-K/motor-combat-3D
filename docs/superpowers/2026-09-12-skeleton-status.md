# Skeleton status — 2026-09-12

Written at the end of the implementation session, before compacting. Everything here is
state that lives nowhere else on disk. The spec, the plan and the README remain the
authorities for *what* the thing is; this file records *where it got to*.

## Verified

- 21 implementation commits on `main`, head `ca3bba9`. **Nothing pushed** — `origin` is
  `git@github.com:5-4-K/motor-combat-3D.git` and 24 commits are ahead of it.
- `unity test . --mode EditMode` → **37 passed, 0 failed**.
  AimMath 6 · DrivePhysics 16 · ArenaMeshBuilder 8 · CarFactory 7.
- `unity projects verify .` → ok, 0 errors, 0 warnings.
- Working tree clean.

## Play-test checklist (plan Task 13)

| # | Row | State |
|---|---|---|
| 1 | W accelerates to a top speed | confirmed |
| 2 | Release W coasts | confirmed |
| 3 | S brakes while moving forward | confirmed |
| 4 | S reverses once stopped | confirmed (after the friction fix) |
| 5 | A/D rotate on the spot | confirmed |
| 6 | Hard turn at speed drifts | **not walked** |
| 7/8 | Mouse sweeps crosshair, stops at cone edges | **not walked** |
| 9 | Steering does NOT move the crosshair | **not walked — the key invariant** |
| 10 | Wall stops the car, slides without catching | **not walked** |
| 11 | Dummy is shoved | **not walked** (see deliberate behaviours) |
| 12 | First person, all of the above holds | **not walked** |

Row 9 is the one worth the most attention: the whole aiming design exists to guarantee it,
and it is the hardest to eyeball.

## Three defects the first play-test found, all fixed in `ca3bba9`

1. **PhysX contact friction was an unmodelled third resistance.** The design routes all
   resistance through `DrivePhysics` and pins `linearDamping` to 0 so PhysX cannot stack a
   second decay — but never accounted for friction. At the default 0.6 that is a flat
   `mu*m*g` of ~7 kN, costing forward thrust 24% and reverse **59%**, which made S look
   dead, and putting top speed 24% under the formula the configs are tuned against.
   `ApplyGrip` *is* this model's tyre-friction, so PhysX friction was also double-counting
   sideways resistance. Cars now carry a zero-friction `PhysicsMaterial` with
   `frictionCombine = Minimum`. Recorded in spec §5 step 3.
2. **Yaw ignored the sign of travel**, so reversing steered opposite to every real car. The
   spec said yaw must not be gated on speed; that conflated *magnitude* (which turn-in-place
   genuinely needs) with *direction* (which it does not). Yaw now inverts past
   `reverseEpsilon` when travelling backwards. `DriveConfig.flipSteeringInReverse` toggles
   car-like vs tank-like. Recorded in spec §5 step 4.
3. **The config null-check ran in `Awake`**, which fires before `CarFactory` assigns config
   on the same line, so every factory-built car reported itself misconfigured. Moved to
   `Start`.

Note: `DriveConfig.asset` on disk does not yet carry `flipSteeringInReverse`; it takes the
C# default (`true`). Unity will write `flipSteeringInReverse: 1` into the asset the next
time it re-serializes — an expected one-line diff, not a change.

## Deliberate behaviours that look like bugs

- **Side-on rams barely move the dummy, and it never spins.** `DrivingModule` assigns
  `angularVelocity` every tick (hard zero for the dummy's null input) and `ApplyGrip` bleeds
  lateral velocity. Correct for the arcade model chosen; resolves when combat lands, via a
  carve-out in the yaw write.
- **Reversing into the wall puts the third-person camera outside the arena, and the wall
  vanishes** — it is a single-sided inward-facing mesh. No camera collision is specified.
  Left alone deliberately so it does not preempt the first/third person evaluation. Cheapest
  mitigations if wanted: clamp camera radius to `arenaRadius - margin`, spherecast from car
  to camera, or render the wall double-sided.

## Open follow-ups, none blocking

From the final whole-branch review, triaged as fine-to-leave:

- Spec §6's crosshair "direction arrow" is a colour tint instead.
- Spec §3 lists `Prefabs/Car.prefab` and `Materials/`; neither exists — the car is built in
  code and materials are created at runtime. Supersession never recorded in the spec.
- Runtime meshes and materials are never destroyed; expect "Cleaning up leaked objects"
  warnings on Play exit.
- Template leftovers still committed: `Assets/Scenes/SampleScene.unity`, `Assets/Readme.asset`,
  `Assets/TutorialInfo/`. `SampleScene` is a "which scene do I open?" trap.
- `Shader.Find` for runtime materials is a shader-stripping risk in a *player build*; zero
  risk in Editor Play mode.
- Test coverage gaps worth one bundled pass: no mirrored `-cone` wrap test; the
  `forwardSpeed == reverseEpsilon` boundary; `ArenaBuilder.RadiusFor`; and nothing pins the
  `normals` array (winding and normals could be flipped together unnoticed).

## Modularity, honestly

Enforced, not aspirational: 12 asmdefs make a cross-module reference a compile error.
`ICarModule` + `GetComponents` means adding a module needs no `CarController` edit, and
`IInputProvider` is a real netcode seam. The limit: `CarFactory.Spawn` hard-codes the module
set, so a car with a different *loadout* means editing that file. Tuning is data; composition
is not, yet.

## Workflow notes for the next session

- The user works directly on `main` — chosen explicitly over a branch or worktree after the
  tradeoff was laid out.
- `unity test` and `unity run` **cannot attach while the Unity Editor has the project open**.
  It fails with "already open in a running Editor (PID …)". Ask for the editor to be closed.
- The CLI reserves batch flags: `unity run . -- -executeMethod <Method>` works;
  adding `-batchmode -nographics -quit` is rejected.
- `.superpowers/` holds the per-task reports and review diffs from the SDD run. Gitignored,
  safe to delete with `rm -rf .superpowers`.
