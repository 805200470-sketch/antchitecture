#!/usr/bin/env python3
"""codex_batch_v5.py [name ...] - the v5 world-detail images (12 Tripo fronts, 6 ground textures) through
Tools/codex_image.sh, three at a time; one line per finished image. Names limit the run, e.g. root_arch seed_pile."""
import concurrent.futures, os, subprocess, sys
S = os.path.dirname(os.path.abspath(__file__))   # Tools/
HOME = os.path.expanduser('~')
FRONTS = HOME + '/Desktop/AntGarden/02_Concepts/Fronts_for_3D/'
TEX = HOME + '/Desktop/AntGarden/02_Concepts/Textures/'
END = (" Three-quarter view from slightly above, the whole object fully visible, centred with margin. Bold, simple, clearly readable form "
       "with restrained detail; soft gouache and watercolour painted look. Background: one perfectly flat, uniform very light neutral grey (#E8ECEC). "
       "Avoid: text, labels, frame, ground plane, cast shadow, glow halo spilling onto the background, busy fine detail.")
TEND = (" Square, seen straight from above (orthographic, no perspective, no horizon), soft even light with no cast shadows and no strong "
        "highlights, evenly spread detail with no single focal point, no vignette, no text.")
jobs = [
 (TEX + 'blue_bank.png', 'Seamless tileable top-down texture of a fibrous fungal mat: interwoven dark blue and teal hyphae threads and thin roots running over velvety moss, a few tiny round pale spore bumps.' + TEND),
 (TEX + 'blue_plateau.png', 'Seamless tileable top-down texture of crusty dark slate soil covered in lichen: patches of pale frost-white lichen rosettes, fine grit and small flat stones, deep blue-grey tones.' + TEND),
 (TEX + 'amber_floor.png', 'Seamless tileable top-down texture of dry cracked ochre clay ground with fine sand grains, tiny rounded pebbles and a few thin cracks, warm ochre and rust tones.' + TEND),
 (TEX + 'amber_bank.png', 'Seamless tileable top-down texture of packed rust-orange earth with layered crumbly soil, thin dried root fibres and small grit, warm terracotta and brown tones.' + TEND),
 (TEX + 'amber_plateau.png', 'Seamless tileable top-down texture of reddish-brown packed earth with rusty orange lichen crust patches, tiny dried leaf fragments and grit.' + TEND),
 (FRONTS + 'pebble_cluster.png', 'Square 1024x1024 image of ONE cluster of seven smooth rounded pebbles of different sizes huddled together into a low mound, dark slate blue-grey stone with a few frost-white lichen spots and tiny velvety moss tufts in the gaps between them.' + END),
 (FRONTS + 'root_arch.png', 'Square 1024x1024 image of ONE gnarled tree root arching out of the ground in a low curve, both ends cut off flat at the same base line, dark navy-brown bark with deep fibrous ridges, a strip of velvety teal moss along the top, a few thick hairy rootlets hanging from the underside.' + END),
 (FRONTS + 'boulder_moss.png', 'Square 1024x1024 image of ONE large rounded boulder, slightly flattened, dark blue-grey stone with weathered pits and shallow cracks, a thick cushion of velvety deep teal moss on top spilling down one side, a few pale frost lichen rosettes on the bare stone.' + END),
 (FRONTS + 'fallen_leaf.png', 'Square 1024x1024 image of ONE fallen dried fungal cap lying on its back: a broad, thick, leathery, wavy-edged shell, curled up at the rim like a shallow bowl, dark teal outside with pale radiating veins, smooth milky cyan-white inside that glows faintly.' + END),
 (FRONTS + 'tiny_mushrooms.png', 'Square 1024x1024 image of ONE small clump of about twelve tiny mushrooms of different heights growing from a rounded dark moss cushion, slender pale stems and round milky cyan-white caps that glow softly from inside.' + END),
 (FRONTS + 'cup_fungus.png', 'Square 1024x1024 image of ONE cluster of five deep cup-shaped fungi of different sizes growing from a short mossy stub, thick rounded rims, outside dark navy and velvety, inside smooth milky cyan that glows softly.' + END),
 (FRONTS + 'glass_sponge.png', 'Square 1024x1024 image of ONE tall glass-sponge fungus: three upright thick-walled hollow tubes of different heights rising from a dark mossy base, the walls a coarse lattice of small square openings like a Venus flower basket, translucent milky cyan-white glowing softly from inside.' + END),
 (FRONTS + 'anemone_fungus.png', 'Square 1024x1024 image of ONE sea-anemone-like fungus: a short thick dark teal stalk opening into a crown of about twenty plump rounded tentacles curving up and outward, the tentacle tips glowing milky cyan.' + END),
 (FRONTS + 'fungus_garden.png', 'Square 1024x1024 image of ONE mound of cultivated fungus garden like a leafcutter-ant fungus comb, about twice as wide as tall, built of many small crumbly cream and beige cells and folds, dusted with tiny white fuzzy hyphae tufts, a few small orange and brown leaf fragments embedded.' + END),
 (FRONTS + 'dry_grass_tuft.png', 'Square 1024x1024 image of ONE clump of dried grass: about fifteen thick curved flat blades in straw yellow and rust orange fanning out from a small root knot, plus three stalks carrying oat-like seed heads; blades stylised and thick, never hair-thin.' + END),
 (FRONTS + 'thorn_succulent.png', 'Square 1024x1024 image of ONE spiky succulent rosette like a small agave: thick fleshy pointed leaves in rust orange and dusty ochre with darker tips and small thorns along the edges, arranged in a low rosette on a short woody base.' + END),
 (FRONTS + 'seed_pile.png', 'Square 1024x1024 image of ONE heap of stored ant food: a low pile, about three times as wide as tall, of mixed seeds and grains - glossy striped sunflower seeds, round golden millet grains and a few split empty husks.' + END),
]
only = set(sys.argv[1:])
todo = [j for j in jobs if not only or os.path.basename(j[0])[:-4] in only]
def run(job):
    out, prompt = job
    r = subprocess.run([S + '/codex_image.sh', out, prompt], capture_output=True, text=True)
    return r.stdout.strip() or r.stderr.strip()
with concurrent.futures.ThreadPoolExecutor(3) as ex:
    for line in ex.map(run, todo):
        print(line, flush=True)
print('batch done', flush=True)
