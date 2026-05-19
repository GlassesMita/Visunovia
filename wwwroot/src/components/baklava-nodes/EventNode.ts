import { defineDynamicNode } from '@baklavajs/core'
import { NodeInterface, DynamicNodeDefinition } from '@baklavajs/core'
import { 
  SelectInterface, 
  TextInputInterface, 
  NumberInterface, 
  CheckboxInterface 
} from '@baklavajs/renderer-vue'
import { EventType, eventTypeConfig } from '@/types'

export default defineDynamicNode({
  type: 'EventNode',
  title: 'nodes.event',
  inputs: {
    execIn: () => new NodeInterface('exec_in', undefined),
    subType: () => new SelectInterface('sub_type', EventType.PlayBGM, 
      Object.values(EventType).map(v => ({
        value: v,
        text: eventTypeConfig[v].labelKey
      })) as any
    )
  },
  outputs: {
    execOut: () => new NodeInterface('exec_out', undefined)
  },
  onUpdate({ subType }) {
    const properties = eventTypeConfig[subType as EventType]?.properties || []
    const inputs: DynamicNodeDefinition = {}

    properties.forEach((prop: any) => {
      if (prop.type === 'string' || prop.type === 'resource') {
        inputs[prop.name] = () => new TextInputInterface(prop.name, prop.defaultValue || '')
      } else if (prop.type === 'number') {
        inputs[prop.name] = () => new NumberInterface(prop.name, prop.defaultValue ?? 0)
      } else if (prop.type === 'boolean') {
        inputs[prop.name] = () => new CheckboxInterface(prop.name, prop.defaultValue ?? false)
      } else if (prop.type === 'select' && prop.options) {
        inputs[prop.name] = () => new SelectInterface(
          prop.name, 
          prop.defaultValue || (prop.options.length > 0 ? prop.options[0].value : ''),
          prop.options.map((o: any) => ({ value: o.value, text: o.label })) as any
        )
      }
    })

    return { inputs }
  }
})
