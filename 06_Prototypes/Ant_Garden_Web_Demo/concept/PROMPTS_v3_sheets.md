# Ant Garden — Orthographic design sheets for 3D carving (v3)

Feedback so far: v1 images had too much detail and unclear forms; single-image reliefs look flat from the
side. v3 asks Codex for **design sheets of single, simple elements** — a front view and a true side view
(plus a top view where noted), same scale, same baseline — which `tools/sheet_to_splat.py` carves into a
real 3D volume with an exact collision footprint. Clusters (spore groves, fern clumps) are assembled from
single elements in the demo, so every element stays readable.

Rules for every sheet: 2:1 landscape (2048×1024); LEFT half = front view, RIGHT half = side view rotated
90° (true profile, not a copy of the front); both orthographic, no perspective, same height and same base
line; one element only; bold simple forms, restrained detail, soft painted (gouache/watercolour) look; flat
background edge to edge; no shadow, floor, text, labels, arrows or dividing line; glow stays inside the
outline. Save as `concept/sheet_<id>.png`.

## 1. `curl_glow` — dark navy background (#0E1822)
```text
Wide 2:1 orthographic design sheet (2048x1024) of ONE glowing curled fungal frond, shown twice at the same scale on the same base line: on the left half the front view, on the right half the true side view rotated 90 degrees. The frond is a single thick, fleshy, milky-translucent stalk rising from a small dark moss cushion and curling over at the top into a broad open spiral like a fiddlehead; in the front view the spiral faces the viewer as a clear open coil, in the side view the frond is seen edge-on as a slim curved S-shaped profile about a third as wide. Bold, simple, clearly readable form with restrained surface detail: smooth milky cyan-white flesh glowing softly from within, a few soft darker veins, a short sparse fringe of glassy hairs along the curl's outer edge. Soft gouache and watercolour painted look. Background: one perfectly flat, uniform very dark navy (#0E1822) on both halves, no dividing line. Orthographic, no perspective. Avoid: text, labels, arrows, grid, borders, shadow, floor, halo or glow spilling onto the background, multiple fronds, busy fine detail.
```

## 2. `spore_stalk` — dark navy background (#0E1822), front view only is fine (rotationally symmetric)
```text
Wide 2:1 orthographic design sheet (2048x1024) of ONE glass spore stalk, shown twice at the same scale on the same base line: front view on the left half, side view rotated 90 degrees on the right half (nearly identical because the stalk is round). A slender, gently curving translucent glass-like stem rising from a tiny dark moss tuft, topped by one round, slightly elongated glossy bulb that glows softly milky cyan-white from inside with a brighter core and a thin darker rim. Bold, simple, clearly readable form with restrained detail; soft gouache and watercolour painted look. Background: one perfectly flat, uniform very dark navy (#0E1822) on both halves, no dividing line. Orthographic, no perspective. Avoid: text, labels, arrows, grid, borders, shadow, floor, halo or glow spilling onto the background, more than one stalk, busy fine detail.
```

## 3. `fan_tree` — light grey background (#E8ECEC)
```text
Wide 2:1 orthographic design sheet (2048x1024) of ONE fungal fan tree, shown twice at the same scale on the same base line: front view on the left half, side view rotated 90 degrees on the right half. A tall, dark blue-teal fibrous trunk with a gentle twist flares at the bottom into five short buttress roots and opens at the top into a broad, smooth umbrella canopy made of fine translucent filaments, tinted mid sea-glass teal, with a slightly drooping fringed rim and a few elongated pearl drops glowing softly along the ribs. The canopy is wide and round, so front and side silhouettes are similar. Bold, simple, clearly readable form with restrained detail; soft gouache and watercolour painted look. Background: one perfectly flat, uniform very light neutral grey (#E8ECEC) on both halves, no dividing line. Orthographic, no perspective. Avoid: text, labels, arrows, grid, borders, shadow, floor, glow halo, busy fine detail, warm gold cast.
```

## 4. `sponge_mound` — light grey background (#E8ECEC); add a third panel? No: front + side only
```text
Wide 2:1 orthographic design sheet (2048x1024) of ONE low rounded mound of dark spongy fungal moss, shown twice at the same scale on the same base line: side view from the front on the left half, side view rotated 90 degrees on the right half. The mound is a soft, lumpy dome about twice as wide as tall in the left view and about one and a half times as wide as tall in the right view, pierced by a handful of clear round cavities of a few sizes, covered in velvety deep blue-green moss with light frost on the top edges and two or three tiny glowing milky cyan-white spore bulbs. Bold, simple, clearly readable form with restrained detail; soft gouache and watercolour painted look. Background: one perfectly flat, uniform very light neutral grey (#E8ECEC) on both halves, no dividing line. Orthographic, no perspective. Avoid: text, labels, arrows, grid, borders, shadow, floor, glow halo, busy fine detail, warm gold cast.
```

## 5. `trunk_night` — light grey background (#E8ECEC), may run off the top and bottom edges
```text
Wide 2:1 orthographic design sheet (2048x1024) of ONE section of a colossal fungal tree trunk, shown twice at the same scale: front view on the left half, side view rotated 90 degrees on the right half; in both views the column enters at the bottom edge and leaves at the top edge, with empty background on its left and right. A slightly irregular dark navy-blue to blue-teal column with broad fibrous ridges, velvety moss patches and light frost, pierced by three or four large clean oval holes in the front view and two or three different holes in the side view, through which only the flat background shows; one small opening sealed by a faintly glowing milky membrane. Bold, simple, clearly readable form with restrained detail; soft gouache and watercolour painted look. Background: one perfectly flat, uniform very light neutral grey (#E8ECEC) on both halves, no dividing line. Orthographic, no perspective. Avoid: text, labels, arrows, grid, borders, shadow, floor, glow halo, busy fine detail, warm gold cast.
```
