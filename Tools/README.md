# Tools · 工具脚本

在项目根目录运行，例如 `python3 Tools/codex_batch.py …`。完整用法见 `Docs/Unity版_操作说明.md` 最后一节"开发用的脚本"。

| 脚本 | 做什么 |
|---|---|
| `tripo.mjs`、`tripo_batch.sh` | 正面图 → Tripo 3D 模型（**要 API key，花钱**；key 只在主开发机的 `.secrets.nosync/`） |
| `plants_to_unity.sh` / `.py` / `.json` | Tripo 模型 → Unity 用的三档精度 FBX 和贴图，并登记到 `plants.json`（用到 Blender） |
| `codex_image.sh`、`codex_batch.py`、`codex_batch_v5.py` | 用 Codex 出图（单张 / 批量） |
| `archive_codex_images.py` | 把 Codex 原图归档到 `02_Concepts/Codex_Archive/` |
| `make_ground_textures.py`、`preview_ground_textures.py` | 地面贴图：调色、无缝、法线、高度；先出审核图 |
| `frames_to_video.sh` / `.py`、`mp4_faststart.py` | 逐帧截图 → MP4（用 Blender，不需要 ffmpeg） |
| `make_poster.py` | 海报 |
| `ants/` | 蚂蚁从 Blender 导出到 Unity / 网页 |
| `core_tests/` | 核心算法测试：C# 版和网页原型的结果逐位比对 |
| `lineart/`、`lineart_v2/` | 线稿插图 |
| `dev/` | 打包并自动跑一遍的开发脚本 |
| `copy_from_T9.sh` | 一次性的：从 T9 硬盘复制资料（已完成） |

⚠️ 有些脚本（`ants/`、`core_tests/`、`lineart*/` 等）里写死了主开发机上的路径 `/Users/xujiafeng/Desktop/AntGarden`。在你的电脑上运行前，把它换成你自己克隆下来的仓库路径。

⛔ 不上传：`portfolio/`（个人作品集，和项目无关）。
