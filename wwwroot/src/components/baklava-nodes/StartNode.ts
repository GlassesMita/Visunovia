import { defineNode } from '@baklavajs/core'
import { NodeInterface } from '@baklavajs/core'

export default defineNode({
  type: 'StartNode',
  title: 'nodes.start',
  inputs: {},
  outputs: {
    execOut: () => new NodeInterface('exec_out', undefined)
  }
})
