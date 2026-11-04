#!/bin/zsh
# queue.sh - who holds the machine's build-and-capture lock (Tools/dev/with_lock.sh), who waits for it in what order,
# and how much disk and swap is left. Observations only: the order itself is with_lock.sh's.
L="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; LOCK="$L/.machine.lock"; Q="$L/.machine.queue"
holder=$(cat "$LOCK/pid" 2>/dev/null)
if [[ -z "$holder" ]]; then echo "running:   nothing"
elif kill -0 "$holder" 2>/dev/null; then echo "running:   $(cat "$LOCK/who" 2>/dev/null) (pid $holder)"
else echo "running:   nothing (the lock's holder $holder is gone; the next waiter takes it over)"; fi
i=0; ticketed=()
for t in "$Q"/*(N); do
  [[ "${t##*.}" != "$holder" ]] && kill -0 "${t##*.}" 2>/dev/null || continue
  (( i++ )); ticketed+=("${t##*.}"); echo "waiting $i: $(cat "$t")"
done
# waiters still running the old with_lock.sh (no ticket): served in no particular order
ps -axo pid=,etime=,args= | while read -r p e a; do
  [[ "$a" =~ '^zsh [^ ]*with_lock\.sh' ]] || continue
  [[ "$p" == "$holder" || ${ticketed[(ie)$p]} -le ${#ticketed} ]] && continue
  echo "waiting (old script, no place): pid $p, $e so far"
done
(( i == 0 )) && echo "waiting:   nobody in the queue"
echo "disk free: $(df -g "$L" | awk 'NR==2{print $4}') GB; swap used: $(sysctl -n vm.swapusage | awk '{print $6}') of $(sysctl -n vm.swapusage | awk '{print $3}')"
