# Weapon core — design spec (weapons sub-project 3 of 6)

Date: 2026-09-15. Status: brainstormed with the user; the user asked for the "Iterative
implementation workflow" (sections → self-review → plan → self-review → SDD) in one pass.
Gameplay rules come from the user. Items marked **(decision)** were settled by Claude where the
user gave no rule, and are listed in §13 for confirmation.

Inputs: `2026-09-14-weapons-requirements.md` (the user's brief),
`2026-09-14-combat-stats-damage-design.md` (sub-project 1) and
`2026-09-14-effects-respawn-design.md` (sub-project 2). The closed-spec rule applies: no major
rework of earlier work; small append-only edits to composition roots are fine.

## 0. Scope

**In:**
1. **Loadout** — three slots per car, authored in `CarDefinition`, fired with LMB / RMB / Space.
2. **Weapon config** — one `WeaponConfig` asset per weapon: muzzle, timing, shot, hit payload,
   self-effects; validated.
3. **Timing** — cooldown, wind-up, recovery lock, exactly as in §3.
4. **Muzzles** — four fixed muzzles (front, rear, left, right) and a front turret that follows the
   crosshair; one global fire height.
5. **Hurtboxes** — a list of car-local boxes per car.
6. **The basic shot** — straight, fixed speed, max range, sphere radius; hit detection by sweeps.
7. **Payload** — damage, effects, and an optional push (speed, direction mode, spin, required
   Reeling). One reusable block, so later hitboxes (explosions, fields, auras) carry their own.
8. **Slots HUD** — three circles top-centre: cooldown overlay, blocked sign.
9. **Debug hooks** — Inspector "Debug: fire slot N" on any car; three test weapons on Bastion.

**Out:** bursts, spread, pierce, bounce, homing, explosions and after-effects (sub-project 4);
floor areas, auras, beams (5); maneuvers and self-states (6); icons, audio, shot pooling, bots,
netcode.

## 1. Gameplay rules (from the user)

| Rule | Detail |
|---|---|
| Slots | 3 slots, one key each: slot 1 LMB, slot 2 RMB, slot 3 Space |
| Press | Each shot needs a fresh press. A press that can't be accepted is dropped, never remembered. Holding doesn't repeat |
| Muzzle kind | A weapon fires either from the **turret** or from one or more **fixed** muzzles — never both |
| Fixed muzzles | Front, rear, left, right. Point straight out of the chassis; aimed by turning the car |
| Turret | At the front muzzle's position; fires along the crosshair (`CarController.AimDirection`), within the existing aim cone |
| Fire height | One global height from the floor (placeholder 0.6 m) for every muzzle on every car |
| Muzzle positions | Derived from the car box: centre of the front face, rear face, left face, right face; turret = front |
| Cooldown | Starts **on the press** |
| Recovery lock | Also starts on the press, runs `recoverySeconds`. While it runs **no weapon** can be pressed, including the one that started it |
| Wind-up | The shot leaves `windUpSeconds` after the press. Independent of recovery: the user tunes recovery to cover wind-up if wanted |
| Interruption | Fire blocked (Stunned, Suppressed, wreck) or destroyed during wind-up cancels the shot. Cooldown and lock keep running |
| Config check | `cooldownSeconds < recoverySeconds` is a configuration error (§2.3) |
| Attack snapshot | Taken **on the press** |
| Self-effects | Applied **when the shot leaves**, only if it does |
| Respawn | Cooldowns keep running through death and respawn; the lock and any wind-up are cleared |
| Shot speed | Fixed world speed along the muzzle direction; does not inherit car velocity |
| Shot reach | Max range in metres |
| Hit targets | Live enemies only. Shots pass through their own car and through wrecks |
| Hit detection | Sphere sweeps against hurtboxes and the arena (approach B) |
| Hurtboxes | Composed of car-local boxes (centre + size, no rotation) in `CarDefinition`; Bastion has one. Empty list → error, car unhittable |
| Push | Optional per payload. Speed in m/s added to the target's velocity, mass ignored, strength/resistance ignored, horizontal only |
| Push direction | Per payload: along travel, or away from the hitbox centre |
| Push spin | Per payload `spinScale`, using the ram spin formula at the hit point |
| Push reel | A push always applies Reeling; its duration is part of the push config and required |
| Push vs states | Armored doesn't block the push; a stunned car is pushed |
| Explosions later | Carry their own payload (and push) — the payload block is reusable |
| HUD | Three circles top-centre; key label inside until icons exist; dark-grey overlay drains top-first over the cooldown; red border + diagonal slash when blocked for any reason other than cooldown; both can show at once; visible (blocked) while dead; empty slot = faint circle |

## 2. Data

### 2.1 `WeaponsConfig` (global, `Assets/_Project/Configs/WeaponsConfig.asset`)

| Field | Placeholder | Meaning |
|---|---|---|
| `fireHeight` | 0.6 m | Height above the car's floor at which every muzzle fires |

Referenced by `CarDefinition.weaponsConfig` (the same pattern as `effectsConfig`).

### 2.2 `WeaponConfig` (one asset per weapon, `Assets/_Project/Configs/Weapons/`)

```
WeaponConfig : ScriptableObject
  string          displayName
  WeaponDelivery  delivery          // enum { Shot } — sub-projects 4–6 append values (decision)
  MuzzleKind      muzzle            // enum { Turret, Fixed }
  FixedMuzzles    fixedMuzzles      // [Flags] { None=0, Front=1, Rear=2, Left=4, Right=8 }; used only when Fixed
  float           cooldownSeconds
  float           windUpSeconds
  float           recoverySeconds
  ShotSettings    shot              // speed (m/s), range (m), radius (m)
  Payload         hitPayload
  EffectSpec[]    selfEffects

[Serializable] ShotSettings { float speed; float range; float radius; }
[Serializable] Payload      { DamageKind damageKind; float damageAmount; EffectSpec[] effects; PushSpec push; }
[Serializable] EffectSpec   { EffectType type; float magnitude; float duration; }
[Serializable] PushSpec     { float speed; PushDirection direction; float spinScale; float reelSeconds; }
enum PushDirection { AlongTravel, AwayFromCentre }
```

The single-choice `MuzzleKind` makes "turret and fixed at once" unrepresentable. `sourceTag`
for every request a weapon sends is the config asset's `name` (decision). `damageAmount = 0`
sends no damage request; `push.speed = 0` means no push.

### 2.3 Validation — `WeaponRules.Validate(WeaponConfig, float fireHeight, List<string> errors)`

Pure static; returns true when `errors` is empty. Each rule adds one readable message:

| Rule | Error when |
|---|---|
| Recovery | `cooldownSeconds < recoverySeconds` (the user's rule) |
| Timings | any timing is negative or non-finite |
| Muzzles | `muzzle == Fixed` and `fixedMuzzles == None` |
| Shot | `speed ≤ 0`, `range ≤ 0` or `radius ≤ 0` (or non-finite) |
| Floor clearance | `radius ≥ fireHeight` — the sweep would hit the floor at once (decision) |
| Damage | `damageAmount < 0` or non-finite |
| Effects | any `EffectSpec` (hit or self) whose magnitude is negative/non-finite, or whose type is timed and duration ≤ 0 (uses `EffectInfo.IsTimed`) |
| Push | `push.speed < 0`, or `push.speed > 0` with `reelSeconds ≤ 0`, or non-finite values |

Where it runs:
- **Inspector:** `WeaponConfig.OnValidate` runs it with `fireHeight = +∞` (which disables the
  floor rule) and logs each message as a **warning** naming the asset.
- **Spawn:** `WeaponModule` runs it once for each slot with the car's `fireHeight` and logs each
  message as an **error**. An invalid weapon's slot is disabled — it behaves and draws as empty
  (decision). It runs lazily on first use (`Start`, the first `Step` or the first `GetStatus`,
  whichever comes first) rather than only in `Start`, because EditMode tests never run `Start` and
  `CarFactory` assigns the fields after `AddComponent` — the same pattern as `CarEffects`' host.

### 2.4 `CarDefinition` additions (append-only)

```
[Header("Weapons")]
WeaponsConfig  weaponsConfig
WeaponConfig[] loadout        // length 3; null entry = empty slot
HurtboxBox[]   hurtboxes      // car-local boxes

[Serializable] HurtboxBox { Vector3 centre; Vector3 size; }   // Core type (§5)
```

Bastion's asset: `loadout = [TestTurret, TestFrontRear, TestSides]`, `hurtboxes = [{centre 0,
size = collision box (2.2075, 1.3422, 4.6568)}]`.

## 3. Timing

### 3.1 State (per car, in `WeaponModule`; decisions made by the pure static `WeaponTiming`)

```
struct SlotTiming { float cooldownEndsAt; bool windingUp; float releaseAt; float attack; }
float lockEndsAt;          // car-wide
int   lockOwner;           // slot that started the lock, −1 when none
```

All times are `Time.fixedTime` seconds (absolute), so a cooldown keeps running while the car is
an inactive wreck — no ticking needed. A fresh slot has `cooldownEndsAt = −∞`. Comparisons use a
tolerance of 1e-4 s, matching effects.

### 3.2 Rules (`WeaponTiming`, pure)

- `CanPress(slot, now, fireAllowed, lockEndsAt)` is true only when **all** hold: the slot has a
  valid weapon; `fireAllowed`; `now ≥ slot.cooldownEndsAt − tol`; `now ≥ lockEndsAt − tol`; the
  slot is not already winding up (decision — only reachable if wind-up > cooldown).
- `Press(ref slot, ref lock, slotIndex, now, cooldown, windUp, recovery, attack)`:
  `cooldownEndsAt = now + cooldown`; `lockEndsAt = now + recovery`, `lockOwner = slotIndex`;
  `attack` stored; returns **release now** when `windUp ≤ tol`, otherwise sets `windingUp = true`,
  `releaseAt = now + windUp`.
- `ShouldRelease(slot, now)`: `windingUp && now ≥ releaseAt − tol`; the caller then clears
  `windingUp`.
- `Cancel(ref slot)`: `windingUp = false`. Cooldown and lock untouched.
- `ResetForRespawn`: every slot's `windingUp = false`; `lockEndsAt = −∞`, `lockOwner = −1`.
  Cooldowns untouched.

### 3.3 Input

`CarInput` gains `int firePressed` — bit *i* set when slot *i*'s key went down this frame
(append-only Core edit). `LocalInputProvider` sets bits from `Mouse.leftButton`,
`Mouse.rightButton`, `Keyboard.spaceKey` `.wasPressedThisFrame`. `NullInputProvider` is unchanged
(zeroed).

A press is a per-frame event like `aimDeltaX` and must be consumed once: `WeaponModule.FrameTick`
ORs `firePressed` into a private pending mask; the next `Tick` takes the mask and clears it. A
press is therefore evaluated at the first physics step after the frame that sampled it, and
dropped if not accepted there — no buffer.

### 3.4 The step (`WeaponModule.Tick` → public `Step(float now)` for tests)

1. `fireAllowed = car.Abilities.Has(Fire) && !(health?.IsDestroyed ?? false)`.
2. **Wind-ups**, slots in order: if `windingUp` and `!fireAllowed` → `Cancel` (logged); else if
   `ShouldRelease` → release (§3.5).
3. **Presses**, slots 0, 1, 2 in order, for each pending bit: if `CanPress` → `Press` (then release
   at once when there is no wind-up). Because the lock blocks every weapon, the lowest slot pressed
   in a step wins and the rest are dropped (decision). A slot whose recovery is 0 sets no lasting
   lock, so another slot pressed in the same step can also fire.
4. Pending mask cleared.

`WeaponModule` also subscribes to `IDamageable.Destroyed` and cancels every wind-up at once.

### 3.5 Release

1. **Self-effects**: each `EffectSpec` → `IEffectReceiver.Apply` on the own car with
   `source = car`, `allowNonEnemy = true`, `sourceTag = config.name`, `attack = slot.attack`.
2. **Shots**: one shot per muzzle — the turret, or each set fixed muzzle in the order Front,
   Rear, Left, Right. Muzzle positions and the turret's direction are read **at release**, not at
   the press (decision): a wind-up weapon fires from where the car and crosshair are when it fires.

## 4. Muzzles (`MuzzleRules`, pure)

Inputs: car root position and rotation, collision box size (`BoxCollider.size`), `AimYaw`,
`fireHeight`.

- Car floor height = `root.y − boxHeight / 2` (the root is the box centre and the car is grounded).
- Muzzle y = floor + `fireHeight`.
- Local horizontal offsets: Front `(0, +L/2)`, Rear `(0, −L/2)`, Left `(−W/2, 0)`, Right
  `(+W/2, 0)`, Turret = Front; rotated by the car's yaw only.
- Directions: flat forward, −forward, −right, +right; Turret = flat forward rotated by `AimYaw`.

A shot starts on the car's own face, so its sphere overlaps its own hurtbox; the own-car pass
rule (§6) makes that harmless.

## 5. Hurtboxes

**Core additions:**
- `HurtboxBox { Vector3 centre; Vector3 size; }` — serializable, car-local.
- `Hurtbox : MonoBehaviour` with `CarController Car` — sits on the hurtbox child object and
  maps its colliders back to the car.
- `PhysicsLayers.HurtboxName = "Hurtbox"`, `Hurtbox` index property; `ConfigureCollisions()`
  appends: `Hurtbox` ignores every layer (0–31) — it's reached only by queries, never by
  contacts or trigger events. A missing `Hurtbox` layer logs an error from the same place.
  `TagManager.asset` gets `Hurtbox` at index 11.
- `HurtboxRules` (pure): `SpansHeight(boxes, boxHeight, fireHeight)` — true when at least one box's
  vertical range, measured from the car floor (`centre.y ± size.y/2 + boxHeight/2`), contains
  `fireHeight`.

**Build (`CarFactory`, append):** a child `"Hurtbox"` on layer `Hurtbox` with the `Hurtbox`
component and one trigger `BoxCollider` per entry (`center`, `size`). A trigger collider joins the
root Rigidbody's compound but adds no contacts, mass or inertia, and `RespawnRule`'s spawn check
queries only `Car`.
- Empty or null list → `Debug.LogError` naming the definition; no hurtbox is built; the car
  can't be hit by weapons.
- Boxes not spanning `fireHeight` → `Debug.LogWarning` (shots could pass the car).

The hurtbox child keeps its layer when the root becomes a `Wreck` (`WreckSequence` and
`CarRespawn` set the root's layer only); wrecks are excluded by the `Targetable` check in §6.

## 6. The basic shot

`Shot : MonoBehaviour` (Weapons), created by `Shot.Launch(in ShotLaunch)`:

```
struct ShotLaunch { CarController source; string sourceTag; Vector3 origin; Vector3 direction;
                    ShotSettings settings; Payload payload; float attack; }
