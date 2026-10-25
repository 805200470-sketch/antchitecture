# 粒子风格 · 第一版效果图：提示词和参考图

这一批是效果图，不是游戏截图：把现有的三种风格（暖琥珀蚁穴、古蚁圣殿、巨人遗物）按粒子蚁穴第 15 版的画法重画，每种两张（入口 / 中心），用来看每种风格落在色轮的哪一段。蓝色真菌林就是粒子蚁穴本身，不另画，直接用 v15 的实机截图（`07_Outputs/particle_nest/v15_template/`）。

- 工具：`Tools/codex_image.sh`，经 `Tools/codex_batch.py` 一次跑三张。任务表：`02_Concepts/Prompts/PROMPTS_v14_particle_styles_jobs.json`（第一轮），重画的在 `PROMPTS_v14b_particle_styles_redo_jobs.json`。
- 画法模板是粒子蚁穴 v15：窄通道（几只蚂蚁宽，两边墙很近）、整场都是不透明的小点、一条引导光丝（最多再陪一条）、细直的竖光柱和脚下的光池、实体的暗色藤蔓（不发光）绕着光柱脚和爬墙、植物只有很弱的呼吸光、亮度低但可控。
- 每种风格保留自己的内容和颜色，光丝、光柱和光池也用这个地方自己的光色（琥珀金、陶土橙、天蓝），不用粒子蚁穴的青色。
- 色轮对应和测量方法见 `mapping.md`，图表是 `01_色轮对应.jpg`，总览是 `00_总览.jpg`。

## 参考图

参考图都在 `_refs/`。游戏截图先裁掉小地图和提示条，抹掉标题和蚂蚁（特别是青色的玩家蚂蚁和光圈，免得把青色带进暖色场景）。每张图附三张：第 1 张是 v15 的蚂蚁视角截图，定画法和空间感；第 2、3 张定内容和颜色。

| 文件 | 来自 | 处理 |
|---|---|---|
| `_refs/style_v15_way_in.jpg` | `07_Outputs/particle_nest/v15_template/v15_r0_1_eye_lab_way_in.png` | 转成 JPG，原尺寸（入口图的画法参考） |
| `_refs/style_v15_heart.jpg` | `07_Outputs/particle_nest/v15_template/v15_r0_3_eye_lab_heart.png` | 转成 JPG，原尺寸（中心图的画法参考） |
| `_refs/amber_canyon.jpg` | `Docs/PPT截图/08_amber_canyon.jpg` | 缩到 1920 宽；抹掉蚂蚁和青色玩家蚂蚁 |
| `_refs/amber_canyon_eye.jpg` | `Docs/screenshots/unity_v7_镜头与场景总览/02_暖琥珀_总览.jpg` | 裁出「第一视角 · 峡谷」 |
| `_refs/amber_resin.jpg` | `Docs/PPT截图/09_amber_resin.jpg` | 缩到 1920 宽；抹掉玩家蚂蚁 |
| `_refs/amber_chamber_eye.jpg` | `Docs/screenshots/unity_v7_镜头与场景总览/02_暖琥珀_总览.jpg` | 裁出「第一视角 · 洞室」 |
| `_refs/temple_corridor.jpg` | `Docs/screenshots/unity_v9_遗迹隧道与接缝/05_temple_corridor.jpg` | 裁掉小地图和底部提示条，抹掉标题和三只蚂蚁 |
| `_refs/temple_shrine.jpg` | `Docs/screenshots/unity_v9_遗迹隧道与接缝/06_temple_heart_shrine.jpg` | 裁掉小地图和底部提示条，抹掉玩家蚂蚁 |
| `_refs/temple_arch_eye.jpg` | `Docs/screenshots/unity_v7_镜头与场景总览/04_遗迹_古蚁圣殿_总览.jpg` | 裁出「第一视角 · 拱门」（备用，没用上） |
| `_refs/relics_teacup_eye.jpg` | `Docs/screenshots/unity_v7_镜头与场景总览/03_遗迹_巨人遗物_总览.jpg` | 裁出「第一视角 · 茶杯」 |
| `_refs/relics_button.jpg` | `Docs/screenshots/unity_v9_遗迹隧道与接缝/09_relics_heart_button.jpg` | 裁掉小地图，抹掉玩家蚂蚁 |
| `_refs/relics_trail.jpg` | `Docs/screenshots/unity_v9_遗迹隧道与接缝/08_relics_guide_trail.jpg` | 裁掉小地图，抹掉玩家蚂蚁 |
| `（直接用原图）` | `02_Concepts/Ruins_v3/B1_temple_tunnel.png、B2_temple_chamber.png、A1_relics_tunnel.png` | 遗迹 v3 设定图，内容参考 |

