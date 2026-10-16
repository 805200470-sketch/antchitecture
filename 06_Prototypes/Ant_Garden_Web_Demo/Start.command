#!/bin/bash
# Double-click to start the Ant Garden canyon demo (needs Node.js 18+ and internet for the 3D libraries).
cd "$(dirname "$0")"
if ! command -v node >/dev/null 2>&1; then
  echo "没有找到 Node.js。请先安装 Node.js（https://nodejs.org），或在本目录运行：python3 -m http.server 8770"
  read -r -p "按回车关闭…"
  exit 1
fi
node serve.mjs --port 8770 --open
