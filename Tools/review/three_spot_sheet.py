#!/usr/bin/env python3
"""three_spot_sheet.py <before out dir> <after out dir> <sheet.jpg> [before label] [after label]

The hub's three-spot sheet: way in / halfway / deep in (r0_1/2/3_play stills of a -sceneshots run), before on the
left, after on the right, one row per spot. Example (kiln k20l):
  three_spot_sheet.py $TMPDIR/antgarden_runs/k19g/out $TMPDIR/antgarden_runs/k20l_full/out k20l_三处_之前之后.jpg \
      "之前：k19g" "之后：k20l（这一轮）"
"""
import sys
from PIL import Image, ImageDraw, ImageFont

before, after, out = sys.argv[1:4]
label_before = sys.argv[4] if len(sys.argv) > 4 else "之前"
label_after = sys.argv[5] if len(sys.argv) > 5 else "之后"
rows = [("r0_1_play_lab_way_in", "入口"), ("r0_2_play_lab_halfway", "半路"), ("r0_3_play_lab_heart", "深处（出口）")]
cols = [(before, label_before), (after, label_after)]
w, h, pad, top, left = 1152, 745, 10, 64, 150
font = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 32)
small = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 28)
s = Image.new("RGB", (left + 2 * (w + pad), top + 3 * (h + pad)), (18, 16, 16))
d = ImageDraw.Draw(s)
for j, (src, label) in enumerate(cols):
    d.text((left + j * (w + pad) + 12, 14), label, font=font, fill=(235, 225, 215))
for i, (name, label) in enumerate(rows):
    y = top + i * (h + pad)
    d.text((14, y + h // 2 - 16), label, font=small, fill=(200, 190, 180))
    for j, (src, _) in enumerate(cols):
        s.paste(Image.open(f"{src}/{name}.png").convert("RGB").resize((w, h)), (left + j * (w + pad), y))
s.save(out, quality=88)
print(out, s.size)
