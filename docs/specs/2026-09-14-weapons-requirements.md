# Weapons — requirements from the user

Date: 2026-09-14. Source: the user's brief that started the weapons work, plus the decisions
made in conversation afterwards. This is the input for weapons sub-projects 2–6; the roadmap is
in `2026-09-14-combat-stats-damage-design.md` §0. Gameplay rules here come from the user. Items
under "Still open" still need a decision.

"Weapon", "ability" and "power" mean the same thing.

## Delivery and hit detection

- **Hitbox/hurtbox only.** A weapon impacts when its hitbox connects with a car's hurtbox. There
  is no hitscan, at least for now.
- **One fire height.** Every weapon on every car fires from the same height, chosen so a shot can
  never pass over any part of any car. Every car must have a hurtbox, and a chassis shaped to
  match, at that height, so no car can duck under a shot.
- **No friendly fire.** There are no teams yet, so a car's own weapons never hurt it. Team modes
  may come later.
- **Firing sides.** Weapons fire from the front, rear or sides, depending on the weapon. Some fire
  from one side only; some from several.
- **Bursts.** A weapon may fire several bursts, with a per-weapon delay between them. Each burst
  holds one or more shots. Shots in a burst either spread across a flat (2D) cone or fly parallel.
- **Weapons that aren't shot:**
  - floor areas that deal area damage and/or apply effects
  - auras: a half sphere with the floor as its base
  - maneuvers, such as a dash or a teleport
- **Aiming.** Some weapons need aiming. Others don't, such as an aura or an area that spawns at the
  car's own position.

## Lifetime, obstacles, pierce and bounce

- **Lifetime.** Each weapon has a range or a lifetime.
- **Perishing.** Some weapons perish when they hit an enemy or an obstacle.
- **Obstacles.** The arena has no obstacles yet; they come later. A weapon can itself act as an
  obstacle and block shots. "Obstacle" means both arena obstacles and obstacle-like weapons unless
  stated otherwise.
- **Pierce and bounce.** Some weapons pierce and some bounce. What they pierce or bounce off (cars,
  obstacles, walls) varies per weapon. For example, one may pierce cars and obstacles but not
  walls; another may bounce off cars but not obstacles or walls.
- **After-effects.** A weapon may leave an after-effect when it ends, whether from a hit or from
  reaching its range or lifetime. The after-effect covers an area and deals area damage and/or
  applies effects.

## Payload

A weapon deals damage and/or applies effects. It may also apply an impulse, like a ram does, with a
strength set per weapon.

Damage types:
- **Flat**
- **HP percentage:** percent of **max** HP, scaled by attack and defense
- **Pulse/tick:** damage every N ms to enemies in contact; the first tick lands on contact

**Damage formula** (built in sub-project 1): `damage × attack/100 × 100/(100 + defense)`. It uses
the attacker's attack **captured at firing time**, not at impact; the snapshot field is added in
sub-project 3. `attack` and `defense` drive damage; `strength` and `resistance` drive ram shove.

## Effects

- **What an effect is.** A timed stat change: a debuff on enemies or a buff on self. Its size and
  duration come from the config of whatever applies it: each weapon and the ram carry their own
  effect config.
- **Self-debuffs.** A weapon may debuff its own car to balance a very strong weapon.
- **No stacking.** Applying an effect a car already has does nothing, whoever applied it. Different
  effects can be active at once.
- **Combined stat changes.** Different effects on the same stat add their percentages (−30% and
  +20% gives −10%).

| Effect | Meaning |
|---|---|
| Stunned | Complete stop; movement and weapons disabled |
| Suppressed | Weapons disabled only |
| Overheated | Takes damage every N seconds |
| Corroded | Defense reduced |
| Reeling | No steering, as after a ram |
| Spiked | Top speed reduced |
| Fortified | Defense boosted |
| Armored | Immune to damage (but not to effects) |
| Overhauled | Removes all effects |
| Exhausted | Attack reduced |

