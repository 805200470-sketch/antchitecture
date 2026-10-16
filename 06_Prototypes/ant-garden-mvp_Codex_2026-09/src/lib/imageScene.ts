import type { HexCell, SceneProfile } from '../types'

const IMAGE_SIZE = 128
const GRID_RADIUS = 6
export const ADAPTIVE_HEX_SIZE = 0.72
export const PATH_HEX_SIZE = 0.235
export const PATH_WORLD_WIDTH = 10.4
export const PATH_WORLD_HEIGHT = 10.4

const neighborOffsets = [
  [-1, -1], [0, -1], [1, -1], [1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0],
]

const axialNeighbors = [
  [1, 0], [1, -1], [0, -1], [-1, 0], [-1, 1], [0, 1],
]

interface SkeletonResult {
  polylines: Array<Array<[number, number]>>
  endpoints: Array<[number, number]>
  junctions: Array<[number, number]>
  skeletonPoints: Array<[number, number]>
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image()
    image.onload = () => resolve(image)
    image.onerror = () => reject(new Error('无法读取路径图像'))
    image.src = url
  })
}

function deterministicNoise(q: number, r: number, seed: number) {
  const value = Math.sin(q * 12.9898 + r * 78.233 + seed * 37.719) * 43758.5453
  return value - Math.floor(value)
}

function otsuThreshold(grays: Uint8Array) {
  const histogram = new Array<number>(256).fill(0)
  grays.forEach((value) => { histogram[value] += 1 })
  const total = grays.length
  let totalMean = 0
  for (let i = 0; i < 256; i += 1) totalMean += i * histogram[i]
  let backgroundWeight = 0
  let backgroundMean = 0
  let bestVariance = -1
  let threshold = 128

  for (let i = 0; i < 256; i += 1) {
    backgroundWeight += histogram[i]
    if (!backgroundWeight) continue
    const foregroundWeight = total - backgroundWeight
    if (!foregroundWeight) break
    backgroundMean += i * histogram[i]
    const meanBackground = backgroundMean / backgroundWeight
    const meanForeground = (totalMean - backgroundMean) / foregroundWeight
    const variance = backgroundWeight * foregroundWeight * (meanBackground - meanForeground) ** 2
    if (variance > bestVariance) {
      bestVariance = variance
      threshold = i
    }
  }
  return Math.min(225, Math.max(55, threshold + 24))
}

function largestComponent(mask: Uint8Array, width: number, height: number) {
  const visited = new Uint8Array(mask.length)
  let largest: number[] = []
  for (let index = 0; index < mask.length; index += 1) {
    if (!mask[index] || visited[index]) continue
    const component: number[] = []
    const queue = [index]
    visited[index] = 1
    while (queue.length) {
      const current = queue.pop()!
      component.push(current)
      const x = current % width
      const y = Math.floor(current / width)
      neighborOffsets.forEach(([dx, dy]) => {
        const nx = x + dx
        const ny = y + dy
        if (nx < 0 || ny < 0 || nx >= width || ny >= height) return
        const next = ny * width + nx
        if (mask[next] && !visited[next]) {
          visited[next] = 1
          queue.push(next)
        }
      })
    }
    if (component.length > largest.length) largest = component
  }
  const output = new Uint8Array(mask.length)
  largest.forEach((index) => { output[index] = 1 })
  return output
}

