import type { GardenScene } from '../types'

export type GardenEvent =
  | { type: 'scene.created'; sceneId: string }
  | { type: 'scene.saved'; sceneId: string }
  | { type: 'navigation.requested'; sceneId: string; entryPoint: string }
  | { type: 'garden.connection.requested'; sceneId: string; targetGardenId: string }

export interface ExtensionHooks {
  emit(event: GardenEvent): void
  validateForAgents(scene: GardenScene): { valid: boolean; issues: string[] }
}

export const extensionHooks: ExtensionHooks = {
  emit(event) {
    window.dispatchEvent(new CustomEvent('ant-garden:event', { detail: event }))
  },
  validateForAgents(scene) {
    const issues: string[] = []
    if (!scene.navigationGraph.nodes.length) issues.push('缺少导航节点')
    if (!scene.entryPoints.length) issues.push('缺少花园入口')
    return { valid: issues.length === 0, issues }
  },
}
