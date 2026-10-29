#!/usr/bin/env python3
"""nest_maps.py <out folder>

Map concepts for the twelve particle nests, seen from above: each nest a hexagonal module with a layout of its own
(after the casts of real ant nests: a shaft with chambers strung on it, parallel shafts, a ring, a fan...), its
openings on the hexagon's sides (ways in and ways out, more than one of each in some), the guiding light's route from
a way in to a way out (the main one bright, the others faint), the nest's own landmark beside that route (not at its
end), and side chambers kept for the stories and things visitors upload. One image per nest and an overview of all
twelve. Plain shapes, no rendering: this is the layout, the look is in the views.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
F1, F2, F3 = ImageFont.truetype(FONT, 44), ImageFont.truetype(FONT, 28), ImageFont.truetype(FONT, 24)
S = 1000            # the hexagon's half-height in pixels (a pointy-top hexagon, its points up and down)
W, H = 2400, 2400   # the image
CX, CY = 1000, 1240  # the hexagon's centre (the legend is on the right)


def hexagon():
    # its corners clockwise on the screen from the top one (side k runs from corner k to corner k+1)
    return [(CX + S * math.cos(math.radians(90 - 60 * k)) * 0.98, CY - S * math.sin(math.radians(90 - 60 * k)) * 0.98) for k in range(6)]


def P(x, y):
    """A point in the hexagon's own units (-1..1 across its height) to pixels."""
    return (CX + x * S, CY - y * S)


def spline(pts, n=24):
    """A smooth line through the points (Catmull-Rom)."""
    if len(pts) < 3:
        return pts
    out = []
    ext = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                    + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    out.append(pts[-1])
    return out


def side_point(side, f):
    """A point on side k of the hexagon (0 top-right .. 5 top-left, clockwise from the top point), f along it."""
    h = hexagon()
    a, b = h[side], h[(side + 1) % 6]
    return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)


def to_units(p):
    return ((p[0] - CX) / S, (CY - p[1]) / S)


# ---------------------------------------------------------------------------------------------- the twelve nests
# Each: tunnels (smooth lines of points in hexagon units, width), chambers (centre, radius), openings (side, along,
# 'in'/'out', where it leads), the main route (points, in order from a way in to a way out), faint routes, the
# landmark (point, label), story chambers (points).
def sideu(side, f):
    return to_units(side_point(side, f))


