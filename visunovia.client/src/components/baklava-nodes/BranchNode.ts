import { defineNode } from '@baklavajs/core'
import { NodeInterface } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import { tSync } from '@/services/translationService'
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
    condition: () => new TextInputInterface(tSync('props.condition', 'Condition'), '')
  },

  outputs: {
    execTrue: () => new NodeInterface('✓', undefined).setPort(true),
    execFalse: () => new NodeInterface('✗', undefined).setPort(true)
  },

  onCreate() {
    setNodeI18nTitle(this, 'nodes.branch', 'Branch')
  }
})
