# Aiming and camera

These two are one subject. The camera exists partly to serve an aiming invariant, and
changing the camera casually will break it.

## The invariant

> **Aiming moves the crosshair. Steering does not.**

Stated in the original brief as *"when a car moves the crosshair will maintain its relative
position in the viewport."* Everything below exists to guarantee it. It is the hardest
behaviour in the project to verify by eye, and the easiest to break with a change that
looks like an improvement.

## How aiming works

`aimYaw` is an angle **relative to the car's forward direction**, never a world angle.

```
aimYaw += mouse.deltaX × mouseSensitivity
aimYaw  = Mathf.Clamp(aimYaw, -cone/2, +cone/2)
```

World aim direction is `car.forward` rotated by `aimYaw`. The cursor is hidden and locked
(`CursorLockMode.Locked`).

Because `aimYaw` is car-relative, steering rotates the aim along with the chassis and the
crosshair holds its screen position **with no extra code**. The invariant falls out of the
representation rather than being enforced by a correction.

There is no vertical aim. Every car is grounded, so the crosshair moves only left and
right.

## Why the crosshair is projected, not lerped

`CrosshairHUD` draws at `Camera.WorldToScreenPoint` of a point along the aim ray — **not**
at a linear interpolation across screen X.

Screen position is a *tangent* function of angle. A linear mapping looks fine near the
centre and drifts visibly from where shots actually land near the screen edges.

Two details in that projection:

- When the aim point falls **behind** the camera, `WorldToScreenPoint` mirrors **both**
  axes, not just X. Both must be un-mirrored.
- If the cone ever exceeds the camera's horizontal FOV, the crosshair clamps to the screen
  edge. The design called for a direction arrow there; it is currently a colour tint.

## Camera

One Camera with a `CameraRig` component, switched by `CameraConfig.mode`:

| Mode | Placement |
|---|---|
| `ThirdPerson` (0) | `distance` behind, `height` above, pitched down by `pitch`, yaw locked to the car, 92° horizontal FOV |
| `FirstPerson` (1) | at the car's `DriverAnchor` child, yaw locked to the car, 102° horizontal FOV |

### Camera yaw is rigid to the chassis and must never be smoothed

Lagging the yaw makes the crosshair drift across the screen during turns — that is exactly
the invariant above, broken. Positional smoothing is permissible later. **Yaw smoothing is
not.** If a future change adds camera lag, it must not touch yaw.

## Every monitor sees the same width of world

A competitive fairness rule. Both camera modes hold a fixed **horizontal** field of view —
102° first person, 92° third person — whatever the player's monitor shape.

Unity's camera takes a *vertical* FOV and derives the horizontal one from the aspect ratio,
so by default wider screens see more to the sides. At a 70° vertical FOV a 21:9 player sees
~118° across against ~102° on 16:9: a car closing from the side, visible earlier. In a car
brawler that is exactly the information that matters.

So `CameraRig` sets the vertical FOV every frame from the configured horizontal angle and the
camera's current aspect, via `CameraFov.VerticalFor`. Wider screens lose some top and bottom
instead, which costs nothing here: every car is grounded and aim is horizontal only. A window
resize is picked up automatically.

This is the approach Overwatch takes. The stricter alternative, locking everyone to 16:9 with
black bars (StarCraft II), was rejected because it wastes screen area for protection this
game does not need.

### The crosshair follows the same rule

`CrosshairHUD.size` and `edgeMargin` are authored in pixels **at a 1920 px reference width**
and scale with the actual screen width. With a fixed horizontal view, a pixel covers the same
angle for every player only when measured against width. Scaling by height would make the
crosshair a different angular size on ultrawide.

### Hits never depend on the monitor

Aim is a world direction from the car. The crosshair only projects it onto the screen, so it
sits over the same world point for every player; its distance from screen centre in pixels
varies slightly with aspect, and that is harmless. Whether a shot hits is decided in the
world.

**When weapons are built, shots must originate at the car, not the camera.** Firing from the
camera toward the crosshair would make first and third person hit differently — the
third-person camera sits 3.5 m up and 8 m back. Identical across monitors either way, but
unfair between camera modes.

## Known behaviour that looks like a bug

**Reversing into the wall puts the third-person camera outside the arena, and the wall
vanishes.** The wall is a single-sided inward-facing mesh, and no camera collision is
specified. Left alone deliberately so it does not preempt the first/third-person
evaluation.

Cheapest fixes when it becomes worth solving: clamp camera radius to `arenaRadius − margin`,
spherecast from car to camera, or render the wall double-sided. The first two solve it; the
third only hides it.

## Tests

`AimMathTests` — 6 tests covering accumulate-and-clamp at both cone edges, no wrapping, and
zero delta leaving `aimYaw` untouched.

`CameraFovTests` — 5 tests: horizontal 102.45° / 91.49° reproduce 70° / 60° vertical at
16:9, a square screen gives equal angles, wider screens always get less vertical view, and
bad input (zero aspect, horizontal ≥ 180°) is clamped rather than producing NaN.

The invariant itself is verified by playing, not by test — see the acceptance checklist in
[workflow.md](workflow.md).
