import { defineNode } from '@baklavajs/core'
import { NodeInterface } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import {
  ARROW_SYMBOL,
  createExecInPort,
  setNodeI18nTitle,
} from './BaseNode'

export const NODE_COLOR = '#9C27B0'

export default defineNode({
  type: 'BranchNode',
  title: 'Branch',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    condition: () => {
      const store = useLocalizationStore()
      return new TextInputInterface(store.t('props.condition', 'Condition'), '')
    }
  },

  outputs: {
    execTrue: () => new NodeInterface('✓', undefined).setPort(true),
    execFalse: () => new NodeInterface('✗', undefined).setPort(true)
  },

  onCreate() {
    setNodeI18nTitle(this, 'nodes.branch', 'Branch')
  }
})
