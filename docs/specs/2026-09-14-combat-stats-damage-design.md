# Combat stats, damage, destruction and HUD — design spec (weapons sub-project 1 of 6)

Date: 2026-09-14. Status: approved in conversation, implementation pending.
Gameplay rules come from the user. Items marked **(decision)** were settled by Claude where the
user gave no rule; they are listed in §9 for confirmation.

## 0. Where this sits

Weapons are built as six sub-projects, each with its own spec → plan → implementation:

| # | Sub-project | Depends on |
|---|---|---|
| **1** | **Combat stats & damage** (this spec): stats, damage, health, destruction, ability switches, HUD | — |
| 2 | Effects: the ten effects, non-stacking, durations | 1 |
| 3 | Weapon core: config data, loadout/input, cooldown/wind-up/recovery, sides, fire height, aiming, hitbox/hurtbox, payload | 1, 2 |
| 4 | Projectiles: bursts, spread, range/lifetime, pierce/bounce/perish, homing, after-effects | 3 |
| 5 | Areas, auras, beams | 3 |
| 6 | Maneuvers & self-states | 2, 3 |

**Closed-spec rule (user requirement).** A later sub-project only *adds*: new files, new config
types, new implementations of existing interfaces, new registrations. It never edits an earlier
sub-project's code or spec. This spec therefore creates every seam §8 lists, checked against the
user's eleven example weapons and ten effects.

## 1. Stats

### 1.1 Per car (`CarDefinition`)

| Field | Placeholder | Meaning |
|---|---|---|
| `maxHealth` | 1000 | Hit points |
| `attack` | 100 | Weapon and ram damage multiplier; 100 = the listed damage |
| `defense` | 0 | Damage reduction with diminishing returns (§2.2) |
| `strength` | 1 | Ram shove dealt; **renamed from `attack`** |
| `resistance` | 1 | Ram shove received; **renamed from `defense`** |

Tuning belongs to the user; values are placeholders.

The rename is a plain field rename in code, docs and `CarDefinition.asset`, with the YAML keys
edited directly and values preserved. `[FormerlySerializedAs]` can't be used: the old key
`attack` would collide with the new `attack` field.

### 1.2 Effective stats and modifiers (Core)

`CarStats` (Core, plain class, owned as `CarController.Stats`) holds base values and modifiers.

```
enum CarStat { Attack, Defense, Strength, Resistance, TopSpeed }

effective(stat) = max(0, base(stat) × (1 + Σ percent(stat) / 100))
```

- `SetBase(CarStat, float)`, `Base(CarStat)`, `Effective(CarStat)`.
- `Add(object source, CarStat stat, float percent)`, `Remove(object source)`, `RemoveAll()`.
  Re-adding the same source on the same stat replaces its previous percentage.
- Different sources on the same stat **add their percentages** (user rule). Corroded −30% and
  Fortified +20% on defense 100 give −10%, so 90. Order never matters.
- `TopSpeed` has base 1, a multiplier. `DrivingModule` multiplies `enginePower` by
  `Effective(TopSpeed)`, so terminal speed scales by the same factor. Spiked (sub-project 2)
  becomes a `TopSpeed` modifier.
- Every consumer reads **effective** values: damage reads `Attack`/`Defense`, ramming reads
  `Strength`/`Resistance`, driving reads `TopSpeed`. With no modifiers, behaviour is identical to
  today.

## 2. Damage

### 2.1 The damage path

All damage (rams now; projectiles, zones, beams and effects later) is one `DamageRequest`
applied to a car's `IDamageable`. `IDamageable` is a Core interface, implemented by `Health` in
the new Combat assembly.

```
struct DamageRequest {
    CarController source;        // null = environment
    string        sourceTag;     // "ram", later a weapon or effect id, for attribution and logs
    DamageKind    kind;          // Flat | MaxHealthPercent
    float         amount;        // flat HP, or percent of max HP
    bool          allowNonEnemy; // false (default): only enemies can be hit
}

interface IDamageable {
    float Current { get; }  float Max { get; }  bool IsDestroyed { get; }
    DamageResult Apply(in DamageRequest request);
    void AddGate(object source, Func<DamageRequest, bool> blocks);
    void RemoveGate(object source);
    event Action<DamageReport> Damaged;
    event Action<DamageReport> Destroyed;
}
```

`Apply` runs these steps in order:

1. **Targetable.** If the target lacks the `Targetable` ability (§4), the outcome is `NotTargetable`.
2. **Hostility.** Unless `allowNonEnemy` is set, `Hostility.AreEnemies(source, target)` must be
   true, otherwise the outcome is `NotHostile`.
