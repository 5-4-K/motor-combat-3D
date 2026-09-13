# Ramming — design spec

Date: 2026-09-13. Status: approved in conversation, implementation pending.
Gameplay rules come from the user; items marked **(decision)** were proposed by Claude and
accepted when the user said to proceed.

## 1. Gameplay rules

### 1.1 What counts as a ram

1. Only **car-vs-car** contacts can be rams. Wall and ground contacts stay plain physics.
2. A car **qualifies as attacker** when all three hold:
   - the contact lies in its **Front** or **FrontCorner** region (§1.2);
   - its **forward speed** just before contact is `>= RamConfig.minRamSpeed`;
   - it is neither **Locked** nor **Reeling** (§1.6). **(decision)** — prevents a shoved
     victim sliding nose-first into a third car from chaining rams.
3. If no car qualifies, the contact is a plain physics bump — nothing in this spec touches it.
4. Rams trigger on **contact start only** (a new touch). Continuous pushing while already
   touching is plain physics.

### 1.2 Regions (the hitbox / hurtbox)

Regions are computed from the contact point in the car's local space against its collider
box (`width × length`, from `CarDefinition`). No extra colliders.

Let `x, z` be the local contact point, `hw = width/2`, `hl = length/2`,
`band = RamConfig.cornerBandMetres`, and distances to faces
`dFront = hl − z`, `dRear = hl + z`, `dSide = hw − |x|`.

| Region | Condition (checked in this order) |
|---|---|
| FrontCorner | `dSide <= band` and `dFront <= band` |
| RearCorner | `dSide <= band` and `dRear <= band` |
| Front / Rear / Side | otherwise, the face with the smallest distance (ties: Front, then Rear, then Side) |

Attack regions: **Front, FrontCorner**.

### 1.3 Ram type

Angles are between **flat** (y = 0) headings. `headOnAngle = RamConfig.headOnAngleDegrees`
(45). "Within" means `<=`.

| Victim region | Type |
|---|---|
| Side | **Flank** |
| Front, FrontCorner | **HeadOn** if angle(attackerFwd, −victimFwd) `<= headOnAngle`, else **Flank** |
| Rear, RearCorner | **Rear** if angle(attackerFwd, victimFwd) `<= headOnAngle`, else **Flank** |

### 1.4 Resolving a pair

Each car that **qualifies** (§1.1) is evaluated as attacker against the other with §1.3.
A car that does not qualify produces no evaluation.

1. Neither qualifies → **None** (plain bump).
2. Any evaluation yields HeadOn → **HeadOn** (both cars participate, even a car that did
   not qualify — e.g. a parked car).
3. Both qualify, neither HeadOn → higher forward speed is the attacker with its type.
   Exact tie → **HeadOn**. **(decision)**
4. Exactly one qualifies → that car attacks with its type.

### 1.5 Effects

Forward speed: `max(0, dot(preContactVelocity, flatForward))`.

Shove (velocity change, **mass ignored**):
```
shoveDv = flatForward_attacker × (attack_attacker / defense_victim) × forwardSpeed_attacker × typeScale
```
`typeScale` is `headOnScale`, `flankScale` or `rearScale`. `shoveDv.y = 0` always.
`defense` is clamped to a minimum of 0.01 so a misconfigured 0 cannot divide by zero.

Spin (flank and rear only), about world up, in rad/s:
```
r        = flat(contactPoint − victimCentre)
k²       = (width² + length²) / 12            // box radius of gyration squared
spinDw   = spinScale × cross(r, shoveDv).y / k²
```
With `spinScale = 1` this is exactly the yaw a real impulse `m·shoveDv` at `r` gives a solid
box of that footprint. Pushing a victim's tail toward its right gives negative yaw (nose
swings left).

**Flank / Rear**
- Attacker: horizontal velocity → 0, angular velocity → 0, then **Locked** for
  `attackerLockSeconds`.
- Victim: horizontal velocity → pre-contact horizontal velocity + `shoveDv`; yaw rate →
  pre-contact yaw rate + `spinDw`; then **Reeling** for `reelSeconds` (restarts if already
  reeling).

**HeadOn**
- Both cars: horizontal velocity → 0, angular velocity → 0.
- Each car then receives `shoveDv` computed from the **other** car's attack, forward speed and
  heading, and its own defense, with `headOnScale`. Applied through the centre — **no spin**.
- Both **Locked** for `attackerLockSeconds`. Neither reels.

Vertical velocity is never modified by any ram (gravity is left alone); X/Z rotation is already
frozen on every car, so no ram can flip a car.

### 1.6 States

A car has two independent timers. Lock sets `lockRemaining = max(lockRemaining, seconds)`;
reel sets `reelRemaining = seconds` (restart).