## 每张图

每条提示词由五段拼成：图的分工 → 这一张的场景 → v15 的画法 → 共用的光（括号里是这个地方的光色）→ 克制要求；重画版在克制要求前多一段修正。`codex_image.sh` 会在前面加一句固定说明（用图像工具画一张、附图是参考、按原尺寸存成 PNG），下面是它后面「Prompt:」的原文。

### amber_way_in.png · 暖琥珀 · 入口（第一轮，未选用）

**结果**：内容对：窄的红褐土峡谷、滴树脂的干蘑菇、金色球菇、土丘、远处的土塔。问题：整体偏亮（平均亮度 0.16，v15 截图是 0.11–0.13），球菇头像灯泡，左边的光柱是一根粗光管，不是细线。所以重画。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_way_in.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_canyon.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_canyon_eye.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, at the way into the Warm Amber nest: a narrow, winding canyon of red-brown earth under a near-black maroon night sky. Its walls are porous, pitted soil in rust, terracotta and dark maroon. Ball-topped stalks with pale-gold heads stand along the walls; a large dry mushroom with a flat tan cap and a twisted stem leans over the path, small drops of amber resin hanging from its rim; dark soil spires rise further back; a pitted soil mound and a thorny succulent sit by the path. Focal point: the nearest beam beside the path, its pool of light and the vines round its foot. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm amber-gold; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### amber_heart.png · 暖琥珀 · 中心（第一轮，未选用）

**结果**：内容对：大干蘑菇下的小洞室，中间一簇发光的琥珀树脂，引导光丝在上面绕一圈。问题：前景有大块景深虚化（像光斑），球菇头像灯泡，光柱是粗光管。所以重画。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_heart.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_resin.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_chamber_eye.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, in the heart of the Warm Amber nest: the canyon opens into a small chamber of red-brown earth under huge dry mushrooms with flat tan caps and twisted stems. In the middle, a cluster of amber resin, translucent orange-gold crystals rising from a dark root knot, glows softly from within (the focal point). Ball-topped stalks with pale-gold heads stand around it; the guiding thread ends here, looping once above the resin; a beam stands just beside it with vines round its foot. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm amber-gold; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### temple_way_in.png · 古蚁圣殿 · 入口（第一轮，未选用）

**结果**：内容很准：螺纹陶土尖塔、带孔的塔身、刻纹尖拱、六边形巢室、卷头植物。问题：整条隧道被均匀照亮（接近黑的部分只占 34%，v15 约 52%），天花板裂缝变成一道带尘的体积光，不像夜里。所以重画。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_way_in.jpg`
- `02_Concepts/Ruins_v3/B1_temple_tunnel.png`
- `02_Concepts/Particle_Styles_v1/_refs/temple_corridor.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, at the way into the Ancient Ant Temple, a narrow ruined tunnel built long ago by another ant colony; an earthen ceiling closes overhead (no sky). Along both walls stand tall packed-clay spires with spiral ridges and small round holes, and pointed earthen arches carved with winding grooves like ant trails; broken hexagonal clay cells sit along the foot of the walls; a few curled stalks with pale-gold heads. Palette: terracotta red, ochre, burnt sienna and bone over deep red-brown shadow. Focal point: a tall carved arch ahead, with a beam falling from a crack in the ceiling just in front of it. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm ember orange; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### temple_heart.png · 古蚁圣殿 · 中心（第一轮，未选用）

