#!/bin/zsh
# uploads_film.sh <out dir> [player args...] - a short film of the visitors' things in a particle nest (Uploads.cs,
# -uploadfilm): for each, the ant comes along the way, reaches out its antennae, its ring lights and its card opens.
# Frames at 30 a second in <out dir>/out, then <out dir>/film.mp4. Reads the exports from 08_Uploads.nosync (kept out
# of git; ANTGARDEN_UPLOADS for another folder). E.g. zsh Tools/dev/uploads_film.sh $R/upfilm -world lab:path-012@blue
# -lang en for the English UI (the player args go first, and the game reads the first of each); ANTGARDEN_SCREEN=1920x1080
# for another size; ANTGARDEN_APP for a copy of the app. (one at a time on this machine, across sessions: with_lock.sh)
if [[ -z "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh "${0:A:h}/with_lock.sh" "zsh ${(q)${0:A}} ${(j: :)${(q)@}}"; fi
O="$1"; shift; mkdir -p "$O/out"
APP="${ANTGARDEN_APP:-${0:A:h}/../../05_Builds.nosync/AntGarden_Mac/AntGarden.app}"
UP="${ANTGARDEN_UPLOADS:-${0:A:h:h:h}/08_Uploads.nosync}"
S="${ANTGARDEN_SCREEN:-1600x900}"
perl -e 'alarm 1200; exec @ARGV' open -n -W "$APP" --args "$@" -mute -quality high -lang zh -uploads "$UP" -uploadfilm "$O/out" \
  -screen-width ${S%x*} -screen-height ${S#*x} -screen-fullscreen 0 -logFile "$O/player.log"
echo "exit $?: $(ls $O/out | wc -l | tr -d ' ') frames"; grep -E "Exception|NullReference|\[AntGarden\] upload" "$O/player.log" | head -8
[[ -f "$O/out/done.txt" ]] && zsh "${0:A:h}/../frames_to_video.sh" "$O/out" "$O/film.mp4" 30 MEDIUM && ls -la "$O/film.mp4"
