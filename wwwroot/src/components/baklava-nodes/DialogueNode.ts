import { defineNode } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import { tSync } from '@/services/translationService'
import {
  ARROW_SYMBOL,
  createExecInPort,
  createExecOutPort,
  setNodeI18nTitle,
} from './BaseNode'

export const NODE_COLOR = '#2196F3'

export default defineNode({
  type: 'DialogueNode',
  title: 'Dialogue',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    speaker: () => new TextInputInterface(tSync('props.speaker', 'Speaker'), ''),
    text: () => new TextInputInterface(tSync('props.text', 'Text'), ''),
    voice: () => new TextInputInterface(tSync('props.voice', 'Voice'), ''),
    sprites: () => new TextInputInterface(tSync('props.sprites', 'Sprites'), ''),
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.dialogue', 'Dialogue')
  },
})
