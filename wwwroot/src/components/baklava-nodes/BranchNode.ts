import { defineNode } from '@baklavajs/core'
import { NodeInterface } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'

export default defineNode({
  type: 'BranchNode',
  title: 'nodes.branch',
  inputs: {
    execIn: () => new NodeInterface('exec_in', undefined),
    condition: () => new TextInputInterface('condition', '')
  },
  outputs: {
    execTrue: () => new NodeInterface('exec_true', undefined),
    execFalse: () => new NodeInterface('exec_false', undefined)
  }
})