```

A plain GameObject with a small sphere visual (primitive, collider removed), no Rigidbody, no
collider. Each `FixedUpdate`:

1. `step = min(speed × dt, remaining)`.
2. `Physics.SphereCastAll(position, radius, direction, step, Hurtbox | Arena, QueryTriggerInteraction.Collide)`.
3. Each hit becomes a `ShotCandidate { distance; bool wall; bool ownCar; bool targetable; bool enemy; }`
   — `wall` for the Arena layer; otherwise the `Hurtbox` component's car, with
   `targetable = Abilities.Has(Targetable) && !IsDestroyed` and `enemy = Hostility.AreEnemies(source, car)`.
   A hit with `distance == 0` (started inside the collider) uses `collider.ClosestPoint(position)`
   as its point.
4. `ShotRules.Resolve(candidates)` (pure) returns the index of the nearest **stopper**, or −1: a
   wall, or a car that is not own, targetable and an enemy. Everything else passes. At equal
   distance a car beats a wall (decision). Two boxes of one car resolve to one hit (the shot stops
   at the first).
5. Stopper is a car → apply the hit payload (§7) with the hit point, travel direction and the
   sphere's centre at impact; destroy the shot. Stopper is a wall → destroy the shot. None → move
   `step`, `remaining −= step`; `remaining ≤ 1e-4` → destroy.

Shots pass through each other. A shot keeps flying if its source car dies or despawns; its attack
is already snapshotted.

## 7. Payload

**Core addition — `PushMath.SpinDelta(contactPoint, centre, pushDelta, width, length, spinScale)`**:
the ram spin formula moved to Core so both modules share it. `RamRules.SpinDelta` becomes a
one-line call to it (the only edit to ramming; behaviour and `RamRulesTests` unchanged).

**`PayloadRules` (pure):**
- `PushDelta(PushSpec, travelDirection, hitboxCentre, targetCentre)` → flat vector of length
  `speed`. AlongTravel: flat travel direction. AwayFromCentre: flat `targetCentre − hitboxCentre`;
  if that is (near) zero, falls back to flat travel (decision). `speed ≤ 0` or no usable direction
  → zero.

**`PayloadApplier.Apply(in Payload, in PayloadHit)`** (Weapons, touches components):

```
struct PayloadHit { CarController source; string sourceTag; float attack; CarController target;
                    Vector3 point; Vector3 travelDirection; Vector3 hitboxCentre; }
