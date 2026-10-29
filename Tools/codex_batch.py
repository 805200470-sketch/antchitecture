#!/usr/bin/env python3
"""codex_batch.py <jobs.json> [name ...] - run a list of Codex images through Tools/codex_image.sh, three at a time.

jobs.json is a list of {"out": "<path relative to the project folder>", "prompt": "<English prompt>",
"refs": [optional reference images, relative to the project folder]}.
Images that already exist are skipped (pass --force to redo them); names limit the run to those files,
e.g.  python3 Tools/codex_batch.py 02_Concepts/Prompts/PROMPTS_v6_ruins_jobs.json A_relics_scene
Afterwards, python3 Tools/archive_codex_images.py files the raw images and their prompts in Codex_Archive.
"""
import concurrent.futures
import json
import os
import subprocess
import sys

TOOLS = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(TOOLS)


def run(job):
    out = os.path.join(ROOT, job['out'])
    env = dict(os.environ)
    if job.get('refs'):   # reference images for style, relative to the project folder
        env['CODEX_REFS'] = ','.join(os.path.join(ROOT, r) for r in job['refs'])
    if job.get('edit'):   # the first reference is the image to edit (a game still), not a style reference
        env['CODEX_EDIT'] = '1'
    r = subprocess.run([os.path.join(TOOLS, 'codex_image.sh'), out, job['prompt']], capture_output=True, text=True, env=env)
    return r.stdout.strip() or r.stderr.strip()


def main():
    args = [a for a in sys.argv[1:] if a != '--force']
    if not args:
        print(__doc__)
        return 2
    jobs = json.load(open(args[0], encoding='utf-8'))
    only = set(args[1:])
    todo = [j for j in jobs
            if (not only or os.path.splitext(os.path.basename(j['out']))[0] in only)
            and ('--force' in sys.argv or not os.path.exists(os.path.join(ROOT, j['out'])))]
    print(f'codex_batch: {len(todo)} image(s) to make', flush=True)
    with concurrent.futures.ThreadPoolExecutor(int(os.environ.get('CODEX_JOBS', '3'))) as ex:   # (CODEX_JOBS=2 when the service limits the rate)
        for line in ex.map(run, todo):
            print(line, flush=True)
    return 0


if __name__ == '__main__':
    sys.exit(main())
