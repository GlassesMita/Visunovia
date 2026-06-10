import { defineNode, NodeInterface } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import { tSync } from '@/services/translationService'
import {
  ARROW_SYMBOL,
  createExecInPort,
  createExecOutPort,
  setNodeI18nTitle,
} from './BaseNode'

const CONTROL_SYMBOL = '◆'

function createCharacterControlPort(label: string) {
  return () => new NodeInterface(label, undefined).setPort(true)
}

export const NODE_COLOR = '#2196F3'

export default defineNode({
  type: 'DialogueNode',
  title: 'Dialogue',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    characterControl1: createCharacterControlPort(`${CONTROL_SYMBOL} 1`),
    characterControl2: createCharacterControlPort(`${CONTROL_SYMBOL} 2`),
    characterControl3: createCharacterControlPort(`${CONTROL_SYMBOL} 3`),
    characterControl4: createCharacterControlPort(`${CONTROL_SYMBOL} 4`),
    characterControl5: createCharacterControlPort(`${CONTROL_SYMBOL} 5`),
    text: () => new TextInputInterface(tSync('props.text', 'Text'), ''),
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.dialogue', 'Dialogue')
  },
})
