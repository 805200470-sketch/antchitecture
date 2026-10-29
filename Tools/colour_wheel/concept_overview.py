#!/usr/bin/env python3
"""concept_overview.py <folder> [out.jpg]

An overview of a round of concept images (<seg>_<key>_way_in.png and <seg>_<key>_heart.png, as Codex made them
from 02_Concepts/Prompts/PROMPTS_v17_natural_concepts_jobs.json): one row per colour-wheel segment, the way in and
the heart side by side, each labelled with the segment, the nest's name and the hue its light measures
(Tools/colour_wheel/measure_hue.py's method). Writes the overview in two halves (segments 0-150 and 180-330), so
each is readable on a phone, and a measures JSON beside them.
"""
import glob
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
os.environ.setdefault('VMIN', '0.6')
os.environ.setdefault('SMIN', '0.3')
import measure_hue  # noqa: E402

NAMES = {
    'kiln': '0° 红 · 古蚁圣殿 · 窑火', 'amber': '30° 橙 · 暖琥珀蚁穴', 'honey': '60° 黄 · 蜜罐粮仓',
    'leafcut': '90° 黄绿 · 切叶蚁菌圃', 'moss': '120° 绿 · 苔藓森林', 'verdigris': '150° 青绿 · 铜绿机城',
    'blue': '180° 青 · 蓝色真菌林', 'azure': '210° 天蓝 · 巨人遗物', 'cistern': '240° 蓝 · 雨水潭',
    'carapace': '270° 紫 · 王虫骸谷', 'slime': '300° 品红 · 黏菌花期', 'nursery': '330° 玫红 · 育儿室',
}
FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2 = ImageFont.truetype(FONT, 30), ImageFont.truetype(FONT, 22)


def main():
    folder = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(folder, '00_总览.jpg')
    pairs = []
    for w in sorted(glob.glob(os.path.join(folder, '*_way_in.png'))):
        base = os.path.basename(w)[:-len('_way_in.png')]
        h = os.path.join(folder, base + '_heart.png')
        seg, key = base.split('_', 1)
        pairs.append((seg, key, w, h if os.path.exists(h) else None))
    measures = {}
    W, H, G = 960, 540, 12
    halves = [p for p in pairs if int(p[0]) < 180], [p for p in pairs if int(p[0]) >= 180]
    for n, half in enumerate(halves):
        if not half:
            continue
        sheet = Image.new('RGB', (G + 2 * (W + G), 70 + len(half) * (H + 60)), (16, 17, 19))
        d = ImageDraw.Draw(sheet)
        d.text((G, 18), '概念图 第三版：自然的颜色、各自的地面、用上我们的模型（左：入口，右：心脏）', font=F1, fill=(236, 232, 222))
        y = 70
        for seg, key, w, h in half:
            files = [f for f in (w, h) if f]
            r = measure_hue.measure(files)
            measures[key] = r
            d.text((G, y + 14), f"{NAMES.get(key, key)}　·　光的色相 {r['hue']:.0f}°（{r['segment_name']}段，占 {r['segment_share']:.0%}）",
                   font=F2, fill=(232, 196, 120))
            for i, f in enumerate((w, h)):
                if not f:
                    continue
                im = Image.open(f).convert('RGB').resize((W, H), Image.LANCZOS)
                sheet.paste(im, (G + i * (W + G), y + 50))
            y += H + 60
        path = out.replace('.jpg', f'_{n + 1}.jpg')
        sheet.save(path, quality=85)
        print(path)
    json.dump(measures, open(os.path.join(folder, 'measures.json'), 'w'), ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