3. **Gates.** If any registered gate returns true, the outcome is `Blocked`. The gate list starts
   empty; Armored registers one in sub-project 2.
4. **Raw amount.** `Flat` uses `amount`. `MaxHealthPercent` uses `amount / 100 × max` (user rule:
   percent of **max** HP).
5. **Mitigation.** `raw × effAttack / 100 × 100 / (100 + max(0, effDefense))`. `effAttack` comes
   from the source's stats (100 if the source is null) and `effDefense` from the target's. It
   applies to every kind (user rule). A result of `<= 0` gives `ZeroAmount` and no event.
6. **Apply.** `current = max(0, current − dealt)`. HP is stored as an exact float.
7. **Events.** `Damaged(DamageReport)` fires. If current reached 0 on this call, `Destroyed(DamageReport)`
   fires exactly once, then destruction starts (§3).

The call returns `DamageResult { outcome, dealt, killed }`, so a caller (for example a projectile
deciding whether it hit) gets an immediate answer.

`DamageReport { source, target, sourceTag, kind, dealt, healthAfter }`.

### 2.2 Formula examples (listed damage 100)

| Attacker attack | Target defense | Damage |
|---|---|---|
| 100 | 0 | 100 |
| 100 | 100 | 50 |
| 150 | 100 | 75 |
| 100 | 50 (defense 100 Corroded −50%) | 66.7 |
| 100 | 150 (defense 100 Fortified +50%) | 40 |
| 100 | 300 | 25 |

Damage never reaches zero while attack is positive, so no floor is needed. This is the MOBA
pattern used by League and Smite.

### 2.3 Tick damage

Tick damage isn't a separate kind. A source sends ordinary `Flat` or `MaxHealthPercent` requests
on a schedule. `TickSchedule` (Core, pure) gives every source the same rule:

- Keyed by (source instance, target).
- A tick is due when that pair has never ticked, or when `now ≥ lastTick + interval`. The first
  tick lands immediately on contact (user rule).
- The schedule survives contact breaks. **(decision)** Leaving and re-entering doesn't reset it,
  so brushing in and out can't tick faster than the interval.
- `Forget(target)` clears a target's entries.

### 2.4 Hostility

`Hostility.AreEnemies(a, b)` is a Core static.

- The rule is currently `a != b`: every other car is an enemy (user rule: no team system yet).
- A null source counts as an enemy of every car. **(decision)**
- The rule is a replaceable delegate, so team modes can swap it without touching any caller.

### 2.5 Ram damage

`RamConfig` gains `flankDamage` and `rearDamage`, both with placeholder **0**.

- On a flank or rear ram, `RammingModule` sends the victim a `Flat` request of that amount, with
  the attacker as source and `sourceTag = "ram"`.
- Head-ons deal no damage (user rule).
- The ram shove is unchanged apart from reading effective `Strength`/`Resistance`.

## 3. Destruction

The moment current HP reaches 0:

- **The car becomes a wreck.** `Health` calls `Abilities.UnblockAll()` and `Stats.RemoveAll()`, which
  ends any ram lock, reel and (later) effects. It then adds an untimed block on
  `Throttle | Steer | Fire | Ram | Targetable`. Grip and YawHold stay on, so the wreck **slides to
  a stop** under normal drag and grip without spinning (user choice).
- **Layer change.** The car root's collider moves to physics layer **`Wreck`**, which collides only
  with **`Arena`** (ground and wall). The wreck passes through cars now, and through weapons and
  obstacles later (user rule).
- **Camera.** `CameraRig` keeps following the wreck's transform. After deactivation it holds the
  last position until game modes decide otherwise.

`WreckSequence` (Combat) plays over time, configured by a new `WreckConfig` asset:

| Field | Placeholder | Meaning |
|---|---|---|
| `rollDegrees` | 180 | Barrel roll about the model's length axis (user choice) |
| `rollSeconds` | 0.8 | Duration of the roll, eased |
| `fadeSeconds` | 1.5 | Alpha goes from 1 to 0 over this time |
| `removeAfterSeconds` | 1.5 | The car GameObject is deactivated, not destroyed, so modes can respawn it |

- **Visual only.** It rotates and fades the visual child. The child's name constant moves from
  `CarFactory.VisualName` to `CarController.VisualChildName` in Core, so Combat can find it. The
  physics box stays flat.
- **Lift during the roll.** The visual rises so its lowest rotated corner never dips below the
  floor: `lift(θ) = max(0, halfW·|sin θ| + halfH·|cos θ| − halfH)`, using the collider's half
  width and half height.
