import type { SceneProfile, UserInput } from '../types'

interface Props {
  input: UserInput
  profile: SceneProfile
  onChange(profile: SceneProfile): void
  onBack(): void
  onGenerate(): void
}

export function ReviewStep({ input, profile, onChange, onBack, onGenerate }: Props) {
  const updateList = (key: 'mood' | 'terrain', value: string) => {
    onChange({ ...profile, [key]: value.split('、').map((item) => item.trim()).filter(Boolean) })
  }

  return (
    <main className="review-shell">
      <header className="topbar">
        <button className="text-button" onClick={onBack}>← 返回输入</button>
        <div><span className="active-step">01 输入</span><b>02 理解</b><span>03 生成</span><span>04 搭建</span></div>
        <span className="prototype-badge">LOCAL MVP</span>
      </header>

      <section className="review-grid">
        <div className="source-panel">
          <div className="eyebrow">USER EVIDENCE</div>
          <h2>你的输入</h2>
          <blockquote>{input.text || '仅使用图像输入'}</blockquote>
          <div className="source-images">
            {input.images.map((image) => <img key={image.id} src={image.url} alt={image.name} />)}
            {!input.images.length && <div className="empty-image">未上传图像</div>}
          </div>
        </div>

        <div className="interpretation-panel">
          <div className="field-header">
            <div><div className="eyebrow">SYSTEM INTERPRETATION</div><h2>系统如何理解这个地方</h2></div>
            <span className="editable-label">可修改</span>
          </div>

          <label className="form-field">
            <span>花园名称</span>
            <input value={profile.title} onChange={(event) => onChange({ ...profile, title: event.target.value })} />
          </label>

          <label className="form-field">
            <span>环境类型</span>
            <select value={profile.environment} onChange={(event) => onChange({ ...profile, environment: event.target.value as SceneProfile['environment'] })}>
              <option value="riverside">河岸 / 湿润</option>
              <option value="forest">森林 / 山地</option>
              <option value="urban">城市 / 街巷</option>
              <option value="garden">花园 / 混合</option>
              <option value="coastal">海岸 / 沙地</option>
            </select>
          </label>

          <label className="form-field">
            <span>氛围关键词</span>
            <input value={profile.mood.join('、')} onChange={(event) => updateList('mood', event.target.value)} />
          </label>

          <label className="form-field">
            <span>地表材料</span>
            <input value={profile.terrain.join('、')} onChange={(event) => updateList('terrain', event.target.value)} />
          </label>

          <div className="form-field">
            <span>色彩取样</span>
            <div className="palette-row">
              {profile.palette.map((color, index) => (
                <label className="color-swatch" key={`${color}-${index}`} style={{ background: color }}>
                  <input type="color" value={color} onChange={(event) => {
                    const palette = [...profile.palette]
                    palette[index] = event.target.value
                    onChange({ ...profile, palette })
                  }} />
                </label>
              ))}
            </div>
          </div>

          <div className="asset-proposals">
            <span>建议生成的特色素材</span>
            {profile.assetIdeas.map((asset, index) => (
              <div key={`${asset.name}-${index}`}>
                <span className="asset-icon">{index + 1}</span>
                <input value={asset.name} onChange={(event) => {
                  const assetIdeas = [...profile.assetIdeas]
                  assetIdeas[index] = { ...asset, name: event.target.value }
                  onChange({ ...profile, assetIdeas })
                }} />
                <small>{asset.source === 'input' ? '来自输入' : '系统补充'}</small>
              </div>
            ))}
          </div>

          <div className="review-actions">
            <p>系统只整理可见线索；地方含义仍由你确认。</p>
            <button className="primary-button" onClick={onGenerate}>确认并生成花园 <span>→</span></button>
          </div>
        </div>
      </section>
    </main>
  )
}
