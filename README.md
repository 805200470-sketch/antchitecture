# 蚂蚁花园 Ant Garden

UCL DFPI 毕业设计。把真实蚂蚁巢穴的路径图变成一座座峡谷般的蚁穴，玩家控制一只蚂蚁在里面爬行、和蚁群碰触角、点亮发光植物、发现遗迹，以后还会读到前一位访客留下的故事。画风是《风之谷》腐海的夜晚。

- **引擎**：Unity 2022.3.62f3c1（国内版 Hub 的 "c1" 版本），Built-in 渲染管线 + Post Processing v2
- **语言**：C#；工具脚本用 Python / Node / zsh
- **演示平台**：Windows 电脑（Mac 上开发和测试）
- **当前状态（2026-10-16）**：可玩的演示。13 个蚁穴加 2 个遗迹拼成一整块蜂巢；支持键盘鼠标和 Xbox 手柄；有音效，没有背景音乐

> 第一次来？按顺序看：本页 → [`Docs/Unity版_操作说明.md`](Docs/Unity版_操作说明.md)（按键、调参数、工具）→ [`Docs/报告/`](Docs/报告/)（最近的改动报告）。

---

## 1. 目录分类

| 文件夹 | 放什么 | 在仓库里？ |
|---|---|---|
| [`01_References/`](01_References/) | 参考资料：每只蚂蚁的设定图（`Ants/Ant_001`–`005`） | ✅（网上找的氛围图 `Mood/` 不上传） |
| [`02_Concepts/`](02_Concepts/) | 概念图：Codex 画的场景、植物、遗迹、地面贴图，以及提示词 | ✅（全部原图 `Codex_Archive/` 不上传） |
| [`03_Models/`](03_Models/) | 3D 模型原文件：Tripo 生成的 GLB、蚂蚁 FBX、Blender 源文件 | ❌ 太大，只有说明。游戏用的版本在 Unity 工程里 |
| [`04_Paths/`](04_Paths/) | 蚁穴路径：13 张原始路径图、识别出的路径数据（JSON）、路径识别工具 | ✅ |
| [`05_Builds.nosync/`](05_Builds.nosync/) | 打包好的游戏（Windows / Mac）和高画质视频 | ❌ 约 2 GB，只有说明 |
| [`06_Prototypes/`](06_Prototypes/) | 早期原型：9 月的网页 MVP、10 月初的 Three.js 网页 Demo | ✅ |
| [`Docs/`](Docs/) | 文档和成果：操作说明、报告、截图、海报、线稿插图、音效试听 | ✅（视频、PPT 不上传） |
| [`Tools/`](Tools/) | 工具脚本：Tripo 建模、Codex 出图、模型转 Unity、地面贴图、视频、海报 | ✅ |
| [`UnityProject.nosync/`](UnityProject.nosync/) | **游戏本体**：Unity 工程 | ✅（Unity 缓存 `Library/` 等不上传） |

文件夹名带 `.nosync` 的不会同步到 iCloud（主开发机的桌面开着 iCloud 同步），对仓库没有影响，名字别改，工具脚本靠这些路径找文件。

每个文件夹里都有自己的 `README.md`，写着里面每个子文件夹是什么。

---

## 2. 改东西之前先看：三类

### ✅ A. 可以直接改（改坏了也容易发现、容易改回）

| 想改 | 去哪里 | 说明 |
|---|---|---|
| 蚂蚁说的话、遗物的台词、访客故事 | `UnityProject.nosync/Assets/AntGarden/Scripts/Runtime/Voices.cs` | 每条都是 `("中文", "English")`，两种语言都要写；注意保留引号和逗号 |
| 各种数值（速度、镜头、发光、植物密度、音量……） | **不用改代码**：在 `UnityProject.nosync/Assets/StreamingAssets/` 里新建 `game_config.json`，只写要改的字段 | 打包后的游戏里也能改：`AntGarden_Data/StreamingAssets/game_config.json`，存盘后重开游戏就生效。全部字段和默认值见 `Scripts/Runtime/GameConfig.cs` |
| 每种植物的发光强度、摇摆 | `UnityProject.nosync/Assets/AntGarden/Art/Plants/plant_looks.json` | 改完要在 Unity 里运行一次菜单 **AntGarden → Build Assets** |
| 概念图、参考图、文档、截图 | `02_Concepts/`、`01_References/`、`Docs/` | 新文件按各文件夹 README 里的规则放；文件名只用英文、数字、下划线 |
| 新的蚁穴路径图 | `04_Paths/Images/` | 再用 `04_Paths/Path_Recognize_Package/` 识别成 JSON，放进 `UnityProject.nosync/Assets/StreamingAssets/Paths/` |

