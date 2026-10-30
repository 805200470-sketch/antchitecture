# 头显版 · 在 Windows 上接手

头显版（分支 `experiment/vr`）从 2026-10-30 起在 Windows 电脑上做：那台电脑连着头显，负责打包、装进头显、测帧率、调参。

这份说明讲五件事：要装什么、东西放在哪、GitHub 连不上怎么办、改完传到哪、哪些不能动。头显版现在到哪了、`quest.json` 每一项管什么、下一步做什么，见同目录的 [README.md](README.md)。

## 1. 设备

| | |
|---|---|
| 头显 | Meta Quest 3 一体机（不是 Quest 3S，也不是 Quest Pro），代号 `eureka`，系统是 Meta Horizon OS（安卓） |
| 怎么玩 | 游戏装在头显里跑，拔线单独玩；不是连电脑的 Quest Link |
| 手柄 | Quest 3 原配的两只 Touch Plus |
| 刷新率 | 72 Hz |
| 连电脑 | 能传数据的 USB-C 线（原配的 C-C 线可以；电脑没有 C 口就用 C 转 A 的数据线） |
| Windows 主机 | RTX 4080。只负责打包、装机、看日志，头显里的画面不靠它 |
| Mac | 电脑版继续在 Mac 上做；头显版不再在 Mac 上改 |

## 2. 东西放在哪

### GitHub

