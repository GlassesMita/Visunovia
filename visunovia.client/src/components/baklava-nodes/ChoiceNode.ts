import { defineDynamicNode, NodeInterface } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import {
  ARROW_SYMBOL,
  createExecInPort,
  setNodeI18nTitle,
} from './BaseNode'

export const NODE_COLOR = '#C2185B'

/**
 * Choice 选项数量上限（推荐值，超过时显示警告但不阻止）
 * 供外部 Bound 组件调用以确定最大可安装选项数
 */
export const MAX_RECOMMENDED_OPTIONS = 3

export default defineDynamicNode({
  type: 'ChoiceNode',
  title: 'Choice',

  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),

    /**
     * 选项数量控制器
     * 使用 TextInputInterface 而非 NumberInterface，因为 BaklavaJS 的 NumberInterface
     * 步长硬编码为 0.1，无法配置为整数增量。
     * TextInputInterface 接受用户输入字符串，内部解析为整数（向下取整）。
     * 默认值为 "2"。
     */
    optionCount: () => new TextInputInterface('Option Count', '2'),
  },

  onCreate() {
    setNodeI18nTitle(this, 'nodes.choice', 'Choice')
  },

  /**
   * 动态更新节点的输入输出端口配置
   * 根据 optionCount 属性生成对应数量的选项：
   *   每个选项包含一个 TextInputInterface（选项文本）和一个 execOut 端口
   *
   * @param params - 当前节点属性快照，至少包含 optionCount 字段
   * @returns 动态生成的 inputs 与 outputs 端口映射
   */
  onUpdate({ optionCount }) {
    // 解析 optionCount 为正整数：
    // - 将输入值转为数字（支持字符串或数字）
    // - Math.floor 向下取整（如 2.5 → 2，"2.9" → 2）
    // - Math.max(1, ...) 确保最小值为 1
    const rawValue = typeof optionCount === 'string' ? parseFloat(optionCount) : optionCount
    const count = Math.max(1, Math.floor(Number(rawValue) || 2))

    const inputs: Record<string, () => NodeInterface<any>> = {}
    const outputs: Record<string, () => NodeInterface<any>> = {}

    // 生成指定数量的选项
    for (let i = 1; i <= count; i++) {
      // 每个选项对应一个 TextInput（选项文本）和一个 execOut 端口
      inputs[`choiceText_${i}`] = () =>
        new TextInputInterface(`Option ${i}`, '')

      outputs[`execOut_${i}`] = () =>
        new NodeInterface(ARROW_SYMBOL, undefined).setPort(true)
    }

    return { inputs, outputs }
  },
})