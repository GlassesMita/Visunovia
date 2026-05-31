import type { WorkflowNodeRegistry, FlowNodeType } from '@flowgram.ai/free-layout-editor'

const execIn = { type: 'input' as const }
const execOut = { type: 'output' as const }
const execOutTrue = { portID: 'exec_true', type: 'output' as const }
const execOutFalse = { portID: 'exec_false', type: 'output' as const }

export const NODE_DEFAULT_DATA: Record<string, Record<string, unknown>> = {
  Start: {},
  End: { eventType: 'end_game', sceneId: '' },
  Dialogue: { speaker: '', text: '', voice: '' },
  Branch: { condition: '', options: '' },
  PlayBGM: { bgmPath: '', volume: 1.0, loop: true },
  StopBGM: { fadeOutDuration: 0.5 },
  PlaySFX: { sfxPath: '', volume: 1.0 },
  PlayVoice: { voicePath: '' },
  ChangeBackground: { imagePath: '', transition: 'fade', duration: 1.0 },
  ShowCharacter: { characterId: '', expression: 'default', position: 'center' },
  HideCharacter: { characterId: '', transition: 'fade' },
  CameraShake: { intensity: 1.0, duration: 0.5, mode: 'shake' },
  FadeScreen: { duration: 1.0, fadeType: 'out', color: '#000000' },
  CustomEvent: { eventName: '', params: '' },
  SetVariable: { varName: '', value: '', operation: 'set' },
  Conditional: { variable: '', operator: '==', compareValue: '' },
  Delay: { duration: 1.0 },
  SubGraph: { resourceType: '', resourcePath: '' },
}

export const nodeRegistries: WorkflowNodeRegistry[] = [
  {
    type: 'Start',
    meta: {
      deleteDisable: true,
      defaultPorts: [execOut],
    },
  },
  {
    type: 'End',
    meta: {
      defaultPorts: [execIn],
    },
  },
  {
    type: 'Dialogue',
    meta: {
      defaultPorts: [execIn, execOut],
    },
  },
  {
    type: 'Branch',
    meta: {
      defaultPorts: [execIn, execOutTrue, execOutFalse],
    },
  },
  {
    type: 'PlayBGM',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'StopBGM',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'PlaySFX',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'PlayVoice',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'ChangeBackground',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'ShowCharacter',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'HideCharacter',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'CameraShake',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'FadeScreen',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'CustomEvent',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'SetVariable',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'Conditional',
    meta: {
      defaultPorts: [execIn, execOutTrue, execOutFalse],
    },
  },
  {
    type: 'Delay',
    meta: { defaultPorts: [execIn, execOut] },
  },
  {
    type: 'SubGraph',
    meta: { defaultPorts: [execIn, execOut] },
  },
]

export function getNodeDefaultRegistry(type: FlowNodeType): WorkflowNodeRegistry {
  return {
    type,
    meta: {
      defaultPorts: [execIn, execOut],
    },
  }
}
