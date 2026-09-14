# Combat

Every car has health, taken through exactly one damage path, and dies through exactly one
destruction sequence. Rams and weapons are the damage sources today; zones, beams and further
effects plug into the same `DamageRequest`, `CarStats`, `CarAbilities` and `Hostility` seams
without editing any of the code this page describes. Effects (Stunned, Corroded, …) are
already built on these seams — see [effects.md](effects.md). Weapons are their own subject —
see [weapons.md](weapons.md).

## Source keys

Ability blocks (`CarAbilities`), stat modifiers (`CarStats`), damage gates (`HealthState`)
and the tick schedule (`TickSchedule`) all key their state off a caller-supplied `object
source`. All four compare that key by **object identity** (`ReferenceEquals`), never by
value — so a boxed value or a `string` built at runtime with the same content as another
key is still a different source. An effect should use **one object** as its key across all
three of a block, a modifier and a gate, so `Unblock`/`Remove`/`RemoveGate` (and, for a
periodic effect, `TickSchedule`'s `source`) all target exactly the state that same instance
registered. `CarAbilities` and `TickSchedule` key `Dictionary`s with `ReferenceKeyComparer`
(Core) rather than the default comparer, which for a `string` key would otherwise compare
content; `CarStats` and `HealthState` already compared explicitly with `ReferenceEquals`.

## Stats

### Per car (`CarDefinition`)

| Field | Placeholder | Meaning |
|---|---|---|
| `maxHealth` | 1000 | Hit points |
| `attack` | 100 | Weapon and ram damage multiplier; 100 deals the listed damage |
| `defense` | 0 | Damage reduction with diminishing returns (see [Formula](#formula)) |
| `strength` | 1 | Ram shove dealt |
| `resistance` | 1 | Ram shove received |

Tuning belongs to the user; the values above are placeholders.

### Effective stats (`CarStats`, `CarController.Stats`)

`CarStats` holds base values plus percentage modifiers, keyed by source:

```
effective(stat) = max(0, base(stat) × (1 + Σ percent(stat) / 100))
```

Percentages from **different sources add** — Corroded −30% and Fortified +20% on a defense of
100 give −10%, so 90, regardless of application order. The **same source on the same stat
replaces** its previous percentage rather than stacking with itself. `Remove(source)` clears
every modifier that source registered, on every stat; `RemoveAll()` clears everything.
`Has(source)` reports whether that source currently has a modifier on any stat.

`CarStat` also carries `TopSpeed`, base **1**, a pure multiplier with no matching
`CarDefinition` field. `DrivingModule` multiplies `enginePower` by `Stats.Effective(CarStat.TopSpeed)`,
so a `TopSpeed` modifier scales terminal speed directly — the seam a future Spiked effect uses.

Every consumer reads **effective** values: damage reads `Attack`/`Defense`, ramming reads
`Strength`/`Resistance`, driving reads `TopSpeed`. With no modifiers, behaviour is identical to
reading the base stat.

## The damage path

All damage — rams now, weapons and effects later — is one `DamageRequest` applied through a
car's `IDamageable` (`Health`, in the Combat assembly). `Health.Apply` runs these steps in
order:

1. **Targetable.** If the target lacks the `Targetable` ability, the outcome is `NotTargetable`.
2. **Hostility.** Unless `allowNonEnemy` is set, `Hostility.AreEnemies(source, target)` must be
   true, otherwise the outcome is `NotHostile`.
3. **Gates.** If any registered gate returns true for the request, the outcome is `Blocked`. The
   gate list starts empty — nothing registers one yet.
4. **Raw amount.** `Flat` uses `amount` directly. `MaxHealthPercent` uses `amount / 100 × max`
   — a percent of **max** HP, not current HP.
5. **Mitigation.** See [Formula](#formula). A result that isn't strictly positive (including
   `NaN` from a broken config) gives `ZeroAmount` and no event.
6. **Apply.** `current = max(0, current − dealt)`.
7. **Events.** `Damaged` fires with a `DamageReport`. If this call took current health to 0,
   `Destroyed` fires exactly once immediately after, and destruction starts (see
   [Destruction](#destruction)).

`DamageRequest` fields: `source` (the attacking `CarController`, or null for the environment),
`sourceTag` (`"ram"`, later a weapon or effect id, for attribution and logs), `kind`
(`Flat` or `MaxHealthPercent`), `amount`, `allowNonEnemy` (false by default — set true for
a self-inflicted debuff), and `attack` (a `float?`: the attack captured when the source fired,
or for Overheated when the effect landed; null uses the source's effective attack at impact —
see [Formula](#formula)).

`Apply` returns a `DamageResult { outcome, dealt, killed }` immediately, so a caller — a
projectile deciding whether it hit, or `RammingModule` after a flank ram — gets an instant
answer. **`DamageResult.dealt` (and `DamageReport.dealt`) is the HP actually removed**, clamped
to whatever health was left — an overkill hit against 40 HP reports `dealt = 40`, never the
raw mitigated amount, so kill credit and logs never over-report.

## Formula

`Mitigate(raw, attack, defense) = raw × attack / 100 × 100 / (100 + max(0, defense))`, where
`attack` is the request's `attack` snapshot when set, otherwise the source's effective attack
(100 when the source is null — the environment hits as hard as a baseline car), and `defense`
is the target's effective defense, floored at 0.

| Attacker attack | Target defense | Damage (raw 100) |
|---|---|---|
| 100 | 0 | 100 |
| 100 | 100 | 50 |
| 150 | 100 | 75 |
| 100 | 50 (Corroded −50%) | 66.7 |
| 100 | 150 (Fortified +50%) | 40 |
| 100 | 300 | 25 |

There is no floor because the formula can't produce one: with `attack` positive, `100 / (100 +
defense)` only ever approaches zero as defense grows, never reaches it. This is the MOBA
pattern used by League and Smite — defense is always worth investing in, but never lets a
target become immune.

## Tick damage

Tick damage isn't a separate `DamageKind` — a source just sends ordinary `Flat` or
`MaxHealthPercent` requests on a schedule. `TickSchedule.TryTick(source, target, now, interval)`
gives every periodic source the same rule: a tick is due, and recorded, when that
`(source, target)` pair has never ticked, or when `now ≥ lastTick + interval`. **The first tick
lands immediately on contact.** The schedule survives contact breaks — leaving and re-entering
doesn't reset the timer, so brushing in and out of a zone can't tick faster than its interval.
`Forget(target)` clears every entry for a target, e.g. when it's removed.

## Hostility

`Hostility.AreEnemies(a, b)` is a Core static, read by damage, ramming and the HUD alike. The
default rule is `a != b`: every other car is an enemy, since there are no teams yet. **A null
source is an enemy of every car** — the environment hurts everyone. The rule itself is a
replaceable delegate (`SetRule` / `ResetRule`), so a future team mode can swap it without
touching any caller.

## Ability switches

`CarAbility` is a flags enum; `CarAbilities` (owned as `CarController.Abilities`) tracks which
are currently blocked, one source at a time:

| Flag | Meaning |
|---|---|
| `Throttle` | Throttle input is used |
| `Steer` | Steer input is used |
| `YawHold` | Driving writes yaw every step; off = the car spins freely |
| `Grip` | Lateral grip is applied; off = the car slides freely |
| `Fire` | Weapons may fire |
| `Ram` | The car may qualify as a ram attacker |
| `Targetable` | The car may take damage and be rammed |

`Block(source, abilities, seconds, refresh)` registers a block; `Has(ability)` is true only
when no active block covers it. When a source that already holds a block blocks again, its
`BlockRefresh` mode decides what happens: `KeepLonger` keeps `max(remaining, seconds)`,
`Restart` resets the remaining time to `seconds`, and `IgnoreIfActive` does nothing — the
non-stacking rule effects need. Blocks from different sources never interact.

Ram lock, reel and the wreck are all just blocks:

| Block | Abilities | Duration | Refresh |
|---|---|---|---|
| Ram lock (attacker, both cars in a head-on) | `Throttle \| Steer \| Ram` | `attackerLockSeconds` | `KeepLonger` |
| Ram reel (victim) | `Throttle \| Steer \| YawHold \| Grip \| Ram` | `reelSeconds` (Reeling effect, see effects.md) | effect stacking (Reeling stacks by default) |
| Wreck | `Throttle \| Steer \| Fire \| Ram \| Targetable` | +∞ | `KeepLonger` |

## Destruction

The moment `Health.Apply` takes current HP to 0, `Health` unblocks and clears everything the
car was carrying — `car.Abilities.UnblockAll()` and `car.Stats.RemoveAll()`, ending any ram
lock, reel or (later) effect — then adds the untimed wreck block from the table above, **before**
`Damaged` fires. This ordering is deliberate: the car becomes a wreck even if a `Damaged`
subscriber throws, since the block is already in place by the time any subscriber runs. Event
order is still `Damaged` → `Destroyed`. On a killing ram specifically, `Health.Destroyed` fires
before `RammingModule.Rammed` — `RammingModule.ApplyRam` calls `IDamageable.Apply` (which raises
both `Health` events synchronously) before it reports the ram and raises `Rammed`. Grip and
`YawHold` are not in the wreck mask, so the wreck keeps sliding to a stop under normal drag and
grip without spinning.

`WreckSequence.OnDestroyedByDamage` (subscribed to `Health.Destroyed`) then moves the car root
to physics layer **`Wreck`**, which collides only with **`Arena`** — the wreck passes through
cars, and later through weapons and obstacles too.

`WreckConfig` (`Assets/_Project/Configs/WreckConfig.asset`) tunes what happens next:

| Field | Placeholder | Meaning |
|---|---|---|
| `rollDegrees` | 180 | Barrel roll about the car's length axis |
| `rollSeconds` | 0.8 | Duration of the roll, eased |
| `fadeSeconds` | 1.5 | Alpha goes from 1 to 0 over this time |
| `removeAfterSeconds` | 1.5 | The car GameObject is deactivated, not destroyed, so respawn can reuse it |

**Lift during the roll.** The rolled model rises so its lowest rotated corner never dips below
the floor: `lift(θ) = max(0, halfWidth·|sin θ| + halfHeight·|cos θ| − halfHeight)`, using the
root's `BoxCollider` half width and half height. A car is wider than it is tall, so lying on its
side would otherwise sink it by `halfWidth − halfHeight`.

**Fade via transparent clones.** At death, every renderer's materials are cloned and switched
to URP's transparent surface (`WreckMaterials.MakeTransparent`: `_Surface = 1`, `_Blend = 0`,
alpha src/dst blend, `_ZWrite = 0`, keyword `_SURFACE_TYPE_TRANSPARENT`, render queue
`Transparent`) — an opaque URP Lit material can't be faded by touching its colour alone. Alpha
is then driven every frame per renderer and material index through the `MaterialPropertyBlock`
value `_BaseColor`, starting from whatever colour the block already held (the team tint) or the
material's own colour otherwise.

**Shadows cut off mid-fade.** Once the fade alpha drops below `WreckMath.ShadowCutoffAlpha`
(0.5), every renderer's `shadowCastingMode` is set to `Off`, once — a solid shadow cast by a
near-invisible car reads as a bug, and cutting it mid-fade rather than waiting for full
transparency avoids a visible pop at the moment of death.

**Visuals restored on removal.** When the wreck deactivates (or the component is destroyed),
every rolled transform, faded colour and swapped material list — including each renderer's
original `shadowCastingMode` — is restored to what it was before death, and the transparent
clones are destroyed.

## Respawn

`CarRespawn.Respawn(car, position, rotation)` (Core) brings a destroyed car back to life, in
this order:

1. `ResetForRespawn()` runs on every `IRespawnable` component on the car root (see the table
   below), while the car is still inactive, so nothing runs a frame against the old life's
   state.
2. The root goes back to the `Car` physics layer.
3. The transform is set to the pose; `AimYaw = 0`; `CarController.ClearMotionSnapshot()` zeroes
   `PreStepVelocity` and `PreStepAngularVelocity`, so a ram in the very first step never reads
   the wreck's last slide.
4. The GameObject is reactivated (`SetActive(true)`).
5. The `Rigidbody`'s position and rotation are set to the pose, and its linear and angular
   velocity are zeroed.

Calling it on a still-live car — a future game mode may want to, for a teleport pad, say — is
not a pure teleport: steps 1 and 5 still run, so it also refills health to max, clears every
block (the ram lock included) and every stat modifier, and ends every active effect, exactly
as it does for a destroyed car. There is no path through `CarRespawn` that moves a car's pose
without also resetting its per-life state.

### What resets

| Component | `ResetForRespawn()` |
|---|---|
| `Health` | `UnblockAll()` (drops the wreck block), `Stats.RemoveAll()`, `HealthState.Revive()` — current health back to max, not destroyed. Gates are kept: each belongs to its own source, which removes it itself |
| `WreckSequence` | Stops the sequence and restores rolled transforms, faded colours and swapped materials. The same restoration happens from a fresh `OnDisable`, so a car deactivated mid-roll never resumes against live visuals |
| `CarEffects` | Ends every active effect — each one's `OnEnd` runs (see [effects.md](effects.md#death-and-respawn)) |
| `WeaponModule` | Clears the recovery lock, wind-ups and queued presses; cooldowns keep running |

A later module with per-life state joins respawn by implementing `IRespawnable` itself; nothing
here changes. Its `ResetForRespawn` must only clear its own state, never add a block or a stat
modifier: `Health.ResetForRespawn` (`UnblockAll`, `Stats.RemoveAll`) runs as just one
`IRespawnable` among the others, in no guaranteed order relative to the rest, so a reset that
adds a block or modifier could run before or after `Health`'s clear and either get wiped
immediately or leak into the new life.

### The placeholder rule

`RespawnRule` (Bootstrap, a `MonoBehaviour` on the `GameBootstrap` object) stands in for a
future game mode:

- `GameBootstrap` calls `Track(car, spawnPosition, spawnRotation)` for each car it spawns.
- On `IDamageable.Destroyed` it records `Time.fixedTime`.
- Every `FixedUpdate`, a pending car respawns once **all** hold:
  - `now ≥ destroyedAt + respawnDelaySeconds`
  - the car GameObject is inactive — the wreck sequence has finished, so respawn never cuts a
    wreck's roll and fade short
  - the spawn box is clear: `Physics.CheckBox` at the pose, the car's `BoxCollider` size grown
    by `clearanceMargin` on every side, `Car` layer only, triggers ignored — the check repeats
    every physics step until it passes, rather than spawning into an overlap and letting PhysX
    launch both cars apart

There is **no spawn protection**: the car is fully live the moment it reappears.

`RespawnConfig` (`Assets/_Project/Configs/RespawnConfig.asset`):

| Field | Placeholder | Meaning |
|---|---|---|
| `respawnDelaySeconds` | 3 s | Seconds from destruction before a respawn may happen |
| `clearanceMargin` | 0.1 m | Added around the car's box on every side when checking the spawn point is clear |

`GameBootstrap.respawnConfig` is validated the same way as its other configs; `ArenaSceneBuilder`
assigns it and `ConfigAssetBootstrap` creates the asset.

The HUD needs no changes for respawn: `CarRegistry` re-registers a car in `OnEnable`, and both
health widgets already hide a car only while `IDamageable.IsDestroyed`.

**Debug hooks.** `Health` carries two Editor-only `[ContextMenu]` entries — "Debug: take 25%
max HP" and "Debug: destroy" — so damage and death can be tested from the Inspector while every
real damage source is still at its 0 placeholder.

**Code note.** The design spec describes the roll as moving only "the visual child" and
relocating `CarFactory.VisualName` into Core. The implementation instead rolls **every direct
child of the car root except `DriverAnchor`** (so the placeholder box and its separate nose
marker both roll) and fades **every renderer under the root**, and leaves the constant where it
is — a deliberate plan deviation recorded because the placeholder's nose marker is a sibling of
the box, not part of it.

## Physics layers

`PhysicsLayers` (Core) holds the layer names and `ConfigureCollisions()`, called once by
`GameBootstrap` before spawning:

| Layer | Index | Used by |
|---|---|---|
| `Arena` | 8 | Ground and wall (`ArenaBuilder`) |
| `Car` | 9 | Live car root (`CarFactory`) |
| `Wreck` | 10 | Destroyed car root |
| `Hurtbox` | 11 | Car hurtbox children — weapons query it; it collides with nothing |

`Wreck` collides only with `Arena`; `Car` collides with `Car` and `Arena`; `Hurtbox` collides
with nothing at all — it is reached only by a weapon's own query, never by a contact or trigger
callback (see [weapons.md](weapons.md#hurtboxes)); every other pair keeps Unity's defaults. The
names live in `ProjectSettings/TagManager.asset`. Later sub-projects add their own layers
(projectiles, obstacles, zones) beside these four without touching them.

**A later layer that should touch wrecks must re-enable that pair itself.**
`ConfigureCollisions()` loops every layer **index** 0–31 and calls
`Physics.IgnoreLayerCollision(wreck, layer, layer != arena)` — so every index is excluded from
`Wreck` at boot, including ones with no name yet assigned. A layer a later sub-project adds
(say, a projectile layer that should still hit a wreck) inherits that exclusion the moment it
takes an index, since the setting is per index-pair, not per name; it must call
`Physics.IgnoreLayerCollision(PhysicsLayers.Wreck, thatLayer, false)` after
`ConfigureCollisions()` runs to opt back in.

## Tests

| Fixture | Count |
|---|---|
| `CarAbilitiesTests` | 15 |
| `CarStatsTests` | 10 |
| `DamageRulesTests` | 9 |
| `HealthStateTests` | 15 |
| `TickScheduleTests` | 9 |
| `HostilityTests` | 4 |
| `HealthTests` | 9 |
| `PhysicsLayersTests` | 3 |
| `WreckMathTests` | 11 |
| `WreckMaterialsTests` | 1 |
| `SourceKeyIdentityTests` | 1 |
| `CarRespawnTests` | 4 |
| `RespawnRulesTests` | 5 |