function zhangSuenThinning(source: Uint8Array, width: number, height: number) {
  const data = source.slice()
  let changed = true
  let iterations = 0

  while (changed && iterations < 80) {
    changed = false
    iterations += 1
    for (let step = 0; step < 2; step += 1) {
      const deletions: number[] = []
      for (let y = 1; y < height - 1; y += 1) {
        for (let x = 1; x < width - 1; x += 1) {
          const index = y * width + x
          if (!data[index]) continue
          const p2 = data[(y - 1) * width + x]
          const p3 = data[(y - 1) * width + x + 1]
          const p4 = data[y * width + x + 1]
          const p5 = data[(y + 1) * width + x + 1]
          const p6 = data[(y + 1) * width + x]
          const p7 = data[(y + 1) * width + x - 1]
          const p8 = data[y * width + x - 1]
          const p9 = data[(y - 1) * width + x - 1]
          const ring = [p2, p3, p4, p5, p6, p7, p8, p9, p2]
          const neighborCount = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9
          let transitions = 0
          for (let i = 0; i < 8; i += 1) if (ring[i] === 0 && ring[i + 1] === 1) transitions += 1
          if (neighborCount < 2 || neighborCount > 6 || transitions !== 1) continue
          const firstCondition = step === 0 ? p2 * p4 * p6 === 0 : p2 * p4 * p8 === 0
          const secondCondition = step === 0 ? p4 * p6 * p8 === 0 : p2 * p6 * p8 === 0
          if (firstCondition && secondCondition) deletions.push(index)
        }
      }
      if (deletions.length) changed = true
      deletions.forEach((index) => { data[index] = 0 })
    }
  }
  return data
}

function pointLineDistance(point: [number, number], start: [number, number], end: [number, number]) {
  const dx = end[0] - start[0]
  const dy = end[1] - start[1]
  if (dx === 0 && dy === 0) return Math.hypot(point[0] - start[0], point[1] - start[1])
  const t = Math.max(0, Math.min(1, ((point[0] - start[0]) * dx + (point[1] - start[1]) * dy) / (dx * dx + dy * dy)))
  return Math.hypot(point[0] - (start[0] + t * dx), point[1] - (start[1] + t * dy))
}

function simplify(points: Array<[number, number]>, epsilon: number): Array<[number, number]> {
  if (points.length <= 2) return points
  let maxDistance = 0
  let splitIndex = 0
  for (let i = 1; i < points.length - 1; i += 1) {
    const distance = pointLineDistance(points[i], points[0], points[points.length - 1])
    if (distance > maxDistance) {
      maxDistance = distance
      splitIndex = i
    }
  }
  if (maxDistance <= epsilon) return [points[0], points[points.length - 1]]
  return [...simplify(points.slice(0, splitIndex + 1), epsilon).slice(0, -1), ...simplify(points.slice(splitIndex), epsilon)]
}

function traceSkeleton(skeleton: Uint8Array, width: number, height: number): SkeletonResult {
  const pixels = new Set<number>()
  skeleton.forEach((value, index) => { if (value) pixels.add(index) })
  const adjacent = (index: number) => {
    const x = index % width
    const y = Math.floor(index / width)
    return neighborOffsets.flatMap(([dx, dy]) => {
      const nx = x + dx
      const ny = y + dy
      if (nx < 0 || ny < 0 || nx >= width || ny >= height) return []
      const candidate = ny * width + nx
      return pixels.has(candidate) ? [candidate] : []
    })
  }
  const keyNodes = [...pixels].filter((index) => adjacent(index).length !== 2)
  const endpoints = keyNodes.filter((index) => adjacent(index).length === 1)
  const junctions = keyNodes.filter((index) => adjacent(index).length >= 3)
  const visitedEdges = new Set<string>()
  const polylines: number[][] = []
  const edgeKey = (a: number, b: number) => a < b ? `${a}:${b}` : `${b}:${a}`

  function walk(start: number, first: number) {
    const line = [start]
    let previous = start
    let current = first
    visitedEdges.add(edgeKey(start, first))
    while (true) {
      line.push(current)
      const candidates = adjacent(current).filter((next) => next !== previous && !visitedEdges.has(edgeKey(current, next)))
      if (adjacent(current).length !== 2 || !candidates.length) break
      const next = candidates[0]
      visitedEdges.add(edgeKey(current, next))
      previous = current
      current = next
      if (current === start) { line.push(current); break }
    }
    if (line.length >= 3) polylines.push(line)
  }

  keyNodes.forEach((start) => adjacent(start).forEach((next) => {
    if (!visitedEdges.has(edgeKey(start, next))) walk(start, next)
  }))
  pixels.forEach((start) => adjacent(start).forEach((next) => {
    if (!visitedEdges.has(edgeKey(start, next))) walk(start, next)
  }))

  const toPoint = (index: number): [number, number] => [(index % width) / (width - 1), Math.floor(index / width) / (height - 1)]
  return {
    polylines: polylines.map((line) => simplify(line.map(toPoint), 0.009)).filter((line) => line.length >= 2),
    endpoints: endpoints.map(toPoint),
    junctions: junctions.map(toPoint),
    skeletonPoints: [...pixels].map(toPoint),
  }
}

