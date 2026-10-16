# 06_Prototypes · 早期原型（只看，不再开发）

| 子文件夹 | 内容 | 怎么打开 |
|---|---|---|
| `ant-garden-mvp_Codex_2026-09/` | 9 月 Codex 做的第一个网页 MVP：上传路径照片，生成六边形花园 | 看它自己的 `README.md`（先 `npm install`） |
| `Ant_Garden_Web_Demo/` | 10 月初的网页版 Demo（Three.js + 高斯泼溅），后来改用 Unity 重做 | 双击 `Start.command`；说明见 `Docs/网页原型_操作说明.md` |

Unity 版的核心算法（路径场、峡谷、拼接）是从这里的 `src/` 逐位移植的，`Tools/core_tests/` 用它做对照测试，所以 `src/` ⛔ 不要改。
