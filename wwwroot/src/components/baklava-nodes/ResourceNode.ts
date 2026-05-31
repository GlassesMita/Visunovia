import { defineNode } from '@baklavajs/core'
import { SelectInterface, TextInputInterface } from '@baklavajs/renderer-vue'
import { type ResourceType, RESOURCE_TYPE_COLORS } from '@/stores/useResourceRegistry'
import { tSync } from '@/services/translationService'
import {
  ARROW_SYMBOL,
  createExecOutPort,
  setNodeI18nTitle,
} from './BaseNode'

export const NODE_COLOR = '#795548'

export default defineNode({
  type: 'ResourceNode',
  title: 'Resources',
  inputs: {
    resourceType: () => new SelectInterface(
      tSync('resource.type', 'Type'),
      'image',
      [
        { value: 'image', text: tSync('resourceTypes.image', 'Image') },
        { value: 'audio', text: tSync('resourceTypes.audio', 'Audio') },
        { value: 'bgm', text: tSync('resourceTypes.bgm', 'BGM') },
        { value: 'voice', text: tSync('resourceTypes.voice', 'Voice') },
        { value: 'video', text: tSync('resourceTypes.video', 'Video') },
        { value: 'scene', text: tSync('resourceTypes.scene', 'Scene') },
        { value: 'font', text: tSync('resourceTypes.font', 'Font') },
        { value: 'data', text: tSync('resourceTypes.data', 'Data') },
      ]
    ),
    resourcePath: () => new TextInputInterface(tSync('properties.resourcePath', 'Path'), ''),
  },
  outputs: {
    resourceOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.resources', 'Resources')
  }
})

/**
 * 根据资源类型获取节点颜色
 * 用于动态着色：通过 CSS data-resource-type 属性实现端口/连线颜色切换
 *
 * @param resourceType - 资源类型（未指定时返回默认节点颜色）
 * @returns 对应的十六进制颜色值
 */
export function getResourceNodeColor(resourceType?: ResourceType): string {
  if (!resourceType) return NODE_COLOR
  return RESOURCE_TYPE_COLORS[resourceType] || NODE_COLOR
}
