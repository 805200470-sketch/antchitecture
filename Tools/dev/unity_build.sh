#!/bin/zsh
# unity_build.sh [assets] [ants] [ground] [plants:a,b,c] [windows] - optionally rebuild the asset manifest (new plants,
# materials; it samples every plant's points from its mesh again, undoing splat swaps), or only the ants' points, or only
# the ground sets (Art/Ground), or only the named plants (their points made; then run Tools/plants/growth_map.py for
# them), then build the Mac player (and the Windows one). Logs go to $ANTGARDEN_RUNS (default:
# $TMPDIR/antgarden_runs).
# (one at a time on this machine, across sessions: Tools/dev/with_lock.sh)
if [[ -z "$ANTGARDEN_LOCK_HELD" ]]; then exec zsh "${0:A:h}/with_lock.sh" "zsh ${(q)${0:A}} ${(j: :)${(q)@}}"; fi
L="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; mkdir -p "$L"
U=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity
P="${0:A:h}/../../UnityProject.nosync"
if [[ " $* " == *" assets "* ]]; then
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.AssetBuilder.Run -logFile "$L/unity_assets.log"
  echo "assets exit $?"; grep -E "error CS|Shader error|\[AntGarden\]|Exception" "$L/unity_assets.log" | grep -v "TestRunner" | tail -12
fi
if [[ " $* " == *" ants "* ]]; then
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.AssetBuilder.AntPointsOnly -logFile "$L/unity_ants.log"
  echo "ants exit $?"; grep -E "error CS|\[AntGarden\] ant points|Exception" "$L/unity_ants.log" | tail -8
fi
for a in "$@"; do
  if [[ "$a" == plants:* ]]; then
    # only these plants' prefabs, materials and points (new or changed models), the others' points kept
    "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.AssetBuilder.PlantsOnly -plants "${a#plants:}" -logFile "$L/unity_plants.log"
    echo "plants exit $?"; grep -E "error CS|\[AntGarden\] plants only|Exception" "$L/unity_plants.log" | tail -4
  fi
done
if [[ " $* " == *" ground "* ]]; then
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.AssetBuilder.GroundSetsOnly -logFile "$L/unity_ground.log"
  echo "ground exit $?"; grep -E "error CS|\[AntGarden\] ground sets|Exception" "$L/unity_ground.log" | tail -4
fi
for attempt in 1 2; do
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.BuildScript.BuildMac -logFile "$L/unity_build_mac.log"
  grep -q "\[AntGarden\] Build.*Succeeded" "$L/unity_build_mac.log" && { echo "mac build ok"; break; }
  echo "build attempt $attempt failed"; grep -E "error CS|Shader error|Exception" "$L/unity_build_mac.log" | head -8
done
grep -E "\[AntGarden\] Build" "$L/unity_build_mac.log" | head -2
if [[ " $* " == *" windows "* ]]; then
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.BuildScript.BuildWindows -logFile "$L/unity_build_win.log"
  grep -E "error CS|\[AntGarden\] Build" "$L/unity_build_win.log" | head -3
fi
