#!/bin/bash
# One command: regenerate golden.json from the JS prototype, compile the C# core (Unity project,
# Assets/AntGarden/Scripts/Core) with Unity's bundled Roslyn, and run the golden test on Unity's
# bundled .NET 6 runtime and on Unity's bundled Mono runtime (the Editor / Mono-player runtime).
# Never starts the Unity editor and never writes into the Unity project.
#
#   ./run_tests.sh              everything
#   SKIP_GOLDEN=1 ./run_tests.sh   reuse the existing golden.json
#   SKIP_MONO=1 ./run_tests.sh     .NET 6 only
#   TIMING_RUNS=5 ./run_tests.sh   timing repetitions (0 = no timing)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
UNITY="${UNITY_CONTENTS:-/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents}"
DOTNET="$UNITY/NetCoreRuntime/dotnet"
CSC_DLL="$UNITY/DotNetSdkRoslyn/csc.dll"
RT="$UNITY/NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21"
NETSTD="$UNITY/NetStandard/ref/2.1.0/netstandard.dll"
MONO="$UNITY/MonoBleedingEdge/bin/mono"
MONOLIB="$UNITY/MonoBleedingEdge/lib/mono/unityjit-macos"
CORE="${CORE_DIR:-/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Scripts/Core}"
NETWORKS="${ANT_GARDEN_NETWORKS:-/Users/xujiafeng/Desktop/AntGarden/04_Paths/Networks}"
JS_SRC="${ANT_GARDEN_SRC:-$HOME/Desktop/AntGarden/06_Prototypes/Ant_Garden_Web_Demo/src}"
GOLDEN="$HERE/golden.json"
BUILD="$HERE/build"
TIMING_RUNS="${TIMING_RUNS:-3}"
CSC=("$DOTNET" "$CSC_DLL" -nologo -langversion:9 -nullable:disable -deterministic -optimize+ -warn:4 -nowarn:1591)

mkdir -p "$BUILD/net6" "$BUILD/mono"
CORE_SRC=("$CORE"/*.cs)
echo "core sources: ${#CORE_SRC[@]} files in $CORE"

# 1. golden data from the JavaScript reference
if [ "${SKIP_GOLDEN:-0}" != "1" ] && [ -d "$JS_SRC" ]; then
  echo "== golden.json from $JS_SRC"
  node "$HERE/dump_golden.mjs" "$GOLDEN"
elif [ -f "$GOLDEN" ]; then
  echo "== reusing $GOLDEN (JS sources not regenerated)"
else
  echo "no golden.json and no JS sources at $JS_SRC" >&2
  exit 2
fi

# 2. the core alone against Unity's .NET Standard 2.1 profile, C# 9 (what Unity compiles it with)
echo "== compile core: netstandard 2.1, C# 9, no UnityEngine"
"${CSC[@]}" -target:library -nostdlib+ -warnaserror+ -r:"$NETSTD" -doc:"$BUILD/AntGarden.Core.xml" \
  -out:"$BUILD/AntGarden.Core.dll" "${CORE_SRC[@]}"

# 3. core + tests on .NET 6
echo "== compile tests: .NET 6"
REFS=()
for f in "$RT"/*.dll; do REFS+=("-r:$f"); done
"${CSC[@]}" -target:exe -nostdlib+ "${REFS[@]}" -out:"$BUILD/net6/CoreTests.dll" "${CORE_SRC[@]}" "$HERE/CoreTests.cs"
cat > "$BUILD/net6/CoreTests.runtimeconfig.json" <<'JSON'
{
  "runtimeOptions": {
    "tfm": "net6.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.21" }
  }
}
JSON

STATUS=0
echo "== run: .NET 6"
"$DOTNET" "$BUILD/net6/CoreTests.dll" --golden "$GOLDEN" --networks "$NETWORKS" --timing "$TIMING_RUNS" --label ".NET 6.0.21 RyuJIT" || STATUS=$?

# 4. core + tests on Unity's Mono (unityjit profile = the Editor and Mono standalone players)
if [ "${SKIP_MONO:-0}" != "1" ] && [ -x "$MONO" ]; then
  echo
  echo "== compile tests: Unity Mono (unityjit-macos class libraries)"
  "${CSC[@]}" -target:exe -nostdlib+ -r:"$MONOLIB/mscorlib.dll" -r:"$MONOLIB/System.dll" -r:"$MONOLIB/System.Core.dll" \
    -out:"$BUILD/mono/CoreTests.exe" "${CORE_SRC[@]}" "$HERE/CoreTests.cs"
  echo "== run: Unity Mono"
  MONO_PATH="$MONOLIB" "$MONO" --debug "$BUILD/mono/CoreTests.exe" --golden "$GOLDEN" --networks "$NETWORKS" --timing "$TIMING_RUNS" --label "Unity Mono $("$MONO" --version | head -1 | sed 's/.*version \([^ ]*\).*/\1/')" || STATUS=$?
fi

echo
if [ "$STATUS" = "0" ]; then echo "run_tests.sh: ALL PASS"; else echo "run_tests.sh: FAILED ($STATUS)"; fi
exit "$STATUS"
