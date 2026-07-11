import { defineNode, NodeInterface } from '@baklavajs/core'
import { SelectInterface, TextInputInterface } from '@baklavajs/renderer-vue'
import { tSync } from '@/services/translationService'
import { AssetSelectInterface } from '@/components/baklava-interfaces/AssetSelectInterface'
import {
  ARROW_SYMBOL,
  createExecInPort,
  createExecOutPort,
  setNodeI18nTitle,
} from './BaseNode'

const speakerSlotOptions = [
  { value: '', text: tSync('dialogue.speakerSlot.none', 'No Speaker') },
  ...[1, 2, 3, 4, 5, 6].map((slot) => ({
    value: String(slot),
    text: `${tSync('characterControl.slot', 'Character Slot')} ${slot}`,
  })),
  { value: 'all', text: tSync('dialogue.speakerSlot.all', 'All Members') },
]

function createVoiceInput(slot: number) {
  return () => new AssetSelectInterface(`${tSync('resourceTypes.voice', 'Voice')} ${slot}`, '', 'voice', { voiceCharacterIdInputKey: 'unmanagedCharacter' })
}

export const NODE_COLOR = '#2196F3'

export default defineNode({
  type: 'DialogueNode',
  title: 'Dialogue',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    speakerSlot: () => new SelectInterface(tSync('dialogue.speakerSlot', 'Speaker Slot'), '', speakerSlotOptions),
    unmanagedCharacter: () => new TextInputInterface(tSync('characterControl.unmanagedCharacter', 'Slot 6 Character Name'), '').setHidden(true),
    voiceCount: () => new SelectInterface(tSync('dialogue.voiceCount', 'Voice Count'), '1', [0, 1, 2, 3, 4, 5].map(count => ({ value: String(count), text: String(count) }))),
    voice1: createVoiceInput(1),
    voice2: createVoiceInput(2),
    voice3: createVoiceInput(3),
    voice4: createVoiceInput(4),
    voice5: createVoiceInput(5),
    text: () => new TextInputInterface(tSync('props.text', 'Text'), ''),
    textKey: () => new TextInputInterface(tSync('props.localizationKey', 'Localization Key'), ''),
    speakerKey: () => new TextInputInterface(tSync('dialogue.speakerKey', 'Speaker Localization Key'), ''),
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    Object.values((this as any).inputs || {}).forEach((input: any) => {
      input.node = this
      if (!input.port && typeof input.setHidden === 'function') input.setHidden(true)
    })
    setNodeI18nTitle(this, 'nodes.dialogue', 'Dialogue')
  },
})