| | Normal | Locked | Reeling |
|---|---|---|---|
| Throttle / steer | used | ignored (treated as 0) | ignored |
| Aim | works | works | works |
| Drag | on | on | on |
| Grip | on | on | **off** |
| Yaw | set from steer | set from steer (= 0) | **not written by driving**; decays at `spinDecayRate` (1/s, `exp(−rate·dt)`) |
| Can attack | yes | no | no |

Reeling takes precedence when both timers run. When the reel ends, grip and steering resume
through their normal code paths (grip is itself a 1/s decay, so the slide eases out).

### 1.7 Numbers

Per car on `CarDefinition`: `attack` (default 1), `defense` (default 1, must be > 0),
`ramConfig` (reference).

`RamConfig` ScriptableObject — placeholder defaults, tuning belongs to the user:

| Field | Placeholder | Unit |
|---|---|---|
| `headOnScale` | 0.2 | × |
| `flankScale` | 1.5 | × |
| `rearScale` | 1.2 | × |
| `attackerLockSeconds` | 0.5 | s |
| `reelSeconds` | 1.0 | s |
| `minRamSpeed` | 3 | m/s |
| `spinScale` | 1 | × |
| `spinDecayRate` | 2 | 1/s |
| `headOnAngleDegrees` | 45 | ° |
| `cornerBandMetres` | 0.3 | m |

## 2. Architecture

### 2.1 Assemblies — no new cross-module references

```
Core      CarStatus (new, pure)          ← timers + CanDrive / IsReeling / CanAttack
          CarController                  ← owns Status; advances it; snapshots pre-step velocity
Driving   DrivingModule                  ← reads Core Status only
Ramming   RamConfig, RamRules (pure)     ← classification, shove, spin
          RammingModule                  ← adapter: collision → RamRules → Rigidbody + Status
Cars      CarDefinition, CarFactory      ← attack/defense/ramConfig wired into RammingModule
```

Driving never references Ramming; they meet only through `CarStatus` in Core. `Cars` already
references `Ramming`; the test assembly gains a `MotorCombat.Ramming` reference.

### 2.2 `CarStatus` (Core, plain C# class)

- `Lock(float seconds)`, `Reel(float seconds)`, `Advance(float dt)` (clamps at 0)
- `IsLocked`, `IsReeling`, `CanDrive => !IsLocked && !IsReeling`, `CanAttack => CanDrive`

### 2.3 `CarController` additions (state, not rules)

- `public CarStatus Status { get; } = new CarStatus();`
- `PreStepVelocity`, `PreStepAngularVelocity`: written at the **end** of `FixedUpdate`, after
  every module ticked — i.e. the velocity going into the physics step. `OnCollisionEnter`
  runs after that step, so these are the pre-contact values.
- `FixedUpdate` order: `Status.Advance(dt)` → module `Tick`s → snapshot.

Known approximation: forces added with `AddForce` in the same step are integrated inside the
physics step and are absent from the snapshot — at most `enginePower/mass × dt ≈ 0.5 m/s`.

### 2.4 `DrivingModule` carve-outs

- `throttle`, `steer` = 0 when `!Status.CanDrive`.
- When `Status.IsReeling`: skip the yaw write and skip grip. Thrust (zero) and drag still run.

This resolves the "Known behaviour that looks like a bug" note in `docs/driving-physics.md`
(side-on hits never spin) for rams; plain bumps are unchanged.

### 2.5 `RamRules` (Ramming, pure static)

```csharp
public enum RamRegion { Front, FrontCorner, Side, RearCorner, Rear }
public enum RamType   { None, HeadOn, Flank, Rear }

public struct RamParticipant { RamRegion region; Vector3 flatForward; float forwardSpeed; bool canAttack; }
public struct RamOutcome     { RamType type; int attacker; }   // 0 = a, 1 = b, -1 = none/head-on

RamRegion  Region(Vector3 localPoint, float width, float length, float cornerBand)
bool       IsAttackRegion(RamRegion region)
float      ForwardSpeed(Vector3 velocity, Vector3 flatForward)
RamType    Classify(RamRegion victimRegion, Vector3 attackerFwd, Vector3 victimFwd, float headOnAngle)
RamOutcome Resolve(in RamParticipant a, in RamParticipant b, float minRamSpeed, float headOnAngle)
float      ScaleFor(RamType type, float headOnScale, float flankScale, float rearScale)
Vector3    ShoveDelta(Vector3 attackerFlatFwd, float attack, float defense, float speed, float scale)
float      SpinDelta(Vector3 contactPoint, Vector3 victimCentre, Vector3 shoveDv, float width, float length, float spinScale)
float      DecaySpin(float yawRate, float rate, float dt)
```

