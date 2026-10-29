#!/bin/zsh
# codex_image.sh <out.png> <prompt>
# One image through the Codex command line (the copy bundled with the ChatGPT app) and its image tool,
# saved at its native resolution (CODEX_REFS="a.png,b.png" attaches reference images; CODEX_EDIT=1: the first one is edited). Uses the app's existing sign-in; no window needs to be open, and it
# also works while the screen is locked. Several can run at once (each takes about a minute).
#   Tools/codex_image.sh ~/Desktop/AntGarden/02_Concepts/Fronts_for_3D/new_plant.png "Square image of ONE ..."
out="$1"; prompt="$2"
if [ -z "$out" ] || [ -z "$prompt" ]; then echo "usage: codex_image.sh <out.png> <prompt>" >&2; exit 2; fi
codex="${CODEX:-/Applications/ChatGPT.app/Contents/Resources/codex}"
logs="${TMPDIR:-/tmp}/codex_image_logs"; mkdir -p "$logs"
name=$(basename "$out" .png)
dir=$(dirname "$out"); mkdir -p "$dir"
start=$(date +%s)
refs=()   # CODEX_REFS="a.png,b.png": reference images Codex sees with the prompt (for style)
for r in ${(s:,:)CODEX_REFS}; do [ -f "$r" ] && refs+=(-i "$r"); done
# "--" ends the options: -i takes several files and would otherwise swallow the prompt as one more
"$codex" exec --skip-git-repo-check -s workspace-write -C "$dir" -o "$logs/$name.last.txt" "${refs[@]}" -- \
"Use your image generation tool to create exactly ONE image from the prompt below${CODEX_REFS:+ ${CODEX_EDIT:+(edit the first attached image as the prompt says; the others are references)}${CODEX_EDIT:-(the attached images are style references only)}}. Then save that generated image as the PNG file $out at its native resolution (do not resize or crop it; overwrite the file if it exists). Do not create, edit or delete any other files. When done, reply with only the saved path.

Prompt: $prompt" < /dev/null > "$logs/$name.log" 2>&1
code=$?
if [ -s "$out" ]; then echo "$name: ok ($(( $(date +%s) - start ))s)"; else echo "$name: FAILED (exit $code, log $logs/$name.log)"; exit 1; fi