- **Fade.** At death, every material on the visual's renderers is cloned and switched to URP
  transparent: `_Surface = 1`, `_Blend = 0`, `_SrcBlend = SrcAlpha`, `_DstBlend = OneMinusSrcAlpha`,
  `_ZWrite = 0`, keyword `_SURFACE_TYPE_TRANSPARENT`, render queue 3000.
  - Alpha is driven per renderer and material index through the `MaterialPropertyBlock` value
    `_BaseColor`. It starts from the block's existing colour when set (the team tint), otherwise
    from the material's colour.
  - The clones are destroyed on deactivation.

**Debug hooks.** `[ContextMenu]` entries on `Health`, "Debug: take 25% max HP" and "Debug: destroy",
let the user test damage and death while every real damage source is still 0. Editor-only.

## 4. Ability switches (Core), replacing `CarStatus`

Ram lock and reel are re-expressed on a general mechanism. Effects (Stunned, Suppressed, Reeling)
and maneuvers (Lance, dash) then add blocks without editing it.

```
[Flags] enum CarAbility {
    Throttle   = 1,    // throttle input used
    Steer      = 2,    // steer input used
    YawHold    = 4,    // driving writes yaw each step (off = free spin)
    Grip       = 8,    // lateral grip applied
    Fire       = 16,   // weapons may fire (read by sub-project 3)
    Ram        = 32,   // may qualify as ram attacker
    Targetable = 64    // may receive damage and be rammed
}

enum BlockRefresh { KeepLonger, Restart, IgnoreIfActive }

class CarAbilities {
    void Block(object source, CarAbility abilities, float seconds, BlockRefresh refresh); // +∞ = untimed
    void Unblock(object source);
    void UnblockAll();
    bool Has(CarAbility ability);    // true only if no active block covers every requested flag
    void Advance(float dt);           // called first in CarController.FixedUpdate, as CarStatus is today
}
```

When a source that already has an active block blocks again, its refresh mode decides what
happens:

- `KeepLonger` keeps `max(remaining, seconds)` and replaces the ability set.
- `Restart` sets the remaining time to `seconds` and replaces the ability set.
- `IgnoreIfActive` does nothing. This is the non-stacking rule effects need.

Blocks from different sources never interact. A block with remaining time 0 or less is removed on
`Advance`.

Today's behaviour, re-expressed exactly:

| Today | Block |
|---|---|
| Ram lock (the attacker, and both cars in a head-on) | `Throttle | Steer | Ram`, `attackerLockSeconds`, `KeepLonger` |
| Ram reel (the victim) | `Throttle | Steer | YawHold | Grip | Ram`, `reelSeconds`, `Restart` |
| Wreck (§3) | `Throttle | Steer | Fire | Ram | Targetable`, +∞ |

Consumer changes, all behaviour-preserving:

- **`DrivingModule`:**
  - Throttle is used only with `Has(Throttle)`, and steer only with `Has(Steer)`.
  - The yaw write is skipped without `Has(YawHold)`, and grip without `Has(Grip)`.
  - Engine force is `enginePower × Stats.Effective(TopSpeed)`.
- **`RammingModule`:**
  - `canAttack = Has(Ram)`.
  - Spin decay runs when `!Has(YawHold)`.
  - A pair isn't resolved if either car lacks `Targetable`.
  - Shove uses effective `Strength`/`Resistance`.

`CarController.Status` becomes `CarController.Abilities`. `CarStatus` and `CarStatusTests` are
replaced by `CarAbilities` and `CarAbilitiesTests`, which cover the same timing cases plus the
refresh modes.

## 5. Physics layers (Core)

`PhysicsLayers` (Core, static) holds the layer name constants and `ConfigureCollisions()`, which
`GameBootstrap` calls once before spawning.

| Layer | Index | Used by |
|---|---|---|
| `Arena` | 8 | Ground and wall (`ArenaBuilder`) |
| `Car` | 9 | Live car root (`CarFactory`) |
| `Wreck` | 10 | Destroyed car root |

- `Wreck` collides only with `Arena`. `Car` collides with `Car` and `Arena`. All other pairs keep
  Unity's defaults.
- The layer names are added to `ProjectSettings/TagManager.asset`.
- Later sub-projects add their own layers (projectiles, obstacles, zones) and exclusions, without
  editing these three rows.

## 6. HUD

### 6.1 Structure

- **Canvas.** `HudRoot` (HUD assembly) builds a Screen Space Overlay canvas in code. Its
  `CanvasScaler` uses Scale With Screen Size, reference 1920×1080, matching **width**. That's the
  fairness rule: identical proportions on every monitor.
