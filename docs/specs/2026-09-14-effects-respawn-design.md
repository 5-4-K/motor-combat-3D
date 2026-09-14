# Effects and respawn — design spec (weapons sub-project 2 of 6)

Date: 2026-09-14. Status: brainstormed with the user; the user asked for spec → plan → SDD in
one pass. Gameplay rules come from the user. Items marked **(decision)** were settled by Claude
where the user gave no rule, and are listed in §10 for confirmation.

Inputs: `2026-09-14-weapons-requirements.md` (the user's brief) and
`2026-09-14-combat-stats-damage-design.md` (sub-project 1, whose seams this spec uses). The
closed-spec rule from sub-project 1 §0 applies: no major rework of earlier work; small
append-only edits to composition roots are fine.

## 0. Scope

1. **Respawn** — a destroyed car comes back live (built first, per the user).
2. **The effect system** — apply, stack rules, expiry, cleanse, clear on death and respawn.
3. **The ten effects** — Stunned, Suppressed, Overheated, Corroded, Reeling, Spiked, Fortified,
   Armored, Overhauled, Exhausted.
4. **Ram reel becomes the Reeling effect.** The attacker's post-ram freeze ("ram lock") is **not**
   an effect — the user: "it's just how i want ram restitution to feel". It stays a block inside
   ramming.
5. **Effect chips on the HUD** and **Inspector debug menus** for applying effects.
6. **Attack snapshot on `DamageRequest`**, pulled forward from sub-project 3 because Overheated
   needs it now.

Not in scope: weapons, zones, maneuvers, spawn protection, game modes, effect icons/art, audio.

## 1. Respawn

### 1.1 Mechanism (Core)

```
interface IRespawnable { void ResetForRespawn(); }

static class CarRespawn {
    static void Respawn(CarController car, Vector3 position, Quaternion rotation);
}
```

`Respawn` runs, in order:

1. `ResetForRespawn()` on every `IRespawnable` component on the car root.
2. Root back to the `Car` physics layer.
3. Transform set to the pose; `AimYaw = 0`; `CarController.ClearMotionSnapshot()` zeroes
   `PreStepVelocity` and `PreStepAngularVelocity`, so a ram in the first step never reads the
   wreck's old slide.
4. `SetActive(true)`.
5. Rigidbody position and rotation set to the pose; linear and angular velocity zeroed.

It works on a live car too (a teleport), which a game mode may want.

### 1.2 What resets

| Component | `ResetForRespawn()` |
|---|---|
| `Health` | `UnblockAll()` (drops the wreck block), `Stats.RemoveAll()`, `HealthState.Revive()` — current back to max, not destroyed. Gates are kept: they belong to their sources, which remove them |
| `WreckSequence` | Stops the sequence and restores visuals. The same happens in a new `OnDisable`, so a car deactivated mid-roll never resumes against live visuals |
| `CarEffects` (§3) | Ends every active effect (each one's `OnEnd` runs) |

A later module with per-life state (weapon cooldowns, sub-project 3) implements `IRespawnable`
and nothing here changes.

### 1.3 Placeholder rule (Bootstrap)

`RespawnRule` (MonoBehaviour on the `GameBootstrap` object) stands in for a future game mode:

- `GameBootstrap` calls `Track(car, spawnPosition, spawnRotation)` for each car it spawns.
- On `IDamageable.Destroyed` it records `Time.fixedTime`.
- Every `FixedUpdate`, a pending car respawns when **all** hold:
  - `now ≥ destroyedAt + respawnDelaySeconds`
  - the car GameObject is inactive — the wreck sequence has finished **(decision)**: respawn
    never cuts a wreck's roll and fade short
  - the spawn box is clear: `Physics.CheckBox` at the pose, the car's `BoxCollider` size plus
    `clearanceMargin` on every side, Car layer only, triggers ignored

  The user chose "wait until clear": the check repeats every physics step until it passes.
- There is **no spawn protection** (user). The car is fully live on arrival.

`RespawnConfig` (Bootstrap, `Assets/_Project/Configs/RespawnConfig.asset`):

| Field | Placeholder | Meaning |
|---|---|---|
| `respawnDelaySeconds` | 3 | Seconds from destruction before a respawn may happen |
| `clearanceMargin` | 0.1 m | Added around the car's box when checking the spawn point |

`GameBootstrap` gets a `respawnConfig` field, validated like the others; `ArenaSceneBuilder`
assigns it; `ConfigAssetBootstrap` creates the asset.

The HUD needs nothing: `CarRegistry` re-registers in `OnEnable`, and both health widgets hide a
car only while `IsDestroyed`.

## 2. Core contracts for effects

Weapons (sub-project 3+) apply effects through these, never through the Effects assembly —
the same way ramming uses `IDamageable`.

```
enum EffectType { Stunned, Suppressed, Overheated, Corroded, Reeling,
                  Spiked, Fortified, Armored, Overhauled, Exhausted }   // explicit 0..9

struct EffectRequest {
    CarController source;    // null = environment
    string sourceTag;        // "ram", a weapon id, "debug"
    EffectType type;
    float magnitude;         // size; see §4 for what it means per effect
    float duration;          // seconds; ignored by Overhauled
    bool allowNonEnemy;      // self-buffs and self-debuffs
    float? attack;           // attack captured at firing; null = read source at apply
}

enum EffectOutcome { Applied, Restarted, AlreadyActive, NotTargetable, NotHostile, Invalid }

struct ActiveEffect { EffectType type; float remaining; float duration; float magnitude; CarController source; }

struct EffectReport { CarController source; CarController target; string sourceTag;
                      EffectType type; EffectOutcome outcome; float magnitude; float duration; }

interface IEffectReceiver {
    EffectOutcome Apply(in EffectRequest request);
    bool Has(EffectType type);
    float Remaining(EffectType type);                 // 0 when inactive
    void GetActive(List<ActiveEffect> into);          // cleared, then filled in EffectType order
    event Action<EffectReport> Applied;                // Applied or Restarted
    event Action<EffectType> Ended;                    // expiry, Overhauled, death, respawn
}

static class EffectInfo {
    bool IsKnown(EffectType);       // a declared value
    bool IsBuff(EffectType);        // Fortified, Armored, Overhauled
    bool IsTimed(EffectType);       // everything but Overhauled
    bool UsesMagnitude(EffectType); // Overheated, Corroded, Spiked, Fortified, Exhausted
}
```

`DamageRequest` gains `float? attack`. `Health.Apply` uses it when set, otherwise the source's
effective attack (100 for a null source), exactly as today. Default `null` keeps every existing
caller unchanged.

## 3. The effect system (new `MotorCombat.Effects` assembly, references Core only)

### 3.1 Parts

| Part | Kind | Job |
|---|---|---|
| `EffectsConfig` | ScriptableObject | Per-type global settings (§3.3) |
| `EffectRules` | pure static | Validation, the apply decision, attack snapshot, stat and ability mappings, spin decay |
| `EffectSet` | plain class | Which effects are active, their timers and stored request data |
| `EffectBehaviour` + subclasses | plain classes | What each effect does on start, refresh, step and end |
| `EffectHost` | plain class | What behaviours act on: car, Rigidbody, `IDamageable`, config, a per-car `TickSchedule`, `Now` |
| `CarEffects` | MonoBehaviour | Implements `IEffectReceiver`, `ICarModule`, `IRespawnable`; thin |

Each behaviour instance is created per car and is itself the **source key** for every block,
stat modifier, damage gate and tick it registers — one object per effect, as `combat.md`'s
source-key rule requires.

### 3.2 Apply

`CarEffects.Apply(request)`:

1. **Targetable.** The car must have `Targetable` and not be destroyed → else `NotTargetable`.
   A wreck takes no effects.
2. **Hostility.** Unless `allowNonEnemy`, `Hostility.AreEnemies(source, car)` → else
   `NotHostile`. Same rule as damage **(decision)**; a car buffing itself sets `allowNonEnemy`.
3. **Valid.** Known type; timed types need `duration > 0` (NaN rejected, +∞ allowed);
   magnitude types need `magnitude ≥ 0` and finite → else `Invalid`. Magnitude is always a
   positive size — the effect decides the sign — so a Corroded can't secretly be a buff
   **(decision)**. A missing `EffectsConfig` also gives `Invalid`.
4. **Overhauled** ends every active effect, buffs included, and is never itself active (user:
   instant cleanse of everything). Outcome `Applied`.
5. **Already active:**
   - the type's `Stacks` setting is false → `AlreadyActive`, nothing changes (user: no stacking
     is the default rule)
   - true → `Restarted`: the timer restarts at the new duration, and the new copy's magnitude,
     source, tag and attack snapshot replace the old (user)
6. **New** → `Applied`: stored, then the behaviour's `OnStart`.

`Applied` and `Restarted` raise `Applied`. The attack snapshot is `request.attack` if set, else
the source's effective attack at apply, else 100.

### 3.3 `EffectsConfig`

| Field | Placeholder | Meaning |
|---|---|---|
| `stunnedStacks` … `exhaustedStacks` (9 bools, no Overhauled) | all false except `reelingStacks` = true | May a new copy land on a car that already has it (user: per-effect "can stack on itself"; Reeling stacks) |
| `overheatedDamageKind` | Flat | Flat or MaxHealthPercent — for every source of Overheated (user) |
| `overheatedTickSeconds` | 1 s | Tick interval for every source (user) |
| `reelingSpinDecayRate` | 2 /s | Moved from `RamConfig.spinDecayRate`, same value |
| `debugDuration` | 3 s | Inspector debug menus |
| `debugPercent` | 30 | Inspector debug menus, stat effects |
| `debugOverheatAmount` | 20 | Inspector debug menus, Overheated |

Each source's own config supplies magnitude and duration (user): `RamConfig.reelSeconds` is the
ram's Reeling duration; weapon configs follow in sub-project 3.

### 3.4 Step (every physics step)

`CarEffects` is an `ICarModule`, added last by `CarFactory`, so it ticks after driving and
ramming. `Tick` calls `Step(dt, Time.fixedTime)`:

1. Every timer counts down by `dt`. Any at or below `1e-4` s expires: removed, `OnEnd`, `Ended`.
   Expiry runs **before** stepping, so an effect never acts in the step it ends — a 3 s burn
   ticking every 1 s ticks at 0, 1 and 2, not at 3 **(decision, user agreed to exclude)**.
2. Every still-active effect's `OnStep`, in `EffectType` order. An effect ended mid-loop (for
   example Overheated killing the car) is skipped.

