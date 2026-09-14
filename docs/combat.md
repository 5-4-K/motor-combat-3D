# Combat

Every car has health, taken through exactly one damage path, and dies through exactly one
destruction sequence. Rams are the only damage source today; projectiles, zones, beams and
effects (later sub-projects) plug into the same `DamageRequest`, `CarStats`, `CarAbilities`
and `Hostility` seams without editing any of the code this page describes.

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
(`Flat` or `MaxHealthPercent`), `amount`, and `allowNonEnemy` (false by default — set true for
a self-inflicted debuff).

`Apply` returns a `DamageResult { outcome, dealt, killed }` immediately, so a caller — a
projectile deciding whether it hit, or `RammingModule` after a flank ram — gets an instant
answer. **`DamageResult.dealt` (and `DamageReport.dealt`) is the HP actually removed**, clamped
to whatever health was left — an overkill hit against 40 HP reports `dealt = 40`, never the
raw mitigated amount, so kill credit and logs never over-report.

## Formula

`Mitigate(raw, attack, defense) = raw × attack / 100 × 100 / (100 + max(0, defense))`, where
`attack` is the source's effective attack (100 when the source is null — the environment hits
as hard as a baseline car) and `defense` is the target's effective defense, floored at 0.

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
| Ram reel (victim) | `Throttle \| Steer \| YawHold \| Grip \| Ram` | `reelSeconds` | `Restart` |
| Wreck | `Throttle \| Steer \| Fire \| Ram \| Targetable` | +∞ | `KeepLonger` |

## Destruction

The moment `Health.Apply` takes current HP to 0, `Health` unblocks and clears everything the
car was carrying — `car.Abilities.UnblockAll()` and `car.Stats.RemoveAll()`, ending any ram
lock, reel or (later) effect — then adds the untimed wreck block from the table above. Grip and
`YawHold` are not in that mask, so the wreck keeps sliding to a stop under normal drag and grip
without spinning.

`WreckSequence.OnDestroyedByDamage` (subscribed to `Health.Destroyed`) then moves the car root
to physics layer **`Wreck`**, which collides only with **`Arena`** — the wreck passes through
cars, and later through weapons and obstacles too.

`WreckConfig` (`Assets/_Project/Configs/WreckConfig.asset`) tunes what happens next:

| Field | Placeholder | Meaning |
|---|---|---|
| `rollDegrees` | 180 | Barrel roll about the car's length axis |
| `rollSeconds` | 0.8 | Duration of the roll, eased |
| `fadeSeconds` | 1.5 | Alpha goes from 1 to 0 over this time |
| `removeAfterSeconds` | 1.5 | The car GameObject is deactivated, not destroyed, so a future respawn can reuse it |

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
clones are destroyed. A future respawn can simply reactivate the car; `WreckSequence` needs no
change.

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

`Wreck` collides only with `Arena`; `Car` collides with `Car` and `Arena`; every other pair
keeps Unity's defaults. The names live in `ProjectSettings/TagManager.asset`. Later
sub-projects add their own layers (projectiles, obstacles, zones) beside these three without
touching them.

## Tests

| Fixture | Count |
|---|---|
| `CarAbilitiesTests` | 15 |
| `CarStatsTests` | 9 |
| `DamageRulesTests` | 9 |
| `HealthStateTests` | 13 |
| `TickScheduleTests` | 7 |
| `HostilityTests` | 4 |
| `HealthTests` | 5 |
| `PhysicsLayersTests` | 3 |
| `WreckMathTests` | 11 |
| `WreckMaterialsTests` | 1 |