async function extractPathGraph(imageUrl: string): Promise<SkeletonResult> {
  const image = await loadImage(imageUrl)
  const canvas = document.createElement('canvas')
  canvas.width = IMAGE_SIZE
  canvas.height = IMAGE_SIZE
  const context = canvas.getContext('2d', { willReadFrequently: true })!
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, IMAGE_SIZE, IMAGE_SIZE)
  const scale = Math.min(IMAGE_SIZE / image.width, IMAGE_SIZE / image.height)
  const width = image.width * scale
  const height = image.height * scale
  context.drawImage(image, (IMAGE_SIZE - width) / 2, (IMAGE_SIZE - height) / 2, width, height)
  const rgba = context.getImageData(0, 0, IMAGE_SIZE, IMAGE_SIZE).data
  const grays = new Uint8Array(IMAGE_SIZE * IMAGE_SIZE)
  for (let index = 0; index < grays.length; index += 1) {
    const offset = index * 4
    grays[index] = Math.round(0.2126 * rgba[offset] + 0.7152 * rgba[offset + 1] + 0.0722 * rgba[offset + 2])
  }
  const threshold = otsuThreshold(grays)
  const binary = new Uint8Array(grays.length)
  grays.forEach((gray, index) => { binary[index] = gray < threshold ? 1 : 0 })
  const cleaned = largestComponent(binary, IMAGE_SIZE, IMAGE_SIZE)
  const skeleton = zhangSuenThinning(cleaned, IMAGE_SIZE, IMAGE_SIZE)
  return traceSkeleton(skeleton, IMAGE_SIZE, IMAGE_SIZE)
}

export function normalizedPathPointToWorld(point: [number, number]): [number, number] {
  return [(point[0] - 0.5) * PATH_WORLD_WIDTH, (point[1] - 0.5) * PATH_WORLD_HEIGHT]
}

function distanceToPath(x: number, z: number, polylines: SkeletonResult['polylines']) {
  let closest = Number.POSITIVE_INFINITY
  polylines.forEach((line) => {
    for (let index = 1; index < line.length; index += 1) {
      const start = normalizedPathPointToWorld(line[index - 1])
      const end = normalizedPathPointToWorld(line[index])
      closest = Math.min(closest, pointLineDistance([x, z], start, end))
    }
  })
  return closest
}

function nearestCellId(cells: HexCell[], point: [number, number]) {
  const [x, z] = normalizedPathPointToWorld(point)
  return cells.reduce<{ id: string; distance: number } | null>((best, cell) => {
    const distance = Math.hypot((cell.worldX ?? 0) - x, (cell.worldZ ?? 0) - z)
    return !best || distance < best.distance ? { id: cell.id, distance } : best
  }, null)?.id
}

function worldToAxial(x: number, z: number, size: number) {
  const q = (Math.sqrt(3) / 3 * x - z / 3) / size
  const r = (2 / 3 * z) / size
  const cubeX = q
  const cubeZ = r
  const cubeY = -cubeX - cubeZ
  let rx = Math.round(cubeX)
  let ry = Math.round(cubeY)
  let rz = Math.round(cubeZ)
  const xDiff = Math.abs(rx - cubeX)
  const yDiff = Math.abs(ry - cubeY)
  const zDiff = Math.abs(rz - cubeZ)
  if (xDiff > yDiff && xDiff > zDiff) rx = -ry - rz
  else if (yDiff > zDiff) ry = -rx - rz
  else rz = -rx - ry
  return { q: rx, r: rz }
}

function createMicroPathHexes(points: Array<[number, number]>) {
  const cells = new Map<string, { id: string; x: number; z: number }>()
  points.forEach((point) => {
    const [worldX, worldZ] = normalizedPathPointToWorld(point)
    const axial = worldToAxial(worldX, worldZ, PATH_HEX_SIZE)
    const id = `path_${axial.q}_${axial.r}`
    cells.set(id, {
      id,
      x: Math.sqrt(3) * PATH_HEX_SIZE * (axial.q + axial.r / 2),
      z: 1.5 * PATH_HEX_SIZE * axial.r,
    })
  })
  return [...cells.values()]
}

