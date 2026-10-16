#!/bin/zsh
# frames_to_video.sh <frames dir> <out.mp4> [fps] [HIGH|MEDIUM|LOW]  -  numbered frames -> H.264 MP4 (uses Blender, no ffmpeg needed)
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
blender="${BLENDER:-/Applications/Blender.app/Contents/MacOS/Blender}"
"$blender" -b --factory-startup -P "$here/frames_to_video.py" -- "$1" "$2" "${3:-30}" "${4:-MEDIUM}" 2>&1 | grep -E "frames_to_video|Error|error" || true
# index to the front, so browsers, phones and previews can start playing at once
python3 "$here/mp4_faststart.py" "$2"