- **Widgets.** Widgets are independent components under the root. This spec adds
  `SelfHealthWidget` and `EnemyHealthBars`. Later sub-projects add weapon slots and effect icons as
  new widgets. The existing OnGUI crosshair doesn't change.
- **Font.** Text uses uGUI `Text` with Unity's built-in `LegacyRuntime.ttf`. **(decision)** This
  avoids importing TextMeshPro resources now; changing the font later is internal to a widget.
- **Data.** The HUD reads only Core:
  - `CarRegistry`, a static list. `CarController` registers in `OnEnable` and unregisters in
    `OnDisable`.
  - `IDamageable` for health.
  - `Hostility` to tell enemies from self.
  - The camera.

  `GameBootstrap` sets the viewer to the player car.

### 6.2 Own HP: `SelfHealthWidget`

- A green bar at the bottom centre, with `current / max` above it.
- Display is `ceil(current)` and `max`, so a living car never shows 0 (user choice).
- The same in first and third person.
- Hidden when the viewer car is destroyed.

### 6.3 Enemy bars: `EnemyHealthBars`

For every registered car that is an enemy of the viewer, each `LateUpdate`:

- **Anchor.** Car position + up × (collider height / 2 + `anchorMarginMetres` 0.4), projected with
  `WorldToScreenPoint`.
- **Hidden** when behind the camera (`z ≤ 0`), fully off screen, or the car is destroyed. The bar is
  removed at the moment of death.
- **Width.** The pixel distance between the projections of anchor ± camera.right × collider
  width / 2, clamped to [`minWidth` 60, `maxWidth` 160] reference pixels (user choice: it shrinks,
  down to a minimum).
- **Height.** `barHeight` is 8 reference pixels.
- **Look.** Red fill on a dark track, with the car's name above it in 14-reference-pixel text (user
  choice: bar plus name). Shown at full HP too (user choice).
- **Draw order.** Nearer cars' bars draw on top; sibling order is sorted by camera distance.
- It always faces the viewer, because it's drawn in screen space.
- The positioning maths (visibility, width clamp, display text) lives in a pure `HealthBarLayout`
  static so it can be tested.

Excluded, and addable later as new widgets:
- Line-of-sight occlusion
- Floating damage numbers
- A damage-chip trail

## 7. Assemblies and files

| Assembly | New / changed |
|---|---|
| Core | + `CarStats`, `CarStat`, `CarAbilities`, `CarAbility`, `BlockRefresh`, `IDamageable`, `DamageRequest`, `DamageKind`, `DamageOutcome`, `DamageResult`, `DamageReport`, `Hostility`, `TickSchedule`, `CarRegistry`, `PhysicsLayers`; `CarController` gains `Stats`, `Abilities`, `VisualChildName`, registry hooks; − `CarStatus` |
| **Combat** (new, refs Core) | `DamageRules` (pure), `HealthState` (pure: steps 1–7 of §2.1 without Unity), `Health` (component), `WreckSequence`, `WreckConfig` |
| Driving | `DrivingModule` reads `Abilities` and `Stats` |
| Ramming | Renames; reads `Abilities`/`Stats`; ram damage; `RamConfig.flankDamage/rearDamage` |
| Cars | `CarDefinition` stats + `wreckConfig`; `CarFactory` sets stat bases, layer `Car`, adds `Health` + `WreckSequence` |
| Arena | Layer `Arena` on ground and wall |
| HUD | `HudRoot`, `SelfHealthWidget`, `EnemyHealthBars`, `HealthBarLayout`; asmdef + `UnityEngine.UI` |
| Bootstrap | `PhysicsLayers.ConfigureCollisions()`, HUD setup, validation of `wreckConfig` |
| EditorTools | `ConfigAssetBootstrap` creates `WreckConfig` |

The assembly rule still holds: Combat, Driving, Ramming and HUD reference only Core, and Cars and
Bootstrap compose them.

## 8. Closed-spec check: every later need has a seam here

