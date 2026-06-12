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

const CONTROL_SYMBOL = '◆'

const speakerSlotOptions = [
  { value: '', text: tSync('dialogue.speakerSlot.none', 'No Speaker') },
  ...[1, 2, 3, 4, 5, 6].map((slot) => ({
    value: String(slot),
    text: `${tSync('characterControl.slot', 'Character Slot')} ${slot}`,
  })),
  { value: 'all', text: tSync('dialogue.speakerSlot.all', 'All Members') },
]

function createCharacterControlPort(label: string) {
  return () => new NodeInterface(label, undefined).setPort(true)
}

function createVoiceInput(slot: number) {
  return () => new AssetSelectInterface(`${tSync('resourceTypes.voice', 'Voice')} ${slot}`, '', 'voice')
}

export const NODE_COLOR = '#2196F3'

export default defineNode({
  type: 'DialogueNode',
  title: 'Dialogue',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    characterControl1: createCharacterControlPort(`${CONTROL_SYMBOL} 1`),
    characterControl2: createCharacterControlPort(`${CONTROL_SYMBOL} 2`),
    characterControl3: createCharacterControlPort(`${CONTROL_SYMBOL} 3`),
    characterControl4: createCharacterControlPort(`${CONTROL_SYMBOL} 4`),
    characterControl5: createCharacterControlPort(`${CONTROL_SYMBOL} 5`),
    characterControl6: createCharacterControlPort(`${CONTROL_SYMBOL} 6`),
    speakerSlot: () => new SelectInterface(tSync('dialogue.speakerSlot', 'Speaker Slot'), '', speakerSlotOptions),
    voice1: createVoiceInput(1),
    voice2: createVoiceInput(2),
    voice3: createVoiceInput(3),
    voice4: createVoiceInput(4),
    voice5: createVoiceInput(5),
    voice6: createVoiceInput(6),
    text: () => new TextInputInterface(tSync('props.text', 'Text'), ''),
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL),
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.dialogue', 'Dialogue')
  },
})