```

Order (decision — effects first so a Stunned in the list doesn't cancel the push that follows):
1. **Effects:** each `EffectSpec` → `IEffectReceiver.Apply { source, sourceTag, type, magnitude,
   duration, attack }` (hostility applies).
2. If the target is now destroyed (an Overheated first tick), stop.
3. **Push** (when `push.speed > 0`): `dv = PushDelta(...)`; `Body.linearVelocity += dv`;
   angular velocity y `+= PushMath.SpinDelta(point, target.position, dv, box.x, box.z, spinScale)`
   using the target's collision `BoxCollider` size; then **Reeling** `{ source, sourceTag,
   duration = reelSeconds }`.
4. **Damage** (when `damageAmount > 0`): `IDamageable.Apply { source, sourceTag, damageKind,
   damageAmount, attack }`.

Missing `IEffectReceiver` / `IDamageable` / `BoxCollider` on the target skips that part (the
dummy and player always have all three).

## 8. `WeaponModule` and the HUD contract

`WeaponModule : MonoBehaviour, ICarModule, IRespawnable, IWeaponSlots` replaces the seam.
`IWeapon.cs` is deleted (unused seam). Fields: `WeaponsConfig config`, `WeaponConfig[] loadout`,
`bool logWeapons`. `CarFactory` assigns both from the definition. A missing `config` logs an
error at validation and every slot is disabled. A `loadout` shorter than 3 leaves the missing
slots empty; entries past index 2 are ignored with a warning (decision).

**Core addition — `IWeaponSlots`:**

```
int SlotCount { get; }                       // 3
WeaponSlotStatus GetStatus(int slot);        // out-of-range → unassigned

