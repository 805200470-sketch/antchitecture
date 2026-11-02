# 头显版（Meta Quest 3 一体机）交接说明

分支 `experiment/vr`。游戏直接装进头显里跑（安卓 APK），不连电脑。
电脑版的画面和参数都不动：头显版的改动只在头显里（或桌面加 `-quest 1` 预览时）生效。
以后头显版在 Windows 那台电脑上做。

## 现在到哪了（2026-11-02，Windows）

- **11-02 早上，用户戴上测试版（手的骨骼修过）后又提的**（测试版已换成改好的，主版本没动）：
  1. **手柄和手对不上。** 头显的日志缓冲只存约 5 分钟，用户那一轮的 Unity 日志已经被冲掉，看不到当时手和手柄差多少。查代码又找到一处：手柄在每帧渲染前按最新姿态摆，手却在 Update 里由 SDK 摆，比手柄早一帧的工作量，动起来手会落在手柄后面。现在手也在渲染前用同一次读数重新读、重新摆（`MetaDevices.Pose`，调手和骨骼自己的方法）；手的网格只在启动时绑一次骨骼，绑的时候骨骼还没好就永远画不出来，现在补绑（`ForceRebind`）。另外游戏每秒往头显里写一行 `vr_track.txt`（和 quest.json 同一个文件夹）：头的位置（Unity 的相机 / SDK 的）、两个手柄（SDK / Unity XR）、两只手的根部位置、置信度、是不是由手柄生成、离手柄多远、网格中心在哪。不插线测也能事后拉回来看。还要用户戴上再测。
  2. **手柄上不要再强调 X、Y、A、B。** 浮在按键上的放大字母和教程让字变大闪的提示都删了（`IVRDevices.Hint` 也删了）；Touch Plus 模型贴图上本来就印着字（≡ X Y、A B），照原样显示。
  3. **第一人称视点稍高一点。** `eyeAbove` 0 → 0.06：蚂蚁的眼睛在身长的 0.27 高，视点现在 0.33（站着看，地面从眼下约 1.08 米变成约 1.32 米）。
  4. **第三人称用右摇杆转方向，要顺滑、连续；转的是蚂蚁的朝向，镜头跟着蚂蚁的朝向**（早上先做成了只绕着蚂蚁转镜头，晚上用户纠正：「右边旋钮控制的是蚂蚁的朝向，镜头是和蚂蚁朝向对应的。不是只控制镜头」）。第三人称里推右摇杆，蚂蚁连续转向（`turnSpeed` 推到底每秒 60°，过了小死区越推越快，起停各约 0.2 秒缓冲，没有一格一格的跳），镜头始终在它正后方跟着转（`VRPlayer.Follow`）。左摇杆在第三人称只往前爬（朝蚂蚁的方向），左右推也是转向，不后退：要是像第一人称那样朝头看的方向爬，蚂蚁一转、镜头跟着转、爬的方向又跟着镜头转，会原地打转。切到第三人称时镜头对准当时看的方向（在眨眼里，看不到跳）。第三人称里转头不再让蚂蚁跟着转，也去掉了「头转开约 1 秒就眨眼把镜头放回蚂蚁身后」（`behindSwing`），那一下就是一顿。第一人称不变（看哪转哪）。教程「换个视角」一步和使用说明各改了一句。
  5. **晚上：手和手柄还是对不上。** 拉回 `vr_track.txt` 一看：头（相机和 SDK）完全一致，手柄位置对；没拿手柄的手骨骼在动、跟着真手；拿着手柄时系统已经按手柄生成了手的姿态（`from-ctrl 1`、置信度高），骨骼却不动（`bones still`），左手一直停在原点（就在眼前），右手停在拿起手柄前最后的位置。原因：Meta 的 `OVRHand` 默认 `m_showState = ControllerNotInHand`，只在没拿手柄时算数据有效，拿着手柄就不更新骨骼，但「被追踪、置信度高」照样是真，而我只用这两个判断显示，就把停住的手画了出来。现在设成 `Always`：拿着手柄的手按手柄生成的姿态包在手柄上；显示还要求数据有效，停住的手不会再画出来。用户再测：**对上了**。`vr_track.txt`（`记录/1102_user_track_2.txt`）：拿着手柄时两只手都由手柄生成、骨骼在动，手腕离手柄 0.11–0.12 米且很稳；放下手柄的手跟着真手动。
  - 使用说明多了一行，按头显的字体估算会超出卡片底边：卡片加高（830 → 900），说明和教程卡片的字改成放不下时自动缩小（`VRTutorial.Fit`），不会再漏到卡片外面。
  - 自动测试没跑成：头显平放在桌上，系统弹「正在寻找房间中的位置」，所有应用都起不来，要有人戴上。这次的改动还没在头显里看过。
  - 11-02 用户那轮测试中两次打开系统菜单后，内核日志里有游戏的 `GPU PAGE FAULT`（10 行），游戏没退出。和下一步第 3 条是同一类问题。

