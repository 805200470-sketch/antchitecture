#!/bin/zsh
# film_run.sh <out dir> [player args...] - walk the glowing trail in each ruin (frames per ruin, 30 fps);
# make the video with Tools/frames_to_video.sh. -lang en for the English UI.
O="$1"; shift; mkdir -p "$O/out"
APP="${0:A:h}/../../05_Builds.nosync/AntGarden_Mac/AntGarden.app"
perl -e 'alarm 900; exec @ARGV' open -n -W "$APP" --args -mute -quality high -trailfilm "$O/out" "$@" \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$O/player.log"
echo "exit $?"; for d in "$O"/out/*/; do echo "$d $(ls $d | wc -l | tr -d ' ') frames"; done
grep -E "Exception|NullReference|trail film" "$O/player.log" | head -6
