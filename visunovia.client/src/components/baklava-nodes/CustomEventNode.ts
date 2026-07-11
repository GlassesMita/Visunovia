import { defineNode } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'
import { ARROW_SYMBOL, createExecInPort, createExecOutPort, setNodeI18nTitle } from './BaseNode'

export const NODE_COLOR = '#455A64'

export default defineNode({
  type: 'CustomEventNode',
  title: 'Custom Event',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    code: () => new TextInputInterface('Code', ''),
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.customEvent', 'Custom Event')
  },
})