**结果**：构图好：大琥珀里封着远古飞蚁，四周是尖塔和尖拱，巢室碎块围着底座。问题：太亮（接近黑的部分只占 18%），前景大面积景深虚化，颗粒是一颗颗圆珠（像鱼子），藤蔓上开出了白花（多余的东西），光柱变成了体积光。所以重画。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_heart.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/temple_shrine.jpg`
- `02_Concepts/Ruins_v3/B2_temple_chamber.png`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, in the shrine at the heart of the Ancient Ant Temple: a small chamber under an earthen ceiling, ringed by spiral-ridged clay spires and pointed carved arches. In the centre stands a large block of amber, built of dense translucent orange-gold points and glowing gently from within, with the dark silhouette of an ancestral winged ant inside (the focal point). Broken hexagonal cells ring its base; the guiding thread ends above the amber; a beam falls from the ceiling beside it, vines climbing the nearest spire. Palette: terracotta, ochre, bone and amber. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm ember orange; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### relics_way_in.png · 巨人遗物 · 入口（选用，没有重画）

**结果**：内容和 Ruins_v3 的隧道版一致：左边半埋的青花瓷茶杯，右边一摞铜绿硬币，远处横在沟上的生锈钥匙，顶上有垂根。曝光和 v15 接近（平均 0.12）。一条带折角的光丝，两根细光柱。弱点：光柱脚下的光池很淡；光丝折角像闪电；墙上的藤蔓偏青绿，有点像在发光。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_way_in.jpg`
- `02_Concepts/Ruins_v3/A1_relics_tunnel.png`
- `02_Concepts/Particle_Styles_v1/_refs/relics_teacup_eye.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, at the way into Giant's Relics, a narrow tunnel in deep blue with an earthen ceiling and a few hanging roots overhead (no sky). Small things lost by people lie here at ant scale: a huge cracked white porcelain teacup with a blue floral pattern lies half buried in the left wall, its mouth a dark hall (the focal point); a stack of coins with green verdigris forms steps on the right; an old rusted key lies across a side gully further on. Pale-blue fungi grow on the relics. Palette: navy and azure blue, porcelain white, small touches of rust and verdigris. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (cool azure blue, pale blue-white at its brightest; not green, not teal): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### relics_heart.png · 巨人遗物 · 中心（第一轮，未选用）

**结果**：内容对：四孔纽扣像小广场，光柱落在扣面上，滴水的果冻菇，斜靠的缝衣针，墙脚的蓝色珊瑚菌。问题：珊瑚菌铺得太满、到处发光，前景有大块虚化，整体偏亮。所以重画。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_heart.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/relics_button.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/relics_trail.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, in the heart of Giant's Relics: a small round chamber of the deep-blue tunnel under an earthen ceiling. In the middle lies a huge four-hole button, cream white, flat like a small plaza (the focal point); a beam falls from the ceiling onto it and leaves a soft pool of light on its face; the guiding thread ends above it. Behind it a large pale jelly-like mushroom with dripping edges; a bent sewing needle leans against the wall; blue coral-like fungi line the foot of the walls. Palette: navy and azure blue, cream, a little rust. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (cool azure blue, pale blue-white at its brightest; not green, not teal): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### temple_way_in_v2.png · 古蚁圣殿 · 入口（重画，选用）

**结果**：夜里的感觉对了，尖塔上爬着实体藤蔓，内容没丢。弱点：比 v15 稍暗（平均 0.09）；表面更像细砂，"一颗颗点"的感觉比第一轮弱；中间的竖光柱很细，存在感弱；光丝折角像闪电。

**重画原因**：第一轮太亮、有体积光。加同样的修正段，并要求隧道大部分在深红褐的阴影里，只有光柱、光丝和几株卷头植物附近被照亮。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_way_in.jpg`
- `02_Concepts/Ruins_v3/B1_temple_tunnel.png`
- `02_Concepts/Particle_Styles_v1/_refs/temple_corridor.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, at the way into the Ancient Ant Temple, a narrow ruined tunnel built long ago by another ant colony; an earthen ceiling closes overhead (no sky). Along both walls stand tall packed-clay spires with spiral ridges and small round holes, and pointed earthen arches carved with winding grooves like ant trails; broken hexagonal clay cells sit along the foot of the walls; a few curled stalks with pale-gold heads. Palette: terracotta red, ochre, burnt sienna and bone over deep red-brown shadow. Focal point: a tall carved arch ahead, with a beam falling from a crack in the ceiling just in front of it. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm ember orange; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Correction for this version: match the exposure of the first attached image: a dark night scene, about half of the frame near black, only the guiding thread, the beams with their pools and a few plant heads bright. Everything in sharp focus like a game render: no depth-of-field blur, no large out-of-focus blobs in the foreground. Each beam is a hair-thin straight line of light, not a tube, column or shaft; no god rays, no dusty light shafts. Keep the same content, but at night: the tunnel lies mostly in deep red-brown shadow; the spires and arches are lit softly only near the beam, the thread and the few glowing curls. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### temple_heart_v2.png · 古蚁圣殿 · 中心（重画，选用）

**结果**：曝光和构图都对：左边细光柱带光池，藤蔓贴着光柱，光丝从天花板折下来停在琥珀上方。弱点：琥珀本身是光滑的玻璃块，叶子像真实的常春藤，这两处不像粒子，搭场景时要用点来做。

**重画原因**：第一轮太亮、景深虚化、圆珠颗粒、多余的花、体积光。加同样的修正段，并要求琥珀只照亮自己的底座和最近的尖塔，表面是小平片和细点而不是圆珠，藤蔓只有暗色小叶、不开花。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_heart.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/temple_shrine.jpg`
- `02_Concepts/Ruins_v3/B2_temple_chamber.png`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, in the shrine at the heart of the Ancient Ant Temple: a small chamber under an earthen ceiling, ringed by spiral-ridged clay spires and pointed carved arches. In the centre stands a large block of amber, built of dense translucent orange-gold points and glowing gently from within, with the dark silhouette of an ancestral winged ant inside (the focal point). Broken hexagonal cells ring its base; the guiding thread ends above the amber; a beam falls from the ceiling beside it, vines climbing the nearest spire. Palette: terracotta, ochre, bone and amber. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm ember orange; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Correction for this version: match the exposure of the first attached image: a dark night scene, about half of the frame near black, only the guiding thread, the beams with their pools and a few plant heads bright. Everything in sharp focus like a game render: no depth-of-field blur, no large out-of-focus blobs in the foreground. Each beam is a hair-thin straight line of light, not a tube, column or shaft; no god rays, no dusty light shafts. The amber block glows softly but is not a lamp: it lights its own base and the nearest spires, and the rest of the chamber falls into deep red-brown shadow. Surfaces are small flat flakes and fine points as in the first image, not round beads or pearls. The vines carry small dark leaves, no flowers. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### amber_heart_v2.png · 暖琥珀 · 中心（重画，选用）

