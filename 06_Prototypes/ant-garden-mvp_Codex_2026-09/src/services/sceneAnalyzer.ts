import type { AssetKind, EnvironmentType, SceneProfile, UserInput } from '../types'

const environmentRules: Array<{ words: string[]; value: EnvironmentType }> = [
  { words: ['海', '沙', '港', 'coast', 'beach'], value: 'coastal' },
  { words: ['河', '湖', '水', '雨', 'river', 'water'], value: 'riverside' },
  { words: ['街', '城市', '建筑', 'urban', 'city'], value: 'urban' },
  { words: ['树林', '森林', '山', 'forest', 'mountain'], value: 'forest' },
]

const paletteMap: Record<EnvironmentType, string[]> = {
  riverside: ['#78877b', '#ad9f84', '#d0b56f', '#50635f'],
  forest: ['#405746', '#6e7757', '#a08962', '#d2c99b'],
  urban: ['#6d726e', '#9b8876', '#c8b999', '#465653'],
  garden: ['#678064', '#a2a66f', '#d2aa72', '#e1d6ad'],
  coastal: ['#69939a', '#c6b78f', '#e3d6b7', '#506d71'],
}

const terrainMap: Record<EnvironmentType, string[]> = {
  riverside: ['moss', 'stone', 'wet soil'],
  forest: ['soil', 'moss', 'fallen leaves'],
  urban: ['stone', 'brick', 'soil'],
  garden: ['grass', 'soil', 'gravel'],
  coastal: ['sand', 'stone', 'grass'],
}

const assetMap: Record<EnvironmentType, Array<{ name: string; kind: AssetKind }>> = {
  riverside: [{ name: '河岸石灯', kind: 'lamp' }, { name: '潮湿石块', kind: 'rock' }, { name: '低矮灌木', kind: 'tree' }],
  forest: [{ name: '林间老树', kind: 'tree' }, { name: '苔藓岩石', kind: 'rock' }, { name: '木制标记', kind: 'marker' }],
  urban: [{ name: '街角路灯', kind: 'lamp' }, { name: '旧门牌', kind: 'marker' }, { name: '庭院树', kind: 'tree' }],
  garden: [{ name: '花园小树', kind: 'tree' }, { name: '季节花簇', kind: 'flower' }, { name: '休息亭', kind: 'pavilion' }],
  coastal: [{ name: '海岸岩石', kind: 'rock' }, { name: '风向标记', kind: 'marker' }, { name: '耐盐灌木', kind: 'tree' }],
}

export async function analyzeInput(input: UserInput): Promise<SceneProfile> {
  await new Promise((resolve) => window.setTimeout(resolve, 750))
  const evidence = `${input.text} ${input.images.map((image) => image.name).join(' ')}`.toLowerCase()
  const environment = environmentRules.find((rule) => rule.words.some((word) => evidence.includes(word)))?.value ?? 'garden'
  const assets = assetMap[environment]

  return {
    title: input.text.trim().slice(0, 24) || input.images[0]?.name.replace(/\.[^.]+$/, '') || '未命名的地方花园',
    environment,
    description: input.text.trim() || '由用户图像生成的地方环境。请在生成前补充或修改系统理解。',
    mood: evidence.includes('夜') ? ['安静', '夜晚', '缓慢'] : ['自然', '安静', '可探索'],
    palette: paletteMap[environment],
    terrain: terrainMap[environment],
    assetIdeas: assets.map((asset, index) => ({ ...asset, source: index === 0 ? 'input' : 'system' })),
    pathStyle: environment === 'urban' ? 'direct' : 'branching',
  }
}
