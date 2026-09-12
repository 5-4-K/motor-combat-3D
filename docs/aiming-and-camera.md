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
| `ThirdPerson` (0) | `distance` behind, `height` above, pitched down by `pitch`, yaw locked to the car |
| `FirstPerson` (1) | at the car's `DriverAnchor` child, yaw locked to the car |

### Camera yaw is rigid to the chassis and must never be smoothed

Lagging the yaw makes the crosshair drift across the screen during turns — that is exactly
the invariant above, broken. Positional smoothing is permissible later. **Yaw smoothing is
not.** If a future change adds camera lag, it must not touch yaw.

## Cone against FOV

At defaults a 60° vertical FOV at 16:9 is roughly 91.5° horizontal, against a 90° cone — so
the crosshair travels almost exactly to the screen edges. That is convenient, not a
constraint; the edge-clamp above covers wider cones.

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

The invariant itself is verified by playing, not by test — see the acceptance checklist in
[workflow.md](workflow.md).
