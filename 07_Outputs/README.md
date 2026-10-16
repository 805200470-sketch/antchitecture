# 07_Outputs · 体验与产出

这里列出项目的全部产出，以及怎么体验它们。

可玩的游戏包和视频文件太大（单个文件超过 GitHub 仓库 100 MB 的上限），所以放在仓库的 **Releases** 页面，不在代码里：
**https://github.com/805200470-sketch/antchitecture/releases**

## 一、玩一下（不需要装 Unity）

### Windows

1. 下载 [AntGarden_Windows.zip](https://github.com/805200470-sketch/antchitecture/releases/latest/download/AntGarden_Windows.zip)（约 335 MB），解压。
2. 双击 `AntGarden_Windows/AntGarden.exe`。游戏在 1280×720 的窗口里打开，按 `F` 全屏。
3. 如果出现"Windows 已保护你的电脑"：点"更多信息" → "仍要运行"（程序没有签名）。
4. 第一次打开要生成世界，约 20 秒。

### Mac（Apple 芯片和 Intel 都可以）

1. 下载 [AntGarden_Mac.zip](https://github.com/805200470-sketch/antchitecture/releases/latest/download/AntGarden_Mac.zip)，解压得到 `AntGarden.app`。
2. 第一次打开：**右键 → 打开**（程序没有签名，直接双击会被拦）。如果还是打不开，在"终端"里运行一次：

   ```bash
   xattr -dr com.apple.quarantine ~/Downloads/AntGarden.app
   ```

### 怎么操作

| | 键盘 / 鼠标 | Xbox 手柄 |
|---|---|---|
| 爬行 / 快爬 | `W` `A` `S` `D`；按住 `Shift` 快爬 | 左摇杆；按住 RT |
| 镜头 | 鼠标左键拖动；滚轮拉近拉远 | 右摇杆；LB / RB |
| 碰触角、和遗物说话 | `E` | A |
| 停下伸触角点植物 | `空格` | B |
| 地图（点哪里传送到哪里） | `M` | Y |
| 画质 / 全屏 / 语言 / 说明 | `Tab` / `F` / `L` / `H` | — / — / — / ☰ |

完整说明：[Unity 版操作说明](../Docs/Unity版_操作说明.md)；所有交互的分类：[交互分类](../Docs/项目地图/2_交互分类.md)。

### 推荐的体验路线（约 10 分钟）

1. 开局在蜂巢中心（蓝色腐海）。沿峡谷走，用触角碰发光植物，看光一株传一株。
2. 和迎面来的蚂蚁碰触角（`E`），有的会说话。
3. 往旁边的琥珀色蚁穴走，跨过接缝：土色互相渗透，接缝上有光粒升起。
4. 按 `M` 打开地图，传送到"古蚁圣殿"或"巨人遗物"。跟着地上的光点走到隧道尽头的核心（琥珀里的祖先 / 纽扣广场），一路和遗物说话。

## 二、产出清单

### 可以玩的

| 产出 | 说明 | 在哪 |
|---|---|---|
| Windows 版 | 2026-10-16，蜂巢世界（13 个蚁穴 + 2 处遗迹） | Releases：`AntGarden_Windows.zip` |
| Mac 版 | 同上 | Releases：`AntGarden_Mac.zip` |
| 网页原型 | 最早的版本（Three.js + 点云素材），保留作对照 | `06_Prototypes/Ant_Garden_Web_Demo/`，说明见 `Docs/网页原型_操作说明.md` |
| Unity 工程 | 打开即可修改、重新打包 | `UnityProject.nosync/`（用 Unity 2022.3.62f3c1 打开） |

### 视频

| 产出 | 说明 | 在哪 |
|---|---|---|
| 完整演示（英文） | 2026-10-04 的导览：爬行、触角、蚁群、光门、琥珀蚁穴 | Releases：`AntGarden_demo_EN.mp4` |
| 遗迹隧道与引路光点 | 2026-10-16：从隔壁蚁穴跨过接缝，沿光点走进两处遗迹，直到核心 | Releases：`ruins_tunnels_guide_trail.mp4` |
| 腿部动画对比 | 程序腿和原版动画片段并排 | Releases：`legs_procedural_vs_clips.mp4` |
| 蚂蚁行走检查 | 2026-10-05 的蚂蚁行走动画检查片 | Releases：`ant_walk_check.mp4` |

### 图片和文字

| 产出 | 在哪 |
|---|---|
| 宣发海报（16:9，两版） | `Docs/宣发海报/` |
| 汇报用的游戏截图（4K，英文界面） | `Docs/PPT截图/` |
| 各版本的过程截图 | `Docs/screenshots/`（按版本分文件夹，最新是 `unity_v9_遗迹隧道与接缝/`） |
| 线稿插图 | `Docs/插图_线稿/` |
| 音效试听 | `Docs/音效试听/` |
| 阶段报告 | `Docs/报告/` |
| 最终汇报讲稿（中英，按页） | `Docs/最终汇报_2026-10-06/讲稿_中英_按页.md` |
| 最终汇报 PPT | 太大，而且里面有组员的页面，没有放进公开仓库；需要的话找项目负责人要 |

## 三、版本记录

| 日期 | 里程碑 |
|---|---|
| 2026-10-02 | 网页原型：路径识别 → 峡谷，点云素材 |
| 2026-10-03 | 转到 Unity：网格模型（Tripo）、运行时生成世界、Windows 版 |
| 2026-10-04 | 两区群岛、光门、蚁群（400 只）、发光植物、PPT 截图、演示视频 |
| 2026-10-06 | 最终汇报 |
| 2026-10-13 | 镜头重做、腿部动画、爬坡与碰撞、音效、Xbox 手柄、15 格蜂巢、性能（55–60 帧） |
| 2026-10-16 | 遗迹改成窄隧道、引路光点、接缝粒子与地面过渡、隧道变暗、贴地光圈；仓库整理与项目地图 |
