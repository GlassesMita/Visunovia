import { Editor } from '@baklavajs/core'
import StartNode from '@/components/baklava-nodes/StartNode'
import EndNode from '@/components/baklava-nodes/EndNode'
import EventNode from '@/components/baklava-nodes/EventNode'
import DialogueNode from '@/components/baklava-nodes/DialogueNode'
import BranchNode from '@/components/baklava-nodes/BranchNode'
import LogicNode from '@/components/baklava-nodes/LogicNode'
import ResourceNode from '@/components/baklava-nodes/ResourceNode'
import ChoiceNode from '@/components/baklava-nodes/ChoiceNode'
import CharacterControlNode from '@/components/baklava-nodes/CharacterControlNode'

export interface NodeCategory {
  name: string
  labelKey: string
  nodes: any[]
}

export const nodeCategories: NodeCategory[] = [
  {
    name: 'flow',
    labelKey: 'Flow Control',
    nodes: [StartNode, EndNode],
  },
  {
    name: 'event',
    labelKey: 'Events',
    nodes: [EventNode],
  },
  {
    name: 'dialogue',
    labelKey: 'Dialogue',
    nodes: [DialogueNode, CharacterControlNode, BranchNode, ChoiceNode],
  },
  {
    name: 'logic',
    labelKey: 'Logic',
    nodes: [LogicNode],
  },
  {
    name: 'resources',
    labelKey: 'Resources',
    nodes: [ResourceNode],
  },
]

export function registerAllNodes(editor: Editor) {
  nodeCategories.forEach((category) => {
    category.nodes.forEach((node) => {
      editor.registerNodeType(node)
    })
  })

  console.log(
    '[BaklavaJS] Registered node types:',
    Array.from(editor.nodeTypes.keys()).join(', ')
  )
}