### 3.5 Death and respawn

`CarEffects` subscribes to `IDamageable.Destroyed` the first time it is used. On destruction it
ends every effect (`OnEnd` and `Ended` for each). `Health` has already unblocked everything and
removed every modifier by then, so `OnEnd`'s own unblock and remove are harmless no-ops.
`ResetForRespawn` does the same.

## 4. The ten effects

| Effect | Behaviour | Magnitude | Start | Step | End |
|---|---|---|---|---|---|
| **Stunned** | `StunnedEffect` | — | Block `Throttle\|Steer\|Fire\|Ram`; **stop once**: horizontal velocity and all angular velocity zeroed, vertical kept | — | Unblock |
| **Suppressed** | `BlockEffect` | — | Block `Fire` | — | Unblock |
| **Overheated** | `OverheatedEffect` | Damage per tick | Tick now | Tick when due | Forget the car's ticks |
| **Corroded** | `StatEffect` | % | `Defense −m%` | — | Remove |
| **Reeling** | `ReelingEffect` | — | Block `Throttle\|Steer\|YawHold\|Grip\|Ram` | Yaw rate × `exp(−reelingSpinDecayRate·dt)` | Unblock |
| **Spiked** | `StatEffect` | % | `TopSpeed −m%` | — | Remove |
| **Fortified** | `StatEffect` | % | `Defense +m%` | — | Remove |
| **Armored** | `ArmoredEffect` | — | Damage gate that blocks every request | — | Remove gate |
| **Overhauled** | none | — | Ends every effect (§3.2 step 4) | — | — |
| **Exhausted** | `StatEffect` | % | `Attack −m%` | — | Remove |

