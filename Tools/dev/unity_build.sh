#!/bin/zsh
# unity_build.sh [assets] [windows] - optionally rebuild the asset manifest (new plants, materials), then build the
# Mac player (and the Windows one). Logs go to $ANTGARDEN_RUNS (default: $TMPDIR/antgarden_runs).
L="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; mkdir -p "$L"
U=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity
P="${0:A:h}/../../UnityProject.nosync"
if [[ " $* " == *" assets "* ]]; then
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.AssetBuilder.Run -logFile "$L/unity_assets.log"
  echo "assets exit $?"; grep -E "error CS|Shader error|\[AntGarden\]|Exception" "$L/unity_assets.log" | grep -v "TestRunner" | tail -12
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
