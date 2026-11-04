"""
Render the sound audition of 2026-11-04 into Docs/报告/2026-11-04_音效试听/.

  python3 Tools/sound/audition.py            (about a minute; no Unity, no machine queue)

Four sets on one timeline (the same events at the same seconds in every file) plus each event on
its own, all at matched loudness, and a sketch of the music under a new bed of nest air.
"""
import json
import sys
import time
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from dsp import (SR, Rand, secs, place, peak, trim_tail, resample_pitch, lufs_max, lufs_integrated, write_wav,
                 fade_out)
from now import Now
from directions import Natural, Musical, WaterWood
from music import nest_bed, music_sketch

OUT = Path.home() / 'Desktop/AntGarden/Docs/报告/2026-11-04_音效试听'

# event, Chinese label, start (s) on the timeline, loudness target (max momentary LUFS)
EVENTS = [
    ('steps_soft', '脚步 · 软地', 0.0, -27.0),
    ('steps_dry', '脚步 · 干地', 3.0, -27.0),
    ('touch', '碰触角', 6.0, -21.0),
    ('flare', '植物亮起（两次）', 8.0, -21.0),
    ('map_open', '打开地图', 11.5, -23.0),
    ('map_close', '收起地图', 13.0, -24.0),
    ('gate', '穿过光门', 14.5, -18.0),
    ('guide', '引路光', 18.5, -21.0),
    ('reply', '回应（蚂蚁说话、物件回应）', 22.0, -21.0),
]
END = 25.5
WALK = 2.4          # seconds of walking per step sample


def walk(step_fn, dry, seed, jitter):
    """Walking at the game's pace: a step every 0.2 x (0.7..1.4) units at 0.75 units/s, now and then
    none, each at 0.55..1 loudness and (A, today) a pitch of 0.92..1.08, as Sfx.Step plays them."""
    r = Rand(seed)
    y = np.zeros(secs(WALK + 0.4))
    t = 0.05
    while t < WALK:
        if r.u() > 0.15:
            clip, loud, pitch = step_fn(dry, r)
            if jitter:
                clip = resample_pitch(clip, pitch)
            place(y, clip * loud, secs(t))
        t += 0.2 * (0.7 + 0.7 * r.u()) / 0.75
    return y


class NowSet:
    key = 'now'
    title = '现在'

    def __init__(self):
        self.n = Now()

    def events(self):
        n = self.n
        r = Rand(77)

        def step(dry, rr):
            clip, loud, pitch = n.step(dry)
            return clip, loud, pitch

        flare = np.zeros(secs(3.0))
        place(flare, resample_pitch(n.shimmer, 0.85 + r.u() * 0.4), 0)
        place(flare, resample_pitch(n.shimmer, 0.85 + r.u() * 0.4), secs(1.4))
        return {
            'steps_soft': walk(step, False, 11, True),
            'steps_dry': walk(step, True, 12, True),
            'touch': resample_pitch(n.murmur, 0.85 + r.u() * 0.35),
            'flare': flare,
            'map_open': n.paper_open.copy(),
            'map_close': resample_pitch(n.paper_close, 1.15),
            'gate': n.breath.copy(),
            'guide': None,                                   # no sound today
            'reply': resample_pitch(n.chime, 1.6),
        }


class NewSet:
    def __init__(self, d, jitter):
        self.d = d
        self.key = d.key
        self.title = d.title
        self.jitter = jitter
        self.cache = {}

    def events(self):
        d = self.d

        def step(dry, rr):
            v = rr.int(8)
            k = (dry, v)
            if k not in self.cache:
                self.cache[k] = d.step(dry, v)
            return self.cache[k], 0.55 + 0.45 * rr.u(), 0.92 + 0.16 * rr.u()

        flare = np.zeros(secs(4.0))
        place(flare, d.flare(0), 0)
        place(flare, d.flare(1), secs(1.4))
        return {
            'steps_soft': walk(step, False, 11, self.jitter),
            'steps_dry': walk(step, True, 12, self.jitter),
            'touch': d.touch(0),
            'flare': flare,
            'map_open': d.map_open(),
            'map_close': d.map_close(),
            'gate': d.gate(),
            'guide': d.guide(),
            'reply': d.reply(1.6),
        }


def matched(x, target):
    """Scale to the event's loudness target (max momentary loudness)."""
    return x * 10 ** ((target - lufs_max(x)) / 20)