NESTS = {
    'kiln': dict(
        title='0° 红 · 古蚁圣殿 · 窑火', light=(232, 58, 40), ground=(96, 44, 34), wall=(52, 26, 22),
        layout='对称的中轴：入口进来是一条笔直的长廊，两侧是一间间小侧殿，尽头横着一条大厅，左右各一个出口',
        tunnels=[([sideu(3, 0.5), (0, -0.55), (0, -0.1), (0, 0.25)], 0.07), ([(-0.62, 0.42), (-0.3, 0.3), (0, 0.25), (0.3, 0.3), (0.62, 0.42)], 0.08),
                 ([(-0.62, 0.42), sideu(5, 0.55)], 0.06), ([(0.62, 0.42), sideu(0, 0.45)], 0.06)]
                + [([(0, y), (sx * 0.22, y)], 0.035) for y in (-0.55, -0.32, -0.1) for sx in (-1, 1)],
        chambers=[((0, 0.25), 0.17)] + [((sx * 0.26, y), 0.06) for y in (-0.55, -0.32, -0.1) for sx in (-1, 1)],
        openings=[(3, 0.5, 'in', '来自 330° 育儿室'), (5, 0.55, 'out', '去 30° 暖琥珀'), (0, 0.45, 'out', '去 300° 黏菌（备用）')],
        route=[sideu(3, 0.5), (0, -0.55), (0, -0.1), (0, 0.25), (-0.3, 0.3), (-0.62, 0.42), sideu(5, 0.55)],
        faint=[[(0, 0.25), (0.3, 0.3), (0.62, 0.42), sideu(0, 0.45)]],
        landmark=((0.0, 0.25), '陶塔：螺旋光带缠绕的高陶塔，立在大厅中央'),
        stories=[(sx * 0.26, y) for y in (-0.55, -0.32) for sx in (-1, 1)]),
    'amber': dict(
        title='30° 橙 · 暖琥珀蚁穴', light=(245, 150, 50), ground=(98, 58, 30), wall=(50, 30, 18),
        layout='一串珠子：一条弯弯的长隧道串着六个圆室，像一串琥珀珠；中途分出一条岔路通向第二个出口',
        tunnels=[([sideu(4, 0.4), (-0.55, -0.35), (-0.35, -0.15), (-0.4, 0.15), (-0.15, 0.35), (0.15, 0.25), (0.35, 0.45), sideu(0, 0.6)], 0.05),
                 ([(0.15, 0.25), (0.35, 0.0), (0.5, -0.25), sideu(2, 0.4)], 0.045)],
        chambers=[((-0.55, -0.35), 0.09), ((-0.35, -0.15), 0.08), ((-0.4, 0.15), 0.1), ((-0.15, 0.35), 0.13), ((0.15, 0.25), 0.08), ((0.35, 0.45), 0.09)],
        openings=[(4, 0.4, 'in', '来自 0° 窑火'), (0, 0.6, 'out', '去 60° 蜜罐'), (2, 0.4, 'out', '去 90° 切叶蚁（备用）')],
        route=[sideu(4, 0.4), (-0.55, -0.35), (-0.35, -0.15), (-0.4, 0.15), (-0.15, 0.35), (0.15, 0.25), (0.35, 0.45), sideu(0, 0.6)],
        faint=[[(0.15, 0.25), (0.35, 0.0), (0.5, -0.25), sideu(2, 0.4)]],
        landmark=((-0.15, 0.35), '琥珀碎晶簇：最大的一颗珠室里，里面封着小虫'),
        stories=[(-0.4, 0.15), (0.35, 0.45)]),
    'honey': dict(
        title='60° 黄 · 蜜罐粮仓', light=(245, 215, 60), ground=(120, 96, 52), wall=(58, 46, 26),
        layout='一根竖井挂满储藏室：入口在上，竖井一路往下，两侧挂着一个个小粮仓；底部分成两条，通向两个出口',
        tunnels=[([sideu(0, 0.15), (0.05, 0.6), (-0.05, 0.3), (0.05, 0.0), (-0.05, -0.3), (0, -0.5)], 0.05),
                 ([(0, -0.5), (-0.35, -0.6), sideu(3, 0.25)], 0.045), ([(0, -0.5), (0.4, -0.55), sideu(2, 0.7)], 0.045)]
                + [([(sx * 0.02, y), (sx * 0.3, y + 0.05)], 0.03) for y in (0.45, 0.2, -0.05, -0.3) for sx in (-1, 1)],
        chambers=[((sx * 0.33, y + 0.05), 0.07) for y in (0.45, 0.2, -0.05, -0.3) for sx in (-1, 1)] + [((0, -0.5), 0.1)],
        openings=[(0, 0.15, 'in', '来自 30° 暖琥珀'), (3, 0.25, 'out', '去 90° 切叶蚁'), (2, 0.7, 'out', '去 120° 苔藓（备用）')],
        route=[sideu(0, 0.15), (0.05, 0.6), (-0.05, 0.3), (0.05, 0.0), (-0.05, -0.3), (0, -0.5), (-0.35, -0.6), sideu(3, 0.25)],
        faint=[[(0, -0.5), (0.4, -0.55), sideu(2, 0.7)]],
        landmark=((0.33, 0.0), '蜜罐蚁吊灯：一间储藏室的顶上挂着一簇蜜罐蚁，蜜球发光'),
        stories=[(-0.33, 0.5), (-0.33, 0.25), (0.33, 0.5), (-0.33, -0.25), (0.33, -0.25)]),
    'leafcut': dict(
        title='90° 黄绿 · 切叶蚁菌圃', light=(185, 235, 70), ground=(70, 82, 34), wall=(34, 40, 18),
        layout='三间大菌圃连成一个三角：每间都是宽敞的房间，用短隧道相连；入口在左，两个出口在右上和右下',
        tunnels=[([sideu(4, 0.5), (-0.45, 0.0)], 0.05), ([(-0.45, 0.0), (0.05, 0.4)], 0.05), ([(-0.45, 0.0), (0.1, -0.35)], 0.05),
                 ([(0.05, 0.4), (0.1, -0.35)], 0.04), ([(0.05, 0.4), sideu(0, 0.7)], 0.05), ([(0.1, -0.35), sideu(2, 0.3)], 0.05)],
        chambers=[((-0.45, 0.0), 0.2), ((0.05, 0.4), 0.17), ((0.1, -0.35), 0.18)],
        openings=[(4, 0.5, 'in', '来自 60° 蜜罐'), (0, 0.7, 'out', '去 120° 苔藓'), (2, 0.3, 'out', '去 150° 铜绿（备用）')],
        route=[sideu(4, 0.5), (-0.45, 0.0), (0.05, 0.4), sideu(0, 0.7)],
        faint=[[(-0.45, 0.0), (0.1, -0.35), sideu(2, 0.3)]],
        landmark=((-0.45, 0.0), '海绵菌圃：第一间大房间里的灰白菌圃，表面成簇的小菌球发光'),
        stories=[(0.1, -0.45), (0.15, 0.5)]),
    'moss': dict(
        title='120° 绿 · 苔藓森林', light=(80, 230, 110), ground=(42, 70, 36), wall=(22, 38, 20),
        layout='林间绕路：小路在几块大苔石之间弯来弯去，绕成两个环；两个入口、一个出口，走哪边都能到',
        tunnels=[([sideu(3, 0.3), (-0.2, -0.55), (-0.45, -0.2), (-0.2, 0.1), (0.15, -0.15), (-0.2, -0.55)], 0.045),
                 ([sideu(4, 0.6), (-0.6, 0.15), (-0.45, -0.2)], 0.045),
                 ([(-0.2, 0.1), (-0.05, 0.4), (0.3, 0.35), (0.35, 0.05), (0.15, -0.15)], 0.045), ([(0.3, 0.35), sideu(0, 0.7)], 0.045)],
        chambers=[((-0.2, 0.1), 0.07), ((0.3, 0.35), 0.06)],
        openings=[(3, 0.3, 'in', '来自 90° 切叶蚁'), (4, 0.6, 'in', '来自 180° 蓝色（另一个入口）'), (0, 0.7, 'out', '去 150° 铜绿')],
        route=[sideu(3, 0.3), (-0.2, -0.55), (-0.45, -0.2), (-0.2, 0.1), (-0.05, 0.4), (0.3, 0.35), sideu(0, 0.7)],
        faint=[[sideu(4, 0.6), (-0.6, 0.15), (-0.45, -0.2)], [(-0.2, 0.1), (0.15, -0.15), (0.35, 0.05), (0.3, 0.35)]],
        landmark=((0.12, 0.2), '大露珠：环中间的苔垫上，一颗比蚂蚁还高的露珠'),
        stories=[(0.05, -0.3), (-0.6, -0.05)], boulders=[((-0.2, -0.25), 0.16), ((0.12, 0.13), 0.1)]),
    'verdigris': dict(
        title='150° 青绿 · 铜绿机城', light=(70, 220, 160), ground=(44, 70, 60), wall=(22, 36, 32),
        layout='机器的线路：走廊直来直去、拐直角，中间是一个圆环（像齿轮）；四个口，两进两出', straight=True,
        tunnels=[([sideu(4, 0.3), (-0.6, 0.29), (-0.6, 0.0), (-0.32, 0.0)], 0.045), ([sideu(3, 0.6), (-0.3, -0.62), (0.0, -0.62), (0.0, -0.32)], 0.045),
                 ([(0.32, 0.0), (0.62, 0.0), (0.62, 0.52), (0.42, 0.52), sideu(0, 0.8)], 0.045), ([(0.0, 0.32), (0.0, 0.58), (-0.3, 0.58)], 0.04),
                 ([(0.0, -0.62), (0.5, -0.62), sideu(2, 0.4)], 0.045)],
        chambers=[], ring=((0, 0), 0.32),
        openings=[(4, 0.3, 'in', '来自 120° 苔藓'), (3, 0.6, 'in', '来自 210° 天蓝（另一个入口）'), (0, 0.8, 'out', '去 180° 蓝色'), (2, 0.4, 'out', '去 240° 雨水潭（备用）')],
        route=[sideu(4, 0.3), (-0.6, 0.29), (-0.6, 0.0), (-0.32, 0.0), (-0.23, 0.23), (0.0, 0.32), (0.23, 0.23), (0.32, 0.0), (0.62, 0.0), (0.62, 0.52), (0.42, 0.52), sideu(0, 0.8)],
        faint=[[sideu(3, 0.6), (-0.3, -0.62), (0.0, -0.62), (0.5, -0.62), sideu(2, 0.4)], [(0.0, -0.62), (0.0, -0.32)]],
        landmark=((0, 0), '破怀表：圆环中间，半埋在地里，游丝发铜绿微光'),
        stories=[(-0.3, 0.58), (0.62, -0.3)]),
    'blue': dict(
        title='180° 青 · 蓝色真菌林（模板）', light=(80, 220, 235), ground=(30, 60, 66), wall=(16, 32, 36),
        layout='螺旋：沿用 v15 的路径（path-012），一条路盘进螺旋再绕出来；入口在下，出口在右',
        tunnels=[([sideu(3, 0.45), (0.0, -0.4), (0.1, -0.05), (0.35, 0.05), sideu(1, 0.3)], 0.05),
                 ([(0.1, -0.05), (0.05, 0.3), (-0.25, 0.5), (-0.45, 0.3), (-0.35, 0.1), (-0.15, 0.2), (-0.2, 0.32)], 0.045)],
        chambers=[((-0.2, 0.32), 0.06)],
        openings=[(3, 0.45, 'in', '来自 150° 铜绿'), (1, 0.3, 'out', '去 210° 天蓝')],
        route=[sideu(3, 0.45), (0.0, -0.4), (0.1, -0.05), (0.35, 0.05), sideu(1, 0.3)],
        faint=[[(0.1, -0.05), (0.05, 0.3), (-0.25, 0.5), (-0.45, 0.3), (-0.35, 0.1), (-0.15, 0.2)]],
        landmark=((0.05, 0.3), '玻璃孢子杆：螺旋入口处一圈，玻璃泡里亮着光'),
        stories=[(-0.2, 0.32)]),
    'azure': dict(
        title='210° 天蓝 · 巨人遗物', light=(90, 170, 245), ground=(64, 76, 92), wall=(30, 36, 46),
        layout='一个大洞厅：中间散着几件巨人的东西（纽扣、茶杯、钥匙），路在它们之间穿行；两个入口、一个出口',
        tunnels=[([sideu(4, 0.5), (-0.5, 0.05)], 0.05), ([sideu(3, 0.5), (-0.05, -0.5)], 0.05), ([(0.45, 0.2), sideu(0, 0.75)], 0.05)],
        chambers=[], hall=[(-0.55, 0.0), (-0.3, 0.4), (0.15, 0.45), (0.5, 0.2), (0.4, -0.25), (0.0, -0.5), (-0.4, -0.35)],
        islands=[((-0.15, 0.05), 0.11, '纽扣'), ((0.2, 0.2), 0.08, '茶杯'), ((0.1, -0.25), 0.07, '钥匙')],
        openings=[(4, 0.5, 'in', '来自 180° 蓝色'), (3, 0.5, 'in', '来自 150° 铜绿（另一个入口）'), (0, 0.75, 'out', '去 240° 雨水潭')],
        route=[sideu(4, 0.5), (-0.5, 0.05), (-0.35, 0.25), (0.0, 0.3), (0.35, 0.05), (0.45, 0.2), sideu(0, 0.75)],
        faint=[[sideu(3, 0.5), (-0.05, -0.5), (0.25, -0.2), (0.35, 0.05)]],
        landmark=((-0.15, 0.05), '巨人的四孔纽扣：斜靠在洞厅里，旁边斜插一根穿红线的针'),
        stories=[(-0.42, -0.25), (0.42, -0.15)]),
    'cistern': dict(
        title='240° 蓝 · 雨水潭', light=(70, 100, 245), ground=(30, 34, 60), wall=(14, 16, 32),
        layout='往下汇的水盆：三条路从高处往中间的水潭汇下来，绕潭半圈后从一条低矮的隧道出去',
        tunnels=[([sideu(5, 0.4), (-0.4, 0.4), (-0.22, 0.12)], 0.045), ([sideu(4, 0.7), (-0.5, -0.25), (-0.22, -0.12)], 0.045),
                 ([sideu(0, 0.5), (0.35, 0.45), (0.2, 0.18)], 0.045), ([(0.24, -0.1), (0.45, -0.35), sideu(2, 0.5)], 0.04)],
        chambers=[], pool=((0, 0), 0.24),
        openings=[(5, 0.4, 'in', '来自 210° 天蓝'), (4, 0.7, 'in', '来自 180° 蓝色（另一个入口）'), (0, 0.5, 'in', '来自 270° 紫（另一个入口）'), (2, 0.5, 'out', '去 270° 骸谷')],
        route=[sideu(5, 0.4), (-0.4, 0.4), (-0.22, 0.12), (-0.27, -0.02), (-0.1, -0.27), (0.24, -0.1), (0.45, -0.35), sideu(2, 0.5)],
        faint=[[sideu(4, 0.7), (-0.5, -0.25), (-0.22, -0.12)], [sideu(0, 0.5), (0.35, 0.45), (0.2, 0.18)]],
        landmark=((0.05, 0.1), '垂根和大水滴：一根根拱在潭上，根尖挂一颗大水滴'),
        stories=[(-0.55, 0.1), (0.5, 0.15)]),
    'carapace': dict(
        title='270° 紫 · 王虫骸谷', light=(150, 80, 235), ground=(46, 34, 56), wall=(24, 18, 30),
        layout='一条脊骨：长廊顺着巨虫的背脊走，两侧伸出一根根肋骨一样的短岔路；一头进，另一头和中段各有一个出口',
        tunnels=[([sideu(4, 0.75), (-0.5, -0.3), (-0.2, -0.1), (0.1, 0.1), (0.4, 0.35), sideu(0, 0.5)], 0.05),
                 ([(0.1, 0.1), (0.4, -0.15), sideu(1, 0.6)], 0.04)]
                + [([(x, y), (x + dx, y + dy)], 0.03) for (x, y) in ((-0.42, -0.25), (-0.2, -0.1), (0.0, 0.03), (0.25, 0.22)) for (dx, dy) in ((-0.13, 0.2), (0.15, -0.18))],
        chambers=[((x + dx, y + dy), 0.045) for (x, y) in ((-0.42, -0.25), (-0.2, -0.1), (0.0, 0.03), (0.25, 0.22)) for (dx, dy) in ((-0.13, 0.2), (0.15, -0.18))],
        openings=[(4, 0.75, 'in', '来自 240° 雨水潭'), (0, 0.5, 'out', '去 300° 黏菌'), (1, 0.6, 'out', '去 330° 育儿室（备用）')],
        route=[sideu(4, 0.75), (-0.5, -0.3), (-0.2, -0.1), (0.1, 0.1), (0.4, 0.35), sideu(0, 0.5)],
        faint=[[(0.1, 0.1), (0.4, -0.15), sideu(1, 0.6)]],
        landmark=((-0.05, 0.03), '巨甲虫的空壳：躺在脊骨中段，鞘翅半开，里面透出暗紫光'),
        stories=[(-0.55, -0.05), (0.15, -0.15), (0.4, 0.04)]),
    'slime': dict(
        title='300° 品红 · 黏菌花期', light=(235, 60, 190), ground=(54, 30, 46), wall=(28, 16, 24),
        layout='放射的扇网：一个小中室，黏菌的脉络从它往外分叉，像扇面一样伸向五个口；从哪个口进都能找到出去的路',
        tunnels=[([(0, -0.05), sideu(k, f)], 0.035 + 0.01 * (k % 2)) for k, f in ((0, 0.5), (1, 0.5), (2, 0.6), (3, 0.4), (5, 0.5))]
                + [([(0.0, -0.05), (0.25, 0.25), (0.45, 0.2)], 0.025), ([(0.0, -0.05), (-0.3, -0.3), (-0.45, -0.2)], 0.025)],
        chambers=[((0, -0.05), 0.13)],
        openings=[(3, 0.4, 'in', '来自 270° 骸谷'), (5, 0.5, 'in', '来自 240° 雨水潭（另一个入口）'), (0, 0.5, 'out', '去 330° 育儿室'),
                  (1, 0.5, 'out', '去 0° 窑火（备用）'), (2, 0.6, 'out', '去 210° 天蓝（备用）')],
        route=[sideu(3, 0.4), (0, -0.05), sideu(0, 0.5)],
        faint=[[sideu(5, 0.5), (0, -0.05)], [(0, -0.05), sideu(1, 0.5)], [(0, -0.05), sideu(2, 0.6)]],
        landmark=((0.07, 0.05), '最高的一丛子实体：中室里，从发光的脉网上长出来'),
        stories=[(0.45, 0.2), (-0.45, -0.2)]),
    'nursery': dict(
        title='330° 玫红 · 育儿室', light=(240, 110, 150), ground=(70, 46, 54), wall=(36, 24, 28),
        layout='两层环：外环一圈排着育儿小室，内环围着蚁后的房间，几条放射的通道把两层连起来；入口在下，出口在上',
        tunnels=[([sideu(3, 0.5), (0.0, -0.58)], 0.045), ([(0.0, 0.58), sideu(0, 0.15)], 0.045)]
                + [([(0.58 * math.cos(a), 0.58 * math.sin(a)), (0.3 * math.cos(a), 0.3 * math.sin(a))], 0.035) for a in (0.6, 2.5, 4.0, 5.4)],
        chambers=[((0.72 * math.cos(a), 0.72 * math.sin(a)), 0.05) for a in [k * 0.52 + 0.26 for k in range(12)] if abs(math.sin(a)) < 0.93]
                 + [((0, 0), 0.16)],
        rings=[((0, 0), 0.58), ((0, 0), 0.3)],
        openings=[(3, 0.5, 'in', '来自 300° 黏菌'), (0, 0.15, 'out', '去 0° 窑火')],
        route=[sideu(3, 0.5), (0.0, -0.58), (0.4, -0.42), (0.58, 0.0), (0.4, 0.42), (0.0, 0.58), sideu(0, 0.15)],
        faint=[[(0.0, -0.58), (-0.4, -0.42), (-0.58, 0.0), (-0.4, 0.42), (0.0, 0.58)]],
        landmark=((0, 0), '蚁后：内环的房间里，躺在一床卵上，腹部透出玫瑰色的光'),
        stories=[(0.72 * math.cos(a), 0.72 * math.sin(a)) for a in (0.26, 2.86, 3.9)]),
}


