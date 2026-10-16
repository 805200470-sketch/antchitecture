import { useMemo, useState } from 'react'
import type { UserInput } from '../types'

interface Props {
  onSubmit(input: UserInput): void
}

export function InputStep({ onSubmit }: Props) {
  const [text, setText] = useState('雨后的河岸，石板路边生长着苔藓，附近有一盏旧石灯。')
  const [images, setImages] = useState<UserInput['images']>([])
  const canSubmit = useMemo(() => images.length === 1, [images])

  function handleFiles(files: FileList | null) {
    const file = files?.[0]
    if (!file) return
    const reader = new FileReader()
    reader.onload = () => setImages([{ id: crypto.randomUUID(), name: file.name, url: String(reader.result) }])
    reader.readAsDataURL(file)
  }

  return (
    <main className="landing-shell">
      <section className="hero-copy">
        <div className="eyebrow">ANT GARDEN / MVP 01</div>
        <h1>把一个地方，<br />变成可搭建的花园。</h1>
        <p>输入一段记忆、一张照片，或两者一起。系统先解释环境，再生成可编辑的六边形 3D 场景。</p>
        <div className="flow-line">
          <span>输入</span><i /> <span>理解</span><i /> <span>生成</span><i /> <span>搭建</span>
        </div>
      </section>

      <section className="input-card">
        <div className="field-header">
          <span>01 / 上传路径与环境</span>
          <small>需要 1 张硬件路径照片</small>
        </div>
        <textarea
          value={text}
          onChange={(event) => setText(event.target.value)}
          placeholder="可选：补充这张照片对应的地点、环境或记忆……"
        />

        <label className="drop-zone">
          <input type="file" accept="image/*" onChange={(event) => handleFiles(event.target.files)} />
          <strong>{images.length ? '更换路径照片' : '上传硬件采集的路径照片'}</strong>
          <span>系统将从照片取色，并把路径映射到六边形地块</span>
        </label>

        {images.length > 0 && (
          <div className="image-strip single">
            {images.map((image) => <img key={image.id} src={image.url} alt={image.name} />)}
          </div>
        )}

        <button className="primary-button" disabled={!canSubmit} onClick={() => onSubmit({ text, images })}>
          生成场景设定 <span>→</span>
        </button>
        <p className="privacy-note">MVP 当前在本地处理流程状态；接入真实服务后需增加上传与数据授权说明。</p>
      </section>
    </main>
  )
}
