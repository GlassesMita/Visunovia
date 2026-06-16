import { ref } from 'vue'
import type { SerializedNode, SerializedConnection, SerializedSceneGraph } from './useNodeOperations'
import { normalizeAssetProperties } from '@/utils/assetPaths'

/**
 * Lor 剧本文件的数据结构定义
 */
export interface LorScene {
  id: string
  background: string
  bgm: {
    path: string
    volume: number
    loop: boolean
  }
  dialogues: LorDialogue[]
  nodes?: SerializedNode[]
  connections?: SerializedConnection[]
}

export interface LorDialogue {
  uuid: string
  type: 'Dialogue' | 'Event' | 'Branch'
  speaker: string
  text: string
  sprites: LorSprite[]
  voice: string
  textEffect: {
    type: string
    speed: number
    shake: boolean
    fadeDuration: number
    delayBeforeStart: number
  }
  animation: {
    type: string
    duration: number
  }
  branch: LorBranch | null
  event: LorEvent | null
  transition: {
    effect: string
    duration: number
  }
  position?: {
    x: number
    y: number
  }
  positionX?: number
  positionY?: number
}

export interface LorSprite {
  path: string
  position: string
  layer: number
  animation: {
    type: string
    duration: number
  }
}

export interface LorBranch {
  choices: {
    text: string
    targetScene: string
    condition: string
  }[]
}

export interface LorEvent {
  eventType: string
  parameters: Record<string, string>
}

/**
 * 事件类型到蓝图节点子类型的映射
 */
const EVENT_TYPE_MAP: Record<string, string> = {
  ChangeBGM: 'playBgm',
  ChangeBgm: 'playBgm',
  StopBGM: 'stopBgm',
  PlaySFX: 'playSfx',
  PlayVoice: 'playVoice',
  ChangeBackground: 'changeBackground',
  ChangeBG: 'changeBackground',
  ShowCharacter: 'showCharacter',
  HideCharacter: 'hideCharacter',
  CameraShake: 'cameraShake',
  FadeScreen: 'fadeScreen',
  SendSystemNotification: 'customEvent',
}

/**
 * 蓝图节点类型映射
 */
const NODE_TYPE_MAP: Record<string, string> = {
  Dialogue: 'DialogueNode',
  Event: 'EventNode',
  Branch: 'BranchNode',
}

const END_EVENT_NAMES = new Set(['end_game', 'return_to_menu', 'jump_to_scene'])
const LOR_FORMAT_VERSION = '1.1'

function normalizeCharacterControl(control: any, fallbackSlot = '1') {
  const slot = String(control?.slot || fallbackSlot || '1')
  const action = String(control?.action || control?.mode || 'show').trim() || 'show'
  return {
    slot,
    mode: action,
    action,
    character: slot === '6'
      ? String(control?.unmanagedCharacter || control?.character || '').trim()
      : String(control?.character || '').trim(),
    unmanagedCharacter: String(control?.unmanagedCharacter || '').trim(),
    sprite: slot === '6' ? '' : String(control?.sprite || '').trim(),
    sfx: String(control?.sfx || '').trim(),
    expression: String(control?.expression || 'default').trim() || 'default',
    fromPosition: String(control?.fromPosition || '').trim(),
    toPosition: String(control?.toPosition || 'none').trim() || 'none',
    position: String(control?.position || 'center').trim() || 'center',
    animation: String(control?.animation || 'fade').trim() || 'fade',
    easing: String(control?.easing || 'easeOutCubic').trim() || 'easeOutCubic',
    duration: Number(control?.duration ?? 0.3) || 0.3,
    expressionBalloon: String(control?.expressionBalloon || '').trim(),
    expressionIcon: String(control?.expressionIcon || '').trim(),
    expressionPreset: String(control?.expressionPreset || '').trim(),
    expressionCorner: String(control?.expressionCorner || 'top-right').trim() || 'top-right',
    expressionDuration: Number(control?.expressionDuration ?? 2) || 2,
  }
}

