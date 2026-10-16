import type { AssetDefinition, AssetKind } from '../types'

export interface AssetRequest {
  name: string
  kind: AssetKind
  prompt: string
  imageUrl?: string
}

export interface AssetJob {
  id: string
  status: 'queued' | 'processing' | 'succeeded' | 'failed'
  progress: number
  result?: AssetDefinition
}

export interface AssetGenerator {
  create(request: AssetRequest): Promise<AssetJob>
  getJob(jobId: string): Promise<AssetJob>
}

/**
 * MVP provider: returns procedural assets rendered by Three.js primitives.
 * Replace this class with BackendAssetGenerator when a real 3D API is connected.
 */
export class MockAssetGenerator implements AssetGenerator {
  private jobs = new Map<string, AssetJob>()

  async create(request: AssetRequest) {
    const id = crypto.randomUUID()
    const job: AssetJob = { id, status: 'processing', progress: 10 }
    this.jobs.set(id, job)

    window.setTimeout(() => {
      this.jobs.set(id, {
        id,
        status: 'succeeded',
        progress: 100,
        result: {
          id: `generated_${id}`,
          name: request.name,
          kind: request.kind,
          source: 'generated',
          status: 'ready',
          previewUrl: request.imageUrl,
          maxInstances: 1,
        },
      })
    }, 900)

    return job
  }

  async getJob(jobId: string) {
    return this.jobs.get(jobId) ?? { id: jobId, status: 'failed', progress: 0 }
  }
}

/** Expected backend contract; API keys stay on the server. */
export class BackendAssetGenerator implements AssetGenerator {
  constructor(private readonly baseUrl = '/api') {}

  async create(request: AssetRequest): Promise<AssetJob> {
    const response = await fetch(`${this.baseUrl}/assets/generate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })
    if (!response.ok) throw new Error('无法创建 3D 素材任务')
    return response.json()
  }

  async getJob(jobId: string): Promise<AssetJob> {
    const response = await fetch(`${this.baseUrl}/assets/jobs/${jobId}`)
    if (!response.ok) throw new Error('无法读取 3D 素材任务')
    return response.json()
  }
}