- **11-01 晚上，用户戴上测试版后又提的**（测试版已换成改好的，主版本没动；头显截图在 `记录/低头_第二轮.png`、`记录/实测_1101晚_v5.png`）：
  1. **第一人称里自己的蚂蚁更透明，只看得出轮廓；爬起来还会挡、和镜头不同步。** 原因：蚂蚁起步时低头、转弯时侧倾、随坡度倾斜，镜头不跟着倾斜，高度还比身体慢半拍（`heightEase`），身体就在眼前晃。现在第一人称里自己的身体不再由 `ParticleAnts` 画，改由 `VRSelf.cs` + `Resources/AntGarden/VR/VRGhostAnt.shader` 画：骨骼动作照旧（腿迈步、触角摆），但整体跟着镜头走（水平、同样的缓动高度、去掉起步低头和转弯侧倾）；半透明，越是侧对眼睛的点越实，正对眼睛的几乎透明，离眼睛很近的部分（脸、触角根）不画，眼睛的点不画。`selfAlpha` 0.8（轮廓的不透明度）、`selfSize` 0.5（点的大小，小一点轮廓更细）、`selfPoints` 0.5。头显实测：低头 65° 腿是一串串半透明的亮点勾出的轮廓，里面是空的；平视时看不到身体。第三人称时仍由 `ParticleAnts` 画完整的蚂蚁。
  2. **小地图更靠上，不挡视线中心。** 挪到约右 24°、上 21°（原来上 6°）。对话条（如「使用说明」）底色加深到和卡片一样，压在亮的植物上也看得清。
  3. **第三人称按摇杆时镜头偏移不自然、乱动。** 原因：镜头跟在蚂蚁后面有延迟（爬时落后、停了再追上），蚂蚁转向时镜头还绕着它滑过去，这些都是玩家没操作的运动。现在镜头和蚂蚁一起平移，不延迟、不自己绕；头转开超过 `behindSwing`（60°）约 1 秒，眨一下眼把镜头放到蚂蚁身后（朝头看的方向；11-02 去掉了，改成右摇杆连续转，见上）。去掉了 `behindEase`、`behindTurn`。实测：自动爬行时三张截图里蚂蚁停在画面同一位置。
  4. **手柄看不清按键。** 手和手柄换成系统追踪（用户选了 A，Meta XR Core SDK，见下一条）：手柄画成 Meta 自己的 Touch Plus 模型，按键上的字（X、Y、≡、A、B）放大浮在按键上，教程等哪个键，哪个键的字就变大、变暖色闪（11-02 用户说不要，删了，见上）；照着手柄捏的假手删掉了。
  5. **电脑版先不动**（终点的门那个问题，`Game.cs`）。
  - 打包时 Unity 两次因为内存不足崩溃（`d3d11 … 8007000e`，这台电脑开着很多浏览器窗口，提交内存只剩约 12 GB），加 `-nographics` 打包就正常了（约 50 秒），打出来的包在头显上跑得正常。
  - 头显刚唤醒的第一轮测试常被系统的边界对话框挡住、游戏起不来，第二轮起正常：自动测试前先跑一轮热身。

