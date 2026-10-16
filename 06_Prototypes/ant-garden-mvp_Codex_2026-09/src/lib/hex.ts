import type { HexCell, SceneProfile } from '../types'

export const HEX_SIZE = 1.08

export function hexToWorld(q: number, r: number): [number, number, number] {
  const x = Math.sqrt(3) * HEX_SIZE * (q + r / 2)
  const z = 1.5 * HEX_SIZE * r
  return [x, 0, z]
}

export function cellToWorld(cell: Pick<HexCell, 'q' | 'r' | 'worldX' | 'worldZ'>): [number, number, number] {
  if (typeof cell.worldX === 'number' && typeof cell.worldZ === 'number') return [cell.worldX, 0, cell.worldZ]
  return hexToWorld(cell.q, cell.r)
}

function hash(q: number, r: number, seed: number) {
  const value = Math.sin(q * 12.9898 + r * 78.233 + seed * 37.719) * 43758.5453
  return value - Math.floor(value)
}

export function createHexGrid(profile: SceneProfile, seed = 1, radius = 3): HexCell[] {
  const cells: HexCell[] = []

  for (let q = -radius; q <= radius; q += 1) {
    const rMin = Math.max(-radius, -q - radius)
    const rMax = Math.min(radius, -q + radius)

    for (let r = rMin; r <= rMax; r += 1) {
      const distance = Math.max(Math.abs(q), Math.abs(r), Math.abs(-q - r))
      const isCore = q === 0 && r === 0
      const isMainPath = q === 0
      const isBranch = r === 0 && q >= -2 && q <= 2
      const isPortal = q === 0 && r === -radius
      const isBoundary = distance === radius && !isPortal
      const random = hash(q, r, seed)

      let type: HexCell['type'] = 'environment'
      if (isCore) type = 'core'
      else if (isPortal) type = 'portal'
      else if (isMainPath || (profile.pathStyle === 'branching' && isBranch)) type = 'path'
      else if (isBoundary && random > 0.42) type = 'boundary'

      const terrainIndex = Math.floor(random * profile.terrain.length)
      const surface = type === 'path' ? 'stone path' : profile.terrain[terrainIndex] ?? 'soil'

      cells.push({
        id: `hex_${q}_${r}`,
        q,
        r,
        type,
        surface,
        height: type === 'boundary' ? 0.18 + random * 0.28 : random * 0.08,
        walkable: type !== 'boundary',
      })
    }
  }

  return cells
}

export function buildNavigationGraph(cells: HexCell[]) {
  const pathCells = cells.filter((cell) => ['core', 'path', 'portal'].includes(cell.type))
  const nodes = pathCells.map((cell) => ({ id: `nav_${cell.id}`, cellId: cell.id }))
  const directions = [
    [1, 0], [1, -1], [0, -1], [-1, 0], [-1, 1], [0, 1],
  ]
  const pathIds = new Set(pathCells.map((cell) => cell.id))
  const edges: Array<{ from: string; to: string }> = []

  pathCells.forEach((cell) => {
    directions.forEach(([dq, dr]) => {
      const neighbor = `hex_${cell.q + dq}_${cell.r + dr}`
      if (pathIds.has(neighbor) && cell.id < neighbor) {
        edges.push({ from: `nav_${cell.id}`, to: `nav_${neighbor}` })
      }
    })
  })

  return { nodes, edges }
}
