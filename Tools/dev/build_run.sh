#!/bin/zsh
# usage: build_run.sh <world> <quality> [skipbuild]
S="${ANTGARDEN_RUNS:-${TMPDIR:-/tmp}/antgarden_runs}"; mkdir -p "$S"
U=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity
P="$HOME/Desktop/AntGarden/UnityProject.nosync"
if [ "$3" != "skipbuild" ]; then
  "$U" -batchmode -quit -projectPath "$P" -executeMethod AntGarden.EditorTools.BuildScript.BuildMac -logFile "$S/unity_build_last.log"
  grep -E "error CS|\[AntGarden\] Build|Exception" "$S/unity_build_last.log" | grep -v "TestRunner/Assertions" | head -15
fi
R="$S/run_$(date +%H%M%S)"; mkdir -p "$R/cap"; echo "$R" > "$S/last_run_dir.txt"
ditto "$HOME/Desktop/AntGarden/05_Builds.nosync/AntGarden_Mac/AntGarden.app" "$R/AntGarden.app"
# open activates the app (front window); -W waits until it quits
perl -e 'alarm 240; exec @ARGV' open -n -W "$R/AntGarden.app" --args -world "$1" -quality "$2" $EXTRA -capture "$R/cap" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "$R/player.log"
echo "player exit $?"
ls "$R/cap"
cat "$R/cap/capture_report.json" 2>/dev/null
grep -E "\[AntGarden\]|Exception|NullReference|  at " "$R/player.log" | head -40