- **手和手柄换成系统追踪（11-01 晚，用户选 A：Meta XR Core SDK 201.0.0）**，只在测试版：
  - 包：`Packages/manifest.json` 加 Meta 的包源（`https://npm.developer.oculus.com`，不用 Unity 账号）和 `com.meta.xr.sdk.core` 201.0.0（支持 Unity 2022.3 的最后一版，203 起要 Unity 6）；OpenXR 1.10.0 → 1.14.0（Meta 自己的样例用这个组合）。连带进来 `com.unity.xr.hands` 1.7.2。第一次导入约 3 分钟。
  - 设置（打包时 `VRSetup.ConfigureQuest` 自动设）：OpenXR（Android）开 `Meta XR Feature`，Unity 的 `Meta Quest Support` 保留；`Assets/Oculus/OculusProjectConfig.asset` 手部追踪 `ControllersAndHands`、频率 `LOW`（HIGH/MAX 就是 Fast Motion Mode，不能和「手柄和手同时」一起开；身体、脸、眼追踪都关）。Unity 的 OpenXR 1.14 会无条件往 manifest 里加眼动追踪（Quest 3 没有），`Editor/VR/QuestManifest.cs` 打包时删掉。打出来的 APK 只多了 `HAND_TRACKING` 权限和「手部追踪（非必需）」。
  - 代码：输入层和游戏逻辑分开。`Scripts/Runtime/VRDevices.cs` 是接口（`IVRDevices`：每帧给出摇杆、按键、扳机、捏合；游戏逻辑在 `VRPlayer` / `VRTutorial` / `Game` 里决定它们做什么）；`Scripts/VR/`（单独的程序集 `AntGarden.VR`，共用的 `AntGarden.Runtime.asmdef` 没动）里 `MetaDevices.cs` 用 Meta 的 SDK 实现它。没有 Meta 运行时（电脑上 Quest Link、电脑预览）时用 `XRDevices`（Unity 的 XR 输入）和原来画的手柄。
  - `MetaDevices`：运行时建 `OVRManager`（手柄和手同时追踪 Multimodal；拿着手柄的手由手柄的传感器生成、包在手柄上，空着的手由摄像头追踪；追踪原点和相机一样是眼睛高度；不重新居中）。手柄：Meta 的 `OVRControllerPrefab`（Quest 3 是 Touch Plus，按键会跟着按下动），每帧渲染前按 SDK 给的位置摆在 VR Origin 下；材质换成自发光的 `VRModel`（Meta 的光照材质在蚁穴里几乎是黑的）。手：`OVRHandPrefab`（系统的手部网格和骨骼），用 `VRGhostHand` 画成主界面那样的深色半透明加亮边；每帧查追踪状态和置信度，丢了或置信度低就停在最后的姿势、0.25 秒淡出，重新看到再淡入。捏合（只认摄像头看得清的空手，不认包在手柄上的手、不认正在做系统手势的手）有迟滞：强度过 0.8 算按下，低于 0.5 才算松开。右手拇指捏食指 = A、捏中指 = B；左手捏住食指 = 往前爬（朝头看的方向）、捏中指 = Y；左手的系统菜单手势 = 菜单。使用说明里加了这一行。
  - 删掉的自己写的部分：`VRHands.cs` 的 `Hand()`（照手柄捏的固定假手）和只给它用的 `Capsule()`，`ghostHands` 设置，`VRGhostHand.shader` 里靠顶点颜色淡出的写法。整个项目里从来没有摄像头画面、图像识别或猜手位置的代码。
  - 没用 Meta Interaction SDK：它要 `OVRCameraRig`，而这里的相机是游戏自己的、挂在缩小的 VR Origin 下；只要显示手和手柄、读捏合和置信度，Core SDK 的 `OVRHand` 就够了。
  - 头显实测（没人戴，手柄在桌上）：能启动，日志 `VR input: Meta XR Core SDK (OVRPlugin 1.201.0); hands with controllers supported; drawn: controllers True, hands True, 5 button letters`；帧率和加 SDK 前一样（约 29.5）；颜色和加之前的截图一样。手柄模型、手、捏合要人戴上才能验证。
  - 用户戴上实测（11-01 夜）：手柄位置对，手横在视野正中、对不上手柄。原因：运行时生成的 `OVRHandPrefab`，骨骼还是预制体里存的旧类型（OVR 手），而这台头显用 OpenXR 手部骨骼；编辑器里把预制体放进场景时会自动改，运行时生成没人改，骨骼找不到数据、一直不动，手的网格停在追踪原点（眼睛那里）的默认姿势。现在生成时按手本身的类型设骨骼类型和数据来源（`MetaDevices.Hand`），第一次看到每只手时日志记下手和手柄的位置（`VR: left hand seen …`），等用户再测。
