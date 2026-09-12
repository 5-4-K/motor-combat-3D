# Arena and rendering

## Procedural geometry, not placed objects

`ArenaBuilder` generates a ground disc and a wall ring at runtime from `ArenaConfig`.
`ArenaMeshBuilder` builds the meshes; both are pure and tested.

Ground and wall are **real meshes, not rings of box colliders**. Box segments are simpler
but leave seams that catch a car sliding along the wall — and sliding along the wall is
something a brawler does constantly.

Non-convex `MeshCollider`s are legal here because this geometry is static.

### Winding

Unity treats clockwise-as-seen-from-the-front as the front face. The wall is wound so its
front face points at the arena centre. Reverse the two triples and the wall becomes
invisible from inside while **still colliding perfectly** — which makes it very easy to
ship by accident. Two tests pin the winding for exactly this reason.

## Sizes are absolute metres

`ArenaConfig.radiusMetres` sets the arena. The arena does not scale with the car.

A texture's scale is a physical property of the material — a paving slab is the same size
whatever drives over it — so `groundTileMetres` and `wallTileMetres` are absolute too.

**The one deliberate exception** is `gridCellInCarLengths`, which sizes the generated
placeholder grid. That grid is a measuring tool: its cells are one car long so speed,
braking distance and drift are countable while play-testing. It is ignored the moment a real
ground material is assigned.

## Tiling is baked into mesh UVs

Not set on the material. The alternative — `mainTextureScale` — would need re-tuning by hand
for every texture swapped in, and again on every radius change. Baked into UVs, any material
dropped into an `ArenaConfig` slot tiles correctly with no setup.

| Mesh | UV rule |
|---|---|
| Disc | `uv = worldXZ / tileSize`, offset a half-tile so the origin sits inside a cell rather than on a line crossing |
| Wall | `u = t × repeats`, `v = height / tileSize` |

### The wall seam is impossible by construction

`repeats = round(circumference / tileSize)`, clamped to at least 1. A fractional repeat
count cuts the texture mid-tile at angle 0 — a visible jump once per lap. Rounding to a
whole number removes the failure mode rather than making it unlikely.

The cost is that the wall's effective tile is slightly off the configured value (at r=45 m
with a 22.5 m tile, 12.57 repeats round to 13, giving a 21.75 m tile — about 3%). Invisible,
but it means wall tiles and floor tiles are not exactly commensurate.

### Vertical UVs keep tiles square

`v = height / tileSize`, not 0→1. Stretching one repeat to the wall height would squash any
real texture by the height-to-tile ratio. A short wall therefore shows a horizontal *slice*
of its texture — which is what undistorted means. Set `wallTileMetres = wallHeight` and the
wall shows exactly one full tile from base to top.

### Tangents are mandatory

`mesh.RecalculateTangents()` runs on both meshes, after UVs and normals.

Normal maps and parallax maps shade in **tangent space**: the shader needs a per-vertex
tangent to know which way U runs across the surface. Leave it out and the tangent array is
empty, the shader reads zeros, the basis degenerates, and the perturbed normal flips per
triangle — the wall shades in visible chunks and the floor's highlights smear. Nothing about
the geometry looks wrong, which is what makes it hard to diagnose.

Any future procedural mesh that will carry a normal-mapped material needs this too.

## Placeholder textures

Leave `groundMaterial` or `wallMaterial` empty and `ArenaTextureBuilder` generates one, so
the project needs no binary art to look right.

- **Ground** — a calibration grid, one cell per car length, heavier line every 5.
- **Wall** — vertical stripes, two per cell, giving the eye a rotation reference.

Both are built to tile, and both rules are pinned by test:

- Grid lines go on the **low edge of each cell only**. Draw both edges and every join
  between tiles carries a double-width line.
- The stripe count is **forced even**. An odd count puts the same colour on both sides of
  the seam, merging two stripes into one double-wide band once per lap.

Textures are generated with mipmaps and anisotropic filtering. Without mipmaps a one-car
grid across a 90 m floor aliases into crawling shimmer at distance — worst exactly where the
player is looking while driving fast.

A tiling texture **cannot** mark a unique direction, so cardinal markers on the wall are not
possible this way. They would need separate geometry at those four angles.

## URP: the traps

The project renders through the **Universal Render Pipeline**. A shader is written against
one specific pipeline — it must match that pipeline's lighting data layout, light loops and
keywords — so shaders are not portable between them.

**Magenta means the material's shader has no variant for the active pipeline.** Nearly
always a built-in-versus-URP mismatch on an imported asset. Fix with
`Window → Rendering → Render Pipeline Converter → Built-in to URP → Material Upgrade`.

Import order matters: **import textures before converting.** The converter reads `_MainTex`
to write `_BaseMap`, so if the texture is missing it faithfully writes null and you get flat
white surfaces instead of magenta ones.

**Property names differ.** URP Lit uses `_BaseMap` / `_BaseColor`; built-in Standard uses
`_MainTex` / `_Color`. A texture assigned to `_MainTex` on a URP material is silently
dropped. `ArenaBuilder.CreateMaterial` sets whichever the shader actually has.

**MSAA lives on the pipeline asset, per quality tier** — `Assets/Settings/PC_RPAsset.asset`
and `Mobile_RPAsset.asset`, both currently 4×. Set one and not the other and jagged edges
reappear on a quality switch looking like a fresh bug.

MSAA anti-aliases **geometry silhouettes only**. The fragment shader still runs once per
pixel, so it does nothing for specular shimmer from glossy materials, high-frequency normal
maps, or alpha-tested cutouts. For those, add FXAA/SMAA on the Camera — post-process AA
works on the finished image — or lower the material's smoothness. URP's **Deferred**
renderer ignores MSAA entirely; relevant if light count ever forces a switch.

## Tests

- `ArenaMeshBuilderTests` — 15 tests: vertex counts, radius, winding for both meshes,
  world-scaled UVs, integer wall repeats, square vertical UVs, usable tangents.
- `ArenaTextureBuilderTests` — 9 tests: the low-edge rule, line counts, even stripe counts,
  wrap mode and mipmaps.
- `ArenaBuilderTests` — 6 tests: absolute radius, tile-size selection, and the
  placeholder-versus-material branch.
