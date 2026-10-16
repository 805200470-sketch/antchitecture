# Ant Garden — Asset Image Prompts (splat-ready)

Prompt pack for ChatGPT / Codex image generation. A local script cuts each object out of its flat background and inflates the silhouette into a rounded Gaussian-splat relief, sampling colour per pixel, so every rule here serves that converter as much as the look.

## How to use

- Paste one prompt per image, exactly as written: each already ends with the shared Style Block and Avoid list. Generate 2–4 takes per asset; keep the one that passes the universal checks and its own checklist.
- Lock the look early: once the first asset is approved, attach it to later prompts with "match only the palette, lighting and rendering of the attached image, not its subject".
- Never type an asset ID into a prompt (models paint it as text). Suggested filename: `concept/env_<asset_id>_<nn>.png`, e.g. `env_trunk_porous_01.png`.
- Size is portrait 2:3 `1024×1536` unless the heading says otherwise; in Codex/API runs set it explicitly and use the highest quality setting.
- The prompts describe the look instead of naming the film or its director; named styles invite refusals and pastiche.
- Hex values mirror `PALETTE` in `src/config.js`, so image-built assets sit with the procedural ones. If you edit the Style Block or Avoid list, update all six prompts.

## Why these rules

- **The outline is the geometry.** Depth is inflated from the silhouette only; painted shading never becomes shape. Anything that must exist in 3D (trunk holes, lace openings, fern spirals, gaps between stems) needs real background showing through it.
- **Side by side, not in front.** Overlapping parts fuse into one bulge, so elements spread across one frontal plane with gaps between them.
- **Translucent means tinted, not see-through.** Membranes, glass and the fan canopy are coloured sea-glass teal; only true holes show the grey.
- **Pale edges are the cutout risk.** Game frost (`#D1EBED`) sits only ~23 RGB from `#E8ECEC`, so frost, hair and filament tips get darker teal roots and edges. If the cutout still eats them, take it to the technical artist (key tolerance, or a transparent-background PNG if the tool offers one) rather than darkening the art.
- **Light is baked; mood belongs to the engine.** Fog, god rays, mist and bloom are added in-game; baked into an image they become permanent fuzz around the asset.
- **The value budget is readability.** Mostly deep/mid teal with small pale and gold accents: the engine blooms bright pixels, and a dark, textured world is what lets the smooth pastel ants read. Warm gold stays rare so it keeps pulling the eye.

## Style Block (shared, embedded in every prompt)

```text
Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object.
```

## Avoid (shared negatives, embedded in every prompt)

```text
Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

## Universal checks (every image)

- [ ] Exactly one object; no text, labels, swatches, insets, borders or extra items.
- [ ] Background is one flat colour: corner and edge samples agree within ±5 RGB and sit near `#E8ECEC` (no gradient, vignette, brush noise, beige/blue drift, floor, shadow or reflection).
- [ ] Nothing cropped (except `trunk_porous` top/bottom) and at least 5% clear margin elsewhere; straight-on and upright, no tilt or perspective convergence.
- [ ] Nothing outside the silhouette: no glow halo, haze, particles or detached pieces.
- [ ] At 100% zoom the surface shows crisp micro-texture (pores, fibres, fronds, droplets), not smear, blur or mushy detail.
- [ ] Mostly deep/mid teal with pale and gold only as accents, no yellow cast, and it sits with the other approved images side by side.

---

## 1. `trunk_porous` — portrait 2:3 (1024×1536), crop exception

**Purpose:** Canyon-wall column, stacked and scaled into the giant trunks (tens of ant lengths tall) that wall the path; the darkest, heaviest mass on screen.

**Crop exception (this asset only):** it is a section of a huge trunk, so it runs off the top and bottom edges on purpose; the left and right edges must never be cropped.

