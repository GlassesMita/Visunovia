import { ref } from 'vue'
import type { SerializedNode, SerializedConnection, SerializedSceneGraph } from './useNodeOperations'

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
  StopBGM: 'stopBgm',
  PlaySFX: 'playSfx',
  PlayVoice: 'playVoice',
  ChangeBackground: 'changeBackground',
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
        properties: {
          speaker: dialogue.speaker || '',
          text: dialogue.text || '',
          voice: dialogue.voice || '',
          sprites,
          textEffect: dialogue.textEffect || { type: '', speed: 0, shake: false, fadeDuration: 0, delayBeforeStart: 0 },
          animation: dialogue.animation || { type: '', duration: 0 },
        },
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
      
      // 根据事件类型映射参数
      switch (dialogue.event.eventType) {
        case 'ChangeBGM':
          properties.bgmPath = dialogue.event.parameters.bgmFile || ''
          properties.volume = 0.8
          properties.loop = true
          break
        case 'ShowCharacter':
          properties.characterId = dialogue.event.parameters.character || ''
          properties.position = 'center'
          properties.expression = 'default'
          break
        case 'ChangeBackground':
          properties.imagePath = dialogue.event.parameters.background || ''
          properties.transition = 'fade'
          properties.duration = 1.0
          break
        case 'StopBGM':
          properties.fadeOutDuration = 0.5
          break
        case 'PlaySFX':
          properties.sfxPath = dialogue.event.parameters.soundFile || ''
          properties.volume = 1.0
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
        properties,
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
      const nodes = scene.nodes.map((node) => ({
        ...node,
        uuid: node.uuid || node.id,
        id: node.id || node.uuid,
        properties: node.properties || {},
        position: normalizeBlueprintPosition(node.position || { x: 0, y: 0 }),
        nextNodeUuids: node.nextNodeUuids || [],
      }))
      const nodeIds = new Set(nodes.map(node => node.id))
      const repairedDialogueId = nodes.find(node => node.nodeType === 'DialogueNode')?.id
        || scene.dialogues.find(dialogue => dialogue.type === 'Dialogue')?.uuid
      const repairedStartId = nodes.find(node => node.nodeType === 'StartNode')?.id
      const repairedEndId = nodes.find(node => node.nodeType === 'EndNode')?.id
      const normalizePort = (port: string, direction: 'source' | 'target') => {
        if (port === '→') return direction === 'source' ? 'execOut' : 'execIn'
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

      return {
        id: scene.id,
        nodes,
        connections: Array.isArray(scene.connections)
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
          : [],
      }
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
        properties: {
          bgmPath: scene.bgm.path,
          volume: scene.bgm.volume / 100,
          loop: scene.bgm.loop,
        },
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
        properties: {
          imagePath: scene.background,
          transition: 'fade',
          duration: 1.0,
        },
        nextNodeUuids: [],
      })
      connections.push(createConnection(previousUuid!, bgUuid))
      previousUuid = bgUuid
    }
    
    // 转换对话列表，使用 UUID 进行连线
    scene.dialogues.forEach((dialogue, index) => {
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
