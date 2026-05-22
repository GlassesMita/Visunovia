import { defineNode } from '@baklavajs/core'
import {
  ARROW_SYMBOL,
  createExecOutPort,
  setNodeI18nTitle,
} from './BaseNode'

export default defineNode({
  type: 'StartNode',
  title: 'Start',
  inputs: {},
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL)
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.start', 'Start')
  }
})

export const NODE_COLOR = '#4CAF50'
