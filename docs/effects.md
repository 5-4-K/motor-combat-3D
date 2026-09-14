# Effects

## What an effect is

An effect is a timed state on a car — a block, a stat modifier, a damage gate, or a periodic
tick. How big and how long a given application is comes from whatever applies it (`RamConfig`
or a `WeaponConfig`); rules that are the same for every source of that effect —
whether it stacks, Overheated's tick interval and damage kind, Reeling's spin decay rate, the
Inspector debug sizes — live in `EffectsConfig`. There are ten effects: Stunned, Suppressed,
Overheated, Corroded, Reeling, Spiked, Fortified, Armored, Overhauled, Exhausted.

## Applying one

Every source — ramming and weapons (hit payloads and self-effects) — sends an `EffectRequest` through the target's
`IEffectReceiver.Apply`, implemented by `CarEffects`:

| Field | Meaning |
|---|---|
| `source` | The car that applied it; null for the environment or a debug menu |
| `sourceTag` | `"ram"`, a weapon id, `"debug"` — attribution and logs |
| `type` | The `EffectType` |
| `magnitude` | Always a positive size; the effect decides the sign. Percent for Corroded, Fortified, Spiked and Exhausted; damage per tick for Overheated. Ignored by the rest |
| `duration` | Seconds. Ignored by Overhauled |
| `allowNonEnemy` | False (default): only enemies receive it. True: self and allies too — self-buffs and self-debuffs |
| `attack` | Attack captured when the source fired. Null: the source's effective attack at apply time (100 for a null source) |

`Apply` decides in this order:

1. **Targetable.** The car must have the `Targetable` ability and not be a wreck → else
   `NotTargetable`. A wreck takes no effects.
2. **Hostility.** Unless `allowNonEnemy`, `Hostility.AreEnemies(source, car)` → else
   `NotHostile`. Same rule as damage; a car buffing itself sets `allowNonEnemy`.
3. **Valid.** The type must be known; a timed type needs `duration > 0` (NaN rejected, `+∞`
   allowed); a sized type needs `magnitude ≥ 0` and finite. A missing `EffectsConfig` also
   gives `Invalid`.
4. **Overhauled.** Always `Applied` once it passes the checks above — it ends every active
   effect, buffs included, and is never itself active.
5. **Already active.** The type's `…Stacks` setting decides: false → `AlreadyActive`, nothing
   changes; true → `Restarted` — the timer restarts at the new duration, and the new copy's
   magnitude, source, tag and attack snapshot replace the old.
6. **New** → `Applied`: stored, then the behaviour's `OnStart`.

| `EffectOutcome` | Meaning |
|---|---|
| `Applied` | Started (or, for Overhauled, every effect ended) |
| `Restarted` | Already active and stacking: timer restarted, new size replaces the old |
| `AlreadyActive` | Already active and not stacking: nothing changed |
| `NotTargetable` | The target is a wreck or otherwise not targetable |
| `NotHostile` | The source is not the target's enemy and `allowNonEnemy` was not set |
| `Invalid` | Unknown type, a bad duration or magnitude, or no `EffectsConfig` |

`Applied` and `Restarted` raise the `Applied` event. The **attack snapshot** is
`request.attack` if set, else the source's effective attack at apply, else 100 (a null
source) — fixed once, so a later change to the source's attack never affects a burn already
running. One exception: `CarEffects.Apply` never raises `Applied` for an effect whose own
start already ended it — for example, Overheated's first tick killing the car. Subscribers
never see `Applied` after `Ended` for the same application; the outcome the caller receives is
still `Applied`.

## Ending one early

`IEffectReceiver.End(type, source)` lets a source end an effect before its timer runs out —
but only the copy that `source` itself applied. Effects don't stack past one active entry per
type, so the type's active copy may belong to someone else entirely (a different source's
Fortified is still Fortified); that source's effect must run its full time regardless of what
`source` wants ended. Matching is by reference — `ReferenceEquals(entry.source, source)` — so
a `null` source matches only a copy applied with a `null` source. On a match, `End` runs the
same end path as expiry (`OnEnd`, then `Ended`) and returns true; otherwise it returns false,
including for a type that isn't active or isn't known.

This is for a source that ends its own effect on something other than the clock: Wild Charge
(see `docs/specs/2026-09-14-weapons-requirements.md`) gives the attacker self-Fortified for a
duration, but ends it early — "on contact with an enemy... it deals flat damage, and the state
ends" — and Tremor's self-buff (its shape is still undecided) should end the moment the car
leaves the zone, not when its timer runs out. Both call `End(type, self)` rather than waiting
for expiry, and neither risks cutting off some other source's Fortified that happens to be
active on the same car instead of its own.

## The ten effects