- **11-01 演示后的反馈**，都只改头显专用的文件（共用文件只在运行时改、不改文件）。新版先打成「Ant Garden 测试版」（`com.ucldfpi.antgarden.test`，`ANTGARDEN_VARIANT=test ANTGARDEN_VARIANT_NAME=测试版`）单独装，测过再替换主版本；10-31 带手柄的主版本另存为 `AntGarden_v2_1031_hands.apk`。
  0. **手和手柄。** 排查结果：头显和手柄的位置本来就读系统（OpenXR，`InputDevices`），没有任何图像识别；但手是照着手柄捏的固定假手（`VRHands.cs` 的 `Hand()`），手柄外形也是仿的。要换成系统的手部追踪，两条路等用户选：A. Meta XR Core SDK 201.0.0（Unity 2022.3 能用的最后一版，Meta 的包源这台电脑连得上；拿着手柄时也有系统给的手，可开 Multimodal）；B. XR Hands 1.8.1（拿着手柄时没有手；「手柄推出来的手」要 XR Hands 1.9，只支持 Unity 6）。
  1. **文字歧义。** 头显里站着玩，摇杆让蚂蚁「爬」，不再说「走」；「走出蚂蚁的身体」改成「换到蚂蚁身后看（第三人称）」；去掉「心脏」（10-27 起就没有心脏了）。游戏自带的目标「跟着地上的光走」在头显里显示成「用左摇杆，沿着地上的光爬」（`VRTutorial.Crawling`）。
  2. **走两步场景消失。** 是头显的安全边界：走出边界，系统把游戏藏起来、显示房间（安全功能，不能关）。现在站着玩时离开起点 0.6 米就提醒「请回到原地」（`strayWarn`）；按住 Meta 键重新居中后，从新的位置重新算（以前重新居中后视角会乱）。演示时可以在头显里把边界设大一点。
  3. **光路引导。** 教程最后一步「出发」：蚁穴压暗 65%，地上的光点和光丝亮 2.5 倍、照得更远，10 秒后恢复（`VRPathFocus.cs`，`focusDark`、`focusTime`）。暗幕是罩住视野的球（和防晕暗角同一个着色器），排在地面、植物、蚂蚁之后，发光的东西（光点、光丝、光尘）和文字之前；玻璃灯排到暗幕下面。都是运行时改。
  4. **回来后世界变成非粒子。** 起点蚁穴同时是列表里的「主世界」，所以 `Game.BuildWorldGates` 给它的两个出口都配了通向遗迹的光门（日志每次都是 `world gates 2 (exits 2): path-003@blue#relics, path-010@amber#temple`），光路终点那道门也在其中；遗迹不是粒子蚁穴，是普通模型。现在头显里终点的门通向下一个粒子蚁穴（azure），别的门回起点蚁穴（`QuestLook.Tune(Game)`）。电脑版这个分支同样有这个问题（`Game.cs` 第 1155 行），要改共用文件，先问用户。
  5. **第一人称。** 视点挪到蚂蚁两眼之间、脸前一点（用点云文件里标成「眼睛」的点算位置，`eyeAbove` 0、`eyeAhead` 0.02），不再在头顶上「骑着」；身体只画 35% 的点、深色底身不画（`bodyFade`），触角不再挡视线。自己的身体用全部点的 50% 画（其他蚂蚁 4.8%），点更小，低头看腿不再是一颗颗大圆片（`selfPoints`）；脚下的青色圈只在第三人称画。（晚上又改成半透明轮廓、跟着镜头走，`bodyFade` 换成 `selfAlpha`，见上面第 1 条。）
  6. **字体和教程。** 教程卡片字号 32 → 40（标题 48），再放大 1.35 倍，约是 10-31 的 1.7 倍；说明卡片 34 号；目标 ×2.3，提示 ×2.2，对话 ×1.9，地区名 ×1.5。卡片和提示条 95% 不透明，使用说明 100%（半透明的深色底在线性颜色空间里挡不住后面的亮字）；卡片在正前方偏下（约 0° 到 -13°），有对话时先让开，教程暂停。左下角的帧率条在头显里不显示。教程只在这台头显第一次玩时出现（`tutorial`：`auto` / `always` / `never`），扳机跳过；之后按菜单键看「使用说明」，在说明里扣扳机可以重看教程。扳机按下、松开用两个阈值（0.7 / 0.3）。
  7. **小地图。** 恢复右上角的小地图（以前在头显里藏起来了），挪进视野（约右 24°、上 6°），放大 1.4 倍；箭头是蚂蚁的实时位置和朝向，再放大 2 倍。
  8. **眩晕。** 蚂蚁爬时视野边缘变暗（`comfort` 0.6，`VRComfort.cs`，38° 以内始终清楚），视线跟着蚂蚁上下更柔和（`heightEase` 6 → 4）。最主要的原因还是帧率（约 30 fps，头显靠补帧），见「下一步」。
  - 测试用的开关：`tutorialFrom`（教程从第几步开始，5 = 出发，-1 = 直接打开使用说明）、`testCrawl`（没人戴时蚂蚁自己往前爬几秒）。
  - **电脑上预览头显画面**（`VRPreview.cs`）。11-01 中午头显放在暗处没人戴，追踪不到房间，系统的「正在寻找房间中的位置」挡在最前，游戏起不来。现在不用头显，也能在电脑上按头显的样子出图：蚂蚁眼睛的视角，加上头显里的文字和小地图。每轮 4 张图，约 20 秒。
    - 打包：`VRSetup.BuildVRPreview`（加 `-buildTarget Win64`），输出到 `05_Builds.nosync/AntGarden_VRPreview`，约 1 分钟。
    - 运行：`XR_RUNTIME_JSON=C:/no-openxr-runtime.json AntGarden.exe -vrpreview <出图目录> -quest <quest.json> -screen-fullscreen 0 -screen-width 1032 -screen-height 1104`。`XR_RUNTIME_JSON` 指向一个不存在的文件，OpenXR 就不会去连插在电脑上的头显。
    - 局限：头固定不动，只能朝前看，或用 `lookDown` 低头；没有摇杆和按键。
  - **下午用电脑预览自查后又改的**（已写进上面第 3、5、6、7 条）：
    - 字还是小，卡片底色太透（后面的粒子像噪点），卡片和小地图重叠。
    - 第一人称低头时，自己身体的点离眼睛太近，一颗颗变成大圆片；脚下的青色圈绕着蚂蚁一圈，正好穿过视线。
    - 光路的暗幕原来在文字画布里，光点要排到画布之后，会画到卡片上。
    - 教程第 2 步去掉「眼前是你的触角」：视点挪到脸前以后，触角不在正前方。
