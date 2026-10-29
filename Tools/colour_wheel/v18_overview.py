#!/usr/bin/env python3
"""v18_overview.py [folder] [maps folder]

The overview of the concept round made by editing v15's stills (02_Concepts/Particle_Styles_v4, from
02_Concepts/Prompts/PROMPTS_v18_v15_edits_jobs.json) next to each nest's map concept (02_Concepts/Nest_Maps_v1): one
row per colour-wheel segment, the game's view and the ant's eye side by side, its map beside them, labelled with the
nest's light, its landmark beside the way and the colour its exit glows (the next nest's). (No measured hue: in these
the warm materials pull the measure off the light; the game's own colours are set in NestTheme.) Written in two
halves (segments 0-150 and 180-330), each readable on a phone.
"""
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFont

# name, light, landmark beside the way, what its exit glows with
NESTS = {
    'kiln': ('0° 红 · 古蚁圣殿', '深红余烬', '盘着光路的陶塔', '琥珀橙'),
    'amber': ('30° 橙 · 暖琥珀蚁穴', '琥珀橙', '封着小虫的琥珀块', '花粉黄'),
    'honey': ('60° 黄 · 蜜罐粮仓', '蜂蜜黄', '挂在根上的蜜罐蚁', '黄绿'),
    'leafcut': ('90° 黄绿 · 切叶蚁菌圃', '黄绿', '灰白菌圃', '苔绿'),
    'moss': ('120° 绿 · 苔藓森林', '苔绿', '比蚂蚁高的露珠', '铜绿'),
    'verdigris': ('150° 青绿 · 铜绿机城', '铜绿', '半埋的怀表', '青'),
    'blue': ('180° 青 · 蓝色真菌林', '青', '一圈玻璃泡孢子柄', '天蓝'),
    'azure': ('210° 天蓝 · 巨人遗物', '天蓝', '斜立的纽扣和针', '群青'),
    'cistern': ('240° 蓝 · 雨水潭', '群青', '拱在水潭上的根', '深紫'),
    'carapace': ('270° 紫 · 王虫骸谷', '深紫', '半开的甲虫空壳', '品红'),
    'slime': ('300° 品红 · 黏菌花期', '品红', '针头状子实体', '玫红'),
    'nursery': ('330° 玫红 · 育儿室', '玫红', '卵堆和透光的茧', '深红'),
}
FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2, F3 = ImageFont.truetype(FONT, 34), ImageFont.truetype(FONT, 26), ImageFont.truetype(FONT, 20)


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else '02_Concepts/Particle_Styles_v4'
    maps = sys.argv[2] if len(sys.argv) > 2 else '02_Concepts/Nest_Maps_v1'
    rows = []
    for play in sorted(glob.glob(os.path.join(folder, '*_play.png'))):
        base = os.path.basename(play)[:-len('_play.png')]
        seg, key = base.split('_', 1)
        eye = os.path.join(folder, base + '_eye.png')
        mp = os.path.join(maps, base + '_map.jpg')
        rows.append((int(seg), key, play, eye if os.path.exists(eye) else None, mp if os.path.exists(mp) else None))
    W, H, M, G = 800, 450, 450, 12
    for n, half in enumerate(([r for r in rows if r[0] < 180], [r for r in rows if r[0] >= 180])):
        if not half:
            continue
        width = G + 2 * (W + G) + M + G
        sheet = Image.new('RGB', (width, 84 + len(half) * (H + 74)), (16, 17, 19))
        d = ImageDraw.Draw(sheet)
        d.text((G, 20), '效果图（以 v15 为底改）：左 游戏视角，中 蚂蚁视角，右 地图', font=F1, fill=(236, 232, 222))
        y = 84
        for seg, key, play, eye, mp in half:
            name, light, landmark, exit_ = NESTS.get(key, (key, '', '', ''))
            d.text((G, y + 12), name, font=F2, fill=(232, 196, 120))
            d.text((G + 360, y + 17), f'光：{light}　路边物件：{landmark}　出口透出：{exit_}', font=F3, fill=(200, 196, 188))
            for i, f in enumerate((play, eye)):
                if f:
                    sheet.paste(Image.open(f).convert('RGB').resize((W, H), Image.LANCZOS), (G + i * (W + G), y + 60))
            if mp:
                sheet.paste(Image.open(mp).convert('RGB').resize((M, M), Image.LANCZOS), (G + 2 * (W + G), y + 60))
            y += H + 74
        out = os.path.join(folder, f'00_总览_{n + 1}.jpg')
        sheet.save(out, quality=86)
        print(out)


if __name__ == '__main__':
    main()
