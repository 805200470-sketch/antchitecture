#!/bin/zsh
# with_lock.sh '<commands>' - run commands holding the machine's build-and-capture lock, so that two sessions (one per
# nest, each its own conversation) never build Unity or run the game at once: a build replaces the app another session
# is capturing with, and two Unity editors cannot open the project together. Waits for the lock; a lock whose holder has
# died is taken over. unity_build.sh and shots_run.sh take it themselves when run on their own; inside with_lock.sh they
# do not take it again (ANTGARDEN_LOCK_HELD). Hold it over a whole build-and-capture sequence:
#   zsh Tools/dev/with_lock.sh 'zsh Tools/dev/unity_build.sh && zsh Tools/dev/shots_run.sh $R/x -world lab:path-007@kiln -particlelook'
L="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; mkdir -p "$L"
LOCK="$L/.machine.lock"
if [[ -n "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh -c "$*"; fi
waited=0
until mkdir "$LOCK" 2>/dev/null; do
  holder=$(cat "$LOCK/pid" 2>/dev/null)
  if [[ -n "$holder" ]] && ! kill -0 "$holder" 2>/dev/null; then
    echo "with_lock: the lock's holder ($holder) is gone, taking it over"
    mv "$LOCK" "$L/.machine.lock.stale.$$" 2>/dev/null
    continue
  fi
  (( waited % 60 == 0 )) && echo "with_lock: waiting for $(cat "$LOCK/who" 2>/dev/null) (pid $holder)"
  sleep 5; (( waited += 5 ))
done
echo $$ > "$LOCK/pid"
echo "${ANTGARDEN_WHO:-$PWD} $(date '+%H:%M:%S')" > "$LOCK/who"
trap 'mv "$LOCK" "$L/.machine.lock.done.$$" 2>/dev/null' EXIT INT TERM
ANTGARDEN_LOCK_HELD=1 zsh -c "$*"