def main():
    t0 = time.time()
    OUT.mkdir(parents=True, exist_ok=True)
    single = OUT / '单个'
    single.mkdir(exist_ok=True)
    sets = [NowSet(), NewSet(Natural(), True), NewSet(Musical(62), False), NewSet(WaterWood(62), False)]
    files = {'now': '1_现在.wav', 'A': '2_A_自然.wav', 'B': '3_B_音乐.wav', 'C': '4_C_水和木.wav'}
    tags = {'now': '现在', 'A': 'A自然', 'B': 'B音乐', 'C': 'C水和木'}
    report = {}
    for s in sets:
        ev = s.events()
        seq = np.zeros(secs(END + 4.0))
        report[s.key] = {}
        for i, (name, label, at, target) in enumerate(EVENTS):
            x = ev[name]
            if x is None:
                continue
            x = trim_tail(matched(x, target), -65, 40)
            place(seq, x, secs(at))
            write_wav(single / f'{i + 1}_{label.split("（")[0].replace(" · ", "")}_{tags[s.key]}.wav', x)
            report[s.key][name] = {'lufs_max': round(lufs_max(x), 1), 'peak_db': round(20 * np.log10(peak(x)), 1),
                                   'secs': round(len(x) / SR, 2)}
        if s.key == 'now':
            # today's beds of air after the effects: 4 s of open night, then 4 s of tunnel
            n = s.n
            bed = np.concatenate([n.air_open[:secs(4.0)], n.air_tunnel[secs(1.0):secs(5.0)]])
            k = secs(0.4)
            bed[:k] *= np.linspace(0, 1, k)
            bed[secs(4.0) - k:secs(4.0)] *= np.linspace(1, 0.3, k)
            bed[secs(4.0):secs(4.0) + k] *= np.linspace(0.3, 1, k)
            bed = fade_out(bed, 600)
            bed = bed * 10 ** ((-30.0 - lufs_integrated(bed)) / 20)
            seq = np.concatenate([seq[:secs(END + 0.5)], bed, np.zeros(secs(0.5))])
            write_wav(single / f'{len(EVENTS) + 1}_环境声_现在.wav', bed)
        else:
            seq = seq[:secs(END + 1.0)]
        seq = fade_out(seq, 50)
        write_wav(OUT / files[s.key], seq)
        report[s.key]['_file'] = {'secs': round(len(seq) / SR, 1), 'peak_db': round(20 * np.log10(peak(seq)), 1)}
        print(f'{s.key}: {len(seq) / SR:.1f}s, peak {20 * np.log10(peak(seq)):.1f} dBFS ({time.time() - t0:.0f}s)')
    # the music sketch over a new bed of nest air (the blue nest, D)
    bed = nest_bed(40.0, seed=5)
    mus = music_sketch(62, 40.0, seed=1)
    bed = bed * 10 ** ((-33.0 - lufs_integrated(bed)) / 20)
    mus = mus * 10 ** ((-27.0 - lufs_integrated(mus)) / 20)
    mix = bed + mus
    for c in range(2):
        mix[c] = fade_out(mix[c], 1500)
        k = secs(1.0)
        mix[c][:k] *= 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / k)
    write_wav(OUT / '5_背景音乐小样.wav', mix)
    report['music'] = {'mix_lufs_i': round(lufs_integrated(mix), 1), 'peak_db': round(20 * np.log10(peak(mix)), 1)}
    print(f'music: peak {20 * np.log10(peak(mix)):.1f} dBFS, {lufs_integrated(mix):.1f} LUFS ({time.time() - t0:.0f}s)')
    (Path(__file__).resolve().parent / 'audition_levels.json').write_text(json.dumps(report, ensure_ascii=False, indent=1))
    write_page(files, tags, {'now': '现在', 'A': 'A 自然', 'B': 'B 音乐', 'C': 'C 水和木'})


