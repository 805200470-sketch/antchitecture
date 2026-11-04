import sys
from PIL import Image, ImageDraw, ImageFont
before, after, v15, out = sys.argv[1:5]
rows = [("r0_1_play_lab_way_in", "入口 · 游戏视角"), ("r0_1_eye_lab_way_in", "入口 · 蚂蚁视角"), ("r0_2_play_lab_halfway", "中途 · 游戏视角"),
        ("r0_2_eye_lab_halfway", "中途 · 蚂蚁视角"), ("r0_3_play_lab_heart", "出口 · 游戏视角"), ("r0_9_aerial", "俯视")]
cols = [(before, "之前（k18c 的内容）"), (after, "之后（k19g，更丰富）"), (v15, "参照：蓝色 v15")]
w, h, pad, top, left = 900, 468, 10, 70, 170
try:
    font = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 30); small = ImageFont.truetype("/System/Library/Fonts/Hiragino Sans GB.ttc", 24)
except Exception:
    font = small = ImageFont.load_default()
W = left + len(cols) * (w + pad); H = top + len(rows) * (h + pad)
s = Image.new("RGB", (W, H), (18, 16, 16)); d = ImageDraw.Draw(s)
for j, (src, label) in enumerate(cols):
    d.text((left + j * (w + pad) + 12, 18), label, font=font, fill=(235, 225, 215))
for i, (name, label) in enumerate(rows):
    y = top + i * (h + pad)
    d.text((12, y + h // 2 - 30), label.replace(" · ", "\n"), font=small, fill=(200, 190, 180))
    for j, (src, _) in enumerate(cols):
        try:
            im = Image.open(f"{src}/{name}.png").convert("RGB").resize((w, h)); s.paste(im, (left + j * (w + pad), y))
        except Exception as e:
            d.text((left + j * (w + pad) + 20, y + 20), "（无此视角）", font=small, fill=(150, 140, 130))
s.save(out, quality=88); print(out, s.size)
