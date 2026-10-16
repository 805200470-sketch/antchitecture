# 交付验证记录 · 1.1.0

日期：2026-10-04。引擎仍为 2.2.0，网络 schema 仍为 2.5。本次更新中英文显示、工作台布局及相关页面交互；算法和独立识别适配器未修改。上一版记录保存在 `VALIDATION_1.0.0.md`。

## 算法与接口

8 组原有算法及独立接口测试全部通过。浏览器对当前彩色与灰度结果逐字段比较上一版真实 JSON（仅忽略 createdAt 时间戳），结果一致：彩色 25 个节点 / 23 条边 / 2 个分量；灰度 31 个节点 / 33 条边 / 2 个分量。

## 界面与浏览器

在 Chrome 154.0.8037.93 上完成 12 项浏览器回归检查，见 `BROWSER_CHECKS.json`：

- 首次默认中文，中文 / English 即时切换，刷新保留选择。
- 四个步骤、八个图层、四档平滑、帮助说明、错误提示和辅助标签提供英文。
- 切换语言保留 ROI、图层、网络、人工擦除结果及撤销/重做记录。
- 换图清理旧结果，通过「选择区域 → 开始识别 → 提取」进入校正。
- JSON、SVG 和四种 PNG 导出正常；纯中心线 PNG 有透明背景。
- 排除推测连接、返回校正、参数 dirty 状态及计算中取消正常。
- 中文和英文分别验证 390 / 768 / 1440 像素宽度，没有页面横向溢出。
- 独立接入演示的双语显示、Worker、下游接收器和排除推测导出正常。
- 浏览器 localStorage 不可用时仍可运行和切换语言。
- 无未捕获页面异常，无外部运行时请求。

截图：`preview.png`（中文）、`preview-en.png`（英文）、`preview-mobile.png`（手机）、`integration-preview.png`（接入演示）。

## 复跑

基础测试：`node scripts/test.mjs`。

可选浏览器测试：开发环境具备 Playwright 与 Chrome 后运行 `node scripts/browser-check.mjs`。也可以通过环境变量 `PLAYWRIGHT_MODULE` 指定已有 Playwright 的模块路径，`CHROME_CHANNEL` 指定本机浏览器通道。测试自行启动本地服务器，默认端口 8876，可用 `PATH_RECOGNIZE_TEST_PORT` 修改；测试产物写入 `qa-artifacts/`，不属于发布文件。

包内网页不依赖 Playwright，正常运行仍然无需 npm install。校验交付文件用 `node scripts/verify.mjs`；主动修改源码后哈希变化属正常情况。

## 交付与范围

外接硬盘的同一个 `Path_Recognize_Package` 包内更新源码、ZIP 和 ZIP 的 SHA-256，内部名称保留英文。发布前逐文件校验清单、ZIP CRC、解压后的文件校验及从解压目录加载本地界面均需通过。

本次功能验证不构成识别准确率或物理深度验证。后续流程继续使用原有 network 接口；具体业务连接由 Claude 按后续需求完成。
