#!/bin/zsh
# plants_to_unity.sh <glb...> [options]
# Tripo plant GLBs -> Unity FBX (LOD0/1/2) + BaseColor/Normal/ORM PNGs + plants.json + preview renders.
# Example: Tools/plants_to_unity.sh 03_Models/Plants/*_p1.glb
# Options: --out DIR  --preview-dir DIR  --facing +Z|-Z  --front auto|+X|-X|+Z|-Z  --tex 2048
#          --ratios 0.30,0.08  --jobs N  --no-verify  --name NAME   (see plants_to_unity.py)
# Needs Blender 5.x (env BLENDER, default /Applications/Blender.app) and python3 with numpy + Pillow
# (env PLANTS_PYTHON, default python3 on PATH). Never touches the GLBs; never starts Unity.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
py="${PLANTS_PYTHON:-$(command -v python3)}"
if ! "$py" -c 'import numpy, PIL' 2>/dev/null; then
  echo "plants_to_unity: $py lacks numpy/Pillow - set PLANTS_PYTHON to a python3 that has both" >&2
  exit 1
fi
exec "$py" "$here/plants_to_unity.py" run "$@"
