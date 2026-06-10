export enum VNNodeType {
  Start = 'Start',
  End = 'End',
  Sequence = 'Sequence',
  Dialogue = 'Dialogue',
  Branch = 'Branch',
  ChangeBackground = 'ChangeBackground',
  ShowCharacter = 'ShowCharacter',
  HideCharacter = 'HideCharacter',
  CameraShake = 'CameraShake',
  FadeScreen = 'FadeScreen',
  PlayBGM = 'PlayBGM',
  StopBGM = 'StopBGM',
  PlaySFX = 'PlaySFX',
  PlayVoice = 'PlayVoice',
  SetVariable = 'SetVariable',
  Conditional = 'Conditional',
  Delay = 'Delay',
  CustomEvent = 'CustomEvent',
  SubGraph = 'SubGraph',
}

export enum PortType {
  Exec = 'exec',
  Data = 'data',
}

export enum DataType {
  String = 'string',
  Number = 'number',
  Boolean = 'boolean',
  Any = 'any',
}

export interface VNNodePort {
  id: string
  label: string
  type: PortType
  dataType?: DataType
  target?: string
  targetPort?: string
}

export interface VNNode {
  id: string
  type: VNNodeType
  position: { x: number; y: number }
  properties: Record<string, unknown>
  inputs: VNNodePort[]
  outputs: VNNodePort[]
}

export interface VNEdge {
  id: string
  source: string
  sourcePort: string
  target: string
  targetPort: string
  type: 'exec' | 'data'
}

export interface Viewport {
  x: number
  y: number
  zoom: number
}

export interface SpriteConfig {
  path: string
  position: { x: number; y: number }
  layer: number
  transition?: string
}

export interface SceneConfig {
  background?: string
  bgm?: {
    path: string
    volume: number
    loop: boolean
  }
}

export interface VNSceneGraph {
  id: string
  viewport: Viewport
  nodes: VNNode[]
  sceneConfig: SceneConfig
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  error?: string
}