**结果**：曝光接近 v15（平均 0.10），没有虚化；细光柱从树脂旁落下，藤蔓缠在脚下，光丝绕一圈停在树脂上方。弱点：球菇头仍是画面里最亮的东西之一；左下的小土丘有点像骷髅。

**重画原因**：第一轮前景虚化、球菇太亮、光柱太粗。原提示词里加了一段修正（对齐第 1 张图的曝光；全画面清晰，不要景深虚化；光柱是发丝细的线，没有体积光），再要求球菇只微微发光、树脂柔和地亮、光柱脚下有小光池和缠着的藤蔓。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_heart.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_resin.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_chamber_eye.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, in the heart of the Warm Amber nest: the canyon opens into a small chamber of red-brown earth under huge dry mushrooms with flat tan caps and twisted stems. In the middle, a cluster of amber resin, translucent orange-gold crystals rising from a dark root knot, glows softly from within (the focal point). Ball-topped stalks with pale-gold heads stand around it; the guiding thread ends here, looping once above the resin; a beam stands just beside it with vines round its foot. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm amber-gold; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Correction for this version: match the exposure of the first attached image: a dark night scene, about half of the frame near black, only the guiding thread, the beams with their pools and a few plant heads bright. Everything in sharp focus like a game render: no depth-of-field blur, no large out-of-focus blobs in the foreground. Each beam is a hair-thin straight line of light, not a tube, column or shaft; no god rays, no dusty light shafts. The ball-topped stalks glow only faintly, dim warm heads rather than lamps; the resin glows softly; the beam beside the resin is a hair-thin line with a small pool of light at its foot and the vines wrapping round that foot. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### amber_way_in_v2.png · 暖琥珀 · 入口（重画，选用）

**结果**：曝光接近 v15（平均 0.09），光柱细了，球菇暗了，还多了一棵带刺的多肉。弱点：藤蔓上的叶子是一圈圈浅色小圆片，有点像亮片；整体比 v15 稍暗。