def draw(key, n, out):
    img = Image.new('RGB', (W, H), (11, 13, 15))
    glow = Image.new('RGB', (W, H), (0, 0, 0))
    d, g = ImageDraw.Draw(img), ImageDraw.Draw(glow)
    hx = hexagon()
    d.polygon(hx, fill=n['wall'])
    d.line(hx + [hx[0]], fill=(90, 92, 96), width=4)
    ground = n['ground']

    straight = n.get('straight', False)   # (a machine's corridors: straight runs and corners, not curves)
    curve = (lambda pts: pts) if straight else spline

    def tube(pts, w, col):
        line = curve([P(*p) if abs(p[0]) <= 1.2 and abs(p[1]) <= 1.2 else p for p in pts])
        d.line(line, fill=col, width=int(w * S), joint='curve')
        r = w * S / 2
        for q in (line[0], line[-1]):
            d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=col)

    if 'hall' in n:
        d.polygon(spline([P(*p) for p in n['hall'] + n['hall'][:2]], 12), fill=ground)
    for pts, w in n['tunnels']:
        tube(pts, w, ground)
    for (c, r) in n.get('rings', []) + ([n['ring']] if 'ring' in n else []):
        cx, cy = P(*c)
        d.ellipse([cx - r * S, cy - r * S, cx + r * S, cy + r * S], outline=ground, width=int(0.05 * S))
    for (c, r) in n['chambers']:
        cx, cy = P(*c)
        d.ellipse([cx - r * S, cy - r * S, cx + r * S, cy + r * S], fill=tuple(min(255, int(v * 1.15)) for v in ground))
    if 'pool' in n:
        (c, r) = n['pool']
        cx, cy = P(*c)
        d.ellipse([cx - (r + 0.06) * S, cy - (r + 0.06) * S, cx + (r + 0.06) * S, cy + (r + 0.06) * S], fill=ground)
        d.ellipse([cx - r * S, cy - r * S, cx + r * S, cy + r * S], fill=(8, 10, 24), outline=(60, 80, 160), width=3)
    for (c, r) in n.get('boulders', []):
        cx, cy = P(*c)
        d.ellipse([cx - r * S, cy - r * S, cx + r * S, cy + r * S], fill=n['wall'], outline=(70, 90, 60), width=3)
    for (c, r, name) in n.get('islands', []):
        cx, cy = P(*c)
        d.ellipse([cx - r * S, cy - r * S, cx + r * S, cy + r * S], fill=(150, 160, 175))
        d.text((cx - 30, cy - 16), name, font=F3, fill=(20, 24, 30))
    # the guiding light: the main route bright, the others faint (dotted)
    lt = n['light']
    main = curve([P(*p) for p in n['route']])
    g.line(main, fill=lt, width=30, joint='curve')
    glow = glow.filter(ImageFilter.GaussianBlur(18))
    img.paste(Image.new('RGB', (W, H), lt), mask=glow.convert('L').point(lambda v: int(v * 0.55)))
    d = ImageDraw.Draw(img)
    d.line(main, fill=tuple(min(255, v + 60) for v in lt), width=6, joint='curve')
    for f in n['faint']:
        pts = curve([P(*p) for p in f])
        if straight:   # (dashes need points along the straight runs)
            pts = [(a[0] + (b[0] - a[0]) * t / 20, a[1] + (b[1] - a[1]) * t / 20) for a, b in zip(pts, pts[1:]) for t in range(20)]
        for i in range(0, len(pts) - 1, 4):
            d.line([pts[i], pts[min(i + 2, len(pts) - 1)]], fill=tuple(int(v * 0.7) for v in lt), width=4)
    # the landmark (a star), the story chambers (small squares)
    (lp, label) = n['landmark']
    lx, ly = P(*lp)
    star = [(lx + (34 if k % 2 == 0 else 14) * math.cos(math.radians(90 + 36 * k)), ly - (34 if k % 2 == 0 else 14) * math.sin(math.radians(90 + 36 * k))) for k in range(10)]
    d.polygon(star, fill=(255, 236, 170), outline=(40, 30, 10))
    for sp in n['stories']:
        sx, sy = P(*sp)
        d.rectangle([sx - 14, sy - 14, sx + 14, sy + 14], outline=(235, 235, 235), width=4)
    # the openings: arrows on the hexagon's sides
    for (side, f, kind, where) in n['openings']:
        q = side_point(side, f)
        vx, vy = q[0] - CX, q[1] - CY
        L = math.hypot(vx, vy)
        ux, uy = vx / L, vy / L
        col = (120, 230, 140) if kind == 'in' else (255, 170, 80)
        tip = (q[0] + ux * (60 if kind == 'out' else -10), q[1] + uy * (60 if kind == 'out' else -10))
        base = (q[0] + ux * (-10 if kind == 'out' else 60), q[1] + uy * (-10 if kind == 'out' else 60))
        px_, py_ = -uy, ux
        d.polygon([tip, (base[0] + px_ * 26, base[1] + py_ * 26), (base[0] - px_ * 26, base[1] - py_ * 26)], fill=col)
        tx, ty = q[0] + ux * 90, q[1] + uy * 90
        txt = ('入口 · ' if kind == 'in' else '出口 · ') + where
        bw = d.textlength(txt, font=F3)
        tx = min(max(tx - bw / 2, 20), W - bw - 20)
        d.text((tx, ty - 14), txt, font=F3, fill=col)
    # title, layout, legend
    d.text((60, 50), n['title'], font=F1, fill=(236, 232, 222))
    lines, cur = [], ''
    for ch in n['layout']:
        cur += ch
        if d.textlength(cur, font=F2) > 2200:
            lines.append(cur)
            cur = ''
    lines.append(cur)
    for i, ln in enumerate(lines):
        d.text((60, 120 + i * 40), ln, font=F2, fill=(170, 172, 176))
    lx0, ly0 = 1960, 760
    items = [('line', lt, '引导光：从入口到出口'), ('dash', lt, '另一条可走的路（光弱）'), ('star', None, '这个蚁穴的特色物件（路旁，不是终点）'),
             ('box', None, '访客上传的故事 / 物件位'), ('in', (120, 230, 140), '入口'), ('out', (255, 170, 80), '出口')]
    for i, (kind, col, text) in enumerate(items):
        y = ly0 + i * 70
        if kind == 'line':
            d.line([(lx0, y), (lx0 + 60, y)], fill=col, width=8)
        elif kind == 'dash':
            for x in range(lx0, lx0 + 60, 16):
                d.line([(x, y), (x + 8, y)], fill=col, width=4)
        elif kind == 'star':
            d.polygon([(lx0 + 30 + (18 if k % 2 == 0 else 8) * math.cos(math.radians(90 + 36 * k)), y - (18 if k % 2 == 0 else 8) * math.sin(math.radians(90 + 36 * k))) for k in range(10)], fill=(255, 236, 170))
        elif kind == 'box':
            d.rectangle([lx0 + 20, y - 12, lx0 + 44, y + 12], outline=(235, 235, 235), width=4)
        else:
            d.polygon([(lx0 + 50, y), (lx0 + 15, y - 18), (lx0 + 15, y + 18)], fill=col)
        d.multiline_text((lx0 + 80, y - 16), '\n'.join([text[i:i + 9] for i in range(0, len(text), 9)]), font=F3, fill=(210, 210, 212), spacing=6)
    d.text((60, H - 80), '特色物件：' + label, font=F2, fill=(255, 236, 170))
    img.save(out, quality=88)


def main():
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    order = ['kiln', 'amber', 'honey', 'leafcut', 'moss', 'verdigris', 'blue', 'azure', 'cistern', 'carapace', 'slime', 'nursery']
    segs = {'kiln': '000', 'amber': '030', 'honey': '060', 'leafcut': '090', 'moss': '120', 'verdigris': '150', 'blue': '180',
            'azure': '210', 'cistern': '240', 'carapace': '270', 'slime': '300', 'nursery': '330'}
    paths = []
    for k in order:
        p = os.path.join(out, f'{segs[k]}_{k}_map.jpg')
        draw(k, NESTS[k], p)
        paths.append(p)
        print(p)
    # the overview: four by three
    tw, th = 800, 800
    sheet = Image.new('RGB', (4 * tw, 3 * th), (11, 13, 15))
    for i, p in enumerate(paths):
        im = Image.open(p).resize((tw, th), Image.LANCZOS)
        sheet.paste(im, ((i % 4) * tw, (i // 4) * th))
    sheet.save(os.path.join(out, '00_地图总览.jpg'), quality=86)


if __name__ == '__main__':
    main()
