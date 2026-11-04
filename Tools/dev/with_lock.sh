#!/bin/zsh
# with_lock.sh '<commands>' - run commands holding the machine's build-and-capture lock, so that two sessions (one per
# nest, each its own conversation) never build Unity or run the game at once: a build replaces the app another session
# is capturing with, and two Unity editors cannot open the project together. Waiters are served first come, first
# served: each takes a ticket in .machine.queue named by its arrival time, and only the oldest live ticket may take the
# lock (11-03: without the queue, later jobs overtook one that had waited 25 minutes). A lock or ticket whose holder has
# died is taken over. The job waits while the disk has less than ANTGARDEN_MIN_FREE_GB (default 6) free: a build or
# capture that fills the disk leaves a half-written app or stills. unity_build.sh and shots_run.sh take the lock
# themselves when run on their own; inside with_lock.sh they do not take it again (ANTGARDEN_LOCK_HELD). Hold it over a
# whole build-and-capture sequence:
#   zsh Tools/dev/with_lock.sh 'zsh Tools/dev/unity_build.sh && zsh Tools/dev/shots_run.sh $R/x -world lab:path-007@kiln -particlelook'
# ANTGARDEN_PRIORITY=1 puts a job at the front of the queue, before every ticket without it; only when the hub conversation
# (the project's lead, Docs/排期.md) says so. Tools/dev/queue.sh shows who runs, who waits in what order, and the free disk.
L="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; mkdir -p "$L"
LOCK="$L/.machine.lock"; Q="$L/.machine.queue"; mkdir -p "$Q/.done"
if [[ -n "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh -c "$*"; fi
zmodload zsh/datetime
T="$Q/${ANTGARDEN_PRIORITY:+0}$(printf '%.6f' $EPOCHREALTIME).$$"
echo "${ANTGARDEN_WHO:-$PWD} $(date '+%H:%M:%S')" > "$T"
trap 'mv "$T" "$Q/.done/" 2>/dev/null' EXIT INT TERM
waited=0
while true; do
  # a ticket leaves the queue when its job ends, or when it is the one running (an older with_lock.sh keeps its ticket)
  h=$(cat "$LOCK/pid" 2>/dev/null)
  for t in "$Q"/*(N); do [[ "${t##*.}" != "$h" ]] && kill -0 "${t##*.}" 2>/dev/null || mv "$t" "$Q/.done/" 2>/dev/null; done
  tickets=("$Q"/*(N))
  [[ "${tickets[1]}" == "$T" ]] && mkdir "$LOCK" 2>/dev/null && break
  holder=$(cat "$LOCK/pid" 2>/dev/null)
  if [[ -n "$holder" ]] && ! kill -0 "$holder" 2>/dev/null; then
    echo "with_lock: the lock's holder ($holder) is gone, taking it over"
    mv "$LOCK" "$L/.machine.lock.stale.$$" 2>/dev/null
    continue
  fi
  (( waited % 60 == 0 )) && echo "with_lock: waiting for $(cat "$LOCK/who" 2>/dev/null) (pid $holder), place ${tickets[(ie)$T]} in the queue"
  sleep 5; (( waited += 5 ))
done
mv "$T" "$Q/.done/" 2>/dev/null
echo $$ > "$LOCK/pid"
echo "${ANTGARDEN_WHO:-$PWD} $(date '+%H:%M:%S')" > "$LOCK/who"
trap 'mv "$LOCK" "$L/.machine.lock.done.$$" 2>/dev/null' EXIT INT TERM
min=${ANTGARDEN_MIN_FREE_GB:-6}; waited=0
while (( $(df -g "$L" | awk 'NR==2{print $4}') < min )); do
  (( waited % 300 == 0 )) && echo "with_lock: less than $min GB free on the disk, not starting until space is freed"
  sleep 30; (( waited += 30 ))
done
ANTGARDEN_LOCK_HELD=1 zsh -c "$*"
