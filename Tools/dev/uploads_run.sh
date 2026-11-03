#!/bin/zsh
# uploads_run.sh <out dir> [player args...] - stills of the visitors' things in a particle nest, each then its card
# (Uploads.cs, -uploadshots), with the current Mac build, reading the exports from 08_Uploads.nosync (kept out of git;
# ANTGARDEN_UPLOADS for another folder). E.g. zsh Tools/dev/uploads_run.sh $R/up1 -world lab:path-012@blue
# (one at a time on this machine, across sessions: Tools/dev/with_lock.sh)
if [[ -z "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh "${0:A:h}/with_lock.sh" "zsh ${(q)${0:A}} ${(j: :)${(q)@}}"; fi
O="$1"; shift; mkdir -p "$O/out"
APP="${0:A:h}/../../05_Builds.nosync/AntGarden_Mac/AntGarden.app"
UP="${ANTGARDEN_UPLOADS:-${0:A:h:h:h}/08_Uploads.nosync}"
perl -e 'alarm 900; exec @ARGV' open -n -W "$APP" --args -mute -quality high -lang zh -uploads "$UP" -uploadshots "$O/out" "$@" \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$O/player.log"
echo "exit $?: $(ls $O/out | wc -l | tr -d ' ') files"; grep -E "Exception|NullReference|\[AntGarden\] uploads" "$O/player.log" | head -12