**重画原因**：第一轮偏亮、球菇像灯泡、光柱太粗。加同样的修正段，并要求球菇只微微发光、光柱是发丝细的线、脚下有小光池、藤蔓只在被照到的地方亮。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_way_in.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_canyon.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/amber_canyon_eye.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, at the way into the Warm Amber nest: a narrow, winding canyon of red-brown earth under a near-black maroon night sky. Its walls are porous, pitted soil in rust, terracotta and dark maroon. Ball-topped stalks with pale-gold heads stand along the walls; a large dry mushroom with a flat tan cap and a twisted stem leans over the path, small drops of amber resin hanging from its rim; dark soil spires rise further back; a pitted soil mound and a thorny succulent sit by the path. Focal point: the nearest beam beside the path, its pool of light and the vines round its foot. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (warm amber-gold; no cyan, teal or blue anywhere): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Correction for this version: match the exposure of the first attached image: a dark night scene, about half of the frame near black, only the guiding thread, the beams with their pools and a few plant heads bright. Everything in sharp focus like a game render: no depth-of-field blur, no large out-of-focus blobs in the foreground. Each beam is a hair-thin straight line of light, not a tube, column or shaft; no god rays, no dusty light shafts. The ball-topped stalks glow only faintly, dim warm heads rather than lamps. The beam on the left is a hair-thin line with a small soft pool of light at its foot; the dark vines wrapping round that foot are lit only where the beam touches them. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

### relics_heart_v2.png · 巨人遗物 · 中心（重画，选用）

**结果**：干净很多，焦点清楚，光丝和光柱的分工对了。弱点：比 v15 暗一截（平均 0.08），墙面几乎看不清；第四个孔被光池盖住，看起来像三孔；最前景还有一点虚化。

**重画原因**：第一轮珊瑚菌太满、前景虚化、偏亮。加同样的修正段，并要求珊瑚菌只在墙脚留几小丛、果冻菇微光、光丝从隧道深处平着过来在纽扣上方打一个小圈、光柱单独从天花板落下。

参考图（按顺序附上，第 1 张定画法，后两张定内容和颜色）：
- `02_Concepts/Particle_Styles_v1/_refs/style_v15_heart.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/relics_button.jpg`
- `02_Concepts/Particle_Styles_v1/_refs/relics_trail.jpg`

提示词（原文）：

```text
Attached image 1, a still from our game's particle nest, sets the rendering style and the kind of space. Attached images 2 and 3 show this place in our older smooth look or in an earlier concept; take only their content and colours (not their rendering), and leave out any interface, text and ants. Scene: ant's-eye view, camera low at ant height, in the heart of Giant's Relics: a small round chamber of the deep-blue tunnel under an earthen ceiling. In the middle lies a huge four-hole button, cream white, flat like a small plaza (the focal point); a beam falls from the ceiling onto it and leaves a soft pool of light on its face; the guiding thread ends above it. Behind it a large pale jelly-like mushroom with dripping edges; a bent sewing needle leans against the wall; blue coral-like fungi line the foot of the walls. Palette: navy and azure blue, cream, a little rust. Make it look like a still from the same game, a real-time render, not a painting. Everything (walls, floor, plants, objects) is built from countless small opaque points and splats, like a Gaussian-splat capture: slight differences in tone keep each material's own colour and grain; no smooth surfaces, no outlines. Points are fine and dense near the camera, smaller and sparser far away; the floor is a carpet of fine points. The passage is narrow, a few ant-widths wide, with walls close on both sides. Night, low but controlled brightness: the darkest areas sit near black, yet every shape stays readable. Light, in this place's own colour (cool azure blue, pale blue-white at its brightest; not green, not teal): one thin guiding thread of light hangs in the air and leads deeper in, in straight stretches with a few sharp turns; one or two thin straight vertical beams stand beside the path, each with a soft pool of light at its foot. Solid vines of dark points, lit by the beams but not glowing, wrap round the beams' feet and climb the walls. Plants glow only faintly, as if breathing. Correction for this version: match the exposure of the first attached image: a dark night scene, about half of the frame near black, only the guiding thread, the beams with their pools and a few plant heads bright. Everything in sharp focus like a game render: no depth-of-field blur, no large out-of-focus blobs in the foreground. Each beam is a hair-thin straight line of light, not a tube, column or shaft; no god rays, no dusty light shafts. Only a few small groups of dim blue fungi at the foot of the walls, not a carpet of glowing fungi; the jelly mushroom glows faintly. The guiding thread arrives from deeper in the tunnel at head height and ends in a small loop above the button; the beam is a separate hair-thin line falling from the ceiling. Mature and restrained: one clear focal point carries the detail, the rest falls into calm masses of points; limited palette. No glitter, sparkles, lens flares, bokeh balls, neon, over-saturation, HDR look, noisy clutter, random extra objects or repeated tiny details; the air is clear apart from a few faint motes. Wide 16:9 landscape. No text, no interface, no frame.
```

## 色轮对应

见 `mapping.md`（表）和 `01_色轮对应.jpg`（图）。
