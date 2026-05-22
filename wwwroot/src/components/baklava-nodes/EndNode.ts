import { defineNode } from '@baklavajs/core'
import { SelectInterface, TextInputInterface } from '@baklavajs/renderer-vue'
import {
  ARROW_SYMBOL,
  createExecInPort,
  setNodeI18nTitle,
} from './BaseNode'

/**
 * EndNode 纯终点节点
 * 用于处理 EventNode 无法覆盖的场景级终止事件，不含 execOut 输出端口：
 * - end_game: 结束游戏并返回标题
 * - return_to_menu: 返回主菜单
 * - jump_to_scene: 跳转到指定场景（需配合 sceneId 使用）
 */
export type EndEventType = 'end_game' | 'return_to_menu' | 'jump_to_scene'

export const NODE_COLOR = '#C62828'

export default defineNode({
  type: 'EndNode',
  title: 'End',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    eventType: () => new SelectInterface(
      'Event Type',
      'end_game',
      [
        { value: 'end_game', text: '结束游戏' },
        { value: 'return_to_menu', text: '返回主菜单' },
        { value: 'jump_to_scene', text: '跳转场景' },
      ]
    ),
    sceneId: () => new TextInputInterface('Scene ID', ''),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.end', 'End')
  }
})
