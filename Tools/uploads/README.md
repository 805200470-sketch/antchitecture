# Tools/uploads · 访客上传用的工具

访客上传的真实内容（照片、文字、用它们做的模型）**不进仓库**。它们放在本机的 `08_Uploads.nosync/`，见主 README 第 1.2 节。这个文件夹里只有工具和一份测试样例：

- `glb_to_mesh.py`：把 Tripo 生成的 GLB 转成游戏读的 `.mesh`，加一张 `_tex` 贴图（最大 2048）。
  `python3 Tools/uploads/glb_to_mesh.py <model.glb> <输出路径不带扩展名>`
- `sample_collection/`：一份测试样例，格式和访客导出一样（`manifest.json`、`cutouts/`），另加 `placement.json`。只有两件项目自己画的测试物件：一张测试卡片（做成薄板）、三块测试卵石（做成石头），都放在蓝色世界。用它检查摆放、光圈、潮汐的光和卡片，不需要任何访客内容：
  - 电脑：`-world lab:path-012@blue -uploads Tools/uploads/sample_collection`
  - 头显：把这个文件夹推到应用的 files 目录，改名叫 `Uploads/`：
    `adb push Tools/uploads/sample_collection /sdcard/Android/data/<包名>/files/Uploads/sample_collection`

`placement.json` 的字段写在 `Scripts/Runtime/Uploads.cs` 的 `Entry` 里。
