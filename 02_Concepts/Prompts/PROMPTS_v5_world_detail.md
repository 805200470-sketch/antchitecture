# Ant Garden — world detail pass (v5): 12 new assets + 6 ground textures

Goal: make the world read finer at ant scale. Ground-detail pieces (pebbles, roots, boulders, litter,
tiny mushrooms) are scattered by the thousand over the canyon floor and banks; new plants add variety to
both nests; the six ground textures give each style its own soil instead of a recoloured copy.

## A. Front images for Tripo (save to 02_Concepts/Fronts_for_3D/<id>.png)

Every image shows ONE object, whole, alone: square 1024×1024, a three-quarter view from slightly above
(about 20° down), the object centred and filling about 80% of the height with clear margin on all sides,
soft even studio light from the front-left, flat light neutral grey background (#E8ECEC) edge to edge.
Bold simple forms, restrained detail, soft gouache and watercolour painted look — the same family as the
existing assets. Glow stays inside the outline. No ground plane, cast shadow, text, labels or frame.

Common ending for every prompt below:
"Three-quarter view from slightly above, the whole object fully visible, centred with margin. Bold, simple, clearly readable form with restrained detail; soft gouache and watercolour painted look. Background: one perfectly flat, uniform very light neutral grey (#E8ECEC). Avoid: text, labels, frame, ground plane, cast shadow, glow halo spilling onto the background, busy fine detail."

| id | style | prompt (before the common ending) |
|---|---|---|
| pebble_cluster | both | Square 1024x1024 image of ONE cluster of seven smooth rounded pebbles of different sizes huddled together into a low mound, dark slate blue-grey stone with a few frost-white lichen spots and tiny velvety moss tufts in the gaps between them. |
| root_arch | both | Square 1024x1024 image of ONE gnarled tree root arching out of the ground in a low curve, both ends cut off flat at the same base line, dark navy-brown bark with deep fibrous ridges, a strip of velvety teal moss along the top, a few thick hairy rootlets hanging from the underside. |
| boulder_moss | both | Square 1024x1024 image of ONE large rounded boulder, slightly flattened, dark blue-grey stone with weathered pits and shallow cracks, a thick cushion of velvety deep teal moss on top spilling down one side, a few pale frost lichen rosettes on the bare stone. |
| fallen_leaf | blue | Square 1024x1024 image of ONE fallen dried fungal cap lying on its back: a broad, thick, leathery, wavy-edged shell, curled up at the rim like a shallow bowl, dark teal outside with pale radiating veins, smooth milky cyan-white inside that glows faintly. |
| tiny_mushrooms | blue | Square 1024x1024 image of ONE small clump of about twelve tiny mushrooms of different heights growing from a rounded dark moss cushion, slender pale stems and round milky cyan-white caps that glow softly from inside. |
| cup_fungus | blue | Square 1024x1024 image of ONE cluster of five deep cup-shaped fungi of different sizes growing from a short mossy stub, thick rounded rims, outside dark navy and velvety, inside smooth milky cyan that glows softly. |
| glass_sponge | blue | Square 1024x1024 image of ONE tall glass-sponge fungus: three upright thick-walled hollow tubes of different heights rising from a dark mossy base, the walls a coarse lattice of small square openings like a Venus flower basket, translucent milky cyan-white glowing softly from inside. |
| anemone_fungus | blue | Square 1024x1024 image of ONE sea-anemone-like fungus: a short thick dark teal stalk opening into a crown of about twenty plump rounded tentacles curving up and outward, the tentacle tips glowing milky cyan. |
| fungus_garden | amber | Square 1024x1024 image of ONE mound of cultivated fungus garden like a leafcutter-ant fungus comb, about twice as wide as tall, built of many small crumbly cream and beige cells and folds, dusted with tiny white fuzzy hyphae tufts, a few small orange and brown leaf fragments embedded. |
| dry_grass_tuft | amber | Square 1024x1024 image of ONE clump of dried grass: about fifteen thick curved flat blades in straw yellow and rust orange fanning out from a small root knot, plus three stalks carrying oat-like seed heads; blades stylised and thick, never hair-thin. |
| thorn_succulent | amber | Square 1024x1024 image of ONE spiky succulent rosette like a small agave: thick fleshy pointed leaves in rust orange and dusty ochre with darker tips and small thorns along the edges, arranged in a low rosette on a short woody base. |
| seed_pile | amber | Square 1024x1024 image of ONE heap of stored ant food: a low pile, about three times as wide as tall, of mixed seeds and grains — glossy striped sunflower seeds, round golden millet grains and a few split empty husks. |

For pebble_cluster, root_arch and boulder_moss the colours are blue-grey; the amber nest recolours them.

## B. Ground textures (save to 02_Concepts/Textures/<id>.png)

Every texture: square 1024×1024, a seamless tileable texture seen straight from above (orthographic, no
perspective, no horizon), soft even light with no cast shadows and no strong highlights, evenly spread
detail with no single focal point, no vignette, no text.

| id | prompt |
|---|---|
| blue_floor | Seamless tileable top-down texture of a soft velvety dark teal moss carpet with tiny round pores and fine fibres, scattered frost-white spore grains and a few minute pale glints, painterly but detailed. |
| blue_bank | Seamless tileable top-down texture of a fibrous fungal mat: interwoven dark blue and teal hyphae threads and thin roots running over velvety moss, a few tiny round pale spore bumps. |
| blue_plateau | Seamless tileable top-down texture of crusty dark slate soil covered in lichen: patches of pale frost-white lichen rosettes, fine grit and small flat stones, deep blue-grey tones. |
| amber_floor | Seamless tileable top-down texture of dry cracked ochre clay ground with fine sand grains, tiny rounded pebbles and a few thin cracks, warm ochre and rust tones. |
| amber_bank | Seamless tileable top-down texture of packed rust-orange earth with layered crumbly soil, thin dried root fibres and small grit, warm terracotta and brown tones. |
| amber_plateau | Seamless tileable top-down texture of reddish-brown packed earth with rusty orange lichen crust patches, tiny dried leaf fragments and grit. |