- **10-31 早上，用户戴上看过（整体满意）后提的 6 条**，都只改头显专用的文件：
  1. **手柄。** 照头显自带主界面（用户叫它「虚拟阁」）的样子画两只手柄：圆形深色的头（摇杆、两颗按钮、菜单键、三颗绿灯）、灰色握把，外面一只半透明、白色描边的手（`VRHands.cs`，着色器在 `Resources/AntGarden/VR/`）。系统只在自己的主界面里画手柄，游戏里要自己画。`hands` / `ghostHands` / `handLight`。
  2. **看哪转哪。** 头转开超过 40°、停 0.5 秒，蚂蚁就转过去；推左摇杆朝看的方向爬；右摇杆不再转身（`turn` 改成 `stick` 可以恢复）。身后视角：蚂蚁爬起来时画面才慢慢绕到它身后，只转头时画面不动。
  3. **蚂蚁的点。** 每只 16 万点只画 4.8%（约 7700 点），每颗相应放大，和周围植物近处的颗粒差不多大（`ants`）。`ParticleAnts.cs` 是共用文件，没有份额参数，所以由 `QuestLook` 用反射改它读进来的点数，共用文件没动。
  4. **第一人称看得到自己。** 眼睛从蚂蚁身体中间挪到头顶稍前（按骨骼里头的位置算，`eyeAbove` 0.15、`eyeAhead` 0.07），蚂蚁的身体显示出来：头在视野下方，触角伸在前面，低头看到脚。蚂蚁转身时绕着眼睛转，画面不会被甩动（`body`）。
  5. **精度。** 主版本换成单体优先（`shell` 0.3、`plants` 0.45、`things` 0.45）；画质版再往上（`plants` 0.6、`shell` 0.42、`things` 0.6）；流畅版不变（`all` 0.4）。用户问能不能在 Unity 里预先烘焙：粒子本来就是加载时生成一次，不是每帧生成；每帧的开销在 GPU 填像素上，烘焙只能缩短 16 秒的加载，帧率不会变。
  6. **潮汐。** 植物摇摆 `wave` 1.6 → 2.6，阵风的波长 `swell` 拉长到 1.5 倍（新），阵风速度 `gusts` 1.1，地面涟漪 `ripples` 1.6 → 2.4。
  - 打包通过（主版本 81 秒，两个副本各约 55 秒），新的主版本已装进头显；**还没在头显上跑过**：房间太暗，头显一唤醒就弹边界对话框，没人戴时游戏起不来。画质版、流畅版的新包打好了还没装，头显里那两个仍是 10-31 凌晨的旧版，可以当备用。上一版演示包另存为 `05_Builds.nosync/AntGarden_Quest/AntGarden_v1_1031_demo.apk`。