/** 后端语言列表 API 返回的语言信息 */
export interface LanguageInfo {
  code: string
  displayName: string
  isCurrent: boolean
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

// 节点类型定义
export enum NodeType {
  Start = 'StartNode',
  End = 'EndNode',
  Event = 'EventNode',
  Dialogue = 'DialogueNode',
  CharacterControl = 'CharacterControlNode',
  Branch = 'BranchNode',
  Logic = 'LogicNode',
}

// EventNode 子类型
export enum EventType {
  PlayBGM = 'playBgm',
  StopBGM = 'stopBgm',
  PlaySFX = 'playSfx',
  PlayVoice = 'playVoice',
  ChangeBackground = 'changeBackground',
  ShowCharacter = 'showCharacter',
  HideCharacter = 'hideCharacter',
  CameraShake = 'cameraShake',
  FadeScreen = 'fadeScreen',
}

// LogicNode 子类型
export enum LogicType {
  SetVariable = 'setVariable',
  Conditional = 'conditional',
  Delay = 'delay',
}

// i18n key 映射
export const eventTypeLabels: Record<EventType, string> = {
  [EventType.PlayBGM]: 'eventTypes.playBgm',
  [EventType.StopBGM]: 'eventTypes.stopBgm',
  [EventType.PlaySFX]: 'eventTypes.playSfx',
  [EventType.PlayVoice]: 'eventTypes.playVoice',
  [EventType.ChangeBackground]: 'eventTypes.changeBackground',
  [EventType.ShowCharacter]: 'eventTypes.showCharacter',
  [EventType.HideCharacter]: 'eventTypes.hideCharacter',
  [EventType.CameraShake]: 'eventTypes.cameraShake',
  [EventType.FadeScreen]: 'eventTypes.fadeScreen',
}

export const logicTypeLabels: Record<LogicType, string> = {
  [LogicType.SetVariable]: 'logicTypes.setVariable',
  [LogicType.Conditional]: 'logicTypes.conditional',
  [LogicType.Delay]: 'logicTypes.delay',
}

// 节点接口配置
export interface PortConfig {
  id: string
  label: string
  type: 'exec' | 'string' | 'number' | 'boolean' | 'any'
  direction: 'input' | 'output'
}

// 节点属性配置
export interface PropertyConfig {
  name: string
  type: 'string' | 'number' | 'boolean' | 'select' | 'resource' | 'character'
  defaultValue: any
  label?: string
  options?: { value: string; label: string }[]
}

// 子类型配置
export interface SubTypeConfig {
  type: EventType | LogicType
  labelKey: string
  properties: PropertyConfig[]
  ports: {
    inputs: PortConfig[]
    outputs: PortConfig[]
  }
}

// 完整的 EventNode 子类型配置
export const eventTypeConfig: Record<EventType, SubTypeConfig> = {
  [EventType.PlayBGM]: {
    type: EventType.PlayBGM,
    labelKey: 'eventTypes.playBgm',
    properties: [
      { name: 'bgmPath', type: 'resource', defaultValue: '', options: [] },
      { name: 'volume', type: 'number', defaultValue: 1.0, options: [] },
      { name: 'loop', type: 'boolean', defaultValue: true, options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.StopBGM]: {
    type: EventType.StopBGM,
    labelKey: 'eventTypes.stopBgm',
    properties: [
      { name: 'fadeOutDuration', type: 'number', defaultValue: 0.5, options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.PlaySFX]: {
    type: EventType.PlaySFX,
    labelKey: 'eventTypes.playSfx',
    properties: [
      { name: 'sfxPath', type: 'resource', defaultValue: '', options: [] },
      { name: 'volume', type: 'number', defaultValue: 1.0, options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.PlayVoice]: {
    type: EventType.PlayVoice,
    labelKey: 'eventTypes.playVoice',
    properties: [
      { name: 'voicePath', type: 'resource', defaultValue: '', options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.ChangeBackground]: {
    type: EventType.ChangeBackground,
    labelKey: 'eventTypes.changeBackground',
    properties: [
      { name: 'imagePath', type: 'resource', defaultValue: '', options: [] },
      { name: 'transition', type: 'select', defaultValue: 'fade', options: [
        { value: 'instant', label: 'Instant' },
        { value: 'fade', label: 'Fade' },
        { value: 'slide', label: 'Slide' }
      ]},
      { name: 'duration', type: 'number', defaultValue: 1.0, options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.ShowCharacter]: {
    type: EventType.ShowCharacter,
    labelKey: 'eventTypes.showCharacter',
    properties: [
      { name: 'characterId', type: 'string', defaultValue: '', options: [] },
      { name: 'expression', type: 'string', defaultValue: 'default', options: [] },
      { name: 'position', type: 'select', defaultValue: 'center', options: [
        { value: 'left', label: 'Left' },
        { value: 'center', label: 'Center' },
        { value: 'right', label: 'Right' }
      ]}
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.HideCharacter]: {
    type: EventType.HideCharacter,
    labelKey: 'eventTypes.hideCharacter',
    properties: [
      { name: 'characterId', type: 'string', defaultValue: '', options: [] },
      { name: 'transition', type: 'select', defaultValue: 'fade', options: [
        { value: 'instant', label: 'Instant' },
        { value: 'fade', label: 'Fade' }
      ]}
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.CameraShake]: {
    type: EventType.CameraShake,
    labelKey: 'eventTypes.cameraShake',
    properties: [
      { name: 'intensity', type: 'number', defaultValue: 1.0, options: [] },
      { name: 'duration', type: 'number', defaultValue: 0.5, options: [] },
      { name: 'mode', type: 'select', defaultValue: 'shake', options: [
        { value: 'shake', label: 'Shake' },
        { value: 'wobble', label: 'Wobble' }
      ]}
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [EventType.FadeScreen]: {
    type: EventType.FadeScreen,
    labelKey: 'eventTypes.fadeScreen',
    properties: [
      { name: 'duration', type: 'number', defaultValue: 1.0, options: [] },
      { name: 'fadeType', type: 'select', defaultValue: 'out', options: [
        { value: 'in', label: 'Fade In' },
        { value: 'out', label: 'Fade Out' }
      ]},
      { name: 'color', type: 'string', defaultValue: '#000000', options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  }
}

// 完整的 LogicNode 子类型配置
export const logicTypeConfig: Record<LogicType, SubTypeConfig> = {
  [LogicType.SetVariable]: {
    type: LogicType.SetVariable,
    labelKey: 'logicTypes.setVariable',
    properties: [
      { name: 'varName', type: 'string', defaultValue: '', options: [] },
      { name: 'value', type: 'string', defaultValue: '', options: [] },
      { name: 'operation', type: 'select', defaultValue: 'set', options: [
        { value: 'set', label: 'Set' },
        { value: 'add', label: 'Add' },
        { value: 'subtract', label: 'Subtract' }
      ]}
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  },
  [LogicType.Conditional]: {
    type: LogicType.Conditional,
    labelKey: 'logicTypes.conditional',
    properties: [
      { name: 'variable', type: 'string', defaultValue: '', options: [] },
      { name: 'operator', type: 'select', defaultValue: '==', options: [
        { value: '==', label: 'Equals' },
        { value: '!=', label: 'Not Equals' },
        { value: '>', label: 'Greater Than' },
        { value: '<', label: 'Less Than' },
        { value: '>=', label: 'Greater Or Equal' },
        { value: '<=', label: 'Less Or Equal' }
      ]},
      { name: 'compareValue', type: 'string', defaultValue: '', options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [
        { id: 'exec_true', label: 'exec_true', type: 'exec', direction: 'output' },
        { id: 'exec_false', label: 'exec_false', type: 'exec', direction: 'output' }
      ]
    }
  },
  [LogicType.Delay]: {
    type: LogicType.Delay,
    labelKey: 'logicTypes.delay',
    properties: [
      { name: 'duration', type: 'number', defaultValue: 1.0, options: [] }
    ],
    ports: {
      inputs: [{ id: 'exec_in', label: 'exec_in', type: 'exec', direction: 'input' }],
      outputs: [{ id: 'exec_out', label: 'exec_out', type: 'exec', direction: 'output' }]
    }
  }
}
