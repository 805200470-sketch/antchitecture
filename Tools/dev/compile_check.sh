#!/bin/zsh
# compile_check.sh - compile the game's Runtime scripts alone with Unity's own Roslyn compiler (a second or two, no
# Unity editor, no lock), to catch errors before a build. Several sessions edit the same scripts; a build started by any
# of them compiles everyone's edits, so one session's typo fails another's build. Prints the errors, or nothing.
# Uses the compiler arguments Unity last wrote (Library/Bee/artifacts/*/AntGarden.Runtime.rsp) with today's files.
P="${0:A:h}/../../UnityProject.nosync"
U=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents
cd "$P" || exit 1
RSP=$(ls -t Library/Bee/artifacts/*/AntGarden.Runtime.rsp 2>/dev/null | head -1)
[[ -z "$RSP" ]] && { echo "compile_check: no AntGarden.Runtime.rsp in Library/Bee (build once first)"; exit 1; }
T="${TMPDIR:-/tmp}/antgarden_compile_check.$$"; mkdir -p "$T"
grep -v '^-out:\|^-refout:\|\.cs"\?$' "$RSP" | sed 's#Library/Bee/artifacts/[^"]*/AntGarden.Core.ref.dll#Library/ScriptAssemblies/AntGarden.Core.dll#' > "$T/check.rsp"
echo "-out:\"$T/check.dll\"" >> "$T/check.rsp"
find Assets/AntGarden/Scripts/Runtime -name "*.cs" | sed 's/.*/"&"/' >> "$T/check.rsp"
"$U/NetCoreRuntime/dotnet" "$U/DotNetSdkRoslyn/csc.dll" /nostdlib+ /noconfig @"$T/check.rsp" 2>&1 | grep -E " error " | head -20
[[ -f "$T/check.dll" ]] && echo "compile_check: ok" || echo "compile_check: FAILED"
mv "$T" "$T.done" 2>/dev/null