- 以下是 10-30 到 10-31 凌晨的进展。
- Windows 上能打包、装机：第一次打包（含全部导入）14 分钟，之后改了脚本再打包约 1.5 分钟；APK 1.0 GB。
- 第一次上真机（10-30 上午，旧的默认参数，`all` 1）：出画面后 20–28 fps，72 Hz 下几乎每帧都没赶上。
  - GPU 每帧 23–35 ms（72 Hz 的预算是 13.9 ms），GPU 占用到 99%，CPU、GPU 都在最高档：瓶颈在 GPU。
  - 加载 16 秒，其中读路径数据 9 秒。
- 做成了能玩的游戏（只改了头显专用的文件）：
  - `VRPlayer.cs`：Y 在蚂蚁的眼睛和它身后（第三人称）之间切换，切换时眨一下眼；X 切换中文 / English；左手柄菜单键看全部操作。
  - `VRTutorial.cs`（新）：眼前的教程卡片，一步一步教：爬行 → 转身 → 换视角 → 触角（B）→ 打招呼（A）→ 跟着光走，每一步做到了才往下走。屏幕提示里的「按 E」在头显里写成「按 A」；目标、提示、对话放大，小地图不显示。
  - `QuestLook.cs`：植物摇摆 ×1.6、阵风 ×1.25、地面涟漪 ×1.6、声音 ×1.6；粒子预算集中到单体上（见下）。
  - 走动方式（10-31 加）：默认站在原地用左摇杆爬，头只能探出 `leanRadius`（0.3 米）；按下右摇杆切换「真实走动」，迈步时蚂蚁跟着爬（`walk`：`stick` / `real`）。
- 10-31 凌晨实测（开局画面，头显不动，GPU 占用都在 95% 以上）：
  - 粒子总量对帧率影响很小：`all` 0.4 / 0.6 / 0.8 是 36 / 33 / 31 fps。粒子少时每颗被放大来盖住表面，GPU 主要花在填像素上。
  - 所以把预算集中到单体上很划算：近处细节 `shell` 0.3、植物粗点 `plants` 0.45、物件 `things` 0.45，只慢 5–6 帧，植物的颗粒明显变细。
  - 调色后期占 2.5 ms，关掉颜色变灰，保留。远处巨树和地面亮点只占 1 ms。地毯点变小变密（`carpet` 1.5）反而慢 2 ms。
  - 降分辨率三条路都走不通：运行中改 `eyeTextureResolutionScale` 会崩；启动前改会被 XR 按头显尺寸重建贴图覆盖掉；`renderViewportScale` 让游戏启动 3 秒就退出。`resolution` 现在不生效。
- 三个版本并排装在头显里对照，应用库里各是一个（10-31 早上起）：
  - Ant Garden（主版本，`quest.json`）：单体优先（`shell` 0.3、`plants` 0.45、`things` 0.45，`far` 0.02、`giants` 4、`ground` 0.08）。
  - Ant Garden 画质版（`quest_quality.json`）：再往上（`plants` 0.6、`shell` 0.42、`things` 0.6）。
  - Ant Garden 流畅版（`quest_smooth.json`）：原来的均衡参数（`all` 0.4、`shell` 0.1、`plants` 0.2）。
  - 打包：`ANTGARDEN_VARIANT=smooth ANTGARDEN_VARIANT_NAME=流畅版`，再跑 BuildQuest。

