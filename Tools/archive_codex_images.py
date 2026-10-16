#!/usr/bin/env python3
"""archive_codex_images.py - copy every image Codex made for Ant Garden into the project folder.

  python3 Tools/archive_codex_images.py

Codex keeps each generated image in ~/.codex/generated_images/<thread>/exec-<id>.png and logs the prompt
in ~/.codex/sessions. This script COPIES (never moves or deletes) the Ant Garden ones into
02_Concepts/Codex_Archive/<batch>/, named <MMDD>_<HHMM>_<name>.png, and writes for each batch:
  00_总览.jpg    a numbered contact sheet
  prompts.md     the prompt of every image, where Codex saved it then, and the final pick in the project
Running it again only adds what is new. New Codex runs started inside ~/Desktop/AntGarden are picked up
automatically (one batch per day).
"""
import datetime
import glob
import hashlib
import json
import os
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

HOME = os.path.expanduser('~')
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GEN = os.path.join(HOME, '.codex', 'generated_images')
DST = os.path.join(ROOT, '02_Concepts', 'Codex_Archive')
EARLY = os.path.join(HOME, 'Documents', 'DFPI毕设', 'output', 'imagegen')   # curated by Codex itself, Sep 27-29

# thread id prefix -> batch folder (the 09-23 "131" logo thread in the same Codex project is not Ant Garden)
BATCHES = {
    '01a0e331': '2026-09-27_early_exploration',
    '01a0ea7f': '2026-09-27_early_exploration',
    '01a0e89d': '2026-09-29_ant_design',
    '01a0feb7': '2026-10-02_scene_assets_v1-v4',
}
SKIP = {'01a0ccaf'}
TITLES = {
    '2026-09-27_early_exploration': '早期探索：腐海场景、外星植物、植物形态设定（9 月 27–29 日，你在 Codex 里做的）',
    '2026-09-29_ant_design': '蚂蚁设定：五种蚂蚁的设定图和全部迭代稿（9 月 29 日–10 月 1 日）',
    '2026-10-02_scene_assets_v1-v4': '场景素材 v1–v4：环境素材、正面 + 侧面设定图、地面贴图、8 种新植物（10 月 2–4 日）',
    '2026-10-05_v5_world_detail': 'v5 世界细节：12 个新素材正面图和 6 张地面贴图（10 月 5 日）',
    '2026-10-11_codex': '10 月 11 日：遗迹 4 个方向（两版）、写实地面 v7（被否）、手绘地面 v8',
}


def md5(path):
    return hashlib.md5(open(path, 'rb').read()).hexdigest()


def session_index():
    """image id -> generation record; thread id -> working folder of that Codex session."""
    images, cwd = {}, {}
    sessions = os.path.join(HOME, '.codex', 'sessions')
    files = [f for f in glob.glob(os.path.join(sessions, '**', '*.jsonl'), recursive=True)
             if os.path.relpath(f, sessions)[:10] >= '2026/09/20']        # the project started late September
    files += glob.glob(os.path.join(HOME, '.codex', 'archived_sessions', '*.jsonl'))
    for f in files:
        tid = os.path.basename(f)[-42:-6]
        with open(f, errors='replace') as fh:
            for line in fh:
                if '"session_meta"' in line[:200]:
                    try:
                        cwd[tid] = json.loads(line)['payload'].get('cwd', '')
                    except (ValueError, KeyError):
                        pass
                if 'image_gen.generation' not in line:
                    continue
                try:
                    item = json.loads(line)['payload']['item']
                except (ValueError, KeyError, TypeError):
                    continue
                if item.get('kind') == 'image_gen.generation' and item.get('id'):
                    images[item['id']] = item
    return images, cwd


def project_hashes():
    """md5 -> path (relative to the project) of every image already in the project, outside this archive."""
    out = {}
    for top in ('01_References', '02_Concepts', '03_Models', 'Docs'):
        for p in glob.glob(os.path.join(ROOT, top, '**', '*'), recursive=True):
            if p.startswith(DST) or os.path.basename(p).startswith('._'):
                continue
            if p.lower().endswith(('.png', '.jpg', '.jpeg', '.webp')) and os.path.isfile(p):
                out.setdefault(md5(p), os.path.relpath(p, ROOT))
    return out


