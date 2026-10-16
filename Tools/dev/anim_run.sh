#!/bin/zsh
# anim_run.sh [skipbuild] - build the Mac player and record the animation check; makes filmstrips
S="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; mkdir -p "$S"
U=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity
P="$HOME/Desktop/AntGarden/UnityProject.nosync"
if [ "$1" != "skipbuild" ]; then
  for attempt in 1 2; do
    "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.BuildScript.BuildMac -logFile "$S/unity_build_last.log"
    grep -q "Build StandaloneOSX: Succeeded" "$S/unity_build_last.log" && break
    echo "build attempt $attempt failed"; grep "error CS" "$S/unity_build_last.log" | head -3
  done
  grep -E "error CS|\[AntGarden\] Build" "$S/unity_build_last.log" | head -3
fi
R="$S/anim_$(date +%H%M%S)"; mkdir -p "$R/frames"; echo "$R" > "$S/last_anim_dir.txt"
ditto "$HOME/Desktop/AntGarden/05_Builds.nosync/AntGarden_Mac/AntGarden.app" "$R/AntGarden.app"
perl -e 'alarm 600; exec @ARGV' open -n -W "$R/AntGarden.app" --args -world "stitch:cluster:path-001,path-002,path-003@blue|path-010@amber" -quality high -lang en -animcheck "$R/frames" -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$R/player.log"
echo "exit $? frames $(ls $R/frames | wc -l)"
grep -E "Exception|NullReference" "$R/player.log" | head -5
python3 - "$R" "$S" <<'PY'
import sys
from PIL import Image, ImageDraw
R, S = sys.argv[1], sys.argv[2]
def strip(prefix, start, count, step, crop, out):
    tiles = []
    for k in range(count):
        tiles.append((start + k * step, Image.open(f'{R}/frames/{prefix}_{start + k * step:04d}.jpg').crop(crop)))
    w, h = tiles[0][1].size
    sheet = Image.new('RGB', (w * 4, h * ((len(tiles) + 3) // 4)))
    d = ImageDraw.Draw(sheet)
    for k, (fr, t) in enumerate(tiles):
        x, y = (k % 4) * w, (k // 4) * h
        sheet.paste(t, (x, y)); d.text((x + 6, y + 4), f'{prefix} f{fr}', fill=(255, 255, 0))
    sheet.save(out, quality=88)
strip('a_walk_side', 30, 12, 2, (340, 200, 940, 560), S + '/anim_walk_side.jpg')
strip('b_walk_front', 30, 8, 2, (290, 160, 990, 600), S + '/anim_walk_front.jpg')
strip('c_run_side', 20, 8, 2, (340, 200, 940, 560), S + '/anim_run_side.jpg')
strip('e_circle', 30, 8, 3, (290, 160, 990, 600), S + '/anim_circle.jpg')
strip('f_top', 20, 8, 2, (340, 160, 940, 560), S + '/anim_top.jpg')
strip('g_front', 20, 8, 2, (340, 200, 940, 560), S + '/anim_front.jpg')
PY
