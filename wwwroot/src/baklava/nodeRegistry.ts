import { Editor } from '@baklavajs/core'
import StartNode from '@/components/baklava-nodes/StartNode'
import EndNode from '@/components/baklava-nodes/EndNode'
import EventNode from '@/components/baklava-nodes/EventNode'
import DialogueNode from '@/components/baklava-nodes/DialogueNode'
import BranchNode from '@/components/baklava-nodes/BranchNode'
import LogicNode from '@/components/baklava-nodes/LogicNode'

export interface NodeCategory {
  name: string
  labelKey: string
  nodes: any[]
}

export const nodeCategories: NodeCategory[] = [
  {
    name: 'flow',
    labelKey: 'Flow',
    nodes: [StartNode, EndNode],
  },
  {
    name: 'event',
    labelKey: 'Event',
    nodes: [EventNode],
  },
  {
    name: 'dialogue',
    labelKey: 'Dialogue',
    nodes: [DialogueNode, BranchNode],
  },
  {
    name: 'logic',
    labelKey: 'Logic',
    nodes: [LogicNode],
  },
]

export function registerAllNodes(editor: Editor) {
  nodeCategories.forEach(category => {
    category.nodes.forEach(node => {
      editor.registerNodeType(node)
    })
  })
  
  console.log('Registered node types:', editor.nodeTypes)
}