### 在这台 Windows 上测试时的坑

- 这台电脑装着 Meta Horizon 电脑端：头显插 USB 会弹「Quest Link 可用」，一点就切进 Link，游戏被压到后台。测之前在头显 设置 → 系统 → Quest Link 关掉。
- Autodesk 的 `AdSSO.exe` 占着 adb 的默认端口 5037：adb 命令前设 `ANDROID_ADB_SERVER_PORT=5038`。
- Git Bash 会把 adb 命令里的 `/sdcard/...` 改写成 Windows 路径：先 `export MSYS_NO_PATHCONV=1`。
- 边界对话框（GuardianDialogActivity）会挡住游戏：房间暗了、头显被挪动、或者息屏后再唤醒时都可能弹出。弹出时游戏打不开，或者加载到一半停住。10-31 凌晨单体优先几次没进入游戏，可能就是它（也可能是头显太热）；没有对话框时单体优先 4 次都正常（30.1 fps）。测之前先看 `adb shell dumpsys activity activities | grep GuardianDialogActivity`。
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

- 11-01 起工程里有 Meta XR Core SDK：第一次打开要能连上 `https://npm.developer.oculus.com`（Meta 的包源，写在 `Packages/manifest.json`）和 packages.unity.cn。
- 这台电脑内存紧（开着很多浏览器窗口）时 Unity 打包会崩（`d3d11 … 8007000e`）：命令行加 `-nographics`（`-batchmode -nographics -quit`），打出来的包一样能用。崩了会把 `ProjectSettings.asset` 留在改了一半的状态，先 `git restore` 它再打。
- 打完 `ProjectSettings.asset` 里的 `preloadedAssets` 会多一项（Meta 的 `OculusRuntimeSettings`，打包时加的），不用提交，`git restore` 即可。

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
| `ants` | 蚂蚁的点，电脑版每只 16 万点的几分之几（再乘 `all`）；点少了每颗画大 |
| `swell` | 阵风的间距，电脑版的几倍：越大波越长，像潮汐 |
| `turn` / `gazeDeadZone` / `gazeDwell` | 第一人称怎么转身：`gaze` 看哪转哪（头转开超过多少度、停几秒，蚂蚁转过去）；`stick` 右摇杆一下转 30° |
| `turnSpeed` | 第三人称：右摇杆让蚂蚁转向（镜头跟在它正后方），推到底每秒转多少度（60）；连续转，越推越快，起停各约 0.2 秒缓冲。觉得晃或一顿一顿的就调小 |
| `body` / `eyeAbove` / `eyeAhead` | 第一人称看得到蚂蚁自己的身体；视点在它两眼的高度上方多少（0.06）、脸前多少（0.02）（蚂蚁身长为 1） |
| `selfAlpha` / `selfSize` | 第一人称时自己身体的轮廓有多实（0.8；越小越透明）/ 点多大（0.5，越小轮廓越细）。身体跟着镜头走，见 `VRSelf.cs` |
| `heightEase` | 视线跟着蚂蚁上下的快慢：越小越柔和 |
| `comfort` | 蚂蚁爬时视野边缘变暗的程度（0 关），防晕 |
| `strayWarn` | 站着玩时离开起点多少米提醒回去（0 关） |
| `tutorial` | 新手教程：`auto` 这台头显第一次玩时出现；`always` 每次；`never` 不出现 |
| `focusDark` / `focusTime` | 教程「出发」那一步压暗蚁穴的程度、持续几秒（0 不压暗） |
| `hands` / `handLight` | 画手柄和手（Quest 上是系统追踪的手和 Meta 的手柄模型）/ 亮度 |
| `lookDown` | 测试用：画面往下低多少度（没人戴时截图看脚），玩的时候是 0 |
| `tutorialFrom` / `testCrawl` | 测试用：教程从第几步开始（配 `tutorial` `always`；-1 直接打开使用说明）；没人戴时蚂蚁自己往前爬几秒。玩的时候都是 0 |
| `selfPoints` | 第一人称时，自己的身体用全部点的多少（0.5）；越多点越小。其他蚂蚁用 `ants` 的份额 |

