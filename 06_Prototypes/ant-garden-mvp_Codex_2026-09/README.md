# Ant Garden Studio — MVP 01

这个原型验证一条最小工作流：

1. 用户上传一张硬件采集的路径照片，并可补充文字；
2. 系统生成可修改的场景设定；
3. 对路径照片进行二值化、Zhang–Suen 骨架化和拓扑追踪，保留闭环、分叉与端点；
4. 使用等尺寸底层六边形完整铺满大六边形，保证地面没有空洞；上层高度和显露边界根据路径距离场变化，形成视觉疏密；
5. 将真实骨架映射到更细一级的深色六边形网格，覆盖在连续地面上，使弯曲与分叉更清晰；
6. Mock 3D provider 生成树、灌木、草簇、岩石、灯、花、长椅、拱门、亭子、蘑菇和水景等程序化低模素材；
7. 系统自动布置，用户可以像素材窗口一样拖入、旋转、缩放和删除；
8. 编辑器内可以继续输入图文生成个人素材；个人素材每项只能放置一次，场景素材可以重复放置；
9. 场景保存到浏览器。

## 本地运行

```bash
npm install
npm run dev
```

默认地址：`http://localhost:4173`

## MVP 边界

- 当前 `sceneAnalyzer.ts` 使用本地规则模拟图文理解。
- 当前 `MockAssetGenerator` 与程序化 Three.js 几何体模拟 3D 生成。
- `BackendAssetGenerator` 已定义真实服务需要的 HTTP 合约。
- API Key 不应放到前端，Meshy／Tripo 等服务应由后端代理。
- 蚂蚁、好友和花园连接不执行，但 `navigationGraph`、`entryPoints`、`interactionAnchors` 与 `social` 已写入场景结构。

## 下一步接入顺序

1. `/api/analyze`：多模态模型输出 `SceneProfile`；
2. `/api/assets/generate`：创建图生／文生 3D 任务；
3. `/api/assets/jobs/:id`：查询异步任务；
4. 服务端下载、标准化并压缩 GLB；
5. 前端 AssetModel 优先加载 `modelUrl`，失败时回退到程序化素材；
6. 添加项目存储、用户测试记录与生成质量标注。

## 关键目录

- `src/services/sceneAnalyzer.ts`：输入到场景语义。
- `src/services/assetGenerator.ts`：3D 生成服务抽象。
- `src/lib/hex.ts`：六边形地图与导航图。
- `src/components/HexScene.tsx`：3D 渲染。
- `src/services/extensionHooks.ts`：蚂蚁与社交的预留事件接口。
