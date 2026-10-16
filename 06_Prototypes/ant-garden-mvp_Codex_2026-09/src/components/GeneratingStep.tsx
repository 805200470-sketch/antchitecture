import type { SceneProfile } from '../types'

interface Props { profile: SceneProfile; progress: number }

export function GeneratingStep({ profile, progress }: Props) {
  const tasks = [
    { label: '建立六边形地块', at: 12 },
    { label: '生成可通行路径', at: 28 },
    { label: '划分环境区域', at: 45 },
    { label: '生成特色 3D 素材', at: 66 },
    { label: '自动布置与碰撞检查', at: 88 },
  ]

  return (
    <main className="generation-shell">
      <div className="generation-orbit">
        <div className="hex-mark">⬡</div>
        <div className="orbit orbit-a" />
        <div className="orbit orbit-b" />
      </div>
      <div className="eyebrow">GENERATING / {String(Math.min(99, progress)).padStart(2, '0')}%</div>
      <h1>正在搭建<br />“{profile.title}”</h1>
      <div className="progress-track"><span style={{ width: `${progress}%` }} /></div>
      <div className="task-list">
        {tasks.map((task) => (
          <div key={task.label} className={progress >= task.at ? 'done' : progress + 12 >= task.at ? 'active' : ''}>
            <i /> <span>{task.label}</span><small>{progress >= task.at ? '完成' : progress + 12 >= task.at ? '处理中' : '等待'}</small>
          </div>
        ))}
      </div>
      <p>当前使用程序化素材模拟生成结果；真实 3D 服务可通过 provider 接口接入。</p>
    </main>
  )
}