Decisions since the brief:
- **Reeling is an effect.** So is every other status. The ram's lock and reel become effects,
  configured by `RamConfig`, in sub-project 2.
- **One Fire switch.** It covers every weapon, maneuvers included, so Suppressed and Stunned also
  block dashes and teleports.
- **Interrupted maneuvers.** A stun, or anything else that blocks Fire, cancels a maneuver in
  progress immediately. The maneuver checks Fire every physics step and ends itself; the effect
  then controls the car's velocity.

## Timing

- **Cooldown.** Each weapon has its own cooldown.
- **Wind-up and recovery.** A weapon may have a wind-up time and/or a recovery time.
- **Recovery blocks other weapons.** During recovery no other weapon can fire, even one that is off
  cooldown.

## Example weapons

These are built later. They don't cover every feature above, but the framework must support every
feature anyway.

| Weapon | Behaviour |
|---|---|
| **Predator** | A single pointy-capsule missile that deals flat damage. It flies straight and turns homing once it comes within X units of an enemy |
| **Pepperbox** | Fires X bursts from Z sides. Each burst is a cone of Y pill-sized, ellipse-shaped pellets that deal flat damage |
| **Magma Blast** | See below |
| **Thumper** | A single capsule shot that deals flat damage and applies Spiked X%. It perishes only on hitting an enemy car or when its lifetime ends. It bounces off walls and obstacles, following their shape |
| **Afterburner** | A flamethrower cone from N sides that deals tick damage. It is attached to the muzzle, isn't aimed with the mouse, and moves with the car |
| **Roadblock** | A cylinder that rolls forward on the ground and stuns. It pierces X cars, perishing on the (X+1)th impact or at max range |
| **Lance** | See below |
| **Tremor** | A ground zone (shape undecided) that appears where the car was when it fired and stays there. Enemies inside take tick damage and effects (undecided). The owner gets buffs (undecided) while it stays inside |
| **Shockwave** | Three waves, one after another with a delay. Each is a disc growing fast from the car's centre to a set radius and deals damage (effect undecided). Cars, obstacles and walls can't block them |
| **Thunderclap** | The attacker dashes forward up to a set distance; hitting anything stops the dash. An enemy hit is stunned and takes flat damage |
| **Wild Charge** | See below |

**Magma Blast**
- A single sphere projectile that deals flat damage.
- When it perishes (on impact or at max range) it explodes: a fast-growing sphere centred on the
  shot that deals its own flat damage.
- A car hit by the shot takes both damages; a car hit only by the explosion takes explosion damage
  only.
- The shot and the explosion both apply Corroded X%.
- It then leaves a circular floor field for a set duration, which applies the same Corroded to
  enemies that touch it.

**Lance**
- A beam that grows fast to max range: a cylinder with a rounded, capsule-like origin.
- It has a wind-up and lasts for its lifetime.
- Unlimited piercing, so it reaches max range or the arena wall.
- Deals tick damage.
- While the beam is active the attacker can't move forward or back, but can steer left and right to
  sweep it.

**Wild Charge**
- The attacker becomes Fortified X% for Y seconds, shown by a visible glow or halo.
- On contact with an enemy, from any direction or angle, it applies a fixed impulse along the
  contact direction. The impulse doesn't depend on speed or strength/resistance.
- It deals flat damage, and the state ends.

## Process rules from the user

- Weapons are built as six sub-projects, each with its own spec, plan and implementation.
- A later sub-project must not require a major change or rework of an earlier one. Small
  append-only edits are fine to composition roots: `CarFactory`, `CarDefinition`, `GameBootstrap`,
  `ConfigAssetBootstrap`, asmdefs and doc tables.
- Respawn is built at the start of sub-project 2.

## Still open

- Tremor's zone shape, its effects on enemies, and the self-buffs it gives.
- Shockwave's effect.
