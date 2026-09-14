# Weapons

## Overview

Every car has three weapon slots, authored in `CarDefinition.loadout` and fired with **LMB**
(slot 1), **RMB** (slot 2) and **Space** (slot 3). An empty slot (`null` in the loadout) simply
never fires. Each slot holds one `WeaponConfig` asset that says everything about that weapon:
which muzzle it fires from, its timing, its shot, and what the shot does on a hit.

A weapon fires from either the **turret** — the front muzzle, aimed along the crosshair
(`CarController.AimDirection`, within the existing aim cone) — or from one or more **fixed**
muzzles pointing straight out of the front, rear, left or right face, aimed only by turning the
car. Never both; `MuzzleKind` is a single choice so "turret and fixed at once" can't be
configured. Every muzzle on every car fires at the same **fire height** above the car's floor
(`WeaponsConfig.fireHeight`, placeholder 0.6 m).

The shot itself is a straight sphere sweep at fixed speed with a maximum range — no gravity, no
homing, no inheriting the car's own velocity. On a hit it delivers a **payload**: damage, zero or
more timed effects, and an optional push. The same payload shape is reused for the weapon's
**self-effects**, applied to the firing car itself when the shot leaves — a wind-up cost or a
recoil debuff a weapon pays for firing.

See [combat.md](combat.md) for damage and effects, [effects.md](effects.md) for the effect
system, and [ramming.md](ramming.md) for the push math weapons share with rams.

## `WeaponConfig` and `WeaponsConfig`

One `WeaponConfig` asset per weapon, under `Assets/_Project/Configs/Weapons/`:

| Field | Meaning |
|---|---|
| `displayName` | Not read by code yet — for the Inspector and a future HUD |
| `delivery` | `WeaponDelivery` — one value, `Shot`, today. Later sub-projects append values (bursts, pierce, explosions, …); nothing here has to change to add them |
| `muzzle` | `MuzzleKind.Turret` or `MuzzleKind.Fixed` |
| `fixedMuzzles` | `[Flags] FixedMuzzles` (`Front`, `Rear`, `Left`, `Right`); read only when `muzzle` is `Fixed` |
| `cooldownSeconds` | Before this weapon can be pressed again. Starts on the press |
| `windUpSeconds` | Delay from the press until the shot leaves |
| `recoverySeconds` | The car-wide lock this press starts; must not exceed `cooldownSeconds` |
| `shot` | `ShotSettings { speed, range, radius }` — m/s, m, m |
| `hitPayload` | `Payload` landed on whatever the shot hits — see [Payload](#payload) |
| `selfEffects` | `EffectSpec[]` applied to the firing car when the shot leaves, `allowNonEnemy = true` |

`Payload { damageKind, damageAmount, effects, push }` and `EffectSpec { type, magnitude,
duration }` are the same reusable shapes a later hitbox (explosion, field, aura) will carry —
nothing about them is shot-specific. `damageAmount = 0` sends no damage request;
`push.speed = 0` means no push. `sourceTag` on every request a weapon sends is the `WeaponConfig`
asset's own `name` — attribution and logs read the weapon by its asset name, the same way ramming
tags its requests `"ram"`.

`WeaponsConfig` (`Assets/_Project/Configs/WeaponsConfig.asset`) holds the one rule shared by
every weapon on every car:

| Field | Placeholder | Meaning |
|---|---|---|
| `fireHeight` | 0.6 m | Height above the car's floor at which every muzzle on every car fires |

Referenced by `CarDefinition.weaponsConfig`, the same pattern as `effectsConfig`.

## Validation

`WeaponRules.Validate(WeaponConfig, float fireHeight, List<string> errors)` is pure and returns
true once `errors` is empty; every broken rule adds one readable message rather than stopping at
the first:

| Rule | Error when |
|---|---|
| Recovery | `cooldownSeconds < recoverySeconds` |
| Timings | any of `cooldownSeconds` / `windUpSeconds` / `recoverySeconds` is negative or non-finite |
| Muzzles | `muzzle == Fixed` and `fixedMuzzles == None` |
| Shot | `shot.speed`, `shot.range` or `shot.radius` is ≤ 0 or non-finite |
| Floor clearance | `shot.radius ≥ fireHeight` — the sweep would touch the floor at once |
| Damage | `hitPayload.damageAmount` is negative or non-finite |
| Effects | any `EffectSpec` (hit or self) has a negative/non-finite magnitude, or an unknown type, or a timed type with `duration ≤ 0` (`EffectInfo.IsTimed`) |
| Push | `hitPayload.push.speed < 0`; or `speed > 0` with `reelSeconds ≤ 0` or non-finite; or `spinScale` non-finite |

`WeaponRules.ValidatePayload` is the public entry a later hitbox (explosions, fields) validates
its own payload with, and is what `Validate` calls for `hitPayload`.

Two call sites, two severities:

- **Inspector** (`WeaponConfig.OnValidate`) runs it with `fireHeight = +∞`, which turns off the
  floor-clearance rule (there's no car in the Inspector), and logs each message as a
  **warning** naming the asset.
- **Spawn** (`WeaponModule`) runs it once per slot with the car's real `fireHeight` and logs each
  message as an **error**. A slot whose weapon fails validation is disabled — it behaves and
  draws exactly like an empty slot. This runs lazily, on first use (`Start`, the first `Step`, or
  the first `GetStatus`, whichever comes first) rather than only in `Start`: EditMode tests never
  run `Start`, and `CarFactory` assigns `config`/`loadout` after `AddComponent`, so `Start` alone
  would miss both — the same pattern `CarEffects`' host already uses.

## Timing

All slot state lives in `WeaponModule`, as absolute `Time.fixedTime` seconds so a cooldown keeps
running while the car is an inactive wreck — nothing needs to tick it. A fresh slot has
`cooldownEndsAt = −∞`. Every comparison uses a `1e-4` s tolerance (`WeaponTiming.Tolerance`),
matching effects.

- **Press.** Starts both the cooldown (`cooldownEndsAt = now + cooldownSeconds`) and the
  car-wide **recovery lock** (`lockEndsAt = now + recoverySeconds`, owned by that slot) at once,
  and snapshots the car's effective Attack. A press is accepted (`WeaponTiming.CanPress`) only
  when the slot has a valid weapon, Fire is allowed, the slot isn't already winding up, its own
  cooldown has elapsed, and the lock isn't running. **The lock blocks every slot, including the
  one that started it** — a weapon can't be re-pressed during its own recovery even once its
  cooldown allows it, since the lock outlasts nothing shorter. A press that can't be accepted is
  dropped outright, never queued for later.
- **Wind-up.** If `windUpSeconds ≤ tolerance` the shot leaves in the same step as the press.
  Otherwise the slot starts winding up and releases `windUpSeconds` later
  (`WeaponTiming.ShouldRelease`). Cooldown and lock are independent of the wind-up — tune
  `recoverySeconds` to cover a slow wind-up if that's the intent.
- **Cancel.** If Fire becomes disallowed (Stunned, Suppressed, a wreck) while a slot is winding
  up, the wind-up is cancelled — the shot never leaves. Cooldown and lock are untouched; only
  the wind-up is lost. `WeaponModule` also subscribes to `IDamageable.Destroyed` and cancels
  every wind-up the instant the car dies — belt-and-braces, since the very next `Step` would
  already do the same thing once `FireAllowed()` sees the wreck (decision 16, see
  [Decisions](#decisions-awaiting-confirmation)).
- **Same-step presses: the lowest slot wins.** `WeaponModule.Step` processes queued presses in
  slot order 0, 1, 2. Pressing slot 0 starts the lock immediately, so slot 1's `CanPress` check
  later in the same step already sees the lock running and drops the press — unless
  `recoverySeconds` is 0, in which case the lock clears itself instantly and a second slot
  pressed in the same step can also fire.
- **Respawn.** `WeaponModule.ResetForRespawn` (`IRespawnable`) clears every slot's wind-up and
  the lock (`lockEndsAt = −∞`, no owner) and drops any still-queued press. **Cooldowns are left
  alone** — they keep counting down through death and respawn, same as effects don't reset a
  timer that isn't theirs. See [combat.md](combat.md#respawn) for the rest of what respawn
  resets.

`WeaponModule.Step` re-reads `FireAllowed()` for **every** slot on every pass — the wind-up loop
and the press loop each check it fresh per slot, rather than once for the whole step. A release
earlier in the same step can apply a self-effect that stuns or kills the firing car; a later slot
in that same step must see that fresh state, not a value captured before it happened.

## Input

`CarInput.firePressed` is an `int`, bit *i* set when slot *i*'s key went down this frame:
`LocalInputProvider` sets the bits from `Mouse.leftButton` / `Mouse.rightButton` /
`Keyboard.spaceKey`, each `.wasPressedThisFrame` — a fresh press only, never a hold repeating.
`NullInputProvider` (the dummy) always reports it zeroed.

Like `aimDeltaX`, this is a **per-frame event that must be consumed exactly once**:
`WeaponModule.FrameTick` ORs it into a private pending mask, and the next physics step
(`Tick` → `Step`) takes the mask and clears it. A press is therefore evaluated at the first
physics step after the frame that sampled it, and dropped if `CanPress` rejects it there — there
is no buffer beyond that one step. See
[architecture.md](architecture.md#input-is-sampled-exactly-once-per-update) for why input
sampling is split this way in general.

## Muzzles

`MuzzleRules` is pure, and reads the car's own collision box rather than any authored muzzle
transforms:

- **Floor and height.** The car root sits at the centre of its `BoxCollider`, so the floor is at
  `root.y − boxSize.y / 2`. Every muzzle fires at `floor + fireHeight`.
- **Horizontal offsets**, before rotation by the car's yaw: Front `(0, +length/2)`, Rear
  `(0, −length/2)`, Left `(−width/2, 0)`, Right `(+width/2, 0)`. Turret shares the Front
  position.
- **Directions.** Fixed muzzles fire flat along their own face normal (forward, −forward,
  −right, +right). The turret fires along flat-forward rotated by `AimYaw` — the same yaw the
  crosshair uses.
- **Multi-muzzle order.** `MuzzleRules.FixedOrder` is Front, Rear, Left, Right; a weapon with
  several fixed muzzles fires one shot per selected muzzle, in that order, all on the same
  release.

Muzzle position and the turret's direction are read **when the shot leaves**, not when the
weapon was pressed — a wind-up weapon fires from wherever the car and crosshair are at release
time, not where they were at press time.

A shot starts flush with the car's own face, so its sphere overlaps its own hurtbox on the very
first sweep; the own-car pass-through rule in [The shot](#the-shot) makes that harmless.

## Hurtboxes

`CarDefinition.hurtboxes` is a `HurtboxBox[]` — car-local boxes (`centre`, `size`, no rotation)
that `CarFactory` builds into one child GameObject named `"Hurtbox"` on the `Hurtbox` physics
layer, carrying a `Hurtbox` component (`Car` back-reference) and one trigger `BoxCollider` per
entry. A trigger collider joins the root Rigidbody's compound but adds no contacts, mass or
inertia — driving and ramming are unaffected.

**The `Hurtbox` layer (index 11) ignores every other layer, 0–31**, configured the same way as
`Arena`/`Car`/`Wreck` in `PhysicsLayers.ConfigureCollisions()`. It is reached only by a shot's
own `Physics.SphereCastAll` query (`QueryTriggerInteraction.Collide`), never by an
`OnCollisionEnter`/`OnTriggerEnter` callback — nothing else in the game ever "touches" a
hurtbox. A missing `Hurtbox` layer logs an error from the same place the other three layers do.

**Empty or null `hurtboxes` → `Debug.LogError` naming the `CarDefinition`; no hurtbox child is
built at all, and that car simply can't be hit by weapons.** Bastion always ships one box sized
to its collision box (see [Test weapons](#test-weapons)).

**A box (or every box) not spanning `fireHeight` → `Debug.LogWarning`** (`HurtboxRules.SpansHeight`,
checked by `CarFactory` after building): a shot at the global fire height could pass clean
through the car. This is a warning, not a build failure — a car is still built and playable,
just possibly unhittable at that one height.

**The hurtbox child rolls with a wreck.** `WreckSequence` rotates every direct child of the car
root except `DriverAnchor` for the barrel-roll visual (see
[combat.md](combat.md#respawn)), and the hurtbox child is an ordinary child like the visual
body — so its trigger boxes visually tumble along with the wreck. This is harmless: a wreck is
never hittable regardless of where its colliders end up, because `ShotRules` excludes it through
the **`Targetable`** check (see [The shot](#the-shot)), not by hiding or disabling the hurtbox
geometry.

## The shot

`Shot.Launch(in ShotLaunch)` (`ShotRules` decides what stops it) spawns a plain GameObject (a small unlit sphere for a visual, no
collider, no Rigidbody) and drives itself from `FixedUpdate`. Each physics step:

1. `step = min(speed × dt, remaining)` (`ShotRules.StepDistance`).
2. `Physics.SphereCastNonAlloc` sweeps a sphere of `shot.radius` from the current position along
   the flat, normalised `direction` for `step`, against the `Hurtbox | Arena` layer mask, with
   `QueryTriggerInteraction.Collide` (hurtboxes are triggers).
3. Each hit becomes a `ShotCandidate { distance, wall, ownCar, targetable, enemy }`: `wall` is
   set for an Arena-layer hit; otherwise the collider's `Hurtbox` component gives the car, and
   `targetable = car.Abilities.Has(Targetable) && !IsDestroyed`, `enemy =
   Hostility.AreEnemies(source, car)`. A hit reported at `distance == 0` (the sweep started
   inside the collider — the shot's own muzzle overlap) uses `collider.ClosestPoint(position)`
   for its point instead of the meaningless zero-distance point.
4. `ShotRules.Resolve(candidates, count)` (pure) returns the nearest **stopper** or −1. A stopper
   is a wall, or a car that is **not** the shot's own, **is** targetable, and **is** an enemy —
   everything else (a wreck, the shot's own car, a non-enemy) is passed straight through.
   **At equal distance a car beats a wall** (`ShotRules.IsStopper` breaks the tie toward the
   car). Two boxes belonging to one car still resolve to a single hit, since only the nearest
   stopper is taken.
5. A car stopper: the payload lands (see [Payload](#payload)) with the swept hit point, the
   travel direction, and the sphere's centre at impact; the shot is destroyed. A wall stopper:
   the shot is destroyed with no payload. No stopper: the shot advances by `step`,
   `remaining -= step`, and is destroyed once `remaining ≤ 1e-4` (out of range).

Shots pass through each other — nothing in `Shot` queries other shots. A shot keeps flying if its
source car dies or despawns mid-flight; its `attack` was already snapshotted at the press, so it
needs nothing more from the source car once launched.

**`_spent` guard.** `Shot.Perish()` is private and marks the shot spent *before* destroying the
GameObject — `Destroy()` (play mode) or `DestroyImmediate()` (edit mode, since `Destroy` is
illegal there and EditMode tests need it) is deferred to end of frame, not immediate. If a single
frame runs more than one physics step, a shot that already hit or ran out of range this step must
not sweep again from the same spot on a second `FixedUpdate` before the deferred destroy takes
effect — `Advance` returns immediately once `_spent` is true, so a shot's payload can never land
twice. Covered by `ShotTests`.

## Payload

**`PayloadRules.PushDelta(PushSpec, travelDirection, hitboxCentre, targetCentre)`** (pure)
returns the flat velocity change a push gives its target, length `push.speed`: `AlongTravel`
uses the flat travel direction; `AwayFromCentre` uses the flat vector from the hitbox's centre to
the target's centre, and **falls back to the travel direction** if that vector is (near) zero —
a hit dead-centre has no "away" to push toward. `speed ≤ 0` or no usable direction → zero vector,
no push.

**`PayloadApplier.Apply(in Payload, in PayloadHit)`** is where a payload actually touches
components. **The applier itself refuses a target that isn't `Targetable`, isn't an enemy of the
source, or is already destroyed** — even though a shot's own `ShotRules.Resolve` step already
filtered to exactly such a target before calling in. This isn't redundant: it's what lets every
future non-shot caller (an explosion's radius, a floor zone, an aura) send a payload straight to
`PayloadApplier` and inherit "live enemies only" for free, without re-implementing `ShotRules`'
sweep-specific filtering.

Order, once a target passes those checks (decision — effects first, so a Stunned or a kill in the
effects list lands before the push tries to move a car that might already be gone):

1. **Effects.** Each `EffectSpec` → `IEffectReceiver.Apply { source, sourceTag, type, magnitude,
   duration, attack = hit.attack }` — hostility and Targetable are re-checked inside `Apply`
   itself, same as any other effect source (see [effects.md](effects.md#applying-one)).
2. **Stop if the target is now destroyed** (an Overheated whose first tick just killed it, say).
3. **Push**, when `push.speed > 0`: `dv = PushDelta(...)`; `Rigidbody.linearVelocity += dv`; if
   the target has a `BoxCollider`, `angularVelocity.y += PushMath.SpinDelta(point,
   target.position, dv, box.size.x, box.size.z, push.spinScale)` — the same spin formula ramming
   uses (**`PushMath.SpinDelta`**, Core; `RamRules.SpinDelta` is now a one-line delegate to it,
   `RamRulesTests` unchanged — see [ramming.md](ramming.md#the-shove)). Then **Reeling** is
   applied: `{ source, sourceTag, duration = push.reelSeconds }` — a push always reels, and
   `WeaponRules.Validate` enforces `reelSeconds > 0` whenever `push.speed > 0`.
4. **Damage**, when `damageAmount > 0`: `IDamageable.Apply { source, sourceTag, damageKind,
   damageAmount, attack = hit.attack }`.

A missing `IEffectReceiver`, `IDamageable` or `BoxCollider` on the target just skips that step —
the dummy and the player always carry all three, so this only matters for a hypothetical future
target that doesn't.

## `IWeaponSlots` and the HUD

`WeaponModule` implements `IWeaponSlots` (Core), the read-only contract the HUD reads:

```
int SlotCount { get; }                    // 3
WeaponSlotStatus GetStatus(int slot);     // out-of-range → unassigned (default struct)

struct WeaponSlotStatus { bool assigned; float cooldownRemaining; float cooldownDuration; bool blocked; }
```

- `assigned` — the slot holds a weapon that passed validation.
- `cooldownRemaining = max(0, cooldownEndsAt − Time.fixedTime)`; `cooldownDuration` is the
  weapon's own `cooldownSeconds`.
- `blocked` — assigned, **and** (Fire isn't allowed, **or** the recovery lock is running and
  owned by a *different* slot). **The lock's own slot never shows blocked for its own recovery**
  — validation guarantees `cooldownSeconds ≥ recoverySeconds`, so the slot that started the lock
  is always still on cooldown for the whole time its own lock runs, and shows only the grey
  drain, never the red slash, for that reason.

`WeaponSlotsWidget` (HUD assembly) is added by `GameBootstrap` alongside the other widgets
(`viewer = player`) and reads the viewer's `IWeaponSlots` every `LateUpdate`. See
[hud.md](hud.md#weapon-slots) for the on-screen contract.

## Debug menus

`WeaponModule` carries three Editor-only `[ContextMenu]` entries, **"Debug: fire slot 1"**
through **"Debug: fire slot 3"**. Each just queues that slot's pending bit
(`WeaponModule.Press(slot)`), exactly as if the key had been pressed that frame — the normal
`CanPress`/cooldown/lock/wind-up rules still apply, and the outcome is logged. This is how the
stationary dummy (which has no input provider driving it) is made to fire at the player for
testing — see checklist row 45 in [workflow.md](workflow.md#acceptance-checklist).

## Test weapons

`ConfigAssetBootstrap.CreateDefaults` creates (only when missing — re-running never clobbers
tuning) `WeaponsConfig` and three weapons in `Configs/Weapons/`, and fills Bastion's
`weaponsConfig`, `loadout` and `hurtboxes` when they're empty:

| Asset | Muzzle | Cooldown / wind-up / recovery | Shot speed / range / radius | Hit payload | Self |
|---|---|---|---|---|---|
| `TestTurret` | Turret | 0.5 / 0 / 0.2 s | 60 / 60 / 0.25 m | Flat 50 | — |
| `TestFrontRear` | Front + Rear | 3 / 0.5 / 1 s | 40 / 40 / 0.35 m | Flat 80, Corroded 30% 4 s | — |
| `TestSides` | Left + Right | 4 / 0 / 0.5 s | 50 / 30 / 0.4 m | Flat 40, push 8 m/s AlongTravel spin 1 reel 1 s | Spiked 20% 2 s |

All numbers are placeholders for the user to tune — see [tuning.md](tuning.md). Bastion's
`hurtboxes` gets one box matching its collision box exactly (`centre = 0`,
`size = (width, height, length)`).

## Tests

| Fixture | Count |
|---|---|
| `WeaponRulesTests` | 12 |
| `WeaponTimingTests` | 10 |
| `MuzzleRulesTests` | 6 |
| `ShotRulesTests` | 7 |
| `ShotTests` | 1 |
| `PayloadRulesTests` | 5 |
| `PayloadApplierTests` | 8 |
| `PushMathTests` | 3 |
| `HurtboxRulesTests` | 4 |
| `WeaponModuleTests` | 18 |
| `WeaponSlotLayoutTests` | 4 |
| `HudShapesTests` | 3 |
| `WeaponSlotsWidgetTests` | 2 |

`WeaponModuleTests` exercises `WeaponModule` end to end on a real `CarController`/`Health`/
`CarEffects`, without a scene: an accepted press starting the cooldown, a dropped press during
cooldown never remembered, the recovery lock blocking every other slot while leaving the owning
slot's status un-blocked, same-step lowest-slot-wins, wind-up release timing, a Fire block during
wind-up cancelling the shot while keeping the cooldown, a dropped press while blocked, death
cancelling a wind-up, respawn clearing the lock but not cooldowns, self-effects landing on
release rather than on the press, the attack snapshot being taken on the press, fixed muzzles
firing one shot each in order, an invalid weapon disabling its slot, a self-Stun on release still
letting that slot's own shot out while blocking a same-step press on another slot, a missing
`WeaponsConfig` disabling every slot, a loadout longer than three warning and truncating, and the
turret reading its direction at release rather than at the press. `PayloadApplierTests` covers
effects landing, push velocity and spin, Reeling, damage, and stopping after effects already
killed the target. `CarFactoryTests` and `PhysicsLayersTests` gained cases for the hurtbox child,
its layer, its boxes, the empty-list error, and `Hurtbox` ignoring every layer. Shot sweeps
against real scene colliders are verified in play (the checklist), not in EditMode — see
[workflow.md](workflow.md#acceptance-checklist).

## Decisions (awaiting confirmation)

Settled by Claude where the user gave no rule during the "weapon core" design pass — spec
[2026-09-15-weapon-core-design.md §13](specs/2026-09-15-weapon-core-design.md#13-decisions-for-confirmation)
lists these:

1. `WeaponDelivery` has one value, `Shot`; later deliveries append.
2. `sourceTag` is the weapon config asset's own `name`.
3. Floor-clearance validation: a shot's radius must be below the fire height.
4. An invalid weapon's slot is disabled and drawn empty; errors at spawn, warnings in the
   Inspector.
5. A slot can't be re-pressed while its own wind-up is pending.
6. Several slots pressed in one step: the lowest slot wins; the rest are dropped, unless the
   winning slot's recovery is 0.
7. Muzzle position and turret direction are read when the shot leaves, not on the press.
8. Equal-distance wall and enemy: the enemy is hit.
9. Payload order: effects → (stop if killed) → push + Reeling → damage.
10. `AwayFromCentre` with coincident centres falls back to the travel direction.
11. The lock's own slot doesn't show the blocked sign during its own recovery.
12. HUD sizes, colours and positions as built in `WeaponSlotsWidget`/`WeaponSlotLayout`/
    `HudShapes`; test weapon numbers as in the table above.
13. Shots keep flying after their source dies; shots pass through each other.
14. The hurtbox layer ignores every layer; wrecks are skipped by the `Targetable` check.

Two further rulings, not in the spec's own §13, made while implementing against the code rather
than the design doc — also awaiting confirmation:

15. **A self-effect that stuns or kills the firing car does not cancel the shot that is
    leaving.** `WeaponModule.Release` applies self-effects and then unconditionally launches the
    shot for that slot in the same call — it doesn't re-check `FireAllowed()` between the two.
    The stun or kill still blocks *other* slots pressed in the same step (`Step` re-reads
    `FireAllowed()` per slot — see [Timing](#timing)), just not the shot already committed to
    leaving. Covered by `WeaponModuleTests.SelfStunOnRelease_BlocksAPressQueuedInTheSameStep`.
16. **The `IDamageable.Destroyed` subscription in `WeaponModule` is defence-in-depth, not the
    only path.** It cancels every wind-up the instant the car dies. But `Step` already re-reads
    `FireAllowed()` (which checks `IsDestroyed`) at the top of its wind-up loop every physics
    step regardless, so the very next `Step` after death would cancel the same wind-ups on its
    own even without the subscription. The subscription exists so a dead car's wind-ups clear
    immediately rather than surviving until the next physics step.
