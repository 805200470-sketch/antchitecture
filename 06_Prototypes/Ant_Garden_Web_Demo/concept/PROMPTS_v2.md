# Ant Garden — Asset Prompts v2 (night, luminous, painterly)

Second pass after the user's two mood references (2026-10-03): a deep-blue night fungal jungle, hand-painted
animation-background feel, organisms that glow from inside, porous sponge-like moss ground, layered tree
silhouettes in blue haze. User feedback on v1: too much detail, forms not clear, weak sense of place. v2 therefore asks for
**bold, simplified, instantly readable forms**: one big silhouette per object, a few large features, painted
texture used sparingly. Same conversion rules as `PROMPTS.md` (one object, flat background, no shadow,
straight-on, nothing cropped, glow stays inside the outline), with one change:

- **Background follows the object.** Luminous, pale assets use a flat very dark navy background (#0E1822) so the
  milky glow separates cleanly; dark assets keep the flat light grey (#E8ECEC).

Save as `concept/env2_<asset_id>_01.png`.

## Style block v2 (embedded below)

Painted look of a hand-painted animated-film background: bold, simplified, clearly readable forms with one strong
silhouette, a few large features and restrained surface detail, soft gouache and watercolour brushwork; an alien
bioluminescent fungal jungle at night. Palette: deep
navy-blue to blue-teal (#0B1724 to #1F4F5E) masses, blue-green moss (#24504F to #3E7A72), milky translucent
cyan-white (#CFEFF2 to #F2FFFF) for glowing flesh, tiny pearl-white droplets; cool moonlit white balance; no warm
gold or yellow.

## 1. `curl_glow` — portrait 2:3 (1024×1536), dark background

```text
Vertical 2:3 portrait image (1024x1536). One cluster of three translucent, glowing curled fungal fronds rising from a low, dark blue-green mossy base, centred with an even empty margin on every side and nothing cropped. Each frond is a thick, fleshy, milky-translucent stalk that sweeps upward and curls over at its tip like a breaking wave or a fiddlehead; the flesh glows softly from within in pale cyan-white, brightest along the curl and dimmer and bluer toward the base, with a few soft darker veins inside the translucent flesh. A short, sparse fringe of glassy hairs runs along the outer edge of each curl. The three fronds have clearly different heights (the tallest in the middle) and lean apart so they never overlap, with dark background visible between them and inside each curl. The mossy base is compact, deep blue-green with faint frost, and ends underneath in a flat base line. Painted look of a hand-painted animated-film background: bold, simplified, clearly readable forms with one strong silhouette, a few large features and restrained surface detail, soft gouache and watercolour brushwork with gentle painted edges; an alien bioluminescent fungal jungle at night. Palette: deep navy-blue to blue-teal (#0B1724 to #1F4F5E), blue-green moss (#24504F to #3E7A72), milky translucent cyan-white (#CFEFF2 to #F2FFFF) for the glowing flesh, tiny pearl-white droplets; cool moonlit white balance; no warm gold or yellow. View: straight-on front elevation from the object's mid-height, near-orthographic, object upright. Background: one perfectly flat, uniform, very dark navy (#0E1822), edge to edge. The glow stays inside the object's outline: no halo, bloom or light spilling onto the background. Avoid: busy fine detail, clutter, noisy micro-texture, many small parts; text, letters, labels, watermark, borders, multiple views, insets; ground, floor, cast shadow, reflection, gradient or vignette in the background; fog, haze, god rays, lens flare; floating particles or loose pieces; depth-of-field blur; tilted or wide-angle views; ink outlines, cel shading, plastic toy look; insects, animals, people; warm or yellow cast.
```

Review: 3 fronds, no overlap, dark gaps inside every curl; glow inside the outline (no halo on the background);
hair fringe crisp; base flat; background flat navy at the corners and edges.

## 2. `spore_glow` — portrait 2:3 (1024×1536), dark background

```text
Vertical 2:3 portrait image (1024x1536). One cluster of seven slender, translucent glass-like stalks rising from a small, compact, dark blue-green moss tuft, centred with an even empty margin on every side and nothing cropped. The stalks have clearly different heights (the tallest near the top margin, the shortest about a quarter as tall), curve gently and fan outward so that no stalk crosses or touches another. Each stalk ends in a round, glossy, slightly elongated bulb that glows softly milky cyan-white from inside, with a brighter core, a thin darker rim and one small highlight; the stalks are faintly luminous blue-white and translucent. The moss tuft is about a fifth of the image width, deep blue-green with faint frost, and ends underneath in a flat base line. Painted look of a hand-painted animated-film background: bold, simplified, clearly readable forms with one strong silhouette, a few large features and restrained surface detail, soft gouache and watercolour brushwork with gentle painted edges; an alien bioluminescent fungal jungle at night. Palette: deep navy-blue to blue-teal (#0B1724 to #1F4F5E), blue-green moss (#24504F to #3E7A72), milky translucent cyan-white (#CFEFF2 to #F2FFFF) for the glowing bulbs, tiny pearl-white droplets; cool moonlit white balance; no warm gold or yellow. View: straight-on front elevation from the object's mid-height, near-orthographic, object upright. Background: one perfectly flat, uniform, very dark navy (#0E1822), edge to edge. The glow stays inside each bulb and stalk: no halo, bloom or light spilling onto the background. Avoid: busy fine detail, clutter, noisy micro-texture, many small parts; text, letters, labels, watermark, borders, multiple views, insets; ground, floor, cast shadow, reflection, gradient or vignette in the background; fog, haze, god rays, lens flare; floating particles or loose pieces; depth-of-field blur; tilted or wide-angle views; ink outlines, cel shading, plastic toy look; insects, animals, people; warm or yellow cast.
```

Review: 6–8 stalks, none crossing; every bulb glows inside its rim, no halo; tuft small and flat underneath;
background flat navy.

## 3. `sponge_mound` — landscape 3:2 (1536×1024), light background

```text
Horizontal 3:2 landscape image (1536x1024). One low, rounded mound of dark, spongy fungal moss seen exactly from the side, level with the mound, so its outline is a soft, lumpy dome about twice as wide as it is tall, centred with an even empty margin on every side and nothing cropped; its underside ends in a flat, straight base line. The mound's surface is pierced by eight to twelve clear round cavities of a few sizes, like a sea sponge, the cavities dark navy inside; the ridges between them are covered in velvety deep blue-green moss with a light frost catching the top edges. A few tiny translucent glowing spore stalks with milky cyan-white bulbs grow from two or three of the cavities, short and well separated. Painted look of a hand-painted animated-film background: bold, simplified, clearly readable forms with one strong silhouette, a few large features and restrained surface detail, soft gouache and watercolour brushwork with gentle painted edges; an alien bioluminescent fungal jungle at night. Palette: deep navy-blue to blue-teal (#0B1724 to #1F4F5E) masses, blue-green moss (#24504F to #3E7A72), milky translucent cyan-white (#CFEFF2 to #F2FFFF) for the small glowing bulbs; cool moonlit white balance; no warm gold or yellow. View: straight-on side elevation from the mound's mid-height, near-orthographic. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Soft, even, diffuse frontal light with a gentle top light; glowing parts glow only inside the object. Avoid: busy fine detail, clutter, noisy micro-texture, many small parts; text, letters, labels, watermark, borders, multiple views, insets; ground, floor, soil, cast or contact shadow, reflection, gradient or vignette in the background; fog, haze, god rays, lens flare; floating particles or loose pieces; depth-of-field blur; tilted, top-down or wide-angle views; ink outlines, cel shading, plastic toy look; insects, animals, people; warm or yellow cast.
```

Review: pure side view, dome over a flat base; many dark cavities, mossy ridges, frost on top edges; a few tiny
glowing stalks uncropped; background flat light grey.

## 4. `trunk_night` — portrait 2:3 (1024×1536), light background, crop exception (top/bottom only)

```text
Vertical 2:3 portrait image (1024x1536). A vertical section of a colossal fungal tree trunk: one tall, slightly irregular column about half the image width and much taller than the image, entering at the bottom edge and leaving at the top edge; only the top and bottom are cut off by the frame, and the left and right sides are fully visible with a wide, even margin of empty background on both sides. The column keeps roughly the same width from bottom to top, with no roots, crown or broken ends. Its bark is dark navy-blue to blue-teal, porous and spongy, with a few broad fibrous ridges and soft painted pores, patched with velvety blue-green moss and light frost. Four or five large, clean oval openings of different sizes pass right through the trunk; through them only the same flat grey background is visible. Two smaller openings are sealed by a thin, translucent, faintly glowing milky cyan-white membrane laced with fine pale veins and a few tiny glowing droplets. Thick, solid bark separates all openings. Painted look of a hand-painted animated-film background: bold, simplified, clearly readable forms with one strong silhouette, a few large features and restrained surface detail, soft gouache and watercolour brushwork with gentle painted edges; an alien bioluminescent fungal jungle at night. Palette: deep navy-blue to blue-teal (#0B1724 to #1F4F5E) masses, blue-green moss (#24504F to #3E7A72), milky translucent cyan-white (#CFEFF2 to #F2FFFF) for the glowing membranes; cool moonlit white balance; no warm gold or yellow. View: straight-on front elevation, near-orthographic, upright. Background: one perfectly flat, uniform, very light neutral grey (#E8ECEC), edge to edge. Soft, even, diffuse frontal light with a gentle top light; glowing parts glow only inside the object. Avoid: busy fine detail, clutter, noisy micro-texture, many small parts; text, letters, labels, watermark, borders, multiple views, insets; ground, floor, cast shadow, reflection, gradient or vignette in the background; fog, haze, god rays, lens flare; floating particles or loose pieces; depth-of-field blur; tilted or wide-angle views; ink outlines, cel shading, plastic toy look; insects, animals, people; warm or yellow cast.
```

Review: cropped top/bottom only, sides clear; width roughly constant (stackable); 5–7 through-holes showing
only grey; 1–2 glowing membranes; dark blue bark, no gold.

## 5. `tex_floor` — square 1:1 (1024×1024), seamless texture (no background)

```text
Square 1:1 image (1024x1024). A seamless, tileable, top-down texture of a canyon floor in an alien bioluminescent fungal jungle at night: soft, velvety deep blue-green moss carpet with broad, gently rounded clumps, a scattering of small round pores and shallow pits, faint frost on the highest clumps and a few tiny milky cyan-white glowing dots. Even, diffuse lighting from directly above, no directional shadows, no perspective, no horizon, no objects standing up. The pattern repeats seamlessly on all four edges with no visible seam and no single dominant feature. Painted look of a hand-painted animated-film background: simplified, readable shapes and restrained detail, soft gouache and watercolour brushwork. Palette: deep navy-blue to blue-teal (#0B1724 to #1F4F5E), blue-green moss (#24504F to #3E7A72), tiny milky cyan-white glows; cool moonlit white balance; no warm gold or yellow. Avoid: text, labels, borders, vignette, perspective, horizon, large objects, strong shadows, busy fine detail.
```

## 6. `tex_bank` — square 1:1 (1024×1024), seamless texture (no background)

```text
Square 1:1 image (1024x1024). A seamless, tileable texture of a steep mossy canyon wall seen straight-on from the side, for wrapping onto a slope: spongy, porous blue-green moss bank with soft rounded bulges, a few clear round cavities, thin pale roots trailing down, frost catching the upper edges of the bulges and a few tiny milky cyan-white glowing specks. Even, diffuse frontal light, no directional shadows, no perspective, no sky, no objects standing out. The pattern repeats seamlessly on all four edges with no visible seam and no single dominant feature. Painted look of a hand-painted animated-film background: simplified, readable shapes and restrained detail, soft gouache and watercolour brushwork. Palette: deep navy-blue to blue-teal (#0B1724 to #1F4F5E), blue-green moss (#24504F to #3E7A72), tiny milky cyan-white glows; cool moonlit white balance; no warm gold or yellow. Avoid: text, labels, borders, vignette, perspective, sky, large objects, strong shadows, busy fine detail.
```

Textures are not cut out: tools/ projects them onto the procedural floor and banks as splat colours, so the
terrain shares the painted look of the assets.
