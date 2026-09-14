# Ramming

Only **car-vs-car** contacts can be rams; walls and the ground stay plain physics. When a
ram resolves, the attacker stops dead and is briefly locked out of input; the victim is
shoved away and reels, sliding and spinning until it recovers. A head-on is the exception —
there is no single attacker or victim. Both cars' velocities are zeroed, and then **each**
receives a small shove computed from the **other** car's strength, forward speed and heading
(and its own resistance), scaled by `headOnScale`. A parked car contributes zero forward speed,
so it gives no shove at all — the moving car simply stops, and only the parked car is pushed.
See [The shove](#the-shove) for the exact head-on mapping.

## Regions are faces, not volumes

A contact is classified by which face of the victim's collider box it lands nearest, in the
victim's local space, against `width × length` from `CarDefinition`. There are no extra
colliders. Let `dFront`, `dRear`, `dSide` be the distances from the local contact point to
each face, and `band = RamConfig.cornerBandMetres`:

| Region | Condition (checked in this order) |
|---|---|
| FrontCorner | `dSide <= band` and `dFront <= band` |
| RearCorner | `dSide <= band` and `dRear <= band` |
| Front / Rear / Side | otherwise, the face with the smallest distance (ties: Front, then Rear, then Side) |

Faces, not volumes, on purpose: a front zone deep enough to also catch corner hits would
swallow the side panel just behind the bumper too, and call a T-bone near the nose a
head-on.

## Type

A car qualifies as an attacker only when the contact lands in its own **Front** or
**FrontCorner** region, its forward speed is at least `RamConfig.minRamSpeed`, and it is
neither **Locked** nor **Reeling** — a shoved victim sliding nose-first into a third car
cannot chain into a second ram. Type comes from the victim's region and the angle between
flat headings, against `headOnAngle = RamConfig.headOnAngleDegrees`:

| Victim region | Type |
|---|---|
| Side | **Flank** |
| Front, FrontCorner | **HeadOn** if `angle(attackerFwd, −victimFwd) <= headOnAngle`, else **Flank** |
| Rear, RearCorner | **Rear** if `angle(attackerFwd, victimFwd) <= headOnAngle`, else **Flank** |

Each qualifying car is evaluated as attacker against the other, then the pair resolves in
this order:

1. Neither qualifies → **None** (plain bump).
2. Any evaluation yields HeadOn → **HeadOn**, and both cars participate — even one that did
   not itself qualify, such as a parked car hit square in the nose.
3. Both qualify, neither HeadOn → the higher forward speed is the attacker, with its type.
   An exact tie is treated as **HeadOn**.
4. Exactly one qualifies → that car attacks with its type.

Three decisions worth knowing came from the design conversation rather than falling out of
the physics: a locked or reeling car cannot attack, the faster car wins a double-qualify,
and an exact speed tie counts as a head-on rather than being decided arbitrarily.

**A wreck can be neither an attacker nor a victim.** `Wreck` collides only with `Arena` (see
[combat.md](combat.md#physics-layers)), so a live car and a wreck never generate
`OnCollisionEnter` for each other at all. `RammingModule.OnCollisionEnter` also checks
`Targetable` on both cars before resolving a pair, belt-and-braces with the layer split.

## The shove

Mass is deliberately ignored — `strength` and `resistance` are the only balance levers. The
velocity change dealt to a victim is

```
shoveDv = flatForward_attacker × (strength_attacker / resistance_victim) × forwardSpeed_attacker × typeScale
```

where `typeScale` is `headOnScale`, `flankScale` or `rearScale`, and `forwardSpeed` is
`max(0, dot(preContactVelocity, flatForward))`. `resistance` is clamped to a minimum of 0.01 so
a misconfigured zero cannot divide by zero. `shoveDv.y` is always 0 — a ram never adds
vertical velocity.

**Head-on input mapping.** There is no single attacker, so the general formula above is
applied twice with the roles swapped and `typeScale = headOnScale`:

```
shoveDv_toCarA = flatForward_carB × (strength_carB / resistance_carA) × forwardSpeed_carB × headOnScale
shoveDv_toCarB = flatForward_carA × (strength_carA / resistance_carB) × forwardSpeed_carA × headOnScale
```

Each car's shove comes from the **other** car's strength, forward speed and heading, divided
by its own resistance. A parked car has `forwardSpeed = 0`, so `shoveDv_toCarA` above is the
zero vector when car B is parked — the moving car (A) simply stops and receives nothing,
while the parked car (B) is pushed by A's strength and speed. In an angled head-on the shove's
sideways component (relative to the now-locked, zero-velocity car it lands on) is mostly
removed by grip on the very next `DrivingModule.Tick`, since grip keeps running while
Locked — only the reeling state turns it off. `RamReport.attacker` for a head-on is simply
the car whose `OnCollisionEnter` happened to resolve the pair (see
[Why velocities are overwritten, not added](#why-velocities-are-overwritten-not-added)); it
carries no gameplay meaning since both cars are treated identically.

Flank and rear rams also spin the victim, about world up:

```
r      = flat(contactPoint − victimCentre)
k²     = (width² + length²) / 12        // box radius of gyration squared
spinDw = spinScale × cross(r, shoveDv).y / k²
```

With `spinScale = 1` this is exactly the yaw a real impulse `m·shoveDv` applied at `r` would
give a solid box of that footprint — pushing a victim's tail toward its right gives negative
yaw (the nose swings left). Head-on rams never spin: the shove is applied through the
centre.

## Damage

Alongside the shove, a flank or rear ram also sends the victim a flat `DamageRequest`
(`source` = the attacker, `sourceTag = "ram"`) for `RamConfig.flankDamage` or `rearDamage`
(both placeholder **0**) — mitigated by the normal combat formula, same as any other damage
source (see [combat.md](combat.md#formula)). **Head-ons never deal damage**: there is no
single attacker to credit, and the shove itself is already deliberately weak
(`headOnScale`) so a head-on shouldn't reward either side.

## Locked and reeling

Ram lock is still a plain `CarAbilities` block — see [combat.md](combat.md#ability-switches)
for the general mechanism — registered under its own module-static source key (`LockBlock`)
so it never interacts with any other system's blocks. It blocks `Throttle | Steer | Ram` for
`attackerLockSeconds` with `BlockRefresh.KeepLonger`. It is the only block ramming still owns
directly.

Ram reel is not: a flank or rear ram now applies the **Reeling effect** to the victim through
its `IEffectReceiver`, after writing the shove —
`EffectRequest { source = attacker, sourceTag = "ram", type = Reeling, duration = reelSeconds }`.
Reeling's own block (`Throttle | Steer | YawHold | Grip | Ram`, the same mask ramming's old
reel block used) is unchanged; only who owns the timer changed. Spin decays at
`EffectsConfig.reelingSpinDecayRate` (moved here from ramming's old per-ram rate, same value)
instead of inside `RammingModule.Tick`, which no longer touches spin at all — that's now
`EffectRules.DecaySpin`, called by `ReelingEffect.OnStep`. A second ram on an
already-reeling car restarts the effect — `Restarted`, not a second entry — because
`EffectsConfig.reelingStacks` is on by default; see [effects.md](effects.md#stacking).

Because Reeling now goes through `IEffectReceiver.Apply` like every other effect, it passes
the normal effect hostility check (`Hostility.AreEnemies`, unless `allowNonEnemy`) before it
lands — the shove itself is physics, applied unconditionally, but the reel is not. Today
every car is everyone else's enemy, so this changes nothing yet; once teams exist, flanking or
rear-ramming a teammate still shoves them but does not reel them. The pre-effects reel block
had no such check — it hit any car, teammate or not.

| | Normal | Locked | Reeling |
|---|---|---|---|
| Throttle / steer | used | ignored (treated as 0) | ignored |
| Aim | works | works | works |
| Drag | on | on | on |
| Grip | on | on | **off** |
| Yaw | set from steer | set from steer (= 0) | **not written by driving**; decays at `EffectsConfig.reelingSpinDecayRate` (1/s, `exp(−rate·dt)`) |
| Can attack | yes | no | no |

An attacker (and both cars in a head-on) is Locked for `attackerLockSeconds`; a flank or
rear victim Reels for `reelSeconds`. `KeepLonger` means locking never shortens a longer lock
already running; the Reeling effect's own stacking restarts reeling on a fresh hit, so
chaining rams on a helpless car keeps it helpless. Because the reel mask is a superset of the
lock mask, a car that picks up both blocks at once (locked as an attacker, then hit as a reel
victim before the lock expires) still loses `YawHold` and `Grip` too — `CarAbilities.Has`
blocks an ability if *any* active block covers it, so the lock block and the Reeling effect's
block simply combine rather than one state overriding the other. When the reel ends, grip and
steering resume through their normal code paths, but the two do not behave the same way. Grip
is itself a 1/s decay (see [every decay value is a
rate](driving-physics.md#every-decay-value-is-a-rate-in-1s)), so the sideways slide eases
back in rather than vanishing in one step. Yaw does not: `DrivingModule` writes
`angularVelocity` directly from `steer` every non-reeling tick (`steer` is 0 for the dummy),
so whatever spin is still running when the reel ends is cut to zero in a single physics
step, not eased out. With the placeholder reel (`reelSeconds = 1`) and spin decay
(`reelingSpinDecayRate = 2`), `exp(-2 × 1) ≈ 13.5%` of the initial spin is typically still
running at that moment and gets snapped away abruptly. Whether to blend yaw back in the way
grip does is a gameplay/tuning decision for the user, not something fixed here.

**One step later.** `CarEffects` ticks last on a car — after `DrivingModule` and
`RammingModule` — so the Reeling effect's expiry, and the unblock it triggers, happens after
driving has already run for that physics step. Driving regains yaw control one physics step
(0.02 s) later than the old block-only reel did. It isn't noticeable.

## Why velocities are overwritten, not added

By the time `OnCollisionEnter` runs, PhysX has already computed and applied its own
collision response for that step — the bounce you would feel if nothing here touched it. A
ram overwrites both cars' horizontal velocity from `CarController.PreStepVelocity`, a
snapshot taken at the **end** of the previous `FixedUpdate` (after every module has ticked,
before the physics step runs), so PhysX's response never shows: the attacker feels no
opposing impulse. Vertical velocity is never touched.

This snapshot has one known approximation: a force added with `AddForce` in the same step is
integrated inside the physics step itself and is absent from it — at most
`enginePower / mass × dt ≈ 0.5 m/s`.

Both cars in a contact receive `OnCollisionEnter` for it in the same step. Whichever
`RammingModule` runs first resolves the pair and records `(partner, Time.fixedTime)` on
**both** modules; the other callback sees its own module already marked for that partner at
that `fixedTime` and returns without re-resolving. This guard remembers one partner per
module, which is exactly right for the current two-car game — extending it to more
simultaneous contacts is unfinished work. Rams resolve on `OnCollisionEnter` only, so
continuous pushing while already touching is plain physics, not a repeated ram.

`logImpacts` on **either** car of a pair is enough to see the log for that pair — the log
checks both modules' flags and fires once, regardless of which car's `OnCollisionEnter`
happened to resolve the contact. `RamReport.attacker`/`victim` for a resolved ram, and the
`Rammed` event itself, are also unaffected by that ordering: `Rammed` is raised on **both**
cars' `RammingModule`, so a subscriber on either car sees every ram it takes part in, not
only the ones its own module happened to resolve. Wall and ground contacts still raise the
`Collided` event but are never logged by `logImpacts`, because a wall has no `CarController`
to resolve against.

**Known risk — depenetration, not yet fixed.** Discrete collision detection lets fast cars
overlap before `OnCollisionEnter` fires, by up to closing speed × fixed timestep — with
`Fixed Timestep = 0.02 s` (`ProjectSettings/TimeManager.asset`) that is up to 0.5 m at a
single car's 25 m/s top speed, already more than `cornerBandMetres` (0.3 m). This shows up
two ways:

1. On the next physics step PhysX separates the overlapping bodies at up to
   `Rigidbody.maxDepenetrationVelocity` (10 m/s by default), which can look like an opposing
   bounce right after this code has zeroed both velocities. Most visible in head-ons.
2. `RamRules.Region` classifies the **attacker's** own contact point too, and by the time the
   contact is reported that point can already sit well inside the attacker's box rather than
   right on its front bumper. An offset hit that should register as `FrontCorner` (still an
   attack region, see [Type](#type)) can then be misread as `Side` — the attacker fails to
   qualify and a real ram silently becomes a plain bump. The victim side of the same contact
   is not similarly fooled: the angle test in `Classify` still separates head-on/rear from
   flank correctly regardless of exactly where on the face the point lands.

Neither is pre-emptively fixed. Candidates to decide with the user: lowering
`maxDepenetrationVelocity` on cars, `ContinuousSpeculative` collision detection (helps both),
or classifying the attacker's region from the local-space contact **normal** instead of the
local-space contact **point** (helps only the second). Watch for the misread by driving
offset hits at speed with `logImpacts` on and checking the logged region against what the hit
looked like.

## Tests

`RamRulesTests` — 36 tests covering every region including corner-band edges,
attacker-qualifying regions, each type including the 45° boundary on both sides, every `Resolve` case from
§1.4, shove magnitude and the resistance clamp, damage (`DamageFor`), and spin (zero through
the centre, sign, linearity in `spinScale`). Spin decay moved to `EffectRules.DecaySpin` and
is covered by `EffectRulesTests`; see [effects.md](effects.md#tests).

Lock's timing — refresh modes, independence between sources, untimed blocks — is covered by
`CarAbilitiesTests`, since it is still an ordinary block. Reeling's timing is covered by
`CarEffectsTests`, since it is now an effect. See [combat.md](combat.md#tests) and
[effects.md](effects.md#tests).
