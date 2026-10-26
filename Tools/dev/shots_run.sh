#!/bin/zsh
# shots_run.sh <out dir> [player args...] - review stills of every region with the current Mac build
# (e.g. -shotregions 4,5 for the two ruins, -particlelook for the particle look). The default world is used.
O="$1"; shift; mkdir -p "$O/out"
APP="${0:A:h}/../../05_Builds.nosync/AntGarden_Mac/AntGarden.app"
perl -e 'alarm 900; exec @ARGV' open -n -W "$APP" --args -mute -quality high -lang zh -sceneshots "$O/out" "$@" \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$O/player.log"
echo "exit $?: $(ls $O/out | wc -l | tr -d ' ') files"; grep -E "Exception|NullReference|\[AntGarden\] (plants|particle)" "$O/player.log" | head -6
