import { useCallback, useMemo, useState } from 'react'
import type { AssetKind, GardenScene, Placement } from '../types'
import { HexScene } from './HexScene'
import { extensionHooks } from '../services/extensionHooks'

interface Props {
  scene: GardenScene
  onChange(scene: GardenScene): void
  onRestart(): void
}

export function GardenEditor({ scene, onChange, onRestart }: Props) {
  const [selectedAssetId, setSelectedAssetId] = useState<string | null>(null)
  const [selectedPlacementId, setSelectedPlacementId] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const [libraryTab, setLibraryTab] = useState<'library' | 'generated'>('library')
  const [assetPrompt, setAssetPrompt] = useState('')
  const [assetImage, setAssetImage] = useState<string | undefined>()
  const [assetImageName, setAssetImageName] = useState('')
  const [generatingAsset, setGeneratingAsset] = useState(false)
  const [dropRequest, setDropRequest] = useState<{ assetId: string; clientX: number; clientY: number; nonce: number } | null>(null)
  const selectedPlacement = useMemo(() => scene.placements.find((item) => item.id === selectedPlacementId) ?? null, [scene.placements, selectedPlacementId])
  const selectedAsset = selectedPlacement ? scene.assets.find((asset) => asset.id === selectedPlacement.assetId) : null

  const placeAsset = useCallback((assetId: string, cellId: string) => {
    const cell = scene.cells.find((item) => item.id === cellId)
    if (!cell?.walkable || ['core', 'path', 'portal'].includes(cell.type)) return
    const asset = scene.assets.find((item) => item.id === assetId)
    if (!asset) return
    const usedCount = scene.placements.filter((item) => item.assetId === assetId).length
    if (asset.maxInstances && usedCount >= asset.maxInstances) return
    const placement: Placement = { id: crypto.randomUUID(), assetId, cellId, rotation: 0, scale: 1 }
    onChange({ ...scene, placements: [...scene.placements, placement] })
    setSelectedPlacementId(placement.id)
    setSelectedAssetId(null)
  }, [onChange, scene])

  function placeSelected(cellId: string) {
    if (selectedAssetId) placeAsset(selectedAssetId, cellId)
  }

  function updatePlacement(patch: Partial<Placement>) {
    if (!selectedPlacementId) return
    onChange({ ...scene, placements: scene.placements.map((item) => item.id === selectedPlacementId ? { ...item, ...patch } : item) })
  }

  function deletePlacement() {
    if (!selectedPlacementId) return
    onChange({ ...scene, placements: scene.placements.filter((item) => item.id !== selectedPlacementId) })
    setSelectedPlacementId(null)
  }

  function saveScene() {
    localStorage.setItem('ant-garden:mvp-scene', JSON.stringify(scene))
    extensionHooks.emit({ type: 'scene.saved', sceneId: scene.id })
    setSaved(true)
    window.setTimeout(() => setSaved(false), 1600)
  }

  function readAssetImage(files: FileList | null) {
    const file = files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => { setAssetImage(String(reader.result)); setAssetImageName(file.name) }
    reader.readAsDataURL(file)
  }

  function inferKind(value: string): AssetKind {
    if (/树|木|灌木|plant|tree/i.test(value)) return 'tree'
    if (/灯|light|lamp/i.test(value)) return 'lamp'
    if (/花|flower/i.test(value)) return 'flower'
    if (/亭|屋|pavilion|house/i.test(value)) return 'pavilion'
    if (/灌木|shrub/i.test(value)) return 'shrub'
    if (/草|grass/i.test(value)) return 'grass'
    if (/椅|长凳|bench/i.test(value)) return 'bench'
    if (/门|拱|arch/i.test(value)) return 'arch'
    if (/蘑菇|mushroom/i.test(value)) return 'mushroom'
    if (/水|池|water/i.test(value)) return 'water'
    if (/标|牌|sign|marker/i.test(value)) return 'marker'
    return 'rock'
  }

  function generatePersonalAsset() {
    if (!assetPrompt.trim() && !assetImage) return
    setGeneratingAsset(true)
    window.setTimeout(() => {
      const id = crypto.randomUUID()
      onChange({
        ...scene,
        assets: [{
          id,
          name: assetPrompt.trim().slice(0, 18) || assetImageName.replace(/\.[^.]+$/, '') || '我的生成素材',
          kind: inferKind(`${assetPrompt} ${assetImageName}`),
          source: 'generated',
          status: 'ready',
          previewUrl: assetImage,
          maxInstances: 1,
        }, ...scene.assets],
      })
      setGeneratingAsset(false)
      setAssetPrompt('')
      setAssetImage(undefined)
      setAssetImageName('')
      setLibraryTab('generated')
    }, 1100)
  }

  const visibleAssets = scene.assets.filter((asset) => libraryTab === 'library' ? asset.source === 'library' : asset.source === 'generated')

  return (
    <main className="editor-shell">
      <header className="editor-header">
        <div className="brand-mark"><span>⬡</span><div><b>ANT GARDEN</b><small>SCENE STUDIO / MVP</small></div></div>
        <div className="project-name"><small>当前花园</small><strong>{scene.profile.title}</strong></div>
        <div className="editor-actions">
          <button className="secondary-button" onClick={onRestart}>新建</button>
          <button className="secondary-button" onClick={saveScene}>{saved ? '已保存 ✓' : '保存'}</button>
        </div>
      </header>

      <aside className="asset-sidebar">
        <div className="sidebar-title"><div><small>ASSET LIBRARY</small><h3>素材库</h3></div><span>{scene.assets.length}</span></div>
        <div className="library-tabs">
          <button className={libraryTab === 'library' ? 'active' : ''} onClick={() => setLibraryTab('library')}>场景素材</button>
          <button className={libraryTab === 'generated' ? 'active' : ''} onClick={() => setLibraryTab('generated')}>我的生成</button>
        </div>
        <details className="asset-generator" open={libraryTab === 'generated'}>
          <summary><span>＋</span>生成新素材</summary>
          <textarea value={assetPrompt} onChange={(event) => setAssetPrompt(event.target.value)} placeholder="描述一个物件，例如：带有苔藓的旧石灯" />
          <label className={assetImage ? 'has-image' : ''}>
            <input type="file" accept="image/*" onChange={(event) => readAssetImage(event.target.files)} />
            {assetImage ? <img src={assetImage} alt="素材参考" /> : <span>上传参考图片（可选）</span>}
          </label>
          <button disabled={generatingAsset || (!assetPrompt.trim() && !assetImage)} onClick={generatePersonalAsset}>{generatingAsset ? '正在生成…' : '生成单件素材'}</button>
          <small>个人生成素材为唯一实例，每个只能放置一次。</small>
        </details>
        <p className="sidebar-help">像素材窗口一样拖到场景中；也可以先点击，再点击地块。</p>
        <div className="asset-list">
          {visibleAssets.map((asset) => {
            const usedCount = scene.placements.filter((item) => item.assetId === asset.id).length
            const exhausted = Boolean(asset.maxInstances && usedCount >= asset.maxInstances)
            return <button
              key={asset.id}
              draggable={!exhausted}
              disabled={exhausted}
              className={`${selectedAssetId === asset.id ? 'selected' : ''} ${exhausted ? 'exhausted' : ''}`}
              onDragStart={(event) => { event.dataTransfer.setData('application/x-ant-garden-asset', asset.id); event.dataTransfer.effectAllowed = 'copy' }}
              onClick={() => { if (!exhausted) { setSelectedAssetId(asset.id); setSelectedPlacementId(null) } }}
            >
              <span className={`mini-asset ${asset.kind}`} style={asset.previewUrl ? { backgroundImage: `url(${asset.previewUrl})` } : undefined}>⬡</span>
              <div><strong>{asset.name}</strong><small>{asset.source === 'generated' ? '生成素材' : '基础素材'}</small></div>
              <i>{exhausted ? '已放置 1/1' : asset.maxInstances ? `${usedCount}/1` : '可重复'}</i>
            </button>
          })}
          {!visibleAssets.length && <div className="empty-library">还没有个人素材<br />在上方输入图文生成一个</div>}
        </div>
      </aside>

      <section
        className="viewport"
        onDragOver={(event) => { if (event.dataTransfer.types.includes('application/x-ant-garden-asset')) { event.preventDefault(); event.dataTransfer.dropEffect = 'copy' } }}
        onDrop={(event) => {
          event.preventDefault()
          const assetId = event.dataTransfer.getData('application/x-ant-garden-asset')
          if (assetId) setDropRequest({ assetId, clientX: event.clientX, clientY: event.clientY, nonce: Date.now() })
        }}
      >
        <HexScene
          scene={scene}
          selectedAssetId={selectedAssetId}
          selectedPlacementId={selectedPlacementId}
          onCellClick={placeSelected}
          onPlacementClick={(id) => { setSelectedPlacementId(id); setSelectedAssetId(null) }}
          dropRequest={dropRequest}
          onDropResolved={(assetId, cellId) => { placeAsset(assetId, cellId); setDropRequest(null) }}
        />
        <div className="path-source-preview">
          {scene.sourceImageUrl && <img src={scene.sourceImageUrl} alt="硬件路径原图" />}
          <div><small>HARDWARE PATH</small><strong>地块与路径来自上传照片</strong></div>
        </div>
        <div className="viewport-hint"><span>拖入素材</span><span>拖动旋转</span><span>滚轮缩放</span><span>点击选择</span></div>
        <div className="scene-stats"><b>{scene.cells.length}</b><span>GROUND CELLS</span><b>{scene.pathHexes?.length ?? 0}</b><span>PATH CELLS</span><b>{scene.placements.length}</b><span>OBJECTS</span></div>
      </section>

      <aside className="property-sidebar">
        <div className="sidebar-title"><div><small>INSPECTOR</small><h3>属性</h3></div></div>
        {selectedPlacement && selectedAsset ? (
          <div className="property-content">
            <div className="selected-preview"><span>⬡</span><div><strong>{selectedAsset.name}</strong><small>{selectedAsset.kind} / {selectedAsset.source}</small></div></div>
            <label><span>旋转</span><output>{Math.round(selectedPlacement.rotation * 180 / Math.PI)}°</output></label>
            <input type="range" min="0" max={Math.PI * 2} step={Math.PI / 12} value={selectedPlacement.rotation} onChange={(event) => updatePlacement({ rotation: Number(event.target.value) })} />
            <label><span>缩放</span><output>{selectedPlacement.scale.toFixed(1)}×</output></label>
            <input type="range" min="0.5" max="1.8" step="0.1" value={selectedPlacement.scale} onChange={(event) => updatePlacement({ scale: Number(event.target.value) })} />
            <div className="data-card"><small>所属地块</small><code>{selectedPlacement.cellId}</code></div>
            <button className="delete-button" onClick={deletePlacement}>删除这个物件</button>
          </div>
        ) : (
          <div className="empty-inspector"><span>◇</span><h4>选择一个物件</h4><p>点击场景中的素材，可以调整旋转和大小。</p></div>
        )}
        <div className="legend">
          <strong>地块图例</strong>
          <div><i className="core" />核心<i className="path" />蚂蚁路径</div>
          <div><i className="environment" />环境<i className="boundary" />边界</div>
        </div>
      </aside>
    </main>
  )
}
