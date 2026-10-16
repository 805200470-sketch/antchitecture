# UnityProject.nosync · 游戏本体（Unity 工程）

- **版本**：Unity **2022.3.62f3c1**（国内版 Hub）。用别的版本打开会改坏工程设置。
- **打开**：Unity Hub → Add（添加）→ 选这个文件夹。第一次打开要几分钟生成缓存（`Library/`，不在仓库里）。
- **运行**：打开 `Assets/AntGarden/Scenes/Main.unity`，点 Play。世界是运行时生成的，场景里本来就几乎是空的。
- **打包**：菜单 **AntGarden → Build → Windows (x64) / macOS**。

| 位置 | 内容 | 可以改？ |
|---|---|---|
| `Assets/AntGarden/Scripts/Runtime/` | 游戏代码（每个文件开头都有说明，按功能找文件见根目录 README 第 3 节） | ✅ 照协作规则改 |
| `Assets/AntGarden/Scripts/Runtime/Voices.cs` | 台词、遗物脚本、访客故事 | ✅ 随便改文字 |
| `Assets/AntGarden/Scripts/Runtime/GameConfig.cs` | 全部可调参数和默认值 | ✅ 只改数值也可以用 `game_config.json` 覆盖 |
| `Assets/AntGarden/Scripts/Core/` | 核心算法（路径场、峡谷、蜂巢拼接），和网页原型逐位对照 | ⛔ |
| `Assets/AntGarden/Editor/` | 打包、资源构建（Build Assets）、工程设置 | 谨慎 |
| `Assets/AntGarden/Shaders/` | 地形、植物、琥珀、树根等着色器 | 谨慎 |
| `Assets/AntGarden/Art/Ants/` | 蚂蚁模型和动画（交付件） | ⛔ |
| `Assets/AntGarden/Art/Plants/` | 植物、遗迹道具的 FBX 和贴图；`plant_looks.json` 可改，`plants.json` 由工具生成 | 部分 |
| `Assets/AntGarden/Art/Ground/` | 地面贴图（蓝色、琥珀两套） | 先出审核图 |
| `Assets/AntGarden/Generated/` | 工具生成的材质和清单 | ⛔ 手改会被覆盖 |
| `Assets/StreamingAssets/Paths/` | 每个蚁穴的路径数据（JSON） | 加新蚁穴时放这里 |
| `Assets/StreamingAssets/game_config.json`（可选） | 不改代码就能覆盖的参数 | ✅ |
| `Packages/`、`ProjectSettings/` | 包和工程设置（用了 Post Processing、新输入系统） | ⛔ |