PAGE = """<!doctype html>
<html lang="zh">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>音效试听</title>
<style>
:root {{ --bg:#f7f6f2; --fg:#23231f; --muted:#86837b; --line:#dedad1; --on:#2f5f5a; --btn:#ffffff; }}
@media (prefers-color-scheme: dark) {{ :root {{ --bg:#151614; --fg:#e7e5df; --muted:#8c8981; --line:#2b2c29; --on:#8cc3bd; --btn:#1e1f1c; }} }}
* {{ box-sizing: border-box; }}
body {{ margin:0; background:var(--bg); color:var(--fg); font:15px/1.6 -apple-system, "PingFang SC", "Helvetica Neue", sans-serif; }}
main {{ max-width:880px; margin:0 auto; padding:36px 16px 72px; }}
h1 {{ font-size:22px; font-weight:600; margin:0 0 6px; }}
p {{ margin:0 0 10px; color:var(--muted); }}
h2 {{ font-size:13px; font-weight:500; color:var(--muted); letter-spacing:.04em; margin:34px 0 8px; }}
.row {{ display:flex; align-items:center; gap:12px; padding:11px 0; border-top:1px solid var(--line); flex-wrap:wrap; }}
.ev {{ width:170px; flex:none; }}
.ev small {{ display:block; color:var(--muted); font-size:12px; }}
.btns {{ display:flex; gap:8px; flex-wrap:wrap; flex:1; }}
button {{ font:inherit; font-size:14px; padding:6px 14px; border-radius:6px; border:1px solid var(--line); background:var(--btn); color:var(--fg); cursor:pointer; min-width:84px; }}
button.on {{ border-color:var(--on); color:var(--on); }}
.none {{ font-size:13px; color:var(--muted); padding:6px 4px; min-width:84px; }}
.tl {{ font-size:13px; padding:2px 0 10px; }}
</style>
</head>
<body>
<main>
<h1>音效试听</h1>
<p>四组声音，音量都对齐了，只比声音本身。点一下播放，再点一下停。</p>
<p>A 自然：没有音高，落叶、虫、风、水滴。B 音乐：毛毡钢琴、玻璃小铃、竖琴，在蚁穴自己的调上。C 水和木：竹子和木头，水滴进陶罐里像铃一样响。</p>
<h2>整段（每段里的先后都一样）</h2>
<div class="row"><div class="ev">整段</div><div class="btns">{full}</div></div>
<p class="tl">{timeline}</p>
<div class="row"><div class="ev">背景音乐<small>40 秒，压在新的环境声下面</small></div><div class="btns">{music}</div></div>
<h2>一个一个比</h2>
{rows}
</main>
<script>
const audio = new Audio();
let cur = null;
function stop() {{ if (cur) cur.classList.remove('on'); cur = null; }}
document.querySelectorAll('button[data-src]').forEach(b => b.addEventListener('click', () => {{
  if (cur === b && !audio.paused) {{ audio.pause(); stop(); return; }}
  stop();
  audio.src = encodeURI(b.dataset.src);
  audio.currentTime = 0;
  audio.play();
  b.classList.add('on');
  cur = b;
}}));
audio.addEventListener('ended', stop);
</script>
</body>
</html>
"""


def write_page(files, tags, names):
    """The listening page: per event, the same sound in each set side by side."""
    def btn(src, label):
        return f'<button data-src="{src}">{label}</button>'
    full = ''.join(btn(files[k], names[k]) for k in ('now', 'A', 'B', 'C'))
    music = btn('5_背景音乐小样.wav', '背景音乐')
    rows = []
    for i, (name, label, at, target) in enumerate(EVENTS):
        base = label.split('（')[0]
        sub = label[len(base):].strip('（）')
        cells = []
        for k in ('now', 'A', 'B', 'C'):
            f = OUT / '单个' / f'{i + 1}_{base.replace(" · ", "")}_{tags[k]}.wav'
            cells.append(btn(f'单个/{f.name}', names[k]) if f.exists() else '<span class="none">现在没有</span>')
        rows.append(f'<div class="row"><div class="ev">{base}<small>{sub or f"第 {at:g} 秒"}</small></div>'
                    f'<div class="btns">{"".join(cells)}</div></div>')
    rows.append('<div class="row"><div class="ev">环境声<small>现在的：野外的风和蟋蟀，然后是隧道</small></div>'
                f'<div class="btns">{btn("单个/" + str(len(EVENTS) + 1) + "_环境声_现在.wav", "现在")}</div></div>')
    def short(label):
        name = label.split('（')[0]
        return '{}（{}）'.format(*name.split(' · ')) if ' · ' in name else name
    timeline = ' · '.join(f'{at:g} 秒 {short(label)}' for (_, label, at, _) in EVENTS)
    (OUT / '试听.html').write_text(PAGE.format(timeline=timeline, full=full, music=music, rows='\n'.join(rows)),
                                   encoding='utf-8')


if __name__ == '__main__':
    main()
