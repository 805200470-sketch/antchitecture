# 接口与后续流程接入

## 1. 推荐：把识别能力作为独立模块

调用层不依赖框架、DOM、网络、AI 或后端。输入已解码像素，输出 JSON 网络和可继续编辑的中间状态。ESM 文件可以放进任意能提供静态资源的项目。

```js
import {recognize, exportNetwork, updateMask} from './dist/path-recognize.mjs';

const packet = recognize({
  rgba: imageData.data,       // Uint8ClampedArray 或 Uint8Array，RGBA 顺序
  width: imageData.width,    // 处理图尺寸，最长边 <= 1400
  height: imageData.height,
  image: {name: file.name, width: originalWidth, height: originalHeight},
  roi: {x: 0, y: 0, width: imageData.width, height: imageData.height},
  pathMode: 'main',          // main 主中心线，full 完整骨架
  smoothing: 1,             // 0 关闭，1..3 柔和程度
  includeInference: true,    // 推测连接仍有逐段来源标记
  options: {sensitivity: 50, contrast: 1.3, denoise: 2, minArea: 50,
    detectSurface: true, closeGaps: false, reconstruct: true, maxGap: 42}
});

onNetworkReady(packet.network); // 实际后续业务接口由调用方实现

// 保持分割结果，切换输出选择，不修改原 packet。
const directNetwork = exportNetwork(packet.analysis, packet.context,
  {includeInference: false});

// 手动补画/擦除修改处理分辨率下的二值 mask，再重新计算中心线。
const editedMask = packet.analysis.mask.slice();
// editedMask[y * width + x] = 0 或 1;
const edited = updateMask(packet, editedMask);
onNetworkReady(edited.network);
```

`recognize` 是同步 CPU 运算。浏览器交互请采用下面的 Worker 接口；React/Vue 组件卸载、用户取消、换图时应终止 Worker，阻止旧结果写回新状态。`updateMask` 不会再做浅通道生长或自动补线。撤销/重做可保存 packet 快照或掩膜与来源的完整快照，不能只回滚显示线条。

### 参数与验证

| 字段 | 默认 / 范围 | 含义 |
| --- | --- | --- |
| width, height | 必填，整数 1..1400 | 处理像素尺寸 |
| rgba | 必填，长度 width×height×4 | 行优先 RGBA；内部复制，透明部分合成白色 |
| image.width/height | 默认处理尺寸；原图不超过 6000 万像素 | 用于输出原图坐标 |
| roi | 默认全图 | 处理图坐标；使用引擎规则取整并限制在图内 |
| sensitivity | 50 / 0..100 | 分割强度 |
| contrast | 1.3 / 1..2 | 局部对比 |
| denoise | 2 / 整数 0..3 | 降噪 |
| minArea | 50 / 非负整数 | 最小区域，处理像素面积；适配层上限 10000000 |
| detectSurface | true / boolean | 多线索浅通道识别 |
| closeGaps | false / boolean | 形态学小缺口修补 |
| reconstruct | true / boolean | 图像线索支持的断点连接 |
| maxGap | 42 / 整数 4..100 | 处理图上允许的缺失长度参数 |
| pathMode | main / main 或 full | 每条主要蚁道中心线 / 完整骨架 |
| smoothing | 1 / 整数 0..3 | 在通道内部约束平滑 |
| includeInference | true / boolean | 是否包含自动推测部分 |

未知算法 options、非法尺寸和数据类型会抛出异常。没有路径是合法成功结果，`edges: []`；不是错误。接口不解码图像文件；完整的文件读取、白底画布与缩放示例见 `examples/integration.js`。

保留比例缩放，原图两个尺寸应与处理图对应。不要先裁出一块独立小图却仍填写完整原图尺寸，那会产生错误映射；要保留完整处理画布并用 ROI 限定，或由外部自行处理裁剪偏移。

## 2. Worker 协议

以下代码放在项目根目录的调用脚本中；脚本位置改变时调整相对路径。`examples/integration.js` 提供了完整、可运行的实现。

```js
const worker = new Worker(new URL('./dist/integration-worker.mjs', import.meta.url),
  {type: 'module'});
const id = crypto.randomUUID();
worker.onmessage = ({data}) => {
  if (data.id !== id) return;
  if (!data.ok) {
    console.error(data.error.name, data.error.message);
    worker.terminate();
    return;
  }
  const packet = data.result;
  onNetworkReady(packet.network);
  worker.terminate();
};
worker.onerror = error => { console.error(error.message); worker.terminate(); };
worker.postMessage({id, kind: 'recognize', input});
// 取消：worker.terminate()。应用还应清理自己的 busy 状态和当前任务 ID。
```

| 请求 | 成功 result |
| --- | --- |
| `{id, kind:'recognize', input}` | `{analysis, context, network}` |
| `{id, kind:'updateMask', packet, mask}` | 新的 `{analysis, context, network}` |
| `{id, kind:'exportNetwork', packet, view}` | 单独的 network |

错误响应统一为 `{id, ok:false, error:{name,message}}`；成功为 `{id,ok:true,result}`。每次请求携带调用者自己管理的 id。Worker 不保存会话状态。发送输入时示例使用结构化克隆以保留原数组；如果自行使用 transfer list，原数组会被转移失效，调用方需要管理所有权。回传 analysis 时 Worker 会转移其 TypedArray 缓冲。