| Effect | Behaviour | Magnitude | Start | Step | End |
|---|---|---|---|---|---|
| Stunned | `StunnedEffect` | — | Block `Throttle\|Steer\|Fire\|Ram`; stop once: horizontal and angular velocity zeroed, vertical kept | — | Unblock |
| Suppressed | `BlockEffect` | — | Block `Fire` | — | Unblock |
| Overheated | `OverheatedEffect` | Damage per tick | Tick now | Tick when due | Forget its own tick on the car |
| Corroded | `StatEffect` | % | `Defense −m%` | — | Remove |
| Reeling | `ReelingEffect` | — | Block `Throttle\|Steer\|YawHold\|Grip\|Ram` | Yaw rate × `exp(−reelingSpinDecayRate·dt)` | Unblock |
| Spiked | `StatEffect` | % | `TopSpeed −m%` | — | Remove |
| Fortified | `StatEffect` | % | `Defense +m%` | — | Remove |
| Armored | `ArmoredEffect` | — | Damage gate that blocks every request | — | Remove gate |
| Overhauled | none | — | Ends every effect (apply step 4 above) | — | — |
| Exhausted | `StatEffect` | % | `Attack −m%` | — | Remove |

Effect-specific notes:

- **Stunned** stops the car once, then leaves it pushable — grip and drag stay on, so a ram or
  impulse still shoves a stunned car. It blocks ramming too.
- **Overheated**'s first tick lands on apply, then every `overheatedTickSeconds`. Each tick is
  a `DamageRequest` with the entry's source, `sourceTag = "overheated"`, `EffectsConfig`'s
  damage kind, `amount = magnitude`, the entry's `allowNonEnemy`, and the attack snapshot from
  apply time. Defense is read live. Armored blocks the ticks.
- **Armored** blocks all damage, including self-inflicted. It doesn't block effects, and it
  doesn't block ram shove, which isn't damage.