# folders whose finals may be processed versions (re-saved, cleaned up) of a batch's raw images
PROCESSED = {
    '2026-10-02_scene_assets_v1-v4': ('02_Concepts/Environment_v1', '02_Concepts/Sheets_v3',
                                      '02_Concepts/Fronts_for_3D', '02_Concepts/Textures'),
    '2026-10-05_v5_world_detail': ('02_Concepts/Fronts_for_3D', '02_Concepts/Textures'),
}


def thumb(path):
    im = Image.open(path).convert('L')
    return im.size[0] / im.size[1], im.resize((24, 24), Image.BILINEAR).tobytes()


def assign_finals(batches, have):
    """raw image -> (final in the project, exact copy?). Exact copies first; then each processed final goes
    to the single raw image closest to it, so look-alike drafts of the same asset stay unnamed."""
    out, taken = {}, set()
    for paths in batches.values():
        for p in paths:
            h = md5(p)
            if h in have:
                out[p] = (have[h], True)
                taken.add(have[h])
    pairs = []
    for batch, paths in batches.items():
        finals = []
        for folder in PROCESSED.get(batch, ()):
            for f in glob.glob(os.path.join(ROOT, folder, '*')):
                rel = os.path.relpath(f, ROOT)
                if rel not in taken and f.lower().endswith(('.png', '.jpg')) and not os.path.basename(f).startswith(('._', '00_')):
                    finals.append((rel, *thumb(f)))
        for p in paths:
            if p in out or not finals:
                continue
            aspect, px = thumb(p)
            for rel, a, q in finals:
                if abs(a - aspect) <= 0.02 * aspect:
                    d = sum(abs(x - y) for x, y in zip(px, q)) / len(px)
                    if d < 12:
                        pairs.append((d, p, rel))
    for d, p, rel in sorted(pairs):
        if p not in out and rel not in taken:
            out[p] = (rel, False)
            taken.add(rel)
    return out


def label(final):
    stem = os.path.splitext(os.path.basename(final))[0]
    parent = os.path.basename(os.path.dirname(final))
    return f'{parent}_{stem}' if parent.startswith('Ant_') else stem


def clean(stem):
    keep = ''.join(c if c.isalnum() or c in '-_' else '_' for c in stem)
    return keep.strip('_')[:60] or 'image'