Every block is registered untimed (`+∞`, `KeepLonger`) and removed in `OnEnd`: `EffectSet` is
the only timer, so an effect's block, modifier and gate can never outlive or undershoot it.

On `Restarted`, `OnRefresh` runs: stat effects re-add their modifier with the new magnitude
(same source replaces), Stunned stops the car again, and everything else does nothing — so
Overheated keeps its tick rhythm and a re-applied burn never ticks early **(decision)**.

Effect-specific rules from the user:

- **Stunned** — stop once, then pushable: grip and drag stay, so a ram or impulse still shoves a
  stunned car. It blocks ramming. A stun cancels a maneuver through `Has(Fire)` (sub-project 6).
- **Overheated** — the first tick lands on apply, then every `overheatedTickSeconds`. Each tick is
  a `DamageRequest` with the entry's source, `sourceTag = "overheated"`, the config's damage kind,
  `amount = magnitude`, the entry's `allowNonEnemy`, and the **attack snapshot from apply time**.
  Defense is read live. Armored blocks the ticks.
- **Armored** — blocks all damage, including self-inflicted. It doesn't block effects, and it
  doesn't block ram shove, which isn't damage.
- **Stat effects** — different effects on one stat add (Corroded 30 + Fortified 20 on defense 100
  gives 90), through `CarStats`.

## 5. Ramming changes

- `RammingModule.ApplyRam` stops blocking the victim itself. It applies
  `EffectRequest { source = attacker, sourceTag = "ram", type = Reeling, duration = reelSeconds }`
  through the victim's `IEffectReceiver`, after writing the shove as today.
- `RammingModule.Tick` no longer decays spin. `ReelBlock`, `ReelMask` and `RamRules.DecaySpin`
  (with its test) are removed; `EffectRules.DecaySpin` replaces it.
- `RamConfig.spinDecayRate` is removed; its value now lives in `EffectsConfig`.
  `reelSeconds` stays, as the ram's Reeling duration.
- The attacker's lock (`LockBlock`, `Throttle|Steer|Ram`, `KeepLonger`) is unchanged and not an
  effect.