`exportNetwork` 的 view 覆盖不会修改 packet.context；希望下一次沿用显示选择时，请自行更新 context 对应字段。`updateMask` 使用当前 context 的显示选择。

## 3. 下游唯一建议传递对象：network

完整类型见 `dist/path-recognize.d.mts`。`packet.analysis` 是内部处理数据，不应当作为长期存储协议；`network` 可直接 `JSON.stringify`，无需特殊序列化。

| 字段 | 下游用途 |
| --- | --- |
| schemaVersion | 当前 `2.5`；先校验版本 |
| engine, engineVersion | 识别来源与复现版本 |
| image | 原文件名、原图宽高 |
| coordinateSystem | 左上角原点；原图像素；Y 向下 |
| roi | 映射到原图坐标的识别区域 |
| processing | 处理尺寸、缩放、处理空间 ROI、参数、人工编辑次数与筛选/平滑元数据 |
| nodes | 节点 id、x/y、类型、边界标志 |
| edges | id、source/target、points、rawPoints、长度、宽度估计、来源 |
| edges[].inferredSegments | 布尔数组，长度 points.length−1；标记相邻点之间的段 |
| inferredConnections | 与选中路线相关的推测连接、证据；证据分数不是概率 |
| stats | 当前选中网络的统计，以及 rawSegments/rawComponents/rawLengthPx 等原始统计 |
| quality | 需要复核、未验证物理连通和真实深度等边界说明 |

`points` 是已选择、平滑的路线；`rawPoints` 是同一条选中路线在平滑前的几何点，不等于完整原始骨架。每条边的首末点与 source/target 节点对齐。所有对外点坐标、边长及宽度都是原图像素；`processing` 中的掩膜/平滑位移参数和 bridge evidence 的像素度量仍属于处理空间。

下游构建图可以直接使用 `examples/downstream.mjs`：

```js
import {acceptNetwork} from './examples/downstream.mjs';
const {nodes, adjacency} = acceptNetwork(network);
// adjacency.get(nodeId) -> [{nodeId: 邻接节点, edgeId: 连接边}, ...]
// 网络默认作为无向图；如要表达运动方向，另行附加业务规则。
```

几何拓扑可能有多个分量、自环和多重边；不要无条件补成一棵树。边 ID 仅在本次结果内稳定。若保存多个结果，建议用外层业务 ID + imageId + revision 管理，不改变模块内部 ID 语义。

二维绘制：`screenX = point[0] * canvasWidth / network.image.width`，Y 同理。Three.js 平面展示可先用 `[x - width/2, height/2 - y, 0]`；物理尺寸比例和第三轴由后续模块定义，不能从本模块的相对凹陷值直接当成毫米深度。

## 4. 保留整个工作台嵌入

```html
<iframe id="recognizer" src="./Path_Recognize/dist/"
  title="蚁穴路径识别" style="width:100%;height:100vh;border:0"></iframe>
```

父页面和 iframe **同源**时，等工作台就绪后可以使用现有页面接口：

```js
const api = document.getElementById('recognizer').contentWindow.Formic;
await api.loadFile(file); // 加载文件；不自动提取上传的新图
if (!api.getState().ready || api.getState().busy) return;
api.setROI({x:0, y:0, width:processingWidth, height:processingHeight});
await api.extract();
const network = api.getResult(true);
if (network) onNetworkReady(network);
```

`window.Formic` 提供 `version / loadFile / setROI / extract / getResult / getState`，1.1.0 另增 `getLanguage()` / `setLanguage("zh" | "en")`，`getState().language` 可读取当前界面语言。切换语言不重新识别，不改变任何网络字段或几何；原图名称是业务数据，保留原值。`getResult` 在尚未提取或 ROI 被修改后返回 null。`loadFile` 对不支持的文件显示页面提示，不承诺 reject；`extract` 被取消/失败也可能返回 null。调用方必须核对文件名/尺寸及结果状态，避免把旧结果当新结果。

本包**没有**现成的跨源 postMessage 协议。若要跨域嵌入，需要 Claude 另加消息类型、origin 校验和加载状态处理。示例页的 `path-recognize:ready` CustomEvent 只存在于示例页，它不是完整工作台的内置事件。通常直接接入独立模块更便于框架整合。

## 5. 部署到整体网页

- 完整工作台可部署为静态子目录，保持 HTML、JS、Worker 和 assets 的相对路径。
- 单独接入时保留相关 `.mjs` 文件依赖链。Vite 等构建器中 Worker 使用 `new URL(..., import.meta.url)`；对原页面的 `./worker.js` 字符串相对路径需按最终目录检查。
- Web 服务器应将 `.mjs` / `.js` 作为 JavaScript MIME 提供。CSP 如有启用需允许本源脚本、Worker 及图片 blob/data；本地启动器已经正确提供 MIME。
- 如后续加入服务端存储，仅将 `network` 和明确需要的图片/业务字段存入后端；本包自身不上传图片。
- 无需 Sites / ChatGPT / Claude 的运行时或登录凭据。原站在线地址只代表开发来源，不是运行依赖。

本包验证的是现有功能、输入输出契约与示例消费者。后续完整业务尚未提供，其数据映射、状态管理、渲染与部署由接入方完成。