def contact_sheet(entries, out, title):
    cell, cols = 260, 6
    rows = (len(entries) + cols - 1) // cols
    head = 56
    sheet = Image.new('RGB', (cols * cell, head + rows * (cell + 34)), (24, 26, 28))
    d = ImageDraw.Draw(sheet)
    try:
        big = ImageFont.truetype('/System/Library/Fonts/STHeiti Medium.ttc', 24)
        small = ImageFont.truetype('/System/Library/Fonts/STHeiti Light.ttc', 14)
    except OSError:
        big = small = ImageFont.load_default()
    d.text((12, 14), title, fill=(235, 235, 235), font=big)
    for i, (label, path) in enumerate(entries):
        im = Image.open(path)
        im.thumbnail((cell - 10, cell - 10))
        x, y = (i % cols) * cell, head + (i // cols) * (cell + 34)
        sheet.paste(im.convert('RGB'), (x + 5 + (cell - 10 - im.width) // 2, y + 5 + (cell - 10 - im.height) // 2))
        d.text((x + 6, y + cell), label[:34], fill=(220, 220, 220), font=small)
    sheet.save(out, quality=88)


def main():
    images, cwd = session_index()
    have = project_hashes()
    batches = {}
    for d in sorted(glob.glob(os.path.join(GEN, '*'))):
        tid = os.path.basename(d)
        if tid[:8] in SKIP:
            continue
        batch = BATCHES.get(tid[:8])
        if batch is None:
            where = cwd.get(tid, '')
            if not where.startswith(os.path.join(HOME, 'Desktop', 'AntGarden')):
                continue
            day = datetime.date.fromtimestamp(os.path.getmtime(d)).isoformat()
            batch = '2026-10-05_v5_world_detail' if day == '2026-10-05' else f'{day}_codex'
        for p in glob.glob(os.path.join(d, '**', '*.png'), recursive=True):
            batches.setdefault(batch, []).append(p)

    finals = assign_finals(batches, have)
    os.makedirs(DST, exist_ok=True)
    early_hashes = set()
    if os.path.isdir(EARLY):          # the curated early folders keep their own names and prompt notes
        target = os.path.join(DST, '2026-09-27_early_exploration')
        for name in sorted(os.listdir(EARLY)):
            src = os.path.join(EARLY, name)
            if os.path.isdir(src):
                shutil.copytree(src, os.path.join(target, name), dirs_exist_ok=True)
            else:
                os.makedirs(target, exist_ok=True)
                shutil.copy2(src, os.path.join(target, name))
        for p in glob.glob(os.path.join(EARLY, '**', '*.png'), recursive=True):
            early_hashes.add(md5(p))

    summary = []
    for batch, paths in sorted(batches.items()):
        folder = os.path.join(DST, batch)
        raw = os.path.join(folder, 'drafts') if batch == '2026-09-27_early_exploration' else folder
        os.makedirs(raw, exist_ok=True)
        paths.sort(key=os.path.getmtime)
        used, rows, sheet = set(), [], []
        known = {md5(os.path.join(raw, f)): f for f in os.listdir(raw) if f.endswith('.png')}
        for p in paths:
            h = md5(p)
            if batch == '2026-09-27_early_exploration' and h in early_hashes:
                continue                          # already in the curated folders above
            stem = os.path.splitext(os.path.basename(p))[0]
            rec = images.get(stem, {})
            saved = rec.get('savedPath') or ''
            final, exact = finals.get(p, ('', True))
            if final:
                name = clean(label(final))
            elif saved and '/generated_images/' not in saved:
                name = clean(os.path.splitext(os.path.basename(saved))[0])
            else:
                name = f'img{len(rows) + 1:03d}'
            t = datetime.datetime.fromtimestamp(os.path.getmtime(p))
            sub = '_'.join(os.path.relpath(p, GEN).split(os.sep)[1:-1])   # e.g. a qa_round3 subfolder
            base = f'{t:%m%d_%H%M}_{name}'
            if sub:
                base = f'{clean(sub)}_{base}'
            out_name, k = base + '.png', 2
            while out_name in used:
                out_name, k = f'{base}_{k}.png', k + 1
            used.add(out_name)
            out = os.path.join(raw, out_name)
            if known.get(h) not in (None, out_name) and not os.path.exists(out):
                os.rename(os.path.join(raw, known[h]), out)      # same picture copied earlier under an older name
            if not (os.path.exists(out) and md5(out) == h):
                shutil.copy2(p, out)
            rows.append((out_name, t, saved, (final, exact), rec.get('revisedPrompt') or ''))
            sheet.append((f'{len(rows):02d} {name}', out))

        if batch == '2026-09-27_early_exploration':
            curated = sorted(glob.glob(os.path.join(folder, '**', '*.png'), recursive=True))
            curated = [c for c in curated if '/drafts/' not in c]
            sheet = [(os.path.basename(c)[:-4], c) for c in curated] + [(f'草稿 {lbl}', pth) for lbl, pth in sheet]
        if sheet:
            contact_sheet(sheet, os.path.join(folder, '00_总览.jpg'), TITLES.get(batch, batch))
        with open(os.path.join(folder, 'prompts.md'), 'w', encoding='utf-8') as fh:
            fh.write(f'# {TITLES.get(batch, batch)}\n\n')
            if batch == '2026-09-27_early_exploration':
                fh.write('子文件夹是 Codex 当时自己整理好的成品（文件夹里的 .md 是那一组的提示词和说明）。'
                         '`drafts/` 是同期没有收进成品文件夹的其余原图，提示词在下面。\n\n')
            fh.write('每张图：生成时间、现在项目里对应的定稿（同一张图，或裁切、调色后的版本），以及提示词。'
                     '没有标定稿的是没被选用的稿子。\n\n')
            for i, (name, t, saved, final, prompt) in enumerate(rows, 1):
                fh.write(f'## {i:02d} · {name}\n\n- 生成时间：{t:%Y-%m-%d %H:%M}\n')
                if saved and '/generated_images/' not in saved:
                    fh.write(f'- Codex 当时保存到：`{saved}`\n')
                if final[0]:
                    fh.write(f'- 项目里的定稿：`{final[0]}`' + ('' if final[1] else '（处理过的版本）') + '\n')
                if prompt:
                    fh.write('\n' + '\n'.join('> ' + ln if ln else '>' for ln in prompt.strip().splitlines()) + '\n')
                fh.write('\n')
        summary.append((batch, len(rows)))
        print(f'{batch}: {len(rows)} raw images')
    return summary


if __name__ == '__main__':
    sys.exit(0 if main() is not None else 1)