struct WeaponSlotStatus { bool assigned; float cooldownRemaining; float cooldownDuration; bool blocked; }
```

- `assigned`: the slot has a weapon that passed validation.
- `cooldownRemaining = max(0, cooldownEndsAt − Time.fixedTime)`; `cooldownDuration` = the config
  value.
- `blocked` = assigned and (Fire not allowed — Stunned, Suppressed, wreck — **or** the recovery
  lock is running and `lockOwner` is a *different* slot). The lock's own slot is always on
  cooldown during its lock (validation guarantees cooldown ≥ recovery), so it shows the grey
  overlay only (decision).

## 9. HUD — `WeaponSlotsWidget` (HUD assembly, Core only)

Added by `GameBootstrap` like the other widgets (`viewer = player`). Reads the viewer's
`IWeaponSlots` every `LateUpdate`.

| Part | Placeholder |
|---|---|
| Position | top-centre, `topMargin` 24 ref px |
| Circle diameter / gap | 72 / 20 ref px |
| Background | filled circle, black α 0.35 |
| Key label | `LMB`, `RMB`, `SPC`, font 18, white (hidden on an empty slot) |
| Cooldown overlay | filled circle, dark grey (0.25, 0.25, 0.25, α 0.85), `Image.Type.Filled`, vertical, origin bottom, `fillAmount = remaining / duration` — the top clears first |
| Border | ring, 4 ref px, white α 0.9; red (0.9, 0.15, 0.15) when blocked |
| Slash | red bar 6 ref px thick across the diameter, top-left to bottom-right, shown when blocked |
| Empty slot | ring only, white α 0.25 |

Circle and ring sprites are generated in code once (`HudShapes`, antialiased edge). Pure
`WeaponSlotLayout`: `CentreX(index, count, diameter, gap)`, `CooldownFill(remaining, duration)`
(clamped 0–1; duration ≤ 0 → 0), `KeyLabel(index)`.

## 10. Test weapons and debug hooks

`ConfigAssetBootstrap` creates (only when missing) `WeaponsConfig` and three assets in
`Configs/Weapons/`, and fills Bastion's `weaponsConfig`, `loadout` and `hurtboxes` when empty.

| Asset | Muzzle | Cooldown / wind-up / recovery | Shot speed / range / radius | Hit payload | Self |
|---|---|---|---|---|---|
| `TestTurret` | Turret | 0.5 / 0 / 0.2 | 60 / 60 / 0.25 | Flat 50 | — |
| `TestFrontRear` | Front + Rear | 3 / 0.5 / 1 | 40 / 40 / 0.35 | Flat 80, Corroded 30% 4 s | — |
| `TestSides` | Left + Right | 4 / 0 / 0.5 | 50 / 30 / 0.4 | Flat 40, push 8 m/s AlongTravel, spin 1, reel 1 s | Spiked 20% 2 s |

All numbers are placeholders for the user to tune.

**Debug:** `WeaponModule` has Editor-only context menus "Debug: fire slot 1/2/3". Each sets that
slot's pending bit, so it goes through the normal rules at the next step and the outcome is
logged — this is how the dummy fires at the player.

## 11. Assemblies and composition

| Assembly | Change |
|---|---|
| Core | `CarInput.firePressed`; `HurtboxBox`, `Hurtbox`, `HurtboxRules`; `PhysicsLayers` Hurtbox; `PushMath`; `IWeaponSlots`, `WeaponSlotStatus` |
| Weapons (→ Core) | `WeaponsConfig`, `WeaponConfig` and its data types, `WeaponRules`, `WeaponTiming`, `MuzzleRules`, `ShotRules`, `PayloadRules`, `PayloadApplier`, `Shot`, `WeaponModule`; `IWeapon` deleted |
| Ramming | `RamRules.SpinDelta` delegates to `PushMath` |
| Controls | `LocalInputProvider` fire bits |
| Cars (+Weapons already) | `CarDefinition` fields; `CarFactory` builds hurtboxes and configures `WeaponModule` |
| HUD | `WeaponSlotsWidget`, `WeaponSlotLayout`, `HudShapes` |
| Bootstrap (+Weapons ref) | validates `weaponsConfig`; adds the widget |
| EditorTools (+Weapons ref) | creates assets and links them |
| ProjectSettings | `TagManager.asset` layer 11 `Hurtbox` |

## 12. Testing

**EditMode (pure):** `WeaponRulesTests` (every §2.3 rule), `WeaponTimingTests` (press
conditions, lock blocks all including owner, same-step lowest slot wins, wind-up release, cancel
keeps cooldown and lock, no re-press during own wind-up, respawn reset), `MuzzleRulesTests`,
`ShotRulesTests`, `PayloadRulesTests`, `PushMathTests`, `HurtboxRulesTests`,
`WeaponSlotLayoutTests`.

**EditMode (components):** `WeaponModuleTests` (accepted press → status; Fire blocked drops the
press; stun during wind-up cancels with cooldown kept; respawn reset; `blocked` flags; self-effects
applied on release; invalid config disables the slot), `PayloadApplierTests` (effects, push
velocity and spin, Reeling, damage, stop after a kill), `CarFactoryTests` (hurtbox child, layer,
boxes, empty-list error), `PhysicsLayersTests` (Hurtbox ignores all). Shot sweeps against real
colliders are verified in play (the checklist), not in EditMode.

**Acceptance checklist (appended to workflow.md):** turret follows the crosshair; fixed muzzles
fire straight out of each face; shots pass through the own car; the wall stops shots; range ends
shots; damage and Corroded land on the dummy; wind-up delay; a stun during wind-up cancels and the
circle keeps its cooldown; recovery blocks the other slots (red slash); TestSides pushes, spins and
reels the dummy and Spikes self; the dummy fires at the player via the debug menu; HUD circles
drain top-first; circles show blocked while dead and cooldowns carry over respawn; shots pass
through a wreck; ram rows unchanged.

## 13. Decisions for confirmation

1. `WeaponDelivery` enum with one value `Shot`, so later deliveries append.
2. `sourceTag` = the weapon config asset's name.
3. Floor-clearance validation: shot radius must be below the fire height.
4. An invalid weapon's slot is disabled and drawn as empty; errors at spawn, warnings in the Inspector.
5. A slot can't be re-pressed while its own wind-up is pending.
6. Several slots pressed in one step: the lowest slot wins, the rest are dropped (unless its recovery is 0).
7. Muzzle position and turret direction are read when the shot leaves, not on the press.
8. Equal-distance wall and enemy: the enemy is hit.
9. Payload order: effects → (stop if killed) → push + Reeling → damage.
10. AwayFromCentre with coincident centres falls back to the travel direction.
11. The lock's own slot doesn't show the blocked sign during its own recovery.
12. HUD sizes, colours and positions as in §9; test weapon numbers as in §10.
13. Shots keep flying after their source dies; shots pass through each other.
14. The hurtbox layer ignores every layer; wrecks are skipped by the `Targetable` check.
