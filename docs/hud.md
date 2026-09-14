# HUD

## Canvas

`HudRoot.Create()` builds a Screen Space Overlay canvas in code, the same way the arena and
config assets are built rather than hand-placed. Its `CanvasScaler` uses Scale With Screen
Size, reference **1920×1080**, matched on **width** — the same fairness rule as the camera and
crosshair: every monitor sees identical proportions, never more UI on a wider screen.

Widgets (`SelfHealthWidget`, `EnemyHealthBars`, `SelfEffectsWidget`) are independent
components added under the root by `GameBootstrap`, each owning its own piece of screen. A
later widget — weapon slots, say — is a new component under the same root, not a change to
this one. The existing OnGUI crosshair is untouched by any of this.

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

## Effect chips

Both the self bar and every enemy bar show the car's active effects as a row of chips, laid
out and pooled by the shared `EffectChipRow` (`Show(IEffectReceiver)` reads `GetActive`, which
is already in `EffectType` order — see [effects.md](effects.md)).

- **`SelfEffectsWidget`** draws the viewer's own chips centred above the self health bar's
  label — in first person you never see your own car, so its state has to live on the screen
  too. Placeholders: chip 72×24 reference px, gap 6, font 15, sitting 96 reference px above
  the bottom of the screen (clears the bar and its `current / max` label).
- **`EnemyHealthBars`** gives each bar its own chip row, anchored just below the bar.
  Placeholders: chip 48×14 reference px, gap 3, font 10 — small enough that several chips fit
  under even the narrowest (60 px) bar.

**Labels, colours and rounding** all come from `EffectChipLayout`, a pure static shared by
both widgets:

- Each effect has a fixed 4-letter label (`STUN`, `SUPP`, `HEAT`, `CORR`, `REEL`, `SPIK`,
  `FORT`, `ARMR`, `OVHL`, `EXHS`) and its own chip colour — placeholder art, text chips until
  icons exist.
- A timed chip reads e.g. `"STUN 1.2s"`: `remaining` is rounded **up** to tenths of a second,
  in invariant culture, so a chip never reads `0.0s` while the effect is still active. An
  untimed effect (`remaining` infinite) shows only its label.

**Pooling.** `EffectChipRow` creates a chip (background image + label text) only the first
time a row needs one more than it currently has; after that, showing, hiding and relabelling
existing chips is all `Show` ever does, so a flickering effect never allocates UI. `RowX`
centres the whole row on 0 for whatever count is currently visible, so a row re-centres itself
as effects come and go rather than leaving a gap where an ended effect's chip used to sit.

## Not included

Excluded on purpose, addable later as new widgets without touching this code:

- Line-of-sight occlusion — a bar is drawn whenever its anchor projects on screen, even through
  a wall or another car.
- Floating damage numbers.
- A damage-chip trail.

A smaller limitation worth knowing: a destroyed car's bar is removed from view, but its entry
in `EnemyHealthBars`'s internal dictionary stays until the car's `GameObject` is actually
destroyed — respawn only reactivates the same GameObject, so the existing entry is simply
reused rather than exercising that cleanup path.

`screenMargin` is in **reference pixels**: it is converted to screen pixels via the canvas
scale factor before the visibility check, so the pop-out point is resolution-independent
rather than a fixed number of physical pixels.

## Tests

| Fixture | Count |
|---|---|
| `HealthBarLayoutTests` | 7 |
| `HudRootTests` | 1 |
| `CarRegistryTests` | 3 |
