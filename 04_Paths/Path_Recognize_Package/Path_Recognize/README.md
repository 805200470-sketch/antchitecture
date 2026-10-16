# Path_Recognize

**1.1.0 · 中英文可切换界面，默认中文版。**

蚁穴图像识别与中心线提取模块。这里包含此前网页的完整可编辑源码、示例图片、独立调用接口、接口接收示例和测试。识别在浏览器本地执行，使用传统图像处理，不调用 AI、云端 API 或后端服务。

## 直接打开运行

**macOS：双击本目录的 `Start.command`。** 它会启动本地服务器并打开完整网页。使用结束后在终端按 `Control+C` 关闭服务器。

也可以在此目录打开终端，运行：

```sh
node scripts/serve.mjs --open
```

需要 Node.js 20 或更高版本；**不需要 `npm install`**。如果只有 Python 3，可运行 `python3 scripts/serve.py`。Windows / Linux 可以使用相同的 Node 命令。

- 完整工作台：<http://127.0.0.1:8765/dist/>
- 独立接入演示：<http://127.0.0.1:8765/examples/>
- 端口被占用时：`node scripts/serve.mjs --port 8766 --open`

`examples/results/` 另附两张示例图片的真实网络 JSON，Claude 不启动页面也能先阅读完整输出结构。

请通过上述服务器地址访问，直接双击 `index.html` 的 `file://` 方式无法正常加载模块与 Worker。移动或重命名项目文件夹后仍可运行，保持内部目录结构即可。

## 中英文界面与新版布局

右上角点击 **中文 / EN**，整套工作台和接入演示会切换语言。第一次打开默认中文；浏览器会记住上次选择。切换语言保留当前图片、ROI、识别结果、参数和撤销记录。

新版扩大了画布，将设置按「区域 → 提取 → 校正 → 导出」分组；桌面右侧设置区独立滚动，主要操作固定在底部。手机采用上下布局。

界面预览：[中文工作台](docs/preview.png) · [English workspace](docs/preview-en.png) · [手机界面](docs/preview-mobile.png)。

## 交给 Claude

让 Claude 打开整个 `Path_Recognize` 文件夹，先读 [CLAUDE.md](CLAUDE.md)，然后读 [接口说明](docs/INTEGRATION.md)。Claude 可以直接修改全部源码，并将其接入 React、Vue 或其他网页工程。

可以直接把这段话交给 Claude：

> 请先阅读 Path_Recognize/CLAUDE.md 和 docs/INTEGRATION.md，运行现有测试与 examples 接入演示。保留已经实现的图片上传、区域选择、主中心线识别、手动校正与导出能力，将这个模块接入我的整体交互流程。下游使用 schemaVersion 2.5 的 network 数据，保留原图坐标、连接关系和推测连接标记。需要改进算法或 UI 时可以修改源码；接口变化请同步更新类型、示例和测试。在我提供后续模块需求后，补齐具体业务数据映射。

## 已包含的功能

- 上传、更换图片，自动建议识别区域和手动框选。
- 亮度处理、降噪、多尺度沟槽线索、受图像证据约束的断点连接。
- 每条主要蚁道保留中心线，削减碎分支，通道范围内适度平滑。
- 完整骨架 / 主中心线切换；默认仅显示线条，无绿色填色和节点标记。
- 补画、擦除、撤销、重做、取消处理。
- 原图、增强、通道、中心线、纯线条、沟槽线索、凹陷图与 2.5D 视图。
- JSON、SVG、通道 PNG、透明中心线 PNG、叠加 PNG、相对凹陷 PNG 导出。
- 独立 ESM API、Worker、TypeScript 类型、可运行的下游网络接收示例。

## 文件入口

```text
Path_Recognize/
  CLAUDE.md                 Claude 开发交接入口
  README.md                 本说明
  Start.command             macOS 启动入口
  package.json              启动、测试、完整性检查命令；无依赖
  dist/                     完整可编辑源码（不是压缩编译产物）
    index.html / style.css / app.js
    i18n.mjs                中英文文案、语言持久化与切换
    path-recognize.mjs       独立调用接口
    path-recognize.d.mts     TypeScript 类型
    integration-worker.mjs  独立接口的后台线程
    network-export.mjs      网页和接口共用的 JSON 序列化
    engine.mjs / surface-evidence.mjs / reconstruction.mjs
    center-routes.mjs / main-paths.mjs / path-smoothing.mjs
    worker.js               完整工作台自己的后台线程
    assets/sample.jpg       原彩色示例
  examples/                 可独立运行的接入演示和灰度原图
  docs/                     接口、测试记录、源码来源与预览
  tests/                    算法及接口回归测试
  scripts/                  无第三方依赖的运行与验证脚本
  MANIFEST.sha256           交付时的文件校验清单
```

## 验证与能力边界

```sh
node scripts/test.mjs
node scripts/verify.mjs
```

第二个命令核对交付文件是否完整；主动修改源码后校验值变化是正常的。测试记录见 [VALIDATION.md](docs/VALIDATION.md)。

本版保留原有算法、识别接口与导出结构，更新了界面布局和中英文显示。接口可供后续模块调用，**尚未定义的后续业务流程需要 Claude 按具体需求接线**。凹陷图是图像推导的相对线索，不是实测三维深度；推测连接也保留来源标记，不能作为已验证的物理连通关系。功能测试不等于识别准确率评测。

包内不包含登录凭据、云端部署绑定、Git 历史或 node_modules。运行和修改不依赖原聊天记录、原网站域名或这台电脑的绝对路径。
