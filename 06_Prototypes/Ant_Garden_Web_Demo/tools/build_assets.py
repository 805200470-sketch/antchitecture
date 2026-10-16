#!/usr/bin/env python3
"""Convert every approved concept image into a splat asset and rebuild the index.

Reads concept/assets.json:
  { "assets": [ { "id": "fan_tree_a", "image": "fan_tree_a.png", "kind": "fan",
                  "height": 12, "thickness": 1.0, "cropEdges": false,
                  "replace": true, "enabled": true } ] }
Writes assets/splats/<id>.ply (+ .json) and assets/splats/index.json, which the
demo loads on start. Images whose file is missing are skipped with a note.

Usage: python3 tools/build_assets.py [--max-side 700]
"""
import argparse
import json
from pathlib import Path

from image_to_splat import convert
from mesh_to_splat import convert as convert_mesh
import numpy as np
from image_to_splat import quat_from_z, write_ply
from sheet_to_splat import carve, load_rgba

ROOT = Path(__file__).resolve().parent.parent


def main(max_side):
    manifest = json.loads((ROOT / 'concept' / 'assets.json').read_text())
    out_dir = ROOT / 'assets' / 'splats'
    out_dir.mkdir(parents=True, exist_ok=True)
    index = []
    for a in manifest['assets']:
        src = ROOT / 'concept' / (a.get('mesh') or a.get('sheet') or a['image'])
        if not src.exists():
            print(f"skip {a['id']}: {src.name} not found")
            continue
        dst = out_dir / f"{a['id']}.ply"
        if a.get('sheet'):
            # orthographic front | side sheet → carved 3D volume
            sheet = load_rgba(src)
            w = sheet.shape[1] // 2
            xyz, n, col, scale, foot, size = carve(sheet[:, :w], sheet[:, w:], None, None, a['height'],
                                                   a.get('res', 200), a.get('tol', 0.06))
            write_ply(dst, xyz, n, col, np.full(len(xyz), 0.97, np.float32), scale, quat_from_z(n))
            meta = {'splats': int(len(xyz)), 'footprint': foot, 'footRadius': max(size[0], size[2]) / 2, 'width': size[0]}
        elif a.get('mesh'):
            # a real 3D model (e.g. from Tripo): sample its textured surface
            meta = convert_mesh(src, dst, a['height'], a.get('splats', 150000))
            meta['footprint'] = meta['footprint3d']
            meta['width'] = meta['size'][0]
        else:
            meta = convert(src, dst, a['height'], a.get('maxSide', max_side), a.get('thickness', 1.0),
                           a.get('tol', 0.09), a.get('cropEdges', False), mode=a.get('mode', 'round'),
                           spread=a.get('spread', 0.18), flatten=a.get('flatten', 1.0))
        index.append({
            'id': a['id'],
            'kind': a['kind'],
            'file': f"./assets/splats/{a['id']}.ply",
            'height': a['height'],
            'footRadius': round(meta['footRadius'] * 0.8, 3),
            'footprint': meta['footprint'],
            'replace': a.get('replace', True),
            'stack': a.get('cropEdges', False) or a.get('stackSheet', False),  # a section of something taller: stack copies
            'enabled': a.get('enabled', True),
            'splats': meta['splats'],
            'cluster': a.get('cluster'),
            'shape': 'carved' if a.get('sheet') else 'mesh' if a.get('mesh') else 'relief',
        })
        print(f"{a['id']}: {meta['splats']} splats, {meta['width']:.1f} x {a['height']} units")
    (out_dir / 'index.json').write_text(json.dumps({'assets': index}, indent=2, ensure_ascii=False))
    print(f'index: {len(index)} assets')


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--max-side', type=int, default=700)
    main(ap.parse_args().max_side)
