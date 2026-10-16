---
name: ant-garden-visual-direction
description: Ant Garden's user-approved look (Nausicaa-style toxic fungal jungle, concept sheet 01-04), the ant-vs-environment contrast rule, and the image-to-splat converter constraints every asset image must respect
metadata:
  type: project
---

Ant Garden (web game, Gaussian splats): a small ant explores a canyon carved along real ant-nest paths, set in a Nausicaa-style "toxic jungle" fungal forest. As of 2026-10-02 the user has approved this direction: "Nausicaa shapes with more realistic material detail". Palette: deep teal/blue-green, pale cyan, pearl white, small warm-gold glows. The approved concept sheet has 01 curled frost fern, 02 fan tree (dome of filaments on ribs), 03 glass spore stalks, and 04 porous lace trunk. Moss mounds and a free-hanging lace curtain were added later. The ants are smooth, pastel teal/cream and slightly toy-like.

**Why:** the user's two reference images (a misty blue-teal fungal-forest painting and the generated concept sheet) are not stored in the repo. This note is the only record of what they showed.

**How to apply:**
- Keep the environment deeper, more textured and more saturated than the ants so the player reads.
- Warm gold stays a rare accent.
- Asset images feed a converter that cuts the object from a flat #E8ECEC background and inflates its silhouette. That means the outline is the geometry, parts must not overlap, translucent areas are tinted rather than see-through, and pale frost/fringe edges are the cutout risk (escalate to technical-artist).
- Full rules are in concept/PROMPTS.md. The game palette's source of truth is PALETTE in src/config.js.
