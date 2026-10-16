# 04_Paths · 蚁穴路径

| 子文件夹 | 内容 | 可以改？ |
|---|---|---|
| `Images/` | 13 张原始路径图（线稿，每张一个六边形蚁穴模块） | ✅ 可以加新图 |
| `Networks/` | 识别出的路径数据（JSON，Path Recognize 的输出格式） | 由工具生成 |
| `Path_Recognize_Package/` | 路径识别工具（浏览器里运行） | ⛔ 不要改 |

**加一个新蚁穴**：路径图放进 `Images/` → 用 `Path_Recognize_Package` 识别 → 得到的 JSON 改名为 `path-NNN.json`，复制到 `UnityProject.nosync/Assets/StreamingAssets/Paths/` → 在 `GameConfig.cs` 的 `defaultWorld` 里加上它。操作说明里有"加入新的蚁穴路径"一节。
