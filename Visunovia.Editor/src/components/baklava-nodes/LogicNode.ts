import { defineDynamicNode, NodeInterface } from '@baklavajs/core'
import {
  SelectInterface,
  TextInputInterface,
  NumberInterface
} from '@baklavajs/renderer-vue'
import { LogicType, logicTypeConfig, logicTypeLabels } from '@/types'
import { tSync } from '@/services/translationService'
import {
  ARROW_SYMBOL,
  createExecInPort,
  setNodeI18nTitle,
} from './BaseNode'

/**
 * LogicNode 节点颜色常量
 * 青色系，用于标识逻辑控制类节点
 */
export const NODE_COLOR = '#00BCD4'

export default defineDynamicNode({
  type: 'LogicNode',
  title: 'Logic',

  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),

    /**
     * 子类型选择器
     * 通过 SelectInterface 展示 LogicType 枚举的所有可选值，
     * 默认选中 SetVariable（赋值操作），切换时触发 onUpdate 重构端口
     */
    subType: () => new SelectInterface(
      tSync('props.subType', 'Sub Type'),
      LogicType.SetVariable,
      Object.values(LogicType).map((v) => ({
        value: v,
        text: tSync(logicTypeLabels[v as LogicType] ?? '', logicTypeLabels[v as LogicType] ?? v)
      }))
    )
  },

  onCreate() {
    setNodeI18nTitle(this, 'nodes.logic', 'Logic')
  },

  /**
   * 动态更新节点的输入输出端口配置
   * 根据当前选中的 subType 从 logicTypeConfig 中读取属性定义，
   * 自动生成对应的输入接口（string / number / select），
   * 并根据子类型决定输出端口的模式：
   *   - Conditional（条件分支）：双输出 ✓/✗
   *   - 其他类型（SetVariable、Delay 等）：单输出 →
   *
   * @param params - 当前节点属性快照，至少包含 subType 字段
   * @returns 动态生成的 inputs 与 outputs 端口映射
   */
  onUpdate({ subType }) {
    const config = logicTypeConfig[subType as LogicType]
    if (!config) return {}

    const properties = config.properties || []
    const inputs: Record<string, () => NodeInterface<any>> = {}

    for (const prop of properties) {
      const label = tSync(`properties.${prop.name}`, prop.name)

      switch (prop.type) {
        case 'string':
          inputs[prop.name] = () =>
            new TextInputInterface(label, prop.defaultValue || '')
          break

        case 'number':
          inputs[prop.name] = () =>
            new NumberInterface(label, prop.defaultValue ?? 0)
          break

        case 'select':
          if (prop.options && prop.options.length > 0) {
            inputs[prop.name] = () =>
              new SelectInterface(
                label,
                prop.defaultValue || prop.options![0].value,
                prop.options!.map((o) => ({
                  value: o.value,
                  text: tSync(`logicOptions.${o.value}`, o.label)
                }))
              )
          }
          break
      }
    }

    const outputs: Record<string, () => NodeInterface<any>> = {}

    if (subType === LogicType.Conditional) {
      outputs.execTrue = () => new NodeInterface('✓', undefined).setPort(true)
      outputs.execFalse = () => new NodeInterface('✗', undefined).setPort(true)
    } else {
      outputs.execOut = () => new NodeInterface(ARROW_SYMBOL, undefined).setPort(true)
    }

    return { inputs, outputs }
  }
})
