#!/bin/zsh
cd -- "$(dirname -- "$0")" || exit 1
if command -v node >/dev/null 2>&1; then
  node scripts/serve.mjs --open
elif command -v python3 >/dev/null 2>&1; then
  python3 scripts/serve.py
else
  echo '请先安装 Node.js 20+ 或 Python 3，再运行。'
  read -r '?按回车退出…'
fi
