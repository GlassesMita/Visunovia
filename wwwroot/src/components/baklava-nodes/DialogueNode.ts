import { defineNode } from '@baklavajs/core'
import { NodeInterface } from '@baklavajs/core'
import { TextInputInterface } from '@baklavajs/renderer-vue'

export default defineNode({
  type: 'DialogueNode',
  title: 'nodes.dialogue',
  inputs: {
    execIn: () => new NodeInterface('exec_in', undefined),
    speaker: () => new NodeInterface('speaker', ''),
    text: () => new TextInputInterface('text', ''),
    voice: () => new NodeInterface('voice', ''),
    sprites: () => new NodeInterface('sprites', '')
  },
  outputs: {
    execOut: () => new NodeInterface('exec_out', undefined)
  }
})