All inputs are plain values, so every rule is testable without a scene.

### 2.6 `RammingModule` flow

`OnCollisionEnter(collision)`:
1. Raise today's `Collided` event as before.
2. Other object has no `RammingModule` → return (walls, ground).
3. Pair guard: if this pair was already resolved at this `Time.fixedTime` (by the other car's
   callback), return. The resolving module records `(partner, fixedTime)` on both modules.
4. Contact point = mean of all contact points.
5. Build a `RamParticipant` for each car from its flat forward, `BoxCollider.size`, local
   contact point, `PreStepVelocity`, and `Status.CanAttack`; call `RamRules.Resolve`.
6. `None` → return. Otherwise apply §1.5 to both Rigidbodies and `Status`es. Horizontal
   components only; keep each body's current `linearVelocity.y`.
7. Raise `Rammed(RamReport)` (type, attacker, victim, shove magnitude) and log when
   `logImpacts`.

`Tick` (FixedUpdate): when `Status.IsReeling` and `config` is set, write
`angularVelocity = up × DecaySpin(angularVelocity.y, spinDecayRate, dt)`.

`attack`, `defense`, `config` are public fields set by `CarFactory`. Width and length come from
the car's own `BoxCollider`. A missing `config` logs an error in `Start`. A pair is resolved
only when **both** modules have a `config`; otherwise the contact is plain physics.
`RamConfig` is global, so the resolving module's `config` supplies thresholds and scales.

`OnDrawGizmosSelected` draws the five regions on the collider footprint.

**Known risk — depenetration.** Discrete collision detection lets fast cars overlap before
`OnCollisionEnter` (up to closing speed × fixed dt — 0.5 m at a single car's 25 m/s top
speed with the project's 0.02 s fixed timestep, already more than `cornerBandMetres` at
0.3 m). On the next step PhysX separates overlapping bodies at up to
`Rigidbody.maxDepenetrationVelocity` (default 10 m/s), which can look like an opposing
bounce after we zero velocities — most visible in head-ons. The same overlap can also put
the contact point well inside the attacker's own box, so `RamRules.Region` misreads an
offset attack as `Side` instead of `FrontCorner` — the attacker then fails to qualify and a
real ram silently becomes a plain bump (the victim's region/angle test is unaffected). Not
pre-emptively fixed; if play shows it, candidates are lowering `maxDepenetrationVelocity` on
cars, `ContinuousSpeculative` detection, or classifying the attacker's region from the
local-space contact normal instead of the contact point. Decide with the user.

### 2.7 Wiring

- `CarDefinition`: `attack`, `defense`, `ramConfig`.
- `CarFactory.Spawn`: `RammingModule.config/attack/defense` from the definition.
- `ConfigAssetBootstrap.CreateDefaults`: create `RamConfig.asset`, assign to `CarDefinition`
  if empty.
- `GameBootstrap.Validate`: error if `carDefinition.ramConfig` is null.

## 3. Testing

EditMode, pure statics.

- `CarStatusTests`: lock/reel count down and clear; reel restarts; lock never shortens a
  longer lock; `CanDrive`/`CanAttack` false in either state; `Advance` clamps at 0.
- `RamRulesTests`: every region incl. corner band edges; attack regions; each type incl. the
  45° boundary both sides; Resolve cases §1.4 (none, below min speed, cannot attack, parked
  car head-on, faster wins, tie → head-on); shove magnitude = ratio × speed × scale, y = 0;
  defense clamp; spin zero through centre, sign for tail-pushed-right, linear in `spinScale`;
  spin decay timestep-independent.
- `CarFactoryTests`: RammingModule receives config, attack, defense.

Play checklist additions (docs/workflow.md): rear ram on the parked dummy; flank ram
(dummy spins, slides, recovers); head-on into the parked dummy's nose (you stop dead and are
locked ~0.5 s; the dummy, having contributed no forward speed, is pushed backwards at
`headOnScale` × your speed, no spin); corner hit at shallow vs steep angle; low-speed touch is
a plain bump; attacker lock ~0.5 s with aim still live; re-ram during reel restarts it; no car
ever leaves the ground.

## 4. Documentation

New `docs/ramming.md` (current-state reference) linked from CLAUDE.md; update
`architecture.md` (ramming no longer a stub, CarStatus seam), `driving-physics.md` (carve-outs,
resolved known-behaviour note), `tuning.md` (RamConfig, attack/defense), `workflow.md` (tests,
checklist), CLAUDE.md "Not built yet".

## 5. Out of scope

Damage, health, HUD feedback for states, audio/VFX, networking, bots, wall rams.