```text
Vertical 2:3 portrait image (1024×1536). A vertical section of a colossal fungal tree trunk: one tall, slightly irregular column about half the image width and much taller than the image, entering at the bottom edge and leaving at the top edge. Only the top and bottom are cut off by the frame; the left and right sides of the trunk are fully visible, with a wide, even margin of empty background on both sides. The column keeps roughly the same width from bottom to top, with no roots, no crown and no broken ends. Its bark is rough, dark teal and sponge-like: densely pitted with small pores, split by long fibrous ridges and patched with frost-tipped moss. Four to six large oval openings of different sizes pass right through the trunk, each framed by a raised rim of pale-gold veins; through these open holes only the same flat grey background is visible, with no interior and no scenery. One or two further openings are sealed by a stretched, translucent sea-glass-teal lace membrane with a fine net of pale-gold veins, small softly glowing pearl droplets at the vein crossings, and a short fringe of fine icy-cyan hairs with darker teal roots along its edge. Thick, solid bark separates all the openings. Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object. Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

**Review checklist**
- [ ] Trunk leaves the frame at the top and bottom only; both sides fully visible with at least 10% background margin each.
- [ ] Width roughly constant from bottom to top, with no roots, crown or broken ends, so two copies could stack.
- [ ] 4–6 through-holes show only the flat background (no painted interior or scenery); 1–2 membrane-sealed openings carry gold veins and pearls.
- [ ] Solid bark between holes is at least as wide as the holes (a column, not a lattice); pores and fibres crisp at 100%.

## 2. `fan_tree` — portrait 2:3 (1024×1536)

**Purpose:** Landmark at path junctions and along the walls; its dome-on-a-stalk outline must read from far down the canyon.

```text
Vertical 2:3 portrait image (1024×1536). One complete fungal fan tree, whole from its root tips to the top of its canopy, centred with an even empty margin on every side and nothing cropped. A tall, dark, fibrous blue-teal trunk, slightly twisted and ridged with long fibres, makes up a little over half of the tree's height and flares at the bottom into five or six buttress roots that spread left and right and end on a flat base line. At the top the trunk splits into seven to nine curved ribs that arch upward and outward like the spokes of an open umbrella, carrying a huge jellyfish-like dome canopy about four fifths of the image width. The canopy is woven from hundreds of very fine filaments radiating from the crown, packed closely into one continuous, softly translucent web: deep teal at the crown and along the ribs, mid sea-glass teal across most of the dome, and pale icy cyan only at the rim, which breaks into a crisp, slightly drooping fringe of free filament tips; the grey background shows only between those fringe tips, never through the dome itself. The ribs show through the canopy, each lined with elongated pearl drops whose warm glowing cores keep their light inside the drop. Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object. Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

**Review checklist**
- [ ] Root tips, both canopy edges and every fringe tip are inside the frame with clear margin.
- [ ] Side-on: trunk vertical, canopy a broad, roughly symmetric dome, roots spreading left/right (none pointing at the camera).
- [ ] Canopy reads as hundreds of fine filaments yet forms one tinted teal mass; grey shows only between the free fringe tips, and the rim separates cleanly from the background.
- [ ] 7–9 ribs visible through the canopy, lined with elongated pearl drops whose glow stays inside them.

## 3. `spore_stalks` — portrait 2:3 (1024×1536)

**Purpose:** Ant-scale light cluster at the path edge, seen closer than anything else; carries the set's warm-gold accent, so detail matters most here.

```text
Vertical 2:3 portrait image (1024×1536). One cluster of five glass spore stalks rising from a small, compact moss tuft, the whole cluster centred with an even empty margin on every side and nothing cropped. The stalks are slender, smooth, translucent glassy stems tinted pale blue-teal with crisp darker teal edges, of clearly different heights (the tallest reaching near the top margin, the shortest about a third as tall), fanning gently outward so that no stem crosses or touches another and every bulb has clear space around it. Each stem ends in a glossy, slightly elongated glass bulb with a crisp darker refractive rim, one small soft highlight and a warm-gold glowing core whose light stays inside the glass. The moss tuft is dense and finely detailed, deep blue-green with frosted pale-cyan tips, about a quarter of the image width, and ends underneath in a flat base line. Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object. Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

**Review checklist**
- [ ] 4–6 stalks of clearly different heights; no stem crosses another, and no bulb touches another bulb or stem.
- [ ] Every bulb holds a warm-gold core with no glow beyond the glass, and at most one small highlight.
- [ ] Stems and bulbs keep crisp darker edges along their whole length; nothing fades into the grey.
- [ ] Moss tuft compact (about a quarter of the width), frost-tipped, flat underneath, no soil.

## 4. `curl_fern` — portrait 2:3 (1024×1536)

**Purpose:** Path-edge dressing a few ant lengths tall; its open spiral is the set's signature near-field shape.

```text
Vertical 2:3 portrait image (1024×1536). One cluster of three curled fungal fiddleheads growing from a low mossy base, centred with an even empty margin on every side and nothing cropped. Each fiddlehead is a thick, fleshy stalk that rises and rolls into a spiral at its tip; every spiral faces the viewer flat-on and is loosely coiled, with clear background visible between the coils. The three stalks have different heights and lean apart (the tallest in the middle, one curling to the left, one to the right) so they never overlap. The stalk skin is deep blue-teal, covered in a fine net of pale-gold lace veins with tiny pearl beads at the vein crossings, growing frostier and paler toward the curl; a dense fringe of short, fine icy-cyan hairs with darker teal roots runs along the outer edge of every curl. The base is a compact cushion of blue-green moss with frosted tips, ending underneath in a flat base line. Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object. Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

**Review checklist**
- [ ] 2–3 fiddleheads whose spirals face the viewer flat-on with background visible between the coils, so the curl reads from the outline alone.
- [ ] Stalks differ in height and never overlap.
- [ ] The hair fringe on each outer curl is crisp, teal-rooted and visible against the grey, not a white blur or glow.
- [ ] Lace veins and pearl beads visible at 100%, frost increasing toward the curl; moss base flat underneath.

