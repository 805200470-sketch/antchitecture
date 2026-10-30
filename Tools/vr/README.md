# 头显版（Meta Quest 3 一体机）交接说明

分支 `experiment/vr`。游戏直接装进头显里跑（安卓 APK），不连电脑。
电脑版的画面和参数都不动：头显版的改动只在头显里（或桌面加 `-quest 1` 预览时）生效。
以后头显版在 Windows 那台电脑上做。

## 现在到哪了（2026-10-30，Windows）

- Windows 上能打包、装机：第一次打包（含全部导入）14 分钟，之后每次约 2 分钟；APK 1.0 GB。
- 第一次上真机（10-30 上午，旧的默认参数，`all` 1）：出画面后 20–28 fps，72 Hz 下几乎每帧都没赶上。
  - GPU 每帧 23–35 ms（72 Hz 的预算是 13.9 ms），GPU 占用到 99%，CPU、GPU 都在最高档：瓶颈在 GPU。
  - 加载 16 秒，其中读路径数据 9 秒。
- 做成了能玩的游戏（只改了头显专用的文件）：
  - `VRPlayer.cs`：Y 在蚂蚁的眼睛和它身后（第三人称）之间切换，切换时眨一下眼；X 切换中文 / English；左手柄菜单键看全部操作。
  - `VRTutorial.cs`（新）：眼前的教程卡片，一步一步教：爬行 → 转身 → 换视角 → 触角（B）→ 打招呼（A）→ 跟着光走，每一步做到了才往下走。屏幕提示里的「按 E」在头显里写成「按 A」；目标、提示、对话放大，小地图不显示。
  - `QuestLook.cs`：植物摇摆 ×1.6、阵风 ×1.25、地面涟漪 ×1.6、声音 ×1.6；预算从远处挪到近处（`shell` 0.12、`fine` 2；`far` 0.05、`giants` 8、`plants` 0.2），空中光尘 `dust` 0.3；`all` 0.6，眼睛贴图 `resolution` 0.9。
- 三个版本并排装在头显里对照，应用库里各是一个：
  - Ant Garden（均衡，`quest.json`）
  - Ant Garden 流畅版（`quest_smooth.json`：`all` 0.45，`resolution` 0.85）
  - Ant Garden 画质版（`quest_quality.json`：`all` 0.8，`resolution` 1.0，植物和光尘多一些）
  - 打包：`ANTGARDEN_VARIANT=smooth ANTGARDEN_VARIANT_NAME=流畅版`，再跑 BuildQuest。
- **新功能和三个版本的帧率还没在真机上验证**：10-30 晚上房间太暗，头显定位丢了（日志里是 `Images too dark`），边界对话框挡住了所有应用。

### 在这台 Windows 上测试时的坑

- 这台电脑装着 Meta Horizon 电脑端：头显插 USB 会弹「Quest Link 可用」，一点就切进 Link，游戏被压到后台。测之前在头显 设置 → 系统 → Quest Link 关掉。
- Autodesk 的 `AdSSO.exe` 占着 adb 的默认端口 5037：adb 命令前设 `ANDROID_ADB_SERVER_PORT=5038`。
- Git Bash 会把 adb 命令里的 `/sdcard/...` 改写成 Windows 路径：先 `export MSYS_NO_PATHCONV=1`。
- 没人戴时让头显保持亮屏：`adb shell am broadcast -a com.oculus.vrpowermanager.prox_close`；恢复：`... automation_disable`。房间要亮，头显要看得到房间。
- 截图：`adb exec-out screencap -p > shot.png`（左右眼一起，4128×2208）。头显的时钟比电脑慢 8 小时。

## 之前（2026-10-29，Mac）

- 代码已写完：
  - `Scripts/Runtime/QuestLook.cs`：头显版的粒子预算、后期处理和帧率日志。
  - `Scripts/Runtime/StreamingFiles.cs`：安卓上读 APK 里的 game_config 和路径文件。
  - `Editor/VR/VRSetup.cs`：多了 Configure Quest 和 Build Quest 两个菜单项。
- 在 Mac 上用 `-quest 1` 跑过蓝色蚁穴：
  - 粒子总量约 150 万（电脑版约 1400 万），蚁穴生成 2 秒，没有报错。
  - 对比图：`07_Outputs/VR_头显版/01_头显版参数_Mac预览对比.jpg`（Mac 上，不在仓库里）。
- **还没在真头显上跑过**，帧率未知。
- 第一个 APK 是在 Mac 上打的：`05_Builds.nosync/AntGarden_Quest/AntGarden.apk`。
  - 1.0 GB，打包用了 221 秒。
  - 包名 com.ucldfpi.antgarden，minSdk 32，arm64；清单里 supportedDevices = quest|quest2|cambria|eureka（eureka 就是 Quest 3）。
- 打包时，`AntGarden/Plant`（网格植物的着色器）在 Vulkan 上有 2 个编译错误（BLENDINDICES 的输入顺序）。
  - 粒子蚁穴里植物网格是隐藏的，用不到它。
  - 以后要在头显里放非粒子的世界，得先修它。

## 在 Windows 上打包

1. 安装 Unity 2022.3.62f3c1，勾选 Android Build Support（含 OpenJDK、Android SDK & NDK Tools）。
2. 用 Unity 打开 `UnityProject.nosync`。
3. 第一次打开会按安卓重新导入资源，比较久。
4. 点菜单 AntGarden → Build → Quest (Android, OpenXR)。
5. 包输出到 `05_Builds.nosync/AntGarden_Quest/AntGarden.apk`。设置了环境变量 `ANTGARDEN_BUILDS` 的话，输出到它指的目录。

