#!/bin/zsh
# story_film.sh <out dir> [player args...] - the presentation video's story shots (11-04; film only): StoryFilm.cs
# (-storyplan map,aerial,crane,walk,depart,arrive,goto:<world>; the ending +ui keeps the screen's words) or, with -objectfilm
# <dir> and -uploads <exports>, the visitors' things gathering and their cards typed out (UploadsFilm.cs). Frames at
# 1920x1080, 30 a second, rendered off the screen whatever the window, in <out dir>/out/<shot>_<nest>/f00000.jpg (stills
# and plan json beside them); Tools/frames_to_video.sh makes the MP4s. English, muted, windowed. ANTGARDEN_APP: the app to
# run (a clone, so another session's build cannot replace it mid-run), else the build in 05_Builds. E.g.
#   ANTGARDEN_WHO="story shots: blue" zsh Tools/dev/story_film.sh $R/blue -world lab:path-012@blue -storyplan map,aerial,crane,walk
# (one at a time on this machine, across sessions: Tools/dev/with_lock.sh)
if [[ -z "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh "${0:A:h}/with_lock.sh" "zsh ${(q)${0:A}} ${(j: :)${(q)@}}"; fi
O="$1"; shift; mkdir -p "$O/out"
APP="${ANTGARDEN_APP:-${0:A:h}/../../05_Builds.nosync/AntGarden_Mac/AntGarden.app}"
[[ " $* " == *" -objectfilm "* ]] || set -- -storyfilm "$O/out" "$@"
perl -e 'alarm 1800; exec @ARGV' open -n -W "$APP" --args "$@" -mute -quality high -lang en \
  -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile "$O/player.log"
echo "exit $?"; for d in "$O"/out/*(N/) "$O"/out/*/*(N/); do n=$(ls "$d" | grep -c '^f[0-9]*\.jpg$'); (( n > 0 )) && echo "$d $n frames"; done
grep -E "Exception|NullReference|story film|objectfilm|not ready" "$O/player.log" | head -30
