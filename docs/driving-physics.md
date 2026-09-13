# Driving physics

Arcade, not simulation. All the maths is in `DrivePhysics` as pure statics;
`DrivingModule` only reads config, calls them, and writes to the Rigidbody.

## Order of operations, every FixedUpdate

1. **Thrust** — `AddForce(forward × throttle × enginePower)` when throttle > 0.
2. **Brake / reverse** — when throttle < 0: if forward speed exceeds `reverseEpsilon`,
   apply `brakeForce` opposing velocity; otherwise apply `reversePower` backward.
3. **Drag** — velocity decays exponentially at `linearDrag` per second.
4. **Yaw** — `angularVelocity = up × steer × turnRate × sense`.
5. **Grip** — decompose velocity into the car's forward and right axes and decay the right
   component. The gap between where the nose points and where velocity points is the drift.

While the car is **locked or reeling**, throttle and steer read as zero. While **reeling**,
yaw and grip are skipped — see [ramming.md](ramming.md#locked-and-reeling).

The order matters: drag before yaw means the yaw block reads the post-drag speed when
deciding steering sense, and grip last means whatever sideways velocity survives is what
you see as drift.

## Every decay value is a RATE in 1/s

This is the single most important correctness property in the module.

```csharp
value *= Mathf.Exp(-rate * dt);     // correct
value *= (1f - rate);               // WRONG — couples handling to the timestep
```

The naive form silently changes how the car handles when Fixed Timestep changes. The
exponential form is timestep-independent: one 0.02 s step equals two 0.01 s steps exactly.
That equivalence is asserted by test, for both grip and drag.

So `linearDrag` and `lateralGripStrength` are **rates in 1/s**, not 0–1 fractions. A field
named `linearDrag = 1` is not a coefficient.

Terminal speed follows directly:

```
topSpeed = enginePower / (mass × linearDrag)
```

At defaults: `30000 / (1200 × 1.0)` = **25 m/s**. Discrete stepping settles marginally
under this — applying drag after the force gives `a·dt·k/(1−k)` with `k = exp(−drag·dt)`,
about 24.75 — which is why that test carries a 0.5 tolerance.

## DrivePhysics is the ONLY source of resistance

Anything else that slows the car stacks a second, differently-shaped decay on top and makes
the terminal-speed formula wrong. Two things must therefore be pinned to zero:

**Rigidbody damping.** `rb.linearDamping = 0` and `rb.angularDamping = 0`. PhysX's built-in
damping uses a different formulation and would compound with `ApplyDrag`.

**PhysX contact friction.** Every car gets a zero-friction `PhysicsMaterial` with
`frictionCombine = Minimum`, assigned in `CarFactory`. Minimum wins over the ground's and
wall's defaults, so neither of those needs its own material.

### Why contact friction mattered so much

This was missed in the original design and only surfaced in play-testing, as *"S does not
reverse."*

PhysX's default material has friction 0.6. On a 1200 kg car that subtracts a flat `μmg` —
about **7 kN** — from every drive force. Because it is flat rather than proportional, it
distorts the tuning curve rather than scaling it, and it hits weak forces far harder than
strong ones:

| | Thrust | Lost to friction |
|---|---|---|
| Forward | 30000 N | **24%** |
| Reverse | 12000 N | **59%** |

Reverse felt broken, and actual top speed sat 24% below what the formula promised.

`ApplyGrip` *is* this model's tyre friction. So PhysX friction was simultaneously
double-counting sideways resistance and adding unmodelled longitudinal resistance.

## Yaw ignores speed magnitude but respects its sign

```csharp
float sense = (flipInReverse && forwardSpeed < -reverseEpsilon) ? -1f : 1f;
return steer * turnRateDegPerSec * sense;
```

**Magnitude is ignored** — that is exactly what makes turn-in-place work with no special
case. Full steer yields the full configured rate at a dead stop.

**Sign is not ignored.** A real car's steering sense inverts in reverse, because yaw rate
goes as `v/L · tan(δ)` and a negative `v` flips it: turn the wheel right while backing up
and the rear swings right. The original design said only "not gated on speed", which
conflated magnitude with direction; without the flip, reversing steered opposite to every
car the player has ever driven.

`DriveConfig.flipSteeringInReverse` selects car-like (`true`, default) against tank-style
absolute steering (`false`). Turn-in-place is identical either way — the flip only engages
past `reverseEpsilon`, below which the car is still effectively stationary.

## Preconditions worth knowing

`ApplyGrip` requires `forward` to be a **horizontal unit vector**. `DrivingModule` flattens
`transform.forward` onto the ground plane before passing it, so a slight pitch from a
collision never leaks into steering or grip.

## Known behaviour that looks like a bug

**Plain bumps barely move a car and never spin it.** A contact that is not a ram (below
minRamSpeed, or not front-first) gets only PhysX's response, and DrivingModule's yaw write
and grip absorb it. Rams bypass both — see [ramming.md](ramming.md).

## Tests

`DrivePhysicsTests` — 16 tests covering timestep independence for grip and drag, terminal
speed convergence, throttle/brake/reverse selection, and all five yaw sign cases.
