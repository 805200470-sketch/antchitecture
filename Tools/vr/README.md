# 头显版（Meta Quest 3 一体机）交接说明

分支 `experiment/vr`。游戏直接装进头显里跑（安卓 APK），不连电脑。
电脑版的画面和参数都不动：头显版的改动只在头显里（或桌面加 `-quest 1` 预览时）生效。
以后头显版在 Windows 那台电脑上做。

## 现在到哪了（2026-10-29，Mac）

- 代码已写完：
  - `Scripts/Runtime/QuestLook.cs`：头显版的粒子预算、后期处理和帧率日志。
  - `Scripts/Runtime/StreamingFiles.cs`：安卓上读 APK 里的 game_config 和路径文件。
  - `Editor/VR/VRSetup.cs`：多了 Configure Quest 和 Build Quest 两个菜单项。
- 在 Mac 上用 `-quest 1` 跑过蓝色蚁穴：
  - 粒子总量约 150 万（电脑版约 1400 万），蚁穴生成 2 秒，没有报错。
  - 对比图：`07_Outputs/VR_头显版/01_头显版参数_Mac预览对比.jpg`（Mac 上，不在仓库里）。
- **还没在真头显上跑过**，帧率未知。
- 第一个 APK 是在 Mac 上打的：`05_Builds.nosync/AntGarden_Quest/AntGarden.apk`。

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

## 下一步

1. **头显上测帧率。** 72 Hz 下 `missed` 要接近 0。
   - 不够就把 `all` 调小；
   - 有富余就调大。
2. **把预算往近处挪。** Mac 预览里，近处的东西（土丘、藤、地上的小物件）颗粒偏大，像一片片圆片；远处还行。
   - 建议加：`shell` 0.12，`fine` 2；
   - 用这些来抵：`far` 0.05，`giants` 8，`plants` 0.2。
   - 头显里看过再定。
3. **辉光。** `grade` 下没有辉光，发光点会显得硬。试 `bloom` 的开销；太贵的话，以后在粒子着色器里做假辉光。
4. **烘焙。** 用户要的"不是实时的粒子"：现在整个蚁穴在加载时生成一次，只有蚂蚁周围两圈细节（`shell`、`groundShell`）边走边在后台线程重做。头显上如果看到卡顿，先把这两项关掉，或者整体预先生成好存成文件。

## 规矩

- 不改电脑版共用的默认值（ParticleLook、SolidThings 等的字段）。头显版只在 QuestLook 里改。
- 合并主分支 `experiment/particle-look` 时，要注意 Game.cs、GameConfig.cs、SolidThings.cs 里几处标了 QuestLook 的小改动。
- 仓库是公开的：安装包不提交，密钥不提交。

## 已知风险（还没在真机上验证）

- 多通道（multi-pass）立体渲染：每只眼睛各画一遍。这样着色器不用改，但画的量翻倍。
- Quest 3 是通过反射加进目标设备列表的（这个版本的 OpenXR 插件默认只列到 Quest Pro）。没加上的话，Quest 3 会按 Quest 2 的性能档跑。
- `LabNest` 的引路场贴图是 RGFloat 格式，用的是双线性采样，个别手机芯片不支持。
- 在头显里长按 Meta 键重新居中之后，视角可能偏一点。
