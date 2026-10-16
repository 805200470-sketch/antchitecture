#!/bin/zsh
# tripo_batch.sh name1 name2 ...
# Front images 02_Concepts/Fronts_for_3D/<name>.png -> 03_Models/Plants/<name>_p1.glb (about 2 minutes, about 60 Tripo
# credits each). Models that already exist are skipped, so a rerun never pays twice. Tripo refuses more than a few
# generations at once (HTTP 429, nothing is charged), so at most MAX run together and a refused one waits and retries.
# Then: Tools/plants_to_unity.sh, and AntGarden > Build Assets in Unity.
A="${0:A:h:h}"
MAX=${TRIPO_PARALLEL:-4}
logs="${TMPDIR:-/tmp}/tripo_logs"; mkdir -p "$logs"
if [ $# -eq 0 ]; then echo "usage: Tools/tripo_batch.sh name1 name2 ..." >&2; exit 2; fi
one() {  # one <name>: generate with retries while Tripo says "too many"
  local n=$1 img="$A/02_Concepts/Fronts_for_3D/$1.png" out="$A/03_Models/Plants/$1_p1.glb"
  for try in 1 2 3 4 5 6; do
    node "$A/Tools/tripo.mjs" image "$img" "$out" --faces 20000 --quality detailed > "$logs/$n.log" 2>&1 && { echo "$n: ok $(tail -1 "$logs/$n.log")"; return 0; }
    # once Tripo has accepted the task it is paid for: never start a new one, only wait for that one
    task=$(grep -o "^task [0-9a-f-]*" "$logs/$n.log" | head -1 | cut -d' ' -f2)
    if [ -n "$task" ]; then
      node "$A/Tools/tripo.mjs" resume "$task" "$out" >> "$logs/$n.log" 2>&1 && { echo "$n: ok (resumed $task)"; return 0; }
      break
    fi
    grep -q "POST /generation/image-to-model → HTTP 429" "$logs/$n.log" || break   # refused before any charge
    sleep $((40 * try))
  done
  echo "$n: FAILED $(tail -1 "$logs/$n.log")"
}
todo=()
for n in "$@"; do
  if [ ! -e "$A/02_Concepts/Fronts_for_3D/$n.png" ]; then echo "$n: no front image"; continue; fi
  if [ -e "$A/03_Models/Plants/${n}_p1.glb" ]; then echo "$n: already exists, skipped"; continue; fi
  todo+=("$n")
done
balance() { node "$A/Tools/tripo.mjs" balance 2>/dev/null | grep -o '"balance": [0-9]*' | grep -o '[0-9]*'; }
before=$(balance)
echo "balance before: ${before:-?} (this batch should use about $(( ${#todo} * 60 )))"
for ((i = 1; i <= ${#todo}; i += MAX)); do
  for n in "${todo[@]:$((i - 1)):$MAX}"; do one "$n" & done
  wait
done
after=$(balance)
echo "balance after: ${after:-?}  used: $(( ${before:-0} - ${after:-0} ))  (expected about $(( ${#todo} * 60 )))"
