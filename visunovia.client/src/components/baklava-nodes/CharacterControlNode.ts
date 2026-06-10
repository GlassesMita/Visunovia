import { defineNode, NodeInterface } from '@baklavajs/core'
import {
  SelectInterface,
  TextInputInterface,
  NumberInterface,
} from '@baklavajs/renderer-vue'
import { tSync } from '@/services/translationService'
import { characterSelectOptions } from '@/services/characterOptions'
import { ARROW_SYMBOL, setNodeI18nTitle } from './BaseNode'

export const NODE_COLOR = '#7B1FA2'

const characterSlots = [1, 2, 3, 4, 5].map((slot) => ({
  value: String(slot),
  text: `${tSync('characterControl.slot', 'Character Slot')} ${slot}`,
}))

const visibilityActions = [
  { value: 'show', text: tSync('characterControl.show', 'Show Character') },
  { value: 'hide', text: tSync('characterControl.hide', 'Hide Character') },
  { value: 'update', text: tSync('characterControl.update', 'Update Character') },
]

const positions = [
  { value: 'left', text: tSync('eventOptions.left', 'Left') },
  { value: 'center', text: tSync('eventOptions.center', 'Center') },
  { value: 'right', text: tSync('eventOptions.right', 'Right') },
]

const animationEffects = [
  { value: 'none', text: tSync('characterControl.animation.none', 'None') },
  { value: 'fade', text: tSync('eventOptions.fade', 'Fade') },
  { value: 'slide', text: tSync('eventOptions.slide', 'Slide') },
  { value: 'pop', text: tSync('characterControl.animation.pop', 'Pop') },
]

export default defineNode({
  type: 'CharacterControlNode',
  title: 'Character Control',
  inputs: {
    character: () => new SelectInterface(tSync('eventProps.characterId', 'Character'), '', characterSelectOptions),
    slot: () => new SelectInterface(tSync('characterControl.slot', 'Character Slot'), '1', characterSlots),
    action: () => new SelectInterface(tSync('characterControl.action', 'Action'), 'show', visibilityActions),
    sprite: () => new TextInputInterface(tSync('characterControl.sprite', 'Sprite'), ''),
    sfx: () => new TextInputInterface(tSync('characterControl.sfx', 'Sound Effect'), ''),
    expression: () => new TextInputInterface(tSync('eventProps.expression', 'Expression'), 'default'),
    position: () => new SelectInterface(tSync('eventProps.position', 'Position'), 'center', positions),
    animation: () => new SelectInterface(tSync('characterControl.animation', 'Animation'), 'fade', animationEffects),
    duration: () => new NumberInterface(tSync('eventProps.duration', 'Duration'), 0.3),
  },
  outputs: {
    controlOut: () => new NodeInterface(ARROW_SYMBOL, undefined),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.characterControl', 'Character Control')
  },
})
