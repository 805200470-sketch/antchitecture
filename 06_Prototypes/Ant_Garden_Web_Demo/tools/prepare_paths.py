#!/usr/bin/env python3
"""Prepare path drawings for Path Recognize.

Downscales each PNG so its longest side is <= 1400 px (Path Recognize's limit),
thickens the thin drawn lines so denoising cannot erase them (the centreline is
unchanged), and writes raw RGBA plus a small JSON sidecar that
tools/convert-paths.mjs feeds to Path Recognize's recognize().

Usage: python3 tools/prepare_paths.py <input_dir> <work_dir>
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageFilter

MAX_SIDE = 1400
LINE_THICKEN = 7  # MinFilter size at processing resolution (odd number)


def main(src: Path, work: Path) -> None:
    work.mkdir(parents=True, exist_ok=True)
    files = sorted(p for p in src.glob('*.png') if not p.name.startswith('._'))
    for f in files:
        im = Image.open(f)
        ow, oh = im.size
        scale = MAX_SIDE / max(ow, oh)
        w, h = round(ow * scale), round(oh * scale)
        gray = im.convert('L').resize((w, h), Image.LANCZOS)
        # dark lines on white: MinFilter grows the dark strokes evenly on both sides
        gray = gray.filter(ImageFilter.MinFilter(LINE_THICKEN))
        rgba = Image.merge('RGBA', (gray, gray, gray, Image.new('L', (w, h), 255)))
        (work / f'{f.stem}.rgba').write_bytes(rgba.tobytes())
        (work / f'{f.stem}.json').write_text(json.dumps(
            {'name': f.name, 'width': w, 'height': h, 'originalWidth': ow, 'originalHeight': oh}))
        print(f'{f.name}: {ow}x{oh} -> {w}x{h}')


if __name__ == '__main__':
    main(Path(sys.argv[1]), Path(sys.argv[2]))