export async function mapImageToAdaptiveHexGrid(profile: SceneProfile, imageUrl: string, seed: number) {
  const graph = await extractPathGraph(imageUrl)
  const candidates: HexCell[] = []
  for (let q = -GRID_RADIUS; q <= GRID_RADIUS; q += 1) {
    const rMin = Math.max(-GRID_RADIUS, -q - GRID_RADIUS)
    const rMax = Math.min(GRID_RADIUS, -q + GRID_RADIUS)
    for (let r = rMin; r <= rMax; r += 1) {
      const x = Math.sqrt(3) * ADAPTIVE_HEX_SIZE * (q + r / 2)
      const z = 1.5 * ADAPTIVE_HEX_SIZE * r
      const distance = distanceToPath(x, z, graph.polylines)
      const influence = Number.isFinite(distance) ? Math.exp(-(distance ** 2) / (2 * 1.15 ** 2)) : 0
      const noise = deterministicNoise(q, r, seed)
      const coarseNoise = deterministicNoise(Math.round(q / 2), Math.round(r / 2), seed + 11)
      const axialDistance = Math.max(Math.abs(q), Math.abs(r), Math.abs(-q - r))
      const terrainIndex = Math.floor(noise * profile.terrain.length)
      candidates.push({
        id: `hex_${q}_${r}`,
        q,
        r,
        worldX: x,
        worldZ: z,
        size: 0.57 + influence * 0.09 + coarseNoise * 0.045,
        type: axialDistance === GRID_RADIUS ? 'boundary' : 'environment',
        surface: profile.terrain[terrainIndex] ?? 'soil',
        height: 0.035 + (influence > 0.35 ? noise : coarseNoise) * 0.12,
        walkable: true,
        pathStrength: influence,
      })
    }
  }

  const pathSet = new Set<string>()
  graph.skeletonPoints.forEach((point) => {
    const nearest = nearestCellId(candidates, point)
    if (nearest) pathSet.add(nearest)
  })
  const corePoint = graph.junctions[0] ?? [0.5, 0.5] as [number, number]
  const coreId = nearestCellId(candidates, corePoint)
  const portalIds = new Set(graph.endpoints.map((point) => nearestCellId(candidates, point)).filter((id): id is string => Boolean(id)))
  const cells = candidates.map((cell) => ({
    ...cell,
    type: cell.id === coreId ? 'core' : portalIds.has(cell.id) ? 'portal' : pathSet.has(cell.id) ? 'path' : cell.type,
    surface: pathSet.has(cell.id) || cell.id === coreId || portalIds.has(cell.id) ? 'ant trail' : cell.surface,
    size: pathSet.has(cell.id) || cell.id === coreId || portalIds.has(cell.id) ? ADAPTIVE_HEX_SIZE * 1.006 : cell.size,
    walkable: true,
  } as HexCell))
  const pathCellIds = cells.filter((cell) => ['path', 'core', 'portal'].includes(cell.type)).map((cell) => cell.id)

  return { cells, pathCellIds, pathPolylines: graph.polylines, pathHexes: createMicroPathHexes(graph.skeletonPoints) }
}

export function buildAdaptiveNavigationGraph(cells: HexCell[]) {
  const nodes = cells.filter((cell) => ['path', 'core', 'portal'].includes(cell.type)).map((cell) => ({ id: `nav_${cell.id}`, cellId: cell.id }))
  const nodeIds = new Set(nodes.map((node) => node.cellId))
  const edges: Array<{ from: string; to: string }> = []
  cells.forEach((cell) => {
    if (!nodeIds.has(cell.id)) return
    axialNeighbors.forEach(([dq, dr]) => {
      const neighbor = `hex_${cell.q + dq}_${cell.r + dr}`
      if (nodeIds.has(neighbor) && cell.id < neighbor) edges.push({ from: `nav_${cell.id}`, to: `nav_${neighbor}` })
    })
  })
  return { nodes, edges }
}