- `RammingModule.Start` warns if the car has no `IEffectReceiver`: its rams wouldn't make victims
  reel.

**Behaviour check against ramming.** Reeling's block equals the old reel mask. With
`reelingStacks = true`, a second ram restarts the reel (checklist row 19 unchanged). One
difference remains: the reel ends in the last module rather than at the start of the step, so
driving regains yaw one physics step (0.02 s) later. It isn't noticeable.

## 6. HUD: effect chips

- **`EffectChipLayout`** (pure):
  - 4-letter labels: STUN, SUPP, HEAT, CORR, REEL, SPIK, FORT, ARMR, OVHL, EXHS
  - a colour per effect
  - text `"STUN 1.2s"`, rounded **up** to tenths in invariant culture, so a chip never reads 0.0s
    while the effect is on; an untimed effect shows only its label
  - `RowX` for a centred row
- **`EffectChipRow`** (plain class): a pooled row of chips (image + text) under a parent rect.
  `Show(IEffectReceiver)` lays out active effects in `EffectType` order and hides the extras.
- **`SelfEffectsWidget`**: a chip row centred above the self health bar's label. Placeholders:
  chip 72×24, gap 6, font 15, bottom 96 reference px.
- **`EnemyHealthBars`**: each bar gets a chip row just below the bar. Placeholders: chip 48×14,
  gap 3, font 10. This is a small extension of a sub-project 1 file, not a rework.
- **`GameBootstrap`** adds the `SelfEffectsWidget`.

## 7. Debug hooks

`CarEffects` carries Editor-only `[ContextMenu]` entries "Debug: apply Stunned" … "Debug: apply
Exhausted", ten in all. Each applies the effect to that car with `source = null`, `sourceTag =
"debug"`, `allowNonEnemy = true`:
- duration: `debugDuration`
- magnitude: `debugOverheatAmount` for Overheated, `debugPercent` for everything else

The result is logged. `CarEffects.logEffects` (off) logs every apply, restart, rejection and end.

## 8. Assemblies and composition

| Change | Where |
|---|---|
| New `MotorCombat.Effects` → Core | `Scripts/Effects/` |
| `CarDefinition.effectsConfig`; `CarFactory` adds `CarEffects` last | Cars (+Effects reference) |
| `GameBootstrap` validates `effectsConfig` and `respawnConfig`, adds `RespawnRule` and `SelfEffectsWidget` | Bootstrap (+Effects reference) |
| `ConfigAssetBootstrap` creates `EffectsConfig`, `RespawnConfig` and links `effectsConfig`; `ArenaSceneBuilder` assigns `respawnConfig` | EditorTools (+Effects reference) |
| Test assembly references Effects and Bootstrap | Tests |

Assets are regenerated with the two `unity run` commands in `docs/workflow.md`. The scene rebuild
is how `respawnConfig` reaches `Arena.unity`.

## 9. Testing

EditMode:
- **Respawn:** `CarRespawn` (resets every `IRespawnable` before reactivating, reactivates at the
  pose, restores the layer, zeroes aim and velocity), `HealthState.Revive`,
  `Health.ResetForRespawn`, and `RespawnRules` (due time, wreck-inactive gate, spawn box maths)
- **Contracts:** `EffectInfo`, and the attack snapshot in `Health.Apply`
- **Effects:** `EffectRules`, `EffectSet`, and `CarEffects` component tests for every effect —
  blocks, stats, gate, stop, spin decay, Overheated's rhythm, snapshot and kinds, stacking both
  ways, same-stat addition, Overhauled, hostility, wreck, death, respawn, `GetActive` order
- **HUD:** `EffectChipLayout` and `EffectChipRow`
- **`CarFactory`:** `CarEffects` attached with its config; module count 5

Play-test only (added to the workflow checklist):
- respawn timing and the wait at a blocked spawn
- wreck visuals restored on respawn
- each debug effect's feel
- chips over the dummy and on your own HUD
- a ram still reeling the dummy and restarting on a second ram

`WreckSequence` reset can't be tested in EditMode (its `Awake` subscription never runs).

## 10. Decisions for confirmation

1. Effects use the damage hostility rule; self-application sets `allowNonEnemy`.
2. Magnitude is a positive size; negative or non-finite is `Invalid`.
3. On restart, stat effects take the new size, Stunned re-stops, and Overheated keeps its rhythm.
4. Stunned's stop keeps vertical velocity.
5. Stack placeholders: only Reeling stacks.
6. Overheated placeholders: Flat, 1 s. Tick tag `"overheated"`.
7. Respawn waits for the wreck sequence to finish, as well as the delay.
8. Respawn placeholders: delay 3 s, clearance margin 0.1 m.
9. HUD chip labels, colours, sizes and positions as in §6.
10. Debug menus apply with a null source and `allowNonEnemy`.
