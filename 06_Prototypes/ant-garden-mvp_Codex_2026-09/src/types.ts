export type Stage = 'input' | 'review' | 'generating' | 'editor'

export type EnvironmentType = 'riverside' | 'forest' | 'urban' | 'garden' | 'coastal'
export type CellType = 'core' | 'path' | 'environment' | 'boundary' | 'portal'
export type AssetKind = 'tree' | 'rock' | 'lamp' | 'flower' | 'marker' | 'pavilion' | 'shrub' | 'grass' | 'bench' | 'arch' | 'mushroom' | 'water'

export interface UserInput {
  text: string
  images: Array<{ id: string; name: string; url: string }>
}

export interface SceneProfile {
  title: string
  environment: EnvironmentType
  description: string
  mood: string[]
  palette: string[]
  terrain: string[]
  assetIdeas: Array<{ name: string; kind: AssetKind; source: 'input' | 'system' }>
  pathStyle: 'curved' | 'direct' | 'branching'
}

export interface HexCell {
  id: string
  q: number
  r: number
  type: CellType
  surface: string
  height: number
  walkable: boolean
  sampledColor?: string
  pathStrength?: number
  worldX?: number
  worldZ?: number
  size?: number
}

export interface AssetDefinition {
  id: string
  name: string
  kind: AssetKind
  source: 'library' | 'generated'
  status: 'ready' | 'processing' | 'failed'
  modelUrl?: string
  previewUrl?: string
  maxInstances?: number
}

export interface Placement {
  id: string
  assetId: string
  cellId: string
  rotation: number
  scale: number
}

export interface GardenScene {
  version: 1
  id: string
  profile: SceneProfile
  sourceImageUrl?: string
  cells: HexCell[]
  assets: AssetDefinition[]
  placements: Placement[]
  pathCellIds: string[]
  pathPolylines: Array<Array<[number, number]>>
  pathHexes: Array<{ id: string; x: number; z: number }>
  navigationGraph: {
    nodes: Array<{ id: string; cellId: string }>
    edges: Array<{ from: string; to: string }>
  }
  entryPoints: string[]
  interactionAnchors: string[]
  social: {
    ownerId: string | null
    visibility: 'private' | 'friends' | 'public'
    connectedGardenIds: string[]
  }
}
