#!/bin/zsh
# film_run.sh <out dir> [player args...] - one process-film run (ProcessFilms.cs: -turntablefilm / -nestpointsfilm) with the
# process-footage agent's own clone of the current build ($TMPDIR/antgarden_runs/process_footage/AntGarden.app), windowed,
# muted, 1920x1080 (the frames are drawn into a 1920x1080 texture whatever the window). Queued (Tools/dev/with_lock.sh).
#   zsh Tools/process_film/film_run.sh $R/tt_blue -world lab:path-012@blue -turntablefilm OUT -ttplants curl_a_crozier -ttant 1
# (OUT in the arguments is replaced by <out dir>/out)
if [[ -z "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh "${0:A:h}/../dev/with_lock.sh" "zsh ${(q)${0:A}} ${(j: :)${(q)@}}"; fi
O="$1"; shift; mkdir -p "$O/out"
APP="${ANTGARDEN_FILM_APP:-${TMPDIR:-/tmp}/antgarden_runs/process_footage/AntGarden.app}"
args=("${@//OUT/$O/out}")
# the language: English unless the arguments ask for another (the game takes the first -lang it finds; films for people
# have the English UI, the user's standing rule)
[[ " ${args[*]} " == *" -lang "* ]] || args=(-lang en "${args[@]}")
perl -e 'alarm 1800; exec @ARGV' open -n -W "$APP" --args -mute -quality high -tutorial 0 "${args[@]}" \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$O/player.log"
echo "exit $?"; ls "$O/out"; for d in "$O"/out/*/; do echo "$d $(ls $d | wc -l | tr -d ' ') frames"; done
grep -E "Exception|NullReference|\[AntGarden\] (turntable|nest points|not ready)" "$O/player.log" | head -20