- **Stat effects** on the same stat add through `CarStats` — Corroded −30% and Fortified +20%
  on a defense of 100 give −10%, so 90 (see [combat.md](combat.md#stats)).

## Stacking

Each effect type has its own `…Stacks` bool in `EffectsConfig` — whether a new copy landing on
a car that already has that effect restarts it (`Restarted`) or does nothing
(`AlreadyActive`). A restart replaces the size: the new copy's magnitude, source, tag and
attack snapshot overwrite the old entry outright, not add to it. On restart, `OnRefresh` runs
instead of `OnStart`:

- Stat effects re-add their modifier with the new magnitude (the same source replaces its own
  entry in `CarStats`).
- Stunned stops the car again.
- Everything else does nothing — Overheated keeps its tick rhythm, so a re-applied burn never
  ticks early.

## Timing

`EffectSet` is the **only** timer. Every block, stat modifier and damage gate an effect
registers is untimed (`+∞`, `KeepLonger`, or a gate with no expiry of its own) and removed in
`OnEnd`, so none of them can ever outlive or undershoot what `EffectSet` says.

Each physics step, `CarEffects.Step(dt, now)`:

1. **Expire.** Every active timer counts down by `dt`. One at or below `EffectRules.ExpiryTolerance`
   (`1e-4` s — physics time accumulates in float steps) is removed, its `OnEnd` runs, and
   `Ended` fires. Expiry runs **before** stepping.
2. **Step.** Every effect still active after expiry gets its `OnStep`, in `EffectType` order.
   An effect ended mid-loop (Overheated killing the car, say) is skipped.

Because expiry runs before stepping, an effect never acts in the step it ends. A 3 s burn
ticking every 1 s ticks at 0 s, 1 s and 2 s — never at 3 s, since by the step that reaches 3 s
the effect has already expired.

## Death and respawn

Every effect ends on death and on respawn. `CarEffects` subscribes to `IDamageable.Destroyed`
the first time it is used; on destruction it ends every active effect (`OnEnd` then `Ended`,
for each). By then `Health` has already unblocked everything and removed every stat modifier,
so each effect's own unblock/remove in `OnEnd` is a harmless no-op. `CarEffects.ResetForRespawn`
(`IRespawnable`) does the same — see [combat.md](combat.md#respawn) for the full respawn
mechanism. A wreck takes no new effects — see [Applying one](#applying-one).

## Source keys

Each `EffectBehaviour` instance is created once per car and is itself the source key for
every block, stat modifier, damage gate and tick it registers — one object per effect, per
car, the same source-key rule as the rest of combat (see
[combat.md](combat.md#source-keys)). `OnEnd` therefore always removes exactly what that
effect's own `OnStart` added, never another effect's or another car's state.

## `EffectsConfig`

`Assets/_Project/Configs/EffectsConfig.asset`:

| Field | Placeholder | Meaning |
|---|---|---|
| `stunnedStacks` … `exhaustedStacks` (9 bools, no Overhauled) | all false except `reelingStacks` = true | May a new copy land on a car that already has it |
| `overheatedDamageKind` | Flat | Flat or MaxHealthPercent — for every source of Overheated |
| `overheatedTickSeconds` | 1 s | Tick interval for every source |
| `reelingSpinDecayRate` | 2 /s | A reeling car's spin decays as `exp(−rate·dt)`; moved here from ramming's old per-ram rate, same value |
| `debugDuration` | 3 s | Inspector debug menus |
| `debugPercent` | 30 | Inspector debug menus, stat effects |
| `debugOverheatAmount` | 20 | Inspector debug menus, Overheated |

Each source's own config still supplies magnitude and duration: `RamConfig.reelSeconds` is the
ram's Reeling duration (see [ramming.md](ramming.md)); a `WeaponConfig` supplies them through
its `hitPayload.effects` and `selfEffects` (see [weapons.md](weapons.md)).

## Debug menus

`CarEffects` carries ten Editor-only `[ContextMenu]` entries, "Debug: apply Stunned" through
"Debug: apply Exhausted", one per effect. Each applies to that car with `source = null`,
`sourceTag = "debug"`, `allowNonEnemy = true`, `duration = debugDuration`, and magnitude
`debugOverheatAmount` for Overheated or `debugPercent` for everything else that uses one. The
result is logged. `CarEffects.logEffects` (off by default) logs every apply, restart,
rejection and end.

## For later sub-projects

- **Weapons apply effects only through `IEffectReceiver`**, the same seam ramming uses now —
  never by referencing the Effects assembly directly.
- **A stun cancels a maneuver** through `Has(Fire)` — a maneuver checks
  `CarAbilities.Has(CarAbility.Fire)` rather than querying `CarEffects` directly, since
  Stunned expresses itself as an ability block, not a flag a maneuver has to know about.
- **Adding an 11th effect** touches every layer, in this order:
  1. A new `EffectType` value.
  2. Bump `EffectInfo.Count`, and update `IsBuff`/`IsTimed`/`UsesMagnitude` if the new effect
     needs to say yes to any of them (most timed effects need nothing done for `IsTimed`; it
     already defaults to true for everything but Overhauled).
  3. A new `…Stacks` field on `EffectsConfig`, and a case for it in `EffectsConfig.Stacks`.
     `EffectsConfigTests.Stacks_ReadsEachEffectsOwnField` finds the field by reflection, named
     `<type>Stacks` with the type's first letter lower-cased — get the name right or the test
     fails on the new type.
  4. An `EffectRules.BlockMask` case if it blocks abilities, or an `EffectRules.StatChange`
     case if it's a stat modifier — only if the new effect is one of those shapes; skip both
     for something like Overheated.
  5. A behaviour class (or reuse `BlockEffect`/`StatEffect` for a plain shape) registered in
     `CarEffects.CreateBehaviours`, at the new type's index.
  6. `EffectChipLayout.Label` (exactly 4 letters, unique — `EffectChipLayoutTests` checks both)
     and `EffectChipLayout.Colour`.
  7. Re-save or regenerate `EffectsConfig.asset` (`ConfigAssetBootstrap.CreateDefaults`) so the
     new `…Stacks` field exists in the serialized asset with its default value.
  8. Tests: at minimum, extend `CarEffectsTests` with the new effect's own case, and check the
     reflection and label tests above still pass.

  A ticking effect (like Overheated) shares `EffectHost.Ticks` — one `TickSchedule` per car,
  keyed by `(behaviour instance, car)` — with every other ticking effect on that car, so its
  `OnEnd` must forget only its own key (`host.Ticks.Forget(this, host.Car)`), never the whole
  car's schedule.

## Tests

EditMode, driven without a scene:

| Fixture | Count |
|---|---|
| `EffectInfoTests` | 4 |
| `EffectRulesTests` | 12 |
| `EffectSetTests` | 6 |
| `EffectsConfigTests` | 2 |
| `CarEffectsTests` | 29 |

`CarEffectsTests` exercises `CarEffects` end to end on a real `Health`, without a scene:
blocks, stats, the Armored gate, Stunned's stop, Reeling's spin decay, Overheated's rhythm and
restack, ending only the source's own copy, the attack snapshot and both damage kinds,
stacking both ways, same-stat addition, Overhauled,
hostility, the wreck case, death and respawn, and `GetActive`'s order.

Two things about how these tests work, not about the game:

- `CarEffects.Apply` stamps the live `Time.fixedTime`, which EditMode never resets to 0
  between runs. `CarEffectsTests` captures `Time.fixedTime` in `SetUp` and drives
  `CarEffects.Step(dt, now)` relative to that captured time, rather than assuming time starts
  at 0.
- A `MonoBehaviour` type defined inside the EditMode test assembly cannot be attached with
  `AddComponent` in this project, so `CarRespawnTests` exercises `CarRespawn`'s
  reset-before-reactivate behaviour through a real production `IRespawnable` (`Health`) rather
  than a test double; the exact ordering (every `IRespawnable` reset before the car is
  reactivated) is verified by inspection of `CarRespawn.Respawn`, not by an executable
  assertion. See [combat.md](combat.md#tests).

Play-test only, not covered by EditMode (`WreckSequence`'s reset can't run there — its `Awake`
subscription never fires in EditMode): wreck-visual restoration on respawn, `RespawnRule`'s
timing and the wait at a blocked spawn, each debug effect's feel, and the chips' on-screen
look. See the checklist in [workflow.md](workflow.md#acceptance-checklist).