function parseCharacterControls(value: unknown) {
  if (Array.isArray(value)) return value.map((control, index) => normalizeCharacterControl(control, String(index + 1)))
  if (typeof value !== 'string') return []
  const trimmed = value.trim()
  if (!trimmed) return []
  try {
    const parsed = JSON.parse(trimmed)
    return Array.isArray(parsed) ? parsed.map((control, index) => normalizeCharacterControl(control, String(index + 1))) : []
  } catch {
    return []
  }
}

function serializeCharacterControls(controls: any[]) {
  return JSON.stringify(controls.map((control, index) => normalizeCharacterControl(control, String(index + 1))))
}

/**
 * 生成 UUID v4
 */
function generateUUID(): string {
  // 优先使用 crypto.randomUUID
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  // 回退方案
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

function normalizeBlueprintPosition(position: { x: number; y: number }) {
  return {
    x: Math.max(0, Number(position.x || 0)),
    y: Number(position.y || 0),
  }
}

/**
 * Lor 剧本 → 蓝图转换器
 * 将视觉小说剧本文件自动转换为 BaklavaJS 节点图
 * 每个节点通过 UUID 进行标识和连线
 */
export function useLorToBlueprint() {
  const isConverting = ref(false)
  const conversionError = ref<string | null>(null)
  const conversionProgress = ref(0)

  /**
   * Lor JSON 解析器。
   */
  function parseJson(jsonContent: string): LorScene {
    console.log('[useLorToBlueprint] parseLorContent called, content length:', jsonContent.length)
    try {
      return JSON.parse(jsonContent.trim()) as LorScene
    } catch (error) {
      throw new Error(`Lor JSON 解析错误: ${error instanceof Error ? error.message : '未知错误'}`)
    }
  }

  function asArray<T = any>(value: unknown): T[] {
    if (Array.isArray(value)) return value as T[]
    if (value === null || value === undefined || value === '') return []
    if (typeof value === 'string') {
      const trimmed = value.trim()
      if (!trimmed || trimmed === '[]') return []
      try {
        const parsed = JSON.parse(trimmed)
        return Array.isArray(parsed) ? parsed as T[] : []
      } catch {
        return []
      }
    }
    return []
  }

  /**
   * 创建执行连接（通过 UUID）
   * 使用 UUID 进行节点对应和连线
   */
  function createConnection(
    fromUuid: string,
    toUuid: string,
    sourcePort = 'execOut',
    targetPort = 'execIn'
  ): SerializedConnection {
    return {
      uuid: `conn_${fromUuid}_${sourcePort}_${toUuid}_${targetPort}`,
      id: `conn_${fromUuid}_${sourcePort}_${toUuid}_${targetPort}`,
      sourceNodeUuid: fromUuid,
      source: fromUuid,
      sourcePort,
      targetNodeUuid: toUuid,
      target: toUuid,
      targetPort,
    }
  }

  /**
   * 转换对话为蓝图节点
   * 使用 UUID 作为节点 ID，保留蓝图上的绝对坐标位置
   * 支持一个节点连接到多个节点（通过 nextNodeUuids）
   */
  function convertDialogueToNode(dialogue: LorDialogue, index: number, uuid: string): SerializedNode | null {
    // 优先使用 JSON 中保存的绝对坐标位置，否则使用自动布局位置
    const rawPosition = typeof dialogue.positionX === 'number' && typeof dialogue.positionY === 'number'
      ? { x: dialogue.positionX, y: dialogue.positionY }
      : dialogue.position && typeof dialogue.position.x === 'number' && typeof dialogue.position.y === 'number'
        ? { x: dialogue.position.x, y: dialogue.position.y }
        : { x: 300, y: 100 + index * 150 }
    const position = normalizeBlueprintPosition(rawPosition)
    
    if (dialogue.type === 'Dialogue') {
      const sprites = asArray(dialogue.sprites).map((s: any) => ({
        path: s?.path || '',
        position: s?.position || 'center',
        layer: s?.layer || 0,
      }))
      
      return {
        uuid,
        id: uuid,
        nodeType: 'DialogueNode',
        position,
        properties: normalizeAssetProperties({
          speaker: dialogue.speaker || '',
          speaker2: asArray((dialogue as any).speakers)[1] || '',
          speaker3: asArray((dialogue as any).speakers)[2] || '',
          speaker4: asArray((dialogue as any).speakers)[3] || '',
          speaker5: asArray((dialogue as any).speakers)[4] || '',
          sprite1: asArray(dialogue.sprites)[0]?.path || '',
          sprite2: asArray(dialogue.sprites)[1]?.path || '',
          sprite3: asArray(dialogue.sprites)[2]?.path || '',
          sprite4: asArray(dialogue.sprites)[3]?.path || '',
          sprite5: asArray(dialogue.sprites)[4]?.path || '',
          voice2: asArray((dialogue as any).voices)[1]?.path || '',
          voice3: asArray((dialogue as any).voices)[2]?.path || '',
          voice4: asArray((dialogue as any).voices)[3]?.path || '',
          voice5: asArray((dialogue as any).voices)[4]?.path || '',
          voiceCount: Math.min(5, Math.max(1, asArray((dialogue as any).voices).length || (dialogue.voice ? 1 : 0))),
          speakerSlot: (dialogue as any).speakerSlot || '1',
          speakers: asArray((dialogue as any).speakers).slice(0, 5),
          text: dialogue.text || '',
          voice: dialogue.voice || '',
          voice1: dialogue.voice || asArray((dialogue as any).voices)[0]?.path || '',
          sprites,
          textEffect: dialogue.textEffect || { type: '', speed: 0, shake: false, fadeDuration: 0, delayBeforeStart: 0 },
          animation: dialogue.animation || { type: '', duration: 0 },
        }),
        nextNodeUuids: [],
      }
    }
    
    if (dialogue.type === 'Event' && dialogue.event) {
      const customEventName = dialogue.event.parameters?.eventName || ''
      if (dialogue.event.eventType === 'Custom' && END_EVENT_NAMES.has(customEventName)) {
        return {
          uuid,
          id: uuid,
          nodeType: 'EndNode',
          position,
          properties: {
            eventType: customEventName,
            sceneId: dialogue.event.parameters?.targetScene || '',
          },
          nextNodeUuids: [],
        }
      }

      const subType = EVENT_TYPE_MAP[dialogue.event.eventType] || 'customEvent'
      const properties: Record<string, any> = { subType }
      const resourcePath = dialogue.event.parameters.resource
        || dialogue.event.parameters.path
        || dialogue.event.parameters.file
        || dialogue.event.parameters.filePath
      
      // 根据事件类型映射参数
      switch (dialogue.event.eventType) {
        case 'ChangeBGM':
        case 'ChangeBgm':
          properties.bgmPath = dialogue.event.parameters.bgmFile || dialogue.event.parameters.bgm || dialogue.event.parameters.bgmPath || dialogue.event.parameters.musicFile || resourcePath || ''
          properties.volume = 0.8
          properties.loop = true
          break
        case 'ShowCharacter':
          properties.characterId = dialogue.event.parameters.character || ''
          properties.position = 'center'
          properties.expression = 'default'
          break
        case 'ChangeBackground':
        case 'ChangeBG':
          properties.imagePath = dialogue.event.parameters.background || dialogue.event.parameters.bgFile || dialogue.event.parameters.bg || dialogue.event.parameters.imagePath || resourcePath || ''
          properties.transition = 'fade'
          properties.duration = 1.0
          break
        case 'StopBGM':
          properties.fadeOutDuration = 0.5
          break
        case 'PlaySFX':
          properties.sfxPath = dialogue.event.parameters.soundFile || dialogue.event.parameters.sfx || resourcePath || ''
          properties.volume = 1.0
          break
        case 'PlayVoice':
          properties.voicePath = dialogue.event.parameters.voiceFile || dialogue.event.parameters.voice || resourcePath || ''
          break
        case 'CameraShake':
          properties.intensity = 1.0
          properties.duration = 0.5
          break
        default:
          properties.eventName = dialogue.event.eventType
          properties.params = JSON.stringify(dialogue.event.parameters)
      }
      
      return {
        uuid,
        id: uuid,
        nodeType: 'EventNode',
        subType,
        position,
        properties: normalizeAssetProperties(properties),
        nextNodeUuids: [],
      }
    }
    
    if (dialogue.type === 'Branch' && dialogue.branch) {
      const choices = asArray(dialogue.branch.choices)

      if (choices.length > 0) {
        const properties: Record<string, any> = {
          optionCount: String(choices.length),
        }

        choices.forEach((choice, choiceIndex) => {
          properties[`choiceText_${choiceIndex + 1}`] = choice.text || ''
        })

        return {
          uuid,
          id: uuid,
          nodeType: 'ChoiceNode',
          position,
          properties,
          nextNodeUuids: choices.map(choice => choice.targetScene).filter(Boolean),
        }
      }

      return {
        uuid,
        id: uuid,
        nodeType: 'BranchNode',
        position,
        properties: {
          choices: asArray(dialogue.branch.choices).map(c => ({
            text: c.text,
            targetScene: c.targetScene,
            condition: c.condition,
          })),
        },
        nextNodeUuids: [],
      }
    }
    
    return null
  }

  /**
   * 主转换函数：Lor 剧本 → 蓝图
   * 每个对话节点分配一个 UUID，通过 UUID 进行连线
   */
  function convertLorToBlueprint(scene: LorScene): SerializedSceneGraph {
    if (Array.isArray(scene.nodes) && scene.nodes.length > 0) {
      const dialogueById = new Map(scene.dialogues.map(dialogue => [dialogue.uuid, dialogue]))
      const nodes = scene.nodes.map((node) => ({
        ...node,
        uuid: node.uuid || node.id,
        id: node.id || node.uuid,
        properties: normalizeAssetProperties(repairLegacyNodeProperties(node, dialogueById.get(node.id || node.uuid))),
        position: normalizeBlueprintPosition(node.position || { x: 0, y: 0 }),
        nextNodeUuids: node.nextNodeUuids || [],
      }))
      const nodeIds = new Set(nodes.map(node => node.id))
      const nodesById = new Map(nodes.map(node => [node.id, node]))
      const repairedDialogueId = nodes.find(node => node.nodeType === 'DialogueNode')?.id
        || scene.dialogues.find(dialogue => dialogue.type === 'Dialogue')?.uuid
      const repairedStartId = nodes.find(node => node.nodeType === 'StartNode')?.id
      const repairedEndId = nodes.find(node => node.nodeType === 'EndNode')?.id
      const normalizePort = (port: string, direction: 'source' | 'target') => {
        if (port === '→') return direction === 'source' ? 'execOut' : 'execIn'
        if (port === 'controlOut') return 'execOut'
        const match = String(port || '').match(/(?:◆|characterControl)\s*(\d+)/i)
        return match ? `characterControl${match[1]}` : port
      }
      const repairNodeId = (nodeId: string, port: string, direction: 'source' | 'target') => {
        if (nodeIds.has(nodeId)) return nodeId
        if (direction === 'source' && normalizePort(port, direction) === 'execOut') return repairedStartId || nodeId
        if (direction === 'target' && normalizePort(port, direction).startsWith('characterControl')) return repairedDialogueId || nodeId
        if (direction === 'target' && normalizePort(port, direction) === 'execIn') return repairedEndId || repairedDialogueId || nodeId
        return nodeId
      }

      const connections = Array.isArray(scene.connections)
        ? scene.connections.map((connection) => ({
            ...connection,
            uuid: connection.uuid || connection.id,
            id: connection.id || connection.uuid,
            sourcePort: normalizePort(connection.sourcePort, 'source'),
            targetPort: normalizePort(connection.targetPort, 'target'),
            sourceNodeUuid: repairNodeId(connection.sourceNodeUuid || connection.source, connection.sourcePort, 'source'),
            source: repairNodeId(connection.source || connection.sourceNodeUuid, connection.sourcePort, 'source'),
            targetNodeUuid: repairNodeId(connection.targetNodeUuid || connection.target, connection.targetPort, 'target'),
            target: repairNodeId(connection.target || connection.targetNodeUuid, connection.targetPort, 'target'),
          })).filter(connection => nodeIds.has(connection.sourceNodeUuid) && nodeIds.has(connection.targetNodeUuid))
        : []

      connections.forEach((connection) => {
        const controlNode = nodesById.get(connection.sourceNodeUuid)
        const dialogue = dialogueById.get(connection.targetNodeUuid)
        if (!controlNode || controlNode.nodeType !== 'CharacterControlNode' || !dialogue) return
        const legacyControls = parseCharacterControls((dialogue as any).characterControls)
        const existingControls = parseCharacterControls(controlNode.properties?.characterControls || controlNode.properties?.characterControlsJson)
        const mergedControls = existingControls.length > 0 ? existingControls : legacyControls
        if (mergedControls.length === 0) return
        controlNode.properties = {
          ...(controlNode.properties || {}),
          characterControls: mergedControls,
          characterControlsJson: serializeCharacterControls(mergedControls),
          tlorFormatVersion: LOR_FORMAT_VERSION,
        }
      })

      return {
        id: scene.id,
        nodes,
        connections,
      }
    }

    function repairLegacyNodeProperties(node: SerializedNode, dialogue?: LorDialogue) {
      const properties: Record<string, any> = { ...(node.properties || {}) }

      if (node.nodeType === 'DialogueNode' && dialogue?.type === 'Dialogue') {
        const voices = asArray((dialogue as any).voices).slice(0, 5)
        const slot6Control = asArray((dialogue as any).characterControls)
          .find((control: any) => String(control?.slot || '') === '6')

        properties.speakerSlot = properties.speakerSlot || (dialogue as any).speakerSlot || '1'
        properties.text = properties.text || dialogue.text || ''
        properties.voiceCount = String(Math.min(5, Math.max(
          Number(properties.voiceCount || 0),
          voices.length,
          dialogue.voice ? 1 : 0,
          1
        )))
        properties.voice1 = properties.voice1 || dialogue.voice || voices[0]?.path || ''
        for (let index = 1; index < 5; index += 1) {
          const key = `voice${index + 1}`
          properties[key] = properties[key] || voices[index]?.path || ''
        }
        if (String(properties.speakerSlot) === '6') {
          properties.unmanagedCharacter = properties.unmanagedCharacter || slot6Control?.unmanagedCharacter || slot6Control?.character || ''
        }
      }

      if (node.nodeType === 'CharacterControlNode') {
        const controls = parseCharacterControls(properties.characterControls || properties.characterControlsJson)
        if (controls.length > 0) {
          properties.characterControls = controls
          properties.characterControlsJson = serializeCharacterControls(controls)
        } else {
          const slot = String(properties.slot || '')
          if (slot === '6') {
            properties.unmanagedCharacter = properties.unmanagedCharacter || properties.character || ''
            properties.character = ''
          }
        }
      }

      return properties
    }

    const nodes: SerializedNode[] = []
    const connections: SerializedConnection[] = []
    const branchTargetUuids = new Set(
      scene.dialogues.flatMap(dialogue =>
        dialogue.branch ? asArray(dialogue.branch.choices).map(choice => choice.targetScene).filter(Boolean) : []
      )
    )
    
    // 用于记录上一个节点的 UUID，用于连线
    let previousUuid: string | null = null
    
    // 第一遍：为每个对话生成 UUID（如果 JSON 中没有则新建）
    scene.dialogues.forEach((dialogue) => {
      if (!dialogue.uuid) {
        dialogue.uuid = generateUUID()
      }
    })
    
    // 创建开始节点（使用固定 UUID）
    const startUuid = 'start_node'
    nodes.push({
      uuid: startUuid,
      id: startUuid,
      nodeType: 'StartNode',
      position: { x: 50, y: 50 },
      properties: {},
      nextNodeUuids: [],
    })
    previousUuid = startUuid
    
    // 处理 BGM 初始配置
    if (scene.bgm && scene.bgm.path) {
      const bgmUuid = generateUUID()
      nodes.push({
        uuid: bgmUuid,
        id: bgmUuid,
        nodeType: 'EventNode',
        subType: 'playBgm',
        position: { x: 300, y: 50 },
        properties: normalizeAssetProperties({
          bgmPath: scene.bgm.path,
          volume: scene.bgm.volume / 100,
          loop: scene.bgm.loop,
        }),
        nextNodeUuids: [],
      })
      connections.push(createConnection(previousUuid!, bgmUuid))
      previousUuid = bgmUuid
    }
    
    // 处理初始背景
    if (scene.background) {
      const bgUuid = generateUUID()
      nodes.push({
        uuid: bgUuid,
        id: bgUuid,
        nodeType: 'EventNode',
        subType: 'changeBackground',
        position: { x: 300, y: 200 },
        properties: normalizeAssetProperties({
          imagePath: scene.background,
          transition: 'fade',
          duration: 1.0,
        }),
        nextNodeUuids: [],
      })
      connections.push(createConnection(previousUuid!, bgUuid))
      previousUuid = bgUuid
    }
    
    // 转换对话列表，使用 UUID 进行连线
    scene.dialogues.forEach((dialogue, index) => {
      const dialogueControls = asArray((dialogue as any).characterControls)
        .map((control, controlIndex) => normalizeCharacterControl(control, String(controlIndex + 1)))
        .filter(control => control.action !== 'none')

      if (dialogueControls.length > 0) {
        const controlUuid = `${dialogue.uuid}-character-control`
        nodes.push({
          uuid: controlUuid,
          id: controlUuid,
          nodeType: 'CharacterControlNode',
          position: normalizeBlueprintPosition({ x: (dialogue.positionX ?? 300) - 220, y: (dialogue.positionY ?? 100 + index * 150) }),
          properties: {
            characterControls: dialogueControls,
            characterControlsJson: serializeCharacterControls(dialogueControls),
            tlorFormatVersion: LOR_FORMAT_VERSION,
          },
          nextNodeUuids: [dialogue.uuid],
        })
        if (previousUuid && !branchTargetUuids.has(controlUuid)) {
          connections.push(createConnection(previousUuid, controlUuid))
        }
        previousUuid = controlUuid
      }

      const node = convertDialogueToNode(dialogue, index, dialogue.uuid)
      if (node) {
        nodes.push(node)
        if (previousUuid && !branchTargetUuids.has(node.id)) {
          connections.push(createConnection(previousUuid, node.id))
        }

        if (node.nodeType === 'ChoiceNode' && dialogue.branch) {
          asArray(dialogue.branch.choices).forEach((choice, choiceIndex) => {
            if (choice.targetScene) {
              connections.push(createConnection(node.id, choice.targetScene, `execOut_${choiceIndex + 1}`))
            }
          })
          previousUuid = null
        } else {
          previousUuid = branchTargetUuids.has(node.id) ? null : node.id
        }
      }
    })
    
    // 如果脚本中没有保存显式结束节点，则补一个默认结束节点
    const lastNode = nodes[nodes.length - 1]
    if (!lastNode || lastNode.nodeType !== 'EndNode') {
      const endUuid = 'end_node'
      nodes.push({
        uuid: endUuid,
        id: endUuid,
        nodeType: 'EndNode',
        position: { x: 300, y: 100 + (scene.dialogues.length + 2) * 150 },
        properties: {},
        nextNodeUuids: [],
      })
      if (previousUuid) {
        connections.push(createConnection(previousUuid, endUuid))
      }
    }
    
    console.log('[useLorToBlueprint] Generated nodes:', nodes.length, 'connections:', connections.length)
    console.log('[useLorToBlueprint] Connections:', connections.map(c => `${c.source}:${c.sourcePort} -> ${c.target}:${c.targetPort}`))
    
    return {
      id: scene.id,
      nodes,
      connections,
    }
  }

  /**
   * 从 Lor JSON 字符串转换
   */
  async function convertFromJson(jsonContent: string): Promise<SerializedSceneGraph> {
    isConverting.value = true
    conversionError.value = null
    conversionProgress.value = 0
    
    try {
      conversionProgress.value = 20
      
      const scene = parseJson(jsonContent)
      console.log('[useLorToBlueprint] Lor parsed, scene.id:', scene?.id, 'dialogues count:', scene?.dialogues?.length)
      console.log('[useLorToBlueprint] Scene details:', JSON.stringify(scene, null, 2).substring(0, 1000))
      conversionProgress.value = 50
      
      if (!scene.id) {
        console.warn('[useLorToBlueprint] scene.id is empty, using default id')
        scene.id = 'untitled'
      }
      if (!scene.dialogues || !Array.isArray(scene.dialogues)) {
        console.warn('[useLorToBlueprint] scene.dialogues is invalid, initializing empty array')
        scene.dialogues = []
      }
      
      conversionProgress.value = 70
      
      const blueprint = convertLorToBlueprint(scene)
      console.log('[useLorToBlueprint] Blueprint generated, nodes:', blueprint?.nodes?.length)
      conversionProgress.value = 100
      
      return blueprint
    } catch (error) {
      conversionError.value = error instanceof Error ? error.message : '转换失败'
      throw error
    } finally {
      isConverting.value = false
    }
  }

  /**
   * 从文件路径加载并转换
   */
  async function convertFromFile(filePath: string): Promise<SerializedSceneGraph> {
    try {
      const response = await fetch(filePath)
      if (!response.ok) {
        throw new Error(`无法读取文件: ${filePath}`)
      }
      const jsonContent = await response.text()
      return await convertFromJson(jsonContent)
    } catch (error) {
      conversionError.value = error instanceof Error ? error.message : '文件读取失败'
      throw error
    }
  }

  /**
   * 验证 Lor 文件格式
   */
  function validateLorFile(content: string): { valid: boolean; errors: string[] } {
    const errors: string[] = []
    
    try {
      const scene = parseJson(content)
      
      if (!scene.id) {
        errors.push('缺少场景 ID')
      }
      
      if (!scene.dialogues || !Array.isArray(scene.dialogues)) {
        errors.push('对话列表格式错误或缺失')
      } else {
        scene.dialogues.forEach((dialogue, index) => {
          if (!dialogue.type) {
            errors.push(`第 ${index + 1} 个对话缺少类型`)
          }
          if (dialogue.type === 'Dialogue' && !dialogue.text) {
            errors.push(`第 ${index + 1} 个对话缺少文本内容`)
          }
          if (dialogue.type === 'Event' && !dialogue.event) {
            errors.push(`第 ${index + 1} 个事件缺少事件配置`)
          }
        })
      }
    } catch (error) {
      errors.push(`Lor JSON 解析错误: ${error instanceof Error ? error.message : '未知错误'}`)
    }
    
    return {
      valid: errors.length === 0,
      errors,
    }
  }

  return {
    isConverting,
    conversionError,
    conversionProgress,
    parseJson,
    convertLorToBlueprint,
    convertFromJson,
    convertFromFile,
    validateLorFile,
  }
}