## 5. `moss_mound` — landscape 3:2 (1536×1024)

**Purpose:** Ground cover at the foot of the canyon walls, seen at the ant's eye level; also hides the seam between wall and floor.

```text
Horizontal 3:2 landscape image (1536×1024). One rounded mound of dense fungal moss, seen exactly from the side, level with the mound, so its outline is a soft, slightly lumpy dome about twice as wide as it is tall, centred with an even empty margin on every side and nothing cropped; its underside ends in a flat, straight base line. The mound is built from countless tiny moss fronds and cushion clumps in deep blue-green to sea-green, their tips frosted pale icy cyan where the top light catches them, while the dome's outline stays green-teal and clearly distinct from the background. Tiny bioluminescent specks, warm gold and pale cyan, sit embedded among the fronds like dew. Three or four tiny sprouts rise from the top (two miniature curled frost fiddleheads and one or two miniature glass spore stalks with glowing bulbs), short, well separated and fully inside the frame. Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object. Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

**Review checklist**
- [ ] Landscape 3:2, pure side view: a dome outline over a flat base line, with no top surface seen from above and no soil.
- [ ] Individual fronds and cushions crisp at 100%; frosted tips, but the dome's outline stays teal-green and separates from the background.
- [ ] Glowing specks are tiny and embedded; none float free, none have halos.
- [ ] 3–4 sprouts (mini fiddleheads and mini glass stalks), uncropped and not touching each other.

## 6. `lace_curtain` — portrait 2:3 (1024×1536)

**Purpose:** Translucent screen hung between neighbouring trunks (or draped on one); a layer the ant looks through that stacks depth in the canyon.

```text
Vertical 2:3 portrait image (1024×1536). One free-hanging sheet of fungal lace membrane, seen flat-on from the front, centred with an even empty margin on every side and nothing cropped. The sheet fills about two thirds of the image width and three quarters of its height; it hangs from a thickened, slightly irregular top edge with three knotted anchor points, falls in very gentle vertical folds and narrows toward a ragged lower edge. The membrane is thin and translucent-looking but clearly tinted mid sea-glass teal, darker in the folds and never grey or white, and it is pierced by about ten oval openings of varied sizes, with plenty of membrane between them, that are true holes showing only the flat grey background. A network of raised pale-gold veins radiates from the anchor points and frames every opening, with small pearl droplets beaded at the vein junctions, a few of them glowing faintly warm from inside. A fringe of fine icy-cyan hairs with darker teal roots hangs from the lower edge and lines the largest openings. Nothing holds the sheet up: no trunks, branches, rods, hooks or ropes. Style: painterly-realistic, highly detailed render of an organism from an alien bioluminescent fungal jungle — imaginative shapes in realistic natural materials (porous spongy bark, wet translucent membrane, fibrous stalks, fine frost hairs, glassy droplets), crisp macro-level micro-texture, everything in sharp focus. Palette: deep teal to blue-green (#0F2B2E to #296161) for the main masses, mid sea-glass teal (#5C9E99 to #9ED6CC) for membranes and glass, pale icy cyan for frost, pale gold (#E6DEB8) veins, warm pearl (#FFF5DB) droplets and small warm-gold (#FFE09E) glowing cores; mostly deep and mid values with pale tones only as small accents; neutral-cool white balance. View: straight-on front elevation from the object's mid-height, near-orthographic, no perspective distortion, object upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Lighting: soft, even, diffuse frontal studio light with a gentle top light; glowing parts glow only inside the object. Avoid: text, letters, numbers, labels, watermark, signature, colour swatches, callout or zoom insets, borders, multiple views; ground, floor, soil, cast or contact shadow, reflection, gradient, vignette, noise or brush texture in the background; fog, haze, mist, god rays, strong rim light or backlight, bloom, lens flare or any glow halo spilling onto the background; floating spores, particles, droplets or loose pieces separate from the object; depth-of-field blur; tilted, top-down, low-angle or wide-angle views; cartoon, cel shading, ink outlines, smooth toy-like plastic; insects, ants, animals, people; yellow or sepia colour cast.
```

**Review checklist**
- [ ] Whole sheet visible (anchors, top edge, both sides, lower fringe); flat-on with no twist, and nothing holds it up.
- [ ] Openings are true holes showing only the flat background, with membrane between them at least as wide as the smaller holes (a sheet, not a net).
- [ ] The membrane is clearly teal, never grey or white, and its outline separates from the background.
- [ ] Pale-gold veins frame every opening, pearls sit at the junctions with their glow contained, and the lower fringe is crisp and teal-rooted with no loose hairs or droplets floating free.