命令行打包：

```
Unity.exe -batchmode -quit -projectPath UnityProject.nosync -buildTarget Android -executeMethod AntGarden.EditorTools.VRSetup.BuildQuest -logFile quest_build.log
```

## 装机和调试（adb：Meta Quest Developer Hub 自带，或 Android SDK platform-tools）

头显要开着开发者模式，并且允许了这台电脑的 USB 调试。Windows 还要装 Oculus ADB 驱动。

```
adb devices
adb install -r AntGarden.apk
adb shell am start -n com.ucldfpi.antgarden/com.unity3d.player.UnityPlayerActivity
adb shell am force-stop com.ucldfpi.antgarden
adb logcat -s Unity | findstr AntGarden
```

Mac 上用 `zsh Tools/vr/quest.sh devices|install|start|stop|push|log|shot` 做同样的事。

游戏每 5 秒往日志写一行帧率，格式是：

```
[AntGarden] quest: 71.9 fps at 72 Hz, 0 of 360 frames missed, slowest 15 ms, 364 MB in use
```

## 不重新打包就能调参

1. 改一份 `Tools/vr/quest.json`。
2. 推进头显：

   ```
   adb push quest.json /sdcard/Android/data/com.ucldfpi.antgarden/files/quest.json
   ```

3. 先停掉游戏，再启动，新值就生效了。日志开头的 `[AntGarden] quest: {...}` 会显示实际用上的值。

各项是电脑版的几分之几：

| 项 | 管什么 |
|---|---|
| `all` | 所有份额一起乘这个数，先调它 |
| `plants` | 植物远处那套 |
| `shell` | 植物近处那套（围着蚂蚁的细节） |
| `things` / `fine` | 地上的小东西；`fine` 是近处每个点再拆成几个（电脑版 4） |
| `far` / `giants` | 墙外的巨树：细点的份额 / 最多几棵 |
| `ground` / `dust` / `walls` / `groundShell` | 地面亮点 / 空气里的尘 / 近处墙面 / 近处地面 |
| `carpet` | 脚下地毯的点距，乘这个数 |
| `vines` | 引路光柱周围的藤 |
| `post` | `grade`（只有调色）、`bloom`（加快速辉光）、`full`（同电脑版）、`none` |
| `resolution` | 眼睛贴图的大小 |
| `world` | 起始的蚁穴，空着就用 game_config 里的 |
| `wave` / `gusts` / `ripples` | 植物在风里摇多远 / 阵风多快 / 地上的涟漪多强，都是电脑版的几倍 |
| `volume` | 所有声音，电脑版的几倍 |
| `view` | 开局在哪看：`eye` 蚂蚁的眼睛，`behind` 它身后（游戏里按 Y 切换） |
| `behindScale` / `behindBack` / `behindUp` | 身后视角：世界的缩放（蚂蚁的眼睛是 0.25）、离蚂蚁多远、多高 |
| `walk` / `leanRadius` | 怎么移动：`stick` 站在原地用左摇杆爬（默认，安全）；`real` 迈步、蚂蚁跟着爬（游戏里按下右摇杆切换）。头离站立处多远以内（米）画面跟着动，再远就不跟了 |

## 下一步

1. **在亮的房间里测三个版本的帧率。** 用户说 60 帧就够、画面优先：GPU 每帧要在 16.7 ms 以内（72 Hz 下 `missed` 会有一些，转头靠系统补帧）。挑一个定下来，写回 `quest.json` 和 `QuestLook` 的默认值。
2. **戴上头显走一遍教程。** Y 的第三人称（离蚂蚁多远多高、会不会穿进墙里，`behindBack`、`behindUp`、`behindScale`）、X 中英文、菜单键、B 碰亮植物、A 打招呼，中文在头显里显示得对不对。
3. **按 Meta 键再回来、摘下头显再戴上**：游戏要能接着跑。10-30 被 Link 切走时，日志里有 GPU page fault（`vk_devicememory was already freed`）。
4. **对照 v15**（`参考_v15/`）：近处颗粒（预算已经往近处挪：`shell` 0.12、`fine` 2，Mac 预览里近处像一片片圆片）、植物微光、光丝和光柱、空中光尘。
5. **辉光。** `grade` 下没有辉光，发光点会显得硬。试 `bloom` 的开销；太贵的话，以后在粒子着色器里做假辉光。
6. **烘焙。** 用户要的"不是实时的粒子"：现在整个蚁穴在加载时生成一次，只有蚂蚁周围两圈细节（`shell`、`groundShell`）边走边在后台线程重做。头显上如果看到卡顿，先把这两项关掉，或者整体预先生成好存成文件。

## 规矩

- 不改电脑版共用的默认值（ParticleLook、SolidThings 等的字段）。头显版只在 QuestLook 里改。
- 合并主分支 `experiment/particle-look` 时，要注意 Game.cs、GameConfig.cs、SolidThings.cs 里几处标了 QuestLook 的小改动。
- 仓库是公开的：安装包不提交，密钥不提交。

## 已知风险（还没在真机上验证）

- 多通道（multi-pass）立体渲染：每只眼睛各画一遍。这样着色器不用改，但画的量翻倍。
- Quest 3 是通过反射加进目标设备列表的（这个版本的 OpenXR 插件默认只列到 Quest Pro）。没加上的话，Quest 3 会按 Quest 2 的性能档跑。
- `LabNest` 的引路场贴图是 RGFloat 格式，用的是双线性采样，个别手机芯片不支持。
- 在头显里长按 Meta 键重新居中之后，视角可能偏一点。