### 🛠 B. 需要改（待办，欢迎认领）

| 待办 | 现状 | 相关位置 |
|---|---|---|
| **遗迹隧道收窄 + 发光足迹引路** | 进行中：两个遗迹太宽，像广场，要收窄成隧道、走向更明确，路上加发光小点引路 | `Scripts/Runtime/RuinScene.cs`、`TunnelRoof.cs` |
| **访客故事 / 故事物件位** | 现在的故事是演示用的占位文字；"上传照片和故事变成物件"这个功能还没做，物件位只显示占位说明 | `Voices.cs` 里的 `Stories`；`Game.cs` 里的 `OpenSlot` |
| **新植物** | 第三版候选图已画好，等挑选后用 Tripo 建模、转成 Unity 资源 | `02_Concepts/Plants_v12_候选/00_候选三版对照.jpg`；流程见操作说明"加入新的 Tripo 植物" |
| **背景音乐** | 还没做（音效已有，全部由程序合成） | `Scripts/Runtime/Sfx.cs`；试听 `Docs/音效试听/` |
| **蜂巢细节** | 蓝色和琥珀格子之间的颜色要不要渐变、琥珀格怎么分布、外圈出口现在是死路，都还没定 | `Docs/报告/2026-10-13_蜂巢拼接/` |
| **蚂蚁的腿** | 现在用原版动画腿，程序腿也保留着，等最终选定 | `Docs/演示视频`（对比视频在网盘）；`GameConfig.cs` 的 `proceduralLegs` |
| **Windows 演示机实测** | 只在 Mac（M3 Max）上测过：高画质 55–60 帧。演示电脑上的帧率和画质要实测 | 游戏里按 `Tab` 切画质 |
| **英文界面检查** | 海报、视频、PPT 截图要用英文界面（`L` 键切换），新加的文字要检查有没有漏译 | 所有 `Loc.T("中文", "English")` |

做完一项就把这一行删掉或标上完成，并在 `Docs/报告/` 里写一句改了什么。

### ⛔ C. 不要改（要改先和负责人商量）

| 不要动 | 原因 |
|---|---|
| `UnityProject.nosync/Assets/AntGarden/Scripts/Core/` | 路径识别、峡谷生成、蜂巢拼接的核心算法，和网页原型逐位对照测试（`Tools/core_tests/`），改了可能整个世界都变 |
| 所有 `.meta` 文件 | Unity 靠它们记住资源之间的引用，删了或改了，模型和材质会全部丢失。挪文件一定要在 Unity 里面挪 |
| `UnityProject.nosync/Assets/AntGarden/Art/Ants/` | 交付的蚂蚁模型和动画 |
| `Assets/AntGarden/Generated/`、`Art/Plants/plants.json` | 工具自动生成的，手改会被下一次生成覆盖 |
| `UnityProject.nosync/ProjectSettings/` | Unity 版本、输入系统（新旧两套并存）、画质设置；改错了工程可能打不开 |
| 任何密钥 | Tripo API key 只放在本机的 `.secrets.nosync/`，**永远不要提交到仓库** |

---

## 3. 去哪里找（按要做的事）

