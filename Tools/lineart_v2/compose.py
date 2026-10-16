import sys, json
from PIL import Image, ImageDraw
def compose(png, out, width=2.6, color=(10,10,10), bg=None, ss=2, wscale=2.6):
    im = Image.open(png).convert("RGBA"); W, H = im.size
    segs = json.load(open(png + ".json")) if __import__("os").path.exists(png + ".json") else []
    lay = Image.new("RGBA", (W*ss, H*ss), (0,0,0,0)); d = ImageDraw.Draw(lay)
    r = width*ss/2
    import math
    for L in segs:
        pts = [(x*ss, y*ss) for x, y in L["pts"]]; w = L["w"]*ss*wscale; r = w/2; dash = L["dash"] and [L["dash"][0]*1.5, L["dash"][1]*1.5]
        if dash:
            on, off = dash[0]*ss, dash[1]*ss; acc = 0.0; drawing = True; seg = [pts[0]]
            for (x1,y1),(x2,y2) in zip(pts, pts[1:]):
                l = math.hypot(x2-x1, y2-y1); t0 = 0.0
                while l > 0 and t0 < l:
                    lim = (on if drawing else off) - acc; step = min(lim, l - t0)
                    xa, ya = x1 + (x2-x1)*(t0/l), y1 + (y2-y1)*(t0/l); xb, yb = x1 + (x2-x1)*((t0+step)/l), y1 + (y2-y1)*((t0+step)/l)
                    if drawing: d.line([(xa,ya),(xb,yb)], fill=color+(255,), width=int(round(w))); d.ellipse([xb-r,yb-r,xb+r,yb+r], fill=color+(255,)); d.ellipse([xa-r,ya-r,xa+r,ya+r], fill=color+(255,))
                    t0 += step; acc += step
                    if acc >= (on if drawing else off) - 1e-6: drawing = not drawing; acc = 0.0
        else:
            d.line(pts, fill=color+(255,), width=int(round(w)), joint="curve")
            for x, y in (pts[0], pts[-1]): d.ellipse([x-r, y-r, x+r, y+r], fill=color+(255,))
    lay = lay.resize((W, H), Image.LANCZOS)
    im.alpha_composite(lay)
    if bg: b = Image.new("RGBA", im.size, bg+(255,)); b.alpha_composite(im); im = b
    im.save(out)
if __name__ == "__main__":
    compose(sys.argv[1], sys.argv[2], bg=(245,243,238) if len(sys.argv) > 3 else None)
