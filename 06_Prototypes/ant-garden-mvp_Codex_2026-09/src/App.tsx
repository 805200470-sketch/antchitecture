import { useEffect, useMemo, useState } from 'react'
import type { AssetDefinition, GardenScene, SceneProfile, Stage, UserInput } from './types'
import { InputStep } from './components/InputStep'
import { ReviewStep } from './components/ReviewStep'
import { GeneratingStep } from './components/GeneratingStep'
import { GardenEditor } from './components/GardenEditor'
import { analyzeInput } from './services/sceneAnalyzer'
import { buildNavigationGraph, createHexGrid } from './lib/hex'
import { buildAdaptiveNavigationGraph, mapImageToAdaptiveHexGrid } from './lib/imageScene'
import { extensionHooks } from './services/extensionHooks'

const libraryAssets: AssetDefinition[] = [
  { id: 'library_tree', name: '基础树木', kind: 'tree', source: 'library', status: 'ready' },
  { id: 'library_rock', name: '自然岩石', kind: 'rock', source: 'library', status: 'ready' },
  { id: 'library_flower', name: '低矮花簇', kind: 'flower', source: 'library', status: 'ready' },
  { id: 'library_marker', name: '地点标记', kind: 'marker', source: 'library', status: 'ready' },
  { id: 'library_lamp', name: '环境照明', kind: 'lamp', source: 'library', status: 'ready' },
  { id: 'library_pavilion', name: '小型构筑物', kind: 'pavilion', source: 'library', status: 'ready' },
  { id: 'library_shrub', name: '成组灌木', kind: 'shrub', source: 'library', status: 'ready' },
  { id: 'library_grass', name: '高低草簇', kind: 'grass', source: 'library', status: 'ready' },
  { id: 'library_bench', name: '木质长椅', kind: 'bench', source: 'library', status: 'ready' },
  { id: 'library_arch', name: '石质拱门', kind: 'arch', source: 'library', status: 'ready' },
  { id: 'library_mushroom', name: '蘑菇群落', kind: 'mushroom', source: 'library', status: 'ready' },
  { id: 'library_water', name: '浅水容器', kind: 'water', source: 'library', status: 'ready' },
]

async function buildScene(profile: SceneProfile, sourceImageUrl: string | undefined, seed = 1): Promise<GardenScene> {
  const baseCells = createHexGrid(profile, seed, 3)
  const imageMapped = sourceImageUrl
    ? await mapImageToAdaptiveHexGrid(profile, sourceImageUrl, seed).catch(() => null)
    : null
  const cells = imageMapped?.cells ?? baseCells
  const pathCellIds = imageMapped?.pathCellIds ?? cells.filter((cell) => ['portal', 'path', 'core'].includes(cell.type)).sort((a, b) => a.r - b.r).map((cell) => cell.id)
  const pathPolylines = imageMapped?.pathPolylines ?? []
  const pathHexes = imageMapped?.pathHexes ?? []
  const generatedAssets: AssetDefinition[] = profile.assetIdeas.map((idea, index) => ({
    id: `generated_${index}`,
    name: idea.name,
    kind: idea.kind,
    source: 'generated',
    status: 'ready',
    maxInstances: 1,
  }))
  const assets = [...generatedAssets, ...libraryAssets]
  const availableCells = cells.filter((cell) => cell.type === 'environment')
  const placements = assets.slice(0, Math.min(14, availableCells.length)).map((asset, index) => ({
    id: `placement_${index}`,
    assetId: asset.id,
    cellId: availableCells[(index * 4 + seed) % availableCells.length].id,
    rotation: (index * Math.PI) / 4,
    scale: 0.82 + (index % 3) * 0.14,
  }))
  const portal = cells.find((cell) => cell.type === 'portal')

  return {
    version: 1,
    id: crypto.randomUUID(),
    profile,
    sourceImageUrl,
    cells,
    assets,
    placements,
    pathCellIds,
    pathPolylines,
    pathHexes,
    navigationGraph: imageMapped ? buildAdaptiveNavigationGraph(cells) : buildNavigationGraph(cells),
    entryPoints: imageMapped ? cells.filter((cell) => cell.type === 'portal').map((cell) => cell.id) : portal ? [portal.id] : [],
    interactionAnchors: cells.filter((cell) => cell.type === 'core').map((cell) => cell.id),
    social: { ownerId: null, visibility: 'private', connectedGardenIds: [] },
  }
}

export default function App() {
  const [stage, setStage] = useState<Stage>('input')
  const [input, setInput] = useState<UserInput>({ text: '', images: [] })
  const [profile, setProfile] = useState<SceneProfile | null>(null)
  const [scene, setScene] = useState<GardenScene | null>(null)
  const [progress, setProgress] = useState(0)
  const [analyzing, setAnalyzing] = useState(false)
  const restoredScene = useMemo(() => localStorage.getItem('ant-garden:mvp-scene'), [])

  function restoreSavedScene() {
    if (!restoredScene) return
    try {
      const parsed = JSON.parse(restoredScene) as GardenScene
      const pathCellIds = parsed.pathCellIds ?? parsed.cells
        .filter((cell) => ['portal', 'path', 'core'].includes(cell.type))
        .sort((a, b) => a.r - b.r)
        .map((cell) => cell.id)
      setScene({
        ...parsed,
        pathCellIds,
        pathPolylines: parsed.pathPolylines ?? [],
        pathHexes: parsed.pathHexes ?? [],
        assets: parsed.assets.map((asset) => ({ ...asset, maxInstances: asset.source === 'generated' ? 1 : undefined })),
      })
      setStage('editor')
    } catch {
      localStorage.removeItem('ant-garden:mvp-scene')
    }
  }

  useEffect(() => {
    if (stage !== 'generating') return
    const timer = window.setInterval(() => setProgress((value) => Math.min(100, value + 4)), 100)
    return () => window.clearInterval(timer)
  }, [stage])

  useEffect(() => {
    if (stage === 'generating' && progress >= 100 && profile) {
      void buildScene(profile, input.images[0]?.url, Date.now() % 97).then((nextScene) => {
        setScene(nextScene)
        extensionHooks.emit({ type: 'scene.created', sceneId: nextScene.id })
        setStage('editor')
      })
    }
  }, [input.images, progress, profile, stage])

  async function handleInput(value: UserInput) {
    setAnalyzing(true)
    setInput(value)
    const result = await analyzeInput(value)
    setProfile(result)
    setAnalyzing(false)
    setStage('review')
  }

  function beginGeneration() {
    setProgress(0)
    setStage('generating')
  }

  if (stage === 'input') {
    return <><InputStep onSubmit={handleInput} />{analyzing && <div className="analysis-overlay"><span>⬡</span><strong>正在整理输入线索…</strong></div>}{restoredScene && <button className="restore-button" onClick={restoreSavedScene}>继续上次保存的花园 →</button>}</>
  }
  if (stage === 'review' && profile) return <ReviewStep input={input} profile={profile} onChange={setProfile} onBack={() => setStage('input')} onGenerate={beginGeneration} />
  if (stage === 'generating' && profile) return <GeneratingStep profile={profile} progress={progress} />
  if (stage === 'editor' && scene) return <GardenEditor scene={scene} onChange={setScene} onRestart={() => setStage('input')} />
  return null
}
