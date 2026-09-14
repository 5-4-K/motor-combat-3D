# HUD

## Canvas

`HudRoot.Create()` builds a Screen Space Overlay canvas in code, the same way the arena and
config assets are built rather than hand-placed. Its `CanvasScaler` uses Scale With Screen
Size, reference **1920×1080**, matched on **width** — the same fairness rule as the camera and
crosshair: every monitor sees identical proportions, never more UI on a wider screen.

Widgets (`SelfHealthWidget`, `EnemyHealthBars`) are independent components added under the
root by `GameBootstrap`, each owning its own piece of screen. A later widget — weapon slots,
effect icons — is a new component under the same root, not a change to this one. The existing
OnGUI crosshair is untouched by any of this.

## Own health

`SelfHealthWidget` draws a green bar at the bottom centre of the screen, with `current / max`
printed above it. The displayed numbers are **rounded up** (`Mathf.CeilToInt`), so a living car
with fractional HP never reads "0" — only a car at exactly 0 does. The widget is identical in
first and third person (in first person you never see your own car, so its health has to live
on the screen), and hides itself entirely once the viewer's car is destroyed.

## Enemy bars

`EnemyHealthBars` draws a bar and name over every registered car that is an enemy of the
viewer (via `CarRegistry` and `Hostility`), rebuilt every `LateUpdate`:

- **Anchor.** Car position + up × (collider height / 2 + `anchorMarginMetres`, 0.4), projected
  through `Camera.WorldToScreenPoint`.
- **Visibility.** A bar is hidden when its anchor is behind the camera, off screen beyond
  `screenMargin` pixels, or the car is destroyed — `IDamageable.IsDestroyed` is checked every
  frame, so a bar disappears the instant its car dies.
- **Width clamp.** The pixel distance between the projections of anchor ± camera-right ×
  collider width / 2 is converted to reference pixels and clamped to
  [`minWidth` 60, `maxWidth` 160] — it shrinks with distance but never below a readable size.
- **Name label and draw order.** The car's name is drawn above the bar in `fontSize`
  (14-reference-pixel) text. Bars are sorted by camera-space depth every frame and reordered
  with `SetSiblingIndex` so nearer cars' bars draw on top of farther ones.
- **Screen space.** Because the whole thing is UI drawn in screen space rather than a
  world-space billboard, a bar always faces the viewer from any angle — there's no billboard
  rotation to get wrong.
- **Execution order.** `EnemyHealthBars` carries `[DefaultExecutionOrder(100)]`, so its
  `LateUpdate` runs after `CameraRig`'s (default order) in the same frame — reading the
  camera's fresh position and rotation rather than lagging a frame behind it.

## Not included

Excluded on purpose, addable later as new widgets without touching this code:

- Line-of-sight occlusion — a bar is drawn whenever its anchor projects on screen, even through
  a wall or another car.
- Floating damage numbers.
- A damage-chip trail.

A few smaller limitations worth knowing:

- Visibility is judged purely by a margin in **screen pixels** (`screenMargin`), not by any
  world-space distance or occlusion check.
- A destroyed car's bar is removed from view, but its entry in `EnemyHealthBars`'s internal
  dictionary stays until the car's `GameObject` is actually destroyed — there's no respawn yet
  to exercise reactivating it.
- `Health.Damaged`/`Destroyed` are plain C# events with no per-subscriber isolation: a
  subscriber that throws would stop `Health.Apply` before the wreck's own block runs, leaving
  the car undestroyed despite reaching 0 HP.

## Tests

| Fixture | Count |
|---|---|
| `HealthBarLayoutTests` | 7 |
| `HudRootTests` | 1 |
| `CarRegistryTests` | 3 |