- 仓库：<https://github.com/805200470-sketch/antchitecture>（公开，约 2.9 GB）
- 头显版只在 `experiment/vr` 上改
- `main`（10-16 演示版）、`experiment/particle-look`（电脑版的粒子蚁穴，Mac 上做）、`recoginize-path`：都不动
- [Releases](https://github.com/805200470-sketch/antchitecture/releases)：打包好的游戏（现在有 10-16 的电脑版）
- 蓝色蚁穴 v15（电脑版的定稿模板）的参考图：`Tools/vr/参考_v15/`；它的代码是提交 `3d9c0c0`。只看不改，见第 12 节

### Windows 本地（建议的位置）

| 位置 | 是什么 |
|---|---|
| `F:\AntGarden\` | 仓库克隆到这里。路径要短、全英文、不带空格；不要放进桌面或 Claude Code Game Studios 模板文件夹。工程、Unity 缓存和安装包加起来，留 20 GB 以上 |
| `F:\AntGarden\UnityProject.nosync\` | Unity 工程，用 Unity Hub 打开这个文件夹 |
| `F:\AntGarden\Tools\vr\` | 头显版的说明（本文件、README.md）和 `quest.json`（调参模板）。`quest.sh` 是 Mac 专用的，Windows 上直接用 adb |
| `F:\AntGarden\05_Builds.nosync\AntGarden_Quest\AntGarden.apk` | 打出来的安装包，约 1 GB。`05_Builds.nosync` 整个不进 Git |
| `F:\AntGarden\05_Builds.nosync\AntGarden_Quest\记录\` | 头显截图、录像、帧率日志、试过的 quest.json（不进 Git） |
| `C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe` | Unity 的默认位置。装到别处的话：Hub → 安装 → 齿轮 → 在资源管理器中显示 |
| `…\2022.3.62f3c1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe` | Unity 自带的 adb |

### 头显里

| 位置 | 是什么 |
|---|---|
| 应用库 → 右上角筛选 → 未知来源 → Ant Garden | 装好的游戏，包名 `com.ucldfpi.antgarden` |
| `/sdcard/Android/data/com.ucldfpi.antgarden/files/quest.json` | 推进去的调参文件，游戏启动时读 |
| `/sdcard/Oculus/Screenshots/`、`/sdcard/Oculus/VideoShots/` | 头显自己截的图、录的像 |

### Mac 上（参考）

| 位置 | 是什么 |
|---|---|
| `~/Desktop/AntGarden/` | 主工程，`experiment/particle-look`（电脑版） |
| `~/Desktop/AntGarden/VR.nosync/` | 头显版以前在 Mac 上的工作目录，已经交给 Windows |
| `~/Desktop/AntGarden/05_Builds.nosync/AntGarden_Quest/` | Mac 上打的第一个安装包（10-29，1.0 GB，没上过头显）和 `头显说明.txt` |
| `~/Desktop/AntGarden/07_Outputs/VR_头显版/` | 电脑版和头显参数的对比图（在 Mac 上预览的） |

## 3. 要装的

| 软件 | 版本 | 下载 | 说明 |
|---|---|---|---|
| Unity Hub（国内版） | 3.x（现在装的 3.3.0-c9 可以） | <https://unity.cn/releases> | 用 Mac 上同一个 Unity 账号登录（学生 / Pro 许可，开屏 Logo 才能关；个人版也能打包，只是开头多一个 Unity Logo） |
| Unity 编辑器 | **2022.3.62f3c1**（changeset `1623fc0bbb97`） | Hub → 安装 → 安装编辑器。列表里没有，就在浏览器打开 `unityhub://2022.3.62f3c1/1623fc0bbb97` | 只能是这个版本，见下 |
| Git for Windows | 最新 | <https://git-scm.com/download/win> | 按默认选项装 |
| GitHub CLI | 最新 | <https://cli.github.com>，或 `winget install --id GitHub.cli` | 登录 GitHub、推代码用（Mac 上也是用它） |
| Meta Quest Developer Hub（MQDH） | 最新 | <https://developers.meta.com/horizon/downloads/package/oculus-developer-hub-win/> | 看头显状态、装 APK、截图、投屏 |
| Oculus ADB 驱动 | 最新 | <https://developers.meta.com/horizon/downloads/package/oculus-adb-drivers/> | Windows 上 adb 认头显要它 |
| Meta Horizon（手机 App） | 最新 | 手机应用商店 | 打开头显的开发者模式 |

**Unity 的版本和模块**

- 只能用 2022.3.62f3c1：国内 Hub 的 `c1` 版本，Mac 上也是它。别的版本（包括国际版 Unity）打开，会改掉 `ProjectSettings/ProjectVersion.txt`；包的来源会从 `packages.unity.cn` 换到国际服，`Packages/packages-lock.json` 也跟着变。也不要点"升级工程"。
- 模块只勾 **Android Build Support**，展开后把它下面的 **OpenJDK** 和 **Android SDK & NDK Tools** 也勾上。其他平台不用。安装时弹出的 Android 许可协议，由用户自己点同意。
- 工程用到的几个包（Input System、Post Processing、XR Management、OpenXR 等），第一次打开时从 `packages.unity.cn` 下载，不走 GitHub。

**头显这边（用户自己操作）**

1. 在 <https://developers.meta.com/horizon/> 用 Meta 账号登录，建一个组织（Organization），按提示验证账号。
2. 手机 Meta Horizon App → 设备 → 这台头显 → 头显设置 → 开发者模式 → 打开，然后重启头显。
3. 头显用数据线接电脑，头显里弹出"允许 USB 调试？"：勾"始终允许这台电脑"，点允许。
4. `adb devices` 里头显后面写着 `device` 就通了；写着 `unauthorized` 是第 3 步还没点。

## 4. GitHub 连不上的时候

拉代码、推代码用的是电脑上的 git，不是 Claude 应用 Connectors 里的 GitHub 连接器。那个连接器显示要重新连接，不影响 git。

Mac 上没设代理，直连 GitHub，用 GitHub CLI 登录推送（10-29、10-30 都推成功过）。Windows 上先分清是哪种问题：

1. **网络不通**：下面这行报 `Failed to connect`、`Connection was reset`，或者一直没反应。

   ```
   git ls-remote https://github.com/805200470-sketch/antchitecture.git experiment/vr
   ```

   - 浏览器能不能打开 github.com？
   - 用代理或加速器的话，在它的设置里找本地 HTTP 端口（比如 7890），只给 GitHub 设代理：`git config --global http.https://github.com.proxy http://127.0.0.1:7890`。不用了就 `git config --global --unset http.https://github.com.proxy`。
   - 或者换个网络试（比如手机热点）。
2. **能拉、不能推**（没登录）：`git push` 报 `403`、`Authentication failed`，或者弹出登录窗口。
   - 和 Mac 一样用 GitHub CLI：`gh auth login`（选 GitHub.com → HTTPS → 用浏览器登录，账号 `805200470-sketch`），再 `gh auth setup-git`。
   - 或者用 Git for Windows 自带的 Git Credential Manager：第一次 push 时会弹出浏览器登录。
   - 登录和两步验证都由用户自己完成。
3. **一直连不上**：先在本地做、本地提交，连上再推。代码拿不到的话，在 Mac 上打一个 git bundle（整个仓库一个文件，约 2.9 GB），用 U 盘或移动硬盘拷过来，从它克隆：

   ```
   git clone -b experiment/vr -c core.autocrlf=false F:/antgarden.bundle F:/AntGarden
   cd F:/AntGarden
   git remote set-url origin https://github.com/805200470-sketch/antchitecture.git
   git config user.name 805200470-sketch
   git config user.email 805200470-sketch@users.noreply.github.com
   ```

## 5. 拿到工程

在 Git Bash 里（Claude 对话的命令行就是 Git Bash）：

```
git clone --filter=blob:none --sparse -b experiment/vr -c core.autocrlf=false -c core.longpaths=true https://github.com/805200470-sketch/antchitecture.git F:/AntGarden
cd F:/AntGarden
git sparse-checkout set UnityProject.nosync Tools
git config user.name 805200470-sketch
git config user.email 805200470-sketch@users.noreply.github.com
git log --oneline -1
```

- 只下载头显版用得到的 Unity 工程和 Tools（约 2 GB）。别的文件夹（概念图、文档、原型）要用时再 `git sparse-checkout add 文件夹名`。
- `core.autocrlf=false`：文件和 Mac 上一字不差（不转换换行符），不会凭空多出几千个"改动"。
- 提交作者用 GitHub 的 noreply 邮箱，不要用学校邮箱：仓库是公开的。

## 6. 第一次打开、打包

命令行打包（顺带完成第一次导入，放后台跑）：

```
"/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" -batchmode -quit -projectPath F:/AntGarden/UnityProject.nosync -buildTarget Android -executeMethod AntGarden.EditorTools.VRSetup.BuildQuest -logFile F:/AntGarden/05_Builds.nosync/quest_build.log
```

或者在 Unity Hub 里：项目 → 添加 → 从磁盘添加 → `F:\AntGarden\UnityProject.nosync`，编辑器选 2022.3.62f3c1，平台选 Android；打开后点菜单 **AntGarden → Build → Quest (Android, OpenXR)**。

- 第一次要导入全部资源（约 2 GB，贴图要压成安卓的格式），可能要一小时左右；之后再打包只要几分钟。
- 成功时日志里有一行 `[AntGarden] Build Android (VR): Succeeded, … MB, … s -> …AntGarden.apk`。命令行打包失败时，Unity 以错误码 1 退出。
- 日志里 `AntGarden/Plant` 着色器的 2 个 Vulkan 编译错误是已知的：网格植物在粒子蚁穴里不显示，不影响。
- 命令行打包时，Unity 编辑器不能同时开着这个工程。
- 打开或打包之后跑一下 `git status`。Unity 不该改动已有的文件；如果改了，先别提交，告诉用户。

## 7. 装进头显、看帧率、截图、调参

```
ADB="/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
"$ADB" devices -l
"$ADB" install -r F:/AntGarden/05_Builds.nosync/AntGarden_Quest/AntGarden.apk
"$ADB" shell am start -n com.ucldfpi.antgarden/com.unity3d.player.UnityPlayerActivity
"$ADB" logcat -v time -s Unity | grep --line-buffered AntGarden
"$ADB" shell am force-stop com.ucldfpi.antgarden
"$ADB" push F:/AntGarden/05_Builds.nosync/AntGarden_Quest/记录/quest.json /sdcard/Android/data/com.ucldfpi.antgarden/files/quest.json
"$ADB" pull /sdcard/Oculus/Screenshots F:/AntGarden/05_Builds.nosync/AntGarden_Quest/记录
```

- 依次是：看头显在不在、安装（1 GB，1–2 分钟）、启动、看日志、停止、推调参文件、拉截图。
- 日志每 5 秒一行帧率，例如 `[AntGarden] quest: 71.9 fps at 72 Hz, 0 of 360 frames missed, slowest 15 ms, 364 MB in use`。`missed` 要接近 0。
- 调参：把 `Tools/vr/quest.json` 复制到 `记录\` 里改，推进头显，停掉游戏再启动就生效，不用重新打包。定下来的值再写回 `Tools/vr/quest.json`。每一项管什么、先调哪个，见 README。
- 截图：头显里按住右手柄的 Meta 键，再按扳机；或者用 MQDH 里的截图按钮。
- MQDH 也带一个 adb。两个版本的 adb 同时用，会互相把对方断开（头显时有时无）：命令行用 Unity 这个，有冲突就先关 MQDH。

## 8. 改完传到哪

| 什么 | 放哪 | 进 GitHub 吗 |
|---|---|---|
| 代码、设置、说明（`QuestLook.cs`、`Editor/VR/`、`Tools/vr/`） | 在 `F:\AntGarden` 的 `experiment/vr` 上提交 | 推到 GitHub 的 `experiment/vr`；每次推之前先问用户 |
| 定下来的头显参数 | 写回 `Tools/vr/quest.json` | 跟代码一起推 |
| 进展 | `Tools/vr/README.md` 的"现在到哪了"和"下一步" | 跟代码一起推 |
| 安装包 APK | `05_Builds.nosync\AntGarden_Quest\` | 不进 Git。测好以后，用户同意的话作为预发布版传到 Releases |
| 截图、录像、帧率日志 | `05_Builds.nosync\AntGarden_Quest\记录\` | 不进，直接发给用户 |

- 提交信息用 `feat(vr): …`、`fix(vr): …`、`docs(vr): …` 开头；Claude 写的提交，末尾照常加 `Co-Authored-By` 那一行。
- 按文件名 `git add`，不要 `git add -A` 或 `git add .`。

## 9. 不能动的

- 只改 `experiment/vr`。不碰 `main`、`experiment/particle-look` 和别的分支，不往它们合并；不用 `--force`，不改已经推上去的历史；不删分支、标签和 Releases。
- 电脑版（`experiment/particle-look`，包括 v15 的 `3d9c0c0`）只能看：不切换到它，不在上面提交，不推。怎么看见第 12 节。
- 电脑版共用的默认值和资源不改：`ParticleLook`、`SolidThings`、`GameConfig`、`game_config.json`、着色器、美术资源、场景、蚁穴路径文件。头显版的数值只在 `QuestLook.cs` 和 `quest.json` 里改。真要动共用文件，先问用户，并且像 `Game.cs` 里现有的几处一样用 `QuestLook.On` 包住，改得越少越好。
- 把电脑版的新进展合进头显版（`git merge origin/experiment/particle-look`），要等用户说；`Game.cs`、`GameConfig.cs`、`SolidThings.cs` 里标了 QuestLook 的几处要保住。
- 不删文件，不用的移到别处；不用 `git reset --hard`、`git clean`。
- 仓库是公开的：安装包、密钥、汇报 PPT、视频、访客上传的内容都不提交。
- 账号、密码、验证码（Meta、GitHub、Unity）都由用户自己输入。
- Claude Code Game Studios 模板文件夹不改：不跑 `/start` 引导，不写 `project.yaml`。
- 整个仓库的规矩见根目录 README 的第 4 节。

## 10. 头显提示"要升级到最新版"，但找不到升级

- 先看提示出在哪：头显里、手机 Meta Horizon，还是 MQDH。
- 头显里：设置 → 系统 → 软件更新（旧版系统在"通用"里）。头显要连着 Wi-Fi、插着电；没有更新就重启再看。
- 系统更新是分批推送的，还没推到这台的话，手动也点不出来。装 APK、开发者模式、adb 一般不要求最新系统。
- 实在要最新，可以加入公测频道（Public Test Channel），会提前收到测试版系统；有风险，由用户决定。

## 11. 以后在 Windows 上新开对话

工程已经在 `F:\AntGarden` 以后，新对话可以直接选这个文件夹，开场贴：

> 你接手 Ant Garden 的头显版（分支 experiment/vr，工程在 F:\AntGarden）。全程用中文回答。先读 Tools/vr/WINDOWS.md 和 Tools/vr/README.md，照里面的规矩做：只改 experiment/vr，不动电脑版共用的东西，推送前先问我。画面对照 Tools/vr/参考_v15/（电脑版蓝色蚁穴 v15，只看不改）。然后告诉我现在到哪了、下一步做什么。

## 12. 蓝色蚁穴 v15：只看、不改的参考

头显版的画面往电脑版的蓝色蚁穴 v15 靠。v15 是用户 10-25 定下的模板。电脑版只能看，不能改；改动只发生在 `experiment/vr` 上。

| 看什么 | 在哪 |
|---|---|
| v15 的截图、总览、视频帧、说明 | `Tools/vr/参考_v15/`，在 `experiment/vr` 上，克隆下来就有。网页：<https://github.com/805200470-sketch/antchitecture/tree/experiment/vr/Tools/vr/%E5%8F%82%E8%80%83_v15> |
| v15 的代码 | 提交 `3d9c0c0`（`experiment/particle-look` 上，10-25 14:42）。它在 `experiment/vr` 的历史里，克隆下来就能看。网页：<https://github.com/805200470-sketch/antchitecture/tree/3d9c0c0> |
| 现在的电脑版 | 分支 `experiment/particle-look`，Mac 上一直在改。头显版 10-29 合进过它的 `c94577c` |

**只读的看法**（这几条都不会改任何东西）：

```
git show 3d9c0c0:UnityProject.nosync/Assets/AntGarden/Scripts/Runtime/ParticleLook.cs
git diff 3d9c0c0 experiment/vr -- UnityProject.nosync/Assets/AntGarden/Scripts/Runtime/ParticleLook.cs
git fetch origin experiment/particle-look
git log --oneline origin/experiment/particle-look -20
```

- 想看现在的电脑版跑起来什么样：在 Unity 编辑器里打开 `F:\AntGarden\UnityProject.nosync`，按 Play。编辑器里用的是电脑版的参数（头显参数只在安卓上、或者加 `-quest` 时才生效），默认就是蓝色蚁穴。
- 想把 v15 本身跑起来：用 `git worktree add --detach F:/AntGarden_v15 3d9c0c0` 另开一个只读的工作目录，再用 Unity 打开里面的 `UnityProject.nosync`。会再下载一部分文件，第一次导入约一小时。在里面不提交、不推；看完以后留不留，问用户。

**推送保护**（装一次就行，除了 `experiment/vr`，别的推送一律挡下）：

```
cd F:/AntGarden
git config remote.origin.push refs/heads/experiment/vr:refs/heads/experiment/vr
cat > .git/hooks/pre-push <<'EOF'
#!/bin/sh
# This computer pushes experiment/vr only: other branches, tags and deletions are refused.
while read local_ref local_sha remote_ref remote_sha; do
  case "$local_sha" in
    *[!0]*) ;;
    *) echo "pre-push: refused to delete $remote_ref" >&2; exit 1 ;;
  esac
  if [ "$remote_ref" != "refs/heads/experiment/vr" ]; then
    echo "pre-push: this computer only pushes experiment/vr (refused: $remote_ref)" >&2
    exit 1
  fi
done
exit 0
EOF
chmod +x .git/hooks/pre-push
```

- `git config` 那一行：`git push` 不带参数时，只推 `experiment/vr`。
- 钩子：推别的分支、推标签、删远程分支，都会被拦下。另开的 worktree 也共用这个钩子。
- 不要用 `--no-verify` 绕过它。
- 两台电脑登录的是同一个 GitHub 账号，GitHub 网站上的分支保护分不出是哪台电脑推的，所以保护放在 Windows 这份克隆里。