| 要做的事 | 文件 |
|---|---|
| 运行、打包游戏 | 用 Unity Hub 打开 `UnityProject.nosync/`，打开场景 `Assets/AntGarden/Scenes/Main.unity` 点 Play；打包：菜单 **AntGarden → Build → Windows (x64) / macOS**（`Assets/AntGarden/Editor/BuildScript.cs`），输出到 `05_Builds.nosync/` |
| 按键、手柄 | `Scripts/Runtime/Game.cs`（键盘）、`Pad.cs`（手柄）；说明见操作说明的"操作"和"Xbox 手柄"两节 |
| 镜头 | `Scripts/Runtime/CameraRig.cs` |
| 玩家蚂蚁的移动、碰撞、爬坡 | `Scripts/Runtime/AntController.cs`；腿：`LegRig.cs` |
| NPC 蚂蚁、蚁群路线 | `Scripts/Runtime/NpcAnt.cs`、`ColonyTrails.cs` |
| 世界怎么拼（哪些蚁穴、什么颜色） | `Scripts/Runtime/GameConfig.cs` 里的 `defaultWorld`；拼接算法在 `Scripts/Core/Stitcher.cs` |
| 地形、地面贴图 | `Scripts/Runtime/TerrainBuilder.cs`、`World.cs`；贴图在 `Art/Ground/`，着色器 `Shaders/Terrain.shader` |
| 植物摆放 | `Scripts/Runtime/Scatter.cs`；规则（种类、密度、成片程度）在 `GameConfig.cs` 的 `scatter.rules` |
| 发光植物、触角碰亮 | `Scripts/Runtime/GlowPlants.cs`；着色器 `Shaders/Plant.shader` |
| 遗迹（巨人遗物、古蚁圣殿） | `Scripts/Runtime/RuinScene.cs`、`TunnelRoof.cs` |
| 对话、台词、故事 | `Scripts/Runtime/Voices.cs`；界面上的对话框：`Hud.cs` |
| 音效 | `Scripts/Runtime/Sfx.cs`；音量在 `GameConfig.cs` 的 `audio` |
| 地图、小地图 | `Scripts/Runtime/MapPainter.cs`、`BigMap.cs` |
| 光门（蚁穴之间的门） | `Scripts/Runtime/Gates.cs` |
| 自动测试 / 截图模式 | `CamCheck.cs`（镜头）、`LegCheck.cs`（腿）、`PadCheck.cs`（手柄）、`SceneShots.cs`（每个场景的截图）、`LookCheck.cs`、`NpcCheck.cs`；用法见操作说明最后一节 |
| 植物模型从 Tripo 到游戏 | `Tools/tripo_batch.sh` → `Tools/plants_to_unity.sh` → Unity 菜单 **AntGarden → Build Assets** |
| 用 Codex 出图 | `Tools/codex_image.sh`、`Tools/codex_batch.py`；提示词在 `02_Concepts/Prompts/` |
| 地面贴图从概念图到游戏 | `Tools/make_ground_textures.py`（先用 `Tools/preview_ground_textures.py` 出审核图） |
| 演示视频、海报 | `Tools/frames_to_video.sh`、`Tools/make_poster.py`；游戏的 `-demo`、`-photos` 参数 |
| 最近改了什么 | `Docs/报告/`；截图在 `Docs/screenshots/` |

上表 `Scripts/`、`Shaders/`、`Art/` 都在 `UnityProject.nosync/Assets/AntGarden/` 下面。

---

## 4. 没放进仓库的东西

| 东西 | 在哪里 | 怎么拿 |
|---|---|---|
| 打包好的游戏（Windows zip 约 330 MB、Mac app） | 主开发机 `05_Builds.nosync/` | 找负责人要网盘链接，或者自己用 Unity 打包 |
| 演示视频、汇报 PPT（都超过 100 MB） | 主开发机 `Docs/演示视频/`、`Docs/最终汇报_2026-10-06/` | 网盘 |
| 3D 模型原文件、蚂蚁 Blender 源文件（约 3.7 GB） | 主开发机 `03_Models/` | 网盘；游戏用的版本已经在 `UnityProject.nosync/Assets/AntGarden/Art/` |
| Codex 出过的全部原图 | 主开发机 `02_Concepts/Codex_Archive/` | 网盘；选用的定稿都在 `02_Concepts/` 其他文件夹里 |
| Tripo API key | 主开发机 `.secrets.nosync/` | 不共享。需要建模时找负责人 |

---

## 5. 协作规则

1. **先拉再改**：开始前 `git pull`；改完新建分支提交，再开 Pull Request，不要直接推到 `main`。
2. **提交信息**：前面写类型，后面一句话说清楚，例如 `feat: 遗迹隧道加发光足迹`、`fix: 手柄 B 键关不掉地图`、`docs: 更新待办`。类型有 `feat` / `fix` / `docs` / `art` / `chore`。
3. **大文件**：单个文件不要超过 50 MB。视频、PPT、原始模型放网盘，在 README 或报告里写链接。
4. **Unity**：
   - 只用 **2022.3.62f3c1**，换版本会改坏 `ProjectSettings`。
   - 挪文件、改名都在 Unity 里做，`.meta` 文件要一起提交。
   - 不要提交 `Library/`（已在 `.gitignore` 里）。
5. **改完要能证明**：
   - 改了玩法或画面，附一张游戏截图，放在 `Docs/screenshots/` 下按日期建的文件夹里。
   - 改了镜头、腿、手柄，跑一下对应的检查模式（见操作说明）。
6. **文件命名**：英文、数字、下划线，不要空格。中文只用在文档和说明文件里。
