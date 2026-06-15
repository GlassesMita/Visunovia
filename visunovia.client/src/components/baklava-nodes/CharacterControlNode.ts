import { defineNode, NodeInterface } from '@baklavajs/core'
import {
  TextInputInterface,
} from '@baklavajs/renderer-vue'
import { tSync } from '@/services/translationService'
import { characterSelectOptions } from '@/services/characterOptions'
import { SpriteSelectInterface } from '@/components/baklava-interfaces/SpriteSelectInterface'
import { ARROW_SYMBOL, createExecInPort, createExecOutPort, setNodeI18nTitle } from './BaseNode'

export const NODE_COLOR = '#7B1FA2'

export default defineNode({
  type: 'CharacterControlNode',
  title: tSync('nodes.characterControl', '角色控制'),
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    characterControlsJson: () => new TextInputInterface(tSync('characterControl.characterControls', 'Character Controls'), '[]').setHidden(true),
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    Object.entries((this as any).inputs || {}).forEach(([key, input]: [string, any]) => {
      input.node = this
      if (!input.port && typeof input.setHidden === 'function') input.setHidden(true)
    })
    setNodeI18nTitle(this, 'nodes.characterControl', 'Character Control')
  },
})
