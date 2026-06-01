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
  position: {
    x: number
    y: number
  }
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
   * 简单的 YAML 解析器
   * 支持基本的 YAML 结构：键值对、数组、嵌套对象
   */
  function parseYaml(yamlContent: string): LorScene {
    console.log('[useLorToBlueprint] parseYaml called, content length:', yamlContent.length)
    
    const lines = yamlContent.split('\n')
    const result: any = { dialogues: [] }
    let currentKey: string | null = null
    let currentArray: any[] | null = null
    let currentObject: any = null
    let indentStack: { indent: number; key: string; isArray: boolean }[] = []

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]
      const trimmedLine = line.trim()

      if (!trimmedLine || trimmedLine.startsWith('#')) continue

      const indent = line.length - line.trimStart().length

      // 处理数组项
      if (trimmedLine.startsWith('- ')) {
        const itemContent = trimmedLine.substring(2).trim()
        
        if (currentArray) {
          if (itemContent.includes(':')) {
            const itemObj: any = {}
            const colonIndex = itemContent.indexOf(':')
            const key = itemContent.substring(0, colonIndex).trim()
            const value = itemContent.substring(colonIndex + 1).trim()
            itemObj[key] = parseValue(value)
            currentArray.push(itemObj)
            currentObject = itemObj
          } else {
            currentArray.push(parseValue(itemContent))
          }
        }
        continue
      }

      // 处理键值对
      if (trimmedLine.includes(':')) {
        const colonIndex = trimmedLine.indexOf(':')
        const key = trimmedLine.substring(0, colonIndex).trim()
        const value = trimmedLine.substring(colonIndex + 1).trim()

        while (indentStack.length > 0 && indentStack[indentStack.length - 1].indent >= indent) {
          indentStack.pop()
        }

        if (value === '' || value === '|' || value === '>') {
          if (i + 1 < lines.length) {
            const nextLine = lines[i + 1].trim()
            if (nextLine.startsWith('- ')) {
              currentArray = []
              if (indentStack.length > 0) {
                const parent = indentStack[indentStack.length - 1]
                if (parent.isArray && currentObject) {
                  currentObject[key] = currentArray
                } else {
                  result[key] = currentArray
                }
              } else {
                result[key] = currentArray
              }
              indentStack.push({ indent, key, isArray: true })
              currentObject = null
              continue
            }
          }
          const newObj: any = {}
          if (indentStack.length > 0) {
            const parent = indentStack[indentStack.length - 1]
            if (parent.isArray && currentArray) {
              const lastItem = currentArray[currentArray.length - 1]
              if (typeof lastItem === 'object' && !Array.isArray(lastItem)) {
                lastItem[key] = newObj
                currentObject = newObj
              }
            } else if (currentObject) {
              currentObject[key] = newObj
              currentObject = newObj
            } else {
              result[key] = newObj
              currentObject = newObj
            }
          } else {
            result[key] = newObj
            currentObject = newObj
          }
          indentStack.push({ indent, key, isArray: false })
        } else {
          const parsedValue = parseValue(value)
          if (indentStack.length > 0 && currentObject) {
            currentObject[key] = parsedValue
          } else {
            result[key] = parsedValue
          }
        }
      }
    }

    console.log('[useLorToBlueprint] parseYaml result:', JSON.stringify(result).substring(0, 500))
    return result as LorScene
  }

  /**
   * 解析 YAML 值
   */
  function parseValue(value: string): any {
    if (value === 'true') return true
    if (value === 'false') return false
    if (value === 'null' || value === '~') return null
    if (/^-?\d+$/.test(value)) return parseInt(value, 10)
    if (/^-?\d+\.\d+$/.test(value)) return parseFloat(value)
    // 移除引号
    if ((value.startsWith('"') && value.endsWith('"')) || 
        (value.startsWith("'") && value.endsWith("'"))) {
      return value.slice(1, -1)
    }
    return value
  }

  /**
   * 创建执行连接（通过 UUID）
   */
  function createConnection(fromUuid: string, toUuid: string): SerializedConnection {
    return {
      id: `conn_${fromUuid}_${toUuid}`,
      source: fromUuid,
      sourcePort: 'exec_out',
      target: toUuid,
      targetPort: 'exec_in',
    }
  }

  /**
   * 转换对话为蓝图节点
   * 使用 UUID 作为节点 ID，保留蓝图上的位置信息
   */
  function convertDialogueToNode(dialogue: LorDialogue, index: number, uuid: string): SerializedNode | null {
    // 优先使用 YAML 中保存的位置，否则使用自动布局位置
    const position = dialogue.position && typeof dialogue.position.x === 'number'
      ? { x: dialogue.position.x, y: dialogue.position.y }
      : { x: 300, y: 100 + index * 150 }
    
    if (dialogue.type === 'Dialogue') {
      const sprites = (dialogue.sprites || []).map((s: any) => ({
        path: s?.path || '',
        position: s?.position || 'center',
        layer: s?.layer || 0,
      }))
      
      return {
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
      }
    }
    
    if (dialogue.type === 'Event' && dialogue.event) {
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
        id: uuid,
        nodeType: 'EventNode',
        position,
        properties,
      }
    }
    
    if (dialogue.type === 'Branch' && dialogue.branch) {
      return {
        id: uuid,
        nodeType: 'BranchNode',
        position,
        properties: {
          choices: dialogue.branch.choices.map(c => ({
            text: c.text,
            targetScene: c.targetScene,
            condition: c.condition,
          })),
        },
      }
    }
    
    return null
  }

  /**
   * 自动布局节点
   */
  function autoLayoutNodes(nodes: SerializedNode[]): SerializedNode[] {
    const NODE_WIDTH = 200
    const NODE_HEIGHT = 120
    const HORIZONTAL_SPACING = 100
    const VERTICAL_SPACING = 50
    
    // 分析连接关系，识别分支结构
    const branchNodes = nodes.filter(n => n.nodeType === 'BranchNode')
    const linearNodes = nodes.filter(n => n.nodeType !== 'BranchNode')
    
    // 布局线性节点
    linearNodes.forEach((node, index) => {
      node.position = {
        x: 100 + (index % 3) * (NODE_WIDTH + HORIZONTAL_SPACING),
        y: 100 + Math.floor(index / 3) * (NODE_HEIGHT + VERTICAL_SPACING),
      }
    })
    
    // 布局分支节点（放在右侧）
    branchNodes.forEach((node, index) => {
      node.position = {
        x: 100 + linearNodes.length * (NODE_WIDTH + HORIZONTAL_SPACING) + 200,
        y: 100 + index * (NODE_HEIGHT + VERTICAL_SPACING) * 2,
      }
    })
    
    return nodes
  }

  /**
   * 主转换函数：Lor 剧本 → 蓝图
   * 每个对话节点分配一个 UUID，通过 UUID 进行连线
   */
  function convertLorToBlueprint(scene: LorScene): SerializedSceneGraph {
    const nodes: SerializedNode[] = []
    const connections: SerializedConnection[] = []
    
    // 用于记录上一个节点的 UUID，用于连线
    let previousUuid: string | null = null
    
    // 第一遍：为每个对话生成 UUID（如果 YAML 中没有则新建）
    scene.dialogues.forEach((dialogue) => {
      if (!dialogue.uuid) {
        dialogue.uuid = generateUUID()
      }
    })
    
    // 创建开始节点（使用固定 UUID）
    const startUuid = 'start_node'
    nodes.push({
      id: startUuid,
      nodeType: 'StartNode',
      position: { x: 50, y: 50 },
      properties: {},
    })
    previousUuid = startUuid
    
    // 处理 BGM 初始配置
    if (scene.bgm && scene.bgm.path) {
      const bgmUuid = generateUUID()
      nodes.push({
        id: bgmUuid,
        nodeType: 'EventNode',
        position: { x: 300, y: 50 },
        properties: {
          subType: 'playBgm',
          bgmPath: scene.bgm.path,
          volume: scene.bgm.volume / 100,
          loop: scene.bgm.loop,
        },
      })
      connections.push(createConnection(previousUuid!, bgmUuid))
      previousUuid = bgmUuid
    }
    
    // 处理初始背景
    if (scene.background) {
      const bgUuid = generateUUID()
      nodes.push({
        id: bgUuid,
        nodeType: 'EventNode',
        position: { x: 300, y: 200 },
        properties: {
          subType: 'changeBackground',
          imagePath: scene.background,
          transition: 'fade',
          duration: 1.0,
        },
      })
      connections.push(createConnection(previousUuid!, bgUuid))
      previousUuid = bgUuid
    }
    
    // 转换对话列表，使用 UUID 进行连线
    scene.dialogues.forEach((dialogue, index) => {
      const node = convertDialogueToNode(dialogue, index, dialogue.uuid)
      if (node) {
        nodes.push(node)
        connections.push(createConnection(previousUuid!, node.id))
        previousUuid = node.id
      }
    })
    
    // 创建结束节点（使用固定 UUID）
    const endUuid = 'end_node'
    nodes.push({
      id: endUuid,
      nodeType: 'EndNode',
      position: { x: 300, y: 100 + (scene.dialogues.length + 2) * 150 },
      properties: {},
    })
    if (previousUuid) {
      connections.push(createConnection(previousUuid, endUuid))
    }
    
    // 应用自动布局（仅对没有预设位置的节点）
    const layoutedNodes = autoLayoutNodes(nodes)
    
    console.log('[useLorToBlueprint] Generated nodes:', nodes.length, 'connections:', connections.length)
    console.log('[useLorToBlueprint] Connections:', connections.map(c => `${c.source}:${c.sourcePort} -> ${c.target}:${c.targetPort}`))
    
    return {
      id: scene.id,
      nodes: layoutedNodes,
      connections,
    }
  }

  /**
   * 从 YAML 字符串转换
   */
  async function convertFromYaml(yamlContent: string): Promise<SerializedSceneGraph> {
    isConverting.value = true
    conversionError.value = null
    conversionProgress.value = 0
    
    try {
      conversionProgress.value = 20
      
      const scene = parseYaml(yamlContent)
      console.log('[useLorToBlueprint] YAML parsed, scene.id:', scene?.id, 'dialogues count:', scene?.dialogues?.length)
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
      const yamlContent = await response.text()
      return await convertFromYaml(yamlContent)
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
      const scene = parseYaml(content)
      
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
      errors.push(`YAML 解析错误: ${error instanceof Error ? error.message : '未知错误'}`)
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
    parseYaml,
    convertLorToBlueprint,
    convertFromYaml,
    convertFromFile,
    validateLorFile,
  }
}
