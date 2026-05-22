import { defineNode } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
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
    speaker: () => {
      const t = useLocalizationStore().t
      return new TextInputInterface(t('props.speaker', 'Speaker'), '')
    },
    text: () => {
      const t = useLocalizationStore().t
      return new TextInputInterface(t('props.text', 'Text'), '')
    },
    voice: () => {
      const t = useLocalizationStore().t
      return new TextInputInterface(t('props.voice', 'Voice'), '')
    },
    sprites: () => {
      const t = useLocalizationStore().t
      return new TextInputInterface(t('props.sprites', 'Sprites'), '')
    },
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.dialogue', 'Dialogue')
  },
})
