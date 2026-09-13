# Ramming

Only **car-vs-car** contacts can be rams; walls and the ground stay plain physics. When a
ram resolves, the attacker stops dead and is briefly locked out of input; the victim is
shoved away and reels, sliding and spinning until it recovers. A head-on is the exception —
it stops both cars instead of picking a winner.

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

## The shove

Mass is deliberately ignored — `attack` and `defense` are the only balance levers. The
velocity change dealt to a victim is

```
shoveDv = flatForward_attacker × (attack_attacker / defense_victim) × forwardSpeed_attacker × typeScale
```

where `typeScale` is `headOnScale`, `flankScale` or `rearScale`, and `forwardSpeed` is
`max(0, dot(preContactVelocity, flatForward))`. `defense` is clamped to a minimum of 0.01 so
a misconfigured zero cannot divide by zero. `shoveDv.y` is always 0 — a ram never adds
vertical velocity.

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

## Locked and reeling

| | Normal | Locked | Reeling |
|---|---|---|---|
| Throttle / steer | used | ignored (treated as 0) | ignored |
| Aim | works | works | works |
| Drag | on | on | on |
| Grip | on | on | **off** |
| Yaw | set from steer | set from steer (= 0) | **not written by driving**; decays at `spinDecayRate` (1/s, `exp(−rate·dt)`) |
| Can attack | yes | no | no |

An attacker (and both cars in a head-on) is Locked for `attackerLockSeconds`; a flank or
rear victim Reels for `reelSeconds`. Locking never shortens a longer lock already running;
reeling restarts on a fresh hit, so chaining rams on a helpless car keeps it helpless.
Reeling takes precedence when both timers are running on the same car. When the reel ends,
grip and steering resume through their normal code paths — grip is itself a 1/s decay, so
the slide eases out rather than snapping straight.

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
continuous pushing while already touching is plain physics, not a repeated ram. With
`logImpacts` enabled, `RammingModule` logs resolved rams and non-ram car-vs-car bumps; wall
and ground contacts still raise the `Collided` event but are never logged, because a wall
has no `CarController` to resolve against.

**Known risk — depenetration, not yet fixed.** Discrete collision detection lets fast cars
overlap before `OnCollisionEnter` fires, by up to closing speed × fixed timestep — around 1 m
in a 50 m/s head-on. On the next physics step PhysX separates the overlapping bodies at up
to `Rigidbody.maxDepenetrationVelocity` (10 m/s by default), which can look like an opposing
bounce right after this code has zeroed both velocities. It is most visible in head-ons.
Candidates if play-testing shows it: lowering `maxDepenetrationVelocity` on cars, or
`ContinuousSpeculative` collision detection — decide with the user before changing either.

## Tests

`RamRulesTests` — 36 tests covering every region including corner-band edges, attack
regions, each type including the 45° boundary on both sides, every `Resolve` case from
§1.4, shove magnitude and the defense clamp, and spin (zero through the centre, sign,
linearity in `spinScale`, timestep-independent decay).

`CarStatusTests` — 7 tests covering lock/reel counting down and clearing, reel restarting,
a lock never shortening a longer one, `CanDrive`/`CanAttack` going false in either state, and
`Advance` clamping at 0.