## 下一步

1. **帧率还远低于 60。** 主版本约 30 fps，GPU 每帧约 30 ms（60 帧要 16.7 ms），而且主要花在填像素上，减粒子省不了多少。剩下的办法都要动共用的东西，先问用户：粒子着色器里限制近处颗粒的大小（少填像素），或者换渲染管线 / 单通道实例化。
2. **戴上头显走一遍教程。** 11-01 的改动只在电脑预览里看过，还没在头显里看过：字够不够大、卡片挡不挡路、小地图的位置、低头看自己的腿、光路压暗时找不找得到光、爬行时的暗角（`comfort`）。Y 的第三人称（离蚂蚁多远多高、会不会穿进墙里，`behindBack`、`behindUp`、`behindScale`）、X 中英文、菜单键、B 碰亮植物、A 打招呼，中文在头显里显示得对不对。
3. **按 Meta 键再回来、摘下头显再戴上**：游戏要能接着跑。10-30 被 Link 切走时，日志里有 GPU page fault（`vk_devicememory was already freed`）。
4. **对照 v15**（`参考_v15/`）：近处颗粒（预算已经往近处挪：`shell` 0.12、`fine` 2，Mac 预览里近处像一片片圆片）、植物微光、光丝和光柱、空中光尘。
5. **辉光。** `grade` 下没有辉光，发光点会显得硬。试 `bloom` 的开销；太贵的话，以后在粒子着色器里做假辉光。
6. **烘焙。** 整个蚁穴在加载时生成一次，只有蚂蚁周围两圈细节（`shell`、`groundShell`）边走边在后台线程重做；帧率卡在 GPU 填像素，烘焙帮不上帧率（10-31 已回答用户）。只有想缩短 16 秒的加载时，才值得把生成好的点存成文件。
7. **手和手柄剩下的验证**（位置 11-02 晚用户确认对上了）：放下手柄的手转到背后或挡住时停住再淡出；放下手柄，右手捏食指 / 中指 = A / B，左手捏住食指往前爬、捏中指 = Y，左手系统菜单手势 = 菜单。测完拉回 `vr_track.txt`（`adb pull /sdcard/Android/data/com.ucldfpi.antgarden.test/files/vr_track.txt`），看 `apart`（手根到手柄，包在手柄上时应该在 0.1 米上下）、`mesh at`、`head` 和 `sdk` 是否一致。第三人称右摇杆转得快慢调 `turnSpeed`；第一人称的视点调 `eyeAbove`、`eyeAhead`，自己身体的透明度调 `selfAlpha`、`selfSize`。
8. **电脑版也有「终点的门通向遗迹」**（`Game.cs` 第 1155 行：起点蚁穴兼作主世界）。头显里已经绕开；电脑版用户说先不动（11-01 晚）。
9. **眩晕**：帧率是主因。已经接了 Meta SDK，可以试它的固定注视点渲染（OpenXR 里的 `Meta XR Foveation`，现在关着）（周边少画像素）提帧率；`comfort` 太暗或不够可以在 0.3–0.8 之间调。

## 规矩

- 不改电脑版共用的默认值（ParticleLook、SolidThings 等的字段）。头显版只在 QuestLook 里改。
- 合并主分支 `experiment/particle-look` 时，要注意 Game.cs、GameConfig.cs、SolidThings.cs 里几处标了 QuestLook 的小改动。
- 仓库是公开的：安装包不提交，密钥不提交。

## 已知风险（还没在真机上验证）

- 多通道（multi-pass）立体渲染：每只眼睛各画一遍。这样着色器不用改，但画的量翻倍。
- Quest 3 是通过反射加进目标设备列表的（这个版本的 OpenXR 插件默认只列到 Quest Pro）。没加上的话，Quest 3 会按 Quest 2 的性能档跑。
- `LabNest` 的引路场贴图是 RGFloat 格式，用的是双线性采样，个别手机芯片不支持。
- 在头显里长按 Meta 键重新居中之后，视角可能偏一点。
