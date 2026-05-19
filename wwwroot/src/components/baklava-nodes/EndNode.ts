import { defineNode } from '@baklavajs/core'
import { NodeInterface } from '@baklavajs/core'

export default defineNode({
  type: 'EndNode',
  title: 'nodes.end',
  inputs: {
    execIn: () => new NodeInterface('exec_in', undefined)
  },
  outputs: {}
})
