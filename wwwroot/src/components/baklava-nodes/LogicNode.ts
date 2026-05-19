import { defineDynamicNode } from '@baklavajs/core'
import { NodeInterface, DynamicNodeDefinition } from '@baklavajs/core'
import { SelectInterface, TextInputInterface, NumberInterface } from '@baklavajs/renderer-vue'
import { LogicType, logicTypeConfig } from '@/types'

export default defineDynamicNode({
  type: 'LogicNode',
  title: 'nodes.logic',
  inputs: {
    execIn: () => new NodeInterface('exec_in', undefined),
    subType: () => new SelectInterface('sub_type', LogicType.SetVariable, 
      Object.values(LogicType).map(v => ({
        value: v,
        text: logicTypeConfig[v].labelKey
      })) as any
    )
  },
  outputs: {
    execOut: () => new NodeInterface('exec_out', undefined),
    execTrue: () => new NodeInterface('exec_true', undefined),
    execFalse: () => new NodeInterface('exec_false', undefined)
  },
  onUpdate({ subType }) {
    const properties = logicTypeConfig[subType as LogicType]?.properties || []
    const inputs: DynamicNodeDefinition = {}

    properties.forEach((prop: any) => {
      if (prop.type === 'string') {
        inputs[prop.name] = () => new TextInputInterface(prop.name, prop.defaultValue || '')
      } else if (prop.type === 'number') {
        inputs[prop.name] = () => new NumberInterface(prop.name, prop.defaultValue ?? 0)
      } else if (prop.type === 'select' && prop.options) {
        inputs[prop.name] = () => new SelectInterface(
          prop.name, 
          prop.defaultValue || (prop.options.length > 0 ? prop.options[0].value : ''),
          prop.options.map((o: any) => ({ value: o.value, text: o.label })) as any
        )
      }
    })

    const outputs: DynamicNodeDefinition = {}
    if (subType === LogicType.Conditional) {
      outputs.execTrue = () => new NodeInterface('exec_true', undefined)
      outputs.execFalse = () => new NodeInterface('exec_false', undefined)
    } else {
      outputs.execOut = () => new NodeInterface('exec_out', undefined)
    }

    return { inputs, outputs }
  }
})