| Later need (user's list) | Seam in this spec |
|---|---|
| Flat, %HP and tick damage from any weapon | `DamageRequest` + `TickSchedule` |
| Corroded, Fortified (defense), Exhausted (attack) | `CarStats.Add` on `Defense` / `Attack` |
| Spiked (top speed) | `CarStat.TopSpeed`, read by driving |
| Armored (damage immunity, not effect immunity) | Damage gates (step 3) |
| Overheated (damage every N s) | `TickSchedule` + `DamageRequest` |
| Self-debuff for balance (for example Overheated on self) | `allowNonEnemy` |
| Stunned (no movement, no weapons) | `Block(Throttle | Steer | Fire)`; the "complete stop" is a one-time velocity write by the effect |
| Suppressed (weapons only) | `Block(Fire)` |
| Reeling as an effect | `Block(Throttle | Steer | YawHold | Grip)` with `IgnoreIfActive` |
| Effects don't stack | `BlockRefresh.IgnoreIfActive`; modifiers and gates keyed by source |
| Overhauled (removes all effects) | `Unblock` / `Remove` / `RemoveGate` for each effect source |
| No friendly fire; teams later | `Hostility` rule delegate |
| Lance: no forward or back, can still sweep | `Block(Throttle)` only |
| Thunderclap dash, Wild Charge | Blocks + stats; contact handling belongs to sub-project 6 |
| Weapons disabled while recovering or wrecked | `Has(Fire)` |
| Weapons ignore wrecks | `Targetable` + `Wreck` layer |
| Kill credit for game modes | `DamageReport.source`, `Destroyed` event |
| Weapon slots, cooldowns, effect icons on the HUD | New HUD widgets; `CarRegistry` |
| Projectile, obstacle and zone layers | New layers added next to §5's |
| Impulse from weapons | Not a damage concern; sub-project 3 applies it through the Rigidbody, as ramming does |

## 9. Decisions (all confirmed by the user, 2026-09-14)

1. The tick schedule survives contact breaks, so brushing in and out can't tick faster.
2. A null damage source (environment) counts as an enemy of everyone and uses attack 100.
3. HUD text uses the legacy built-in font rather than TextMeshPro, for now.

## 10. Testing

EditMode tests, on pure statics and plain classes:

- **`DamageRulesTests`**: the §2.2 table; `MaxHealthPercent` is taken of max HP; negative defense is
  treated as 0.
- **`HealthStateTests`**:
  - The outcome for each step in order: not targetable, not hostile, blocked, zero amount, applied.
  - Clamping at 0; `Destroyed` firing once; `allowNonEnemy`; a null source.
- **`CarStatsTests`**: percentages add, floor at 0, same-source replacement, `Remove`/`RemoveAll`,
  identity with no modifiers.
- **`CarAbilitiesTests`**: the previous `CarStatus` timing cases, all three refresh modes, sources
  staying independent, untimed blocks, `UnblockAll`.
- **`TickScheduleTests`**: first tick immediate, interval respected, schedule survives breaks,
  `Forget`.
- **`HostilityTests`**: the default rule, a null source, a replaced rule.
- **`HealthBarLayoutTests`**: hidden behind the camera, width clamp, rounded-up display, never
  "0" while alive.
- **`PhysicsLayersTests`**: layer names resolve; after configuration `Wreck` ignores `Car` and
  collides with `Arena`.
- **`CarFactoryTests`**: stat bases come from the definition; the root sits on the `Car` layer;
  `Health` and `WreckSequence` are attached with their config.
- **`RamRulesTests`**: existing tests with parameter renames only.

Play checklist additions (`docs/workflow.md`):

- **Bars at full HP.** Your own HP bar and the dummy's bar both show at full HP. The dummy's bar faces
  you from every side and shrinks with distance.
- **Debug damage.** "Debug: take 25% max HP" on the dummy shortens its bar; on the player it
  updates the bottom number, rounded up.
- **Ram damage.** Temporarily set `flankDamage` above 0: a flank ram deals damage and a head-on
  doesn't. Then set it back.
- **Debug destroy.** "Debug: destroy" on the dummy:
  - Its bar vanishes instantly.
  - The wreck slides to a stop, rolls sideways, fades, and disappears at about 1.5 s.
  - You can drive through it while it fades, but it never sinks through the floor or passes the
    wall.
- **Ram rows.** Every existing ram checklist row still passes, since the ability re-expression is
  behaviour-preserving.

## 11. Documentation

- **New:**
  - `docs/combat.md`: stats, damage path, formula, ticks, hostility, destruction, abilities, layers
  - `docs/hud.md`
- **Updated:**
  - `CLAUDE.md`: doc table and "Not built yet"
  - `docs/architecture.md`: the Combat assembly and Core contracts
  - `docs/ramming.md`: renames, ram damage, abilities
  - `docs/driving-physics.md`: abilities and `TopSpeed`
  - `docs/tuning.md`: new fields and `WreckConfig`
  - `docs/workflow.md`: tests and checklist

## 12. Out of scope

- The effects themselves, weapons, projectiles, zones, maneuvers and obstacles (sub-projects 2–6)
- Respawn and game modes
- Healing
- Damage numbers and occlusion
- Audio and VFX beyond the wreck's roll and fade
