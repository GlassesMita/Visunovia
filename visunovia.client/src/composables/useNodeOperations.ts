import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useCharacterStore } from '@/stores/useCharacterStore'
import { sceneGraphApi } from '@/api'
import type { Editor } from '@baklavajs/core'
import { normalizeAssetProperties } from '@/utils/assetPaths'

export interface SerializedNode {
  /** 节点唯一标识符（UUID） */
  uuid: string
  /** 节点类型名称 */
  nodeType: string
  /** 节点子类型 */
  subType?: string
  /** 蓝图视图中的绝对坐标位置 */
  position: { x: number; y: number }
  /** 节点属性 */
  properties: Record<string, any>
  /** 下一个执行步骤的节点 UUID 列表 */
  nextNodeUuids: string[]
  /** 兼容旧代码的 id 属性 */
  id: string
}

export interface SerializedConnection {
  /** 连线唯一标识符（UUID） */
  uuid: string
  /** 源节点 UUID */
  sourceNodeUuid: string
  /** 源端口名称 */
  sourcePort: string
  /** 目标节点 UUID */
  targetNodeUuid: string
  /** 目标端口名称 */
  targetPort: string
  /** 兼容旧代码的属性 */
  id: string
  source: string
  target: string
}

export interface SerializedSceneGraph {
  id: string
  nodes: SerializedNode[]
  connections: SerializedConnection[]
}

function getEditorInstance(): Editor | null {
  return (window as any).__editor as Editor | null
}

function extractNodeType(node: any): string {
  return node.type || 'UnknownNode'
}

function normalizeNodeType(nodeType: string | undefined): string {
  const type = nodeType || 'UnknownNode'
  const aliases: Record<string, string> = {
    Start: 'StartNode',
    End: 'EndNode',
    Event: 'EventNode',
    Dialogue: 'DialogueNode',
    Branch: 'BranchNode',
    Logic: 'LogicNode',
    Resource: 'ResourceNode',
    Choice: 'ChoiceNode',
    CharacterControl: 'CharacterControlNode',
  }

  return aliases[type] || type
}

function extractSubType(node: any): string | undefined {
  const subTypeInterface = node.inputs?.subType
  if (subTypeInterface && subTypeInterface.value !== undefined) {
    return subTypeInterface.value
  }
  return undefined
}

function extractNodeProperties(node: any): Record<string, any> {
  const properties: Record<string, any> = {}
  
  if (!node.inputs) return properties
  
  Object.entries(node.inputs).forEach(([key, iface]: [string, any]) => {
    if (key === 'exec_in' || key === 'execIn' || key.startsWith('characterControl') || key === 'subType') return
    
    if (iface && iface.value !== undefined) {
      properties[key] = iface.value
    }
  })

  if (normalizeNodeType(extractNodeType(node)) === 'DialogueNode') {
    const speakers = ['speaker', 'speaker2', 'speaker3', 'speaker4', 'speaker5']
      .map(key => String(properties[key] ?? '').trim())
      .filter(Boolean)
      .slice(0, 5)
    const dialogueCharacters = [1, 2, 3, 4, 5, 6]
      .map(index => {
        const suffix = index === 1 ? '' : String(index)
        return {
          speaker: String(properties[`speaker${suffix}`] ?? '').trim(),
          sprite: String(properties[`sprite${index}`] ?? '').trim(),
          voice: String(properties[`voice${index}`] ?? properties[`voice${suffix}`] ?? '').trim(),
          slot: String(index),
        }
      })
      .filter(item => item.speaker || item.sprite || item.voice)

    if (speakers.length > 0) {
      properties.speakers = speakers
      properties.speaker = speakers[0]
    }

    if (dialogueCharacters.length > 0) {
      properties.dialogueCharacters = dialogueCharacters
      properties.sprites = dialogueCharacters
        .filter(item => item.sprite)
        .map((item, index) => ({ path: item.sprite, character: item.speaker, position: 'center', layer: index }))
      properties.voices = dialogueCharacters
        .filter(item => item.voice)
        .map(item => ({ speaker: item.speaker, slot: item.slot, path: item.voice }))
    }
  }
  
  return properties
}

function getInterfaceValue(node: any, key: string): any {
  return node?.inputs?.[key]?.value
}

function parseCharacterControls(value: any): any[] {
  if (Array.isArray(value)) return value
  const raw = String(value || '').trim()
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

function normalizeCharacterControl(control: any, fallbackSlot = '1') {
  const slot = String(control?.slot || fallbackSlot || '1')
  const action = String(control?.action || control?.mode || 'none').trim() || 'none'
  return {
    slot,
    mode: action,
    action,
    character: slot === '6'
      ? String(control?.unmanagedCharacter || control?.character || '').trim()
      : String(control?.character || '').trim(),
    unmanagedCharacter: String(control?.unmanagedCharacter || '').trim(),
    sprite: slot === '6' ? '' : String(normalizeAssetProperties({ sprite: control?.sprite }).sprite ?? '').trim(),
    sfx: String(normalizeAssetProperties({ sfx: control?.sfx }).sfx ?? '').trim(),
    expression: String(control?.expression || 'default').trim() || 'default',
    fromPosition: String(control?.fromPosition || '').trim(),
    toPosition: String(control?.toPosition || 'none').trim() || 'none',
    position: String(control?.position || 'center').trim() || 'center',
    animation: String(control?.animation || 'fade').trim() || 'fade',
    easing: String(control?.easing || 'easeOutCubic').trim() || 'easeOutCubic',
    duration: Number(control?.duration ?? 0.3) || 0.3,
  }
}

function getCharacterControlsFromNode(node: any, fallbackSlot = '1') {
  const storedControls = parseCharacterControls(getInterfaceValue(node, 'characterControlsJson') || node?.data?.characterControls || node?.data?.characterControlsJson)
  if (storedControls.length > 0) {
    return storedControls.map((control, index) => normalizeCharacterControl(control, String(index + 1))).filter(control => control.action !== 'none')
  }

  const selectedSlot = String(getInterfaceValue(node, 'slot') ?? fallbackSlot).trim() || fallbackSlot
  const legacyAction = String(getInterfaceValue(node, 'action') ?? '').trim()
  const legacyCharacter = getInterfaceValue(node, 'character')
  const legacySprite = getInterfaceValue(node, 'sprite')
  if (!legacyAction && !legacyCharacter && !legacySprite) return []

  return [normalizeCharacterControl({
    slot: selectedSlot,
    action: legacyAction || 'show',
    character: legacyCharacter,
    unmanagedCharacter: getInterfaceValue(node, 'unmanagedCharacter'),
    sprite: legacySprite,
    sfx: getInterfaceValue(node, 'sfx'),
    expression: getInterfaceValue(node, 'expression'),
    fromPosition: getInterfaceValue(node, 'fromPosition'),
    toPosition: getInterfaceValue(node, 'toPosition'),
    position: getInterfaceValue(node, 'position'),
    animation: getInterfaceValue(node, 'animation'),
    easing: getInterfaceValue(node, 'easing'),
    duration: getInterfaceValue(node, 'duration'),
  }, selectedSlot)]
}

function buildCharacterControlsForDialogue(editor: Editor, dialogueNode: any) {
  return editor.graph.connections
    .map((conn) => {
      if (conn.to.nodeId !== dialogueNode.id) return null

      const controlNode = editor.graph.nodes.find((node) => node.id === conn.from.nodeId) as any
      if (!controlNode || normalizeNodeType(extractNodeType(controlNode)) !== 'CharacterControlNode') return null

      const sourcePort = normalizePortName(String(conn.from?.name || conn.from?.port || ''))
      const targetPort = findInterfaceKey(dialogueNode.inputs, conn.to, conn.to.name)
      const isLegacyControlPort = targetPort.startsWith('characterControl')
      const isExecutionControlLink = (sourcePort === 'execOut' || sourcePort === 'controlOut') && targetPort === 'execIn'
      if (!isLegacyControlPort && !isExecutionControlLink) return null

      return { controlNode, targetPort }
    })
    .filter(Boolean)
    .flatMap((item) => {
      const connectedSlot = item!.targetPort.startsWith('characterControl')
        ? item!.targetPort.replace('characterControl', '')
        : '1'
      return getCharacterControlsFromNode(item!.controlNode, connectedSlot || '1')
    })
    .filter(Boolean)
    .sort((a: any, b: any) => Number(a.slot) - Number(b.slot))
}

function findInterfaceKey(interfaces: Record<string, any> | undefined, iface: any, fallback: string): string {
  if (!interfaces || !iface) return fallback

  const entry = Object.entries(interfaces).find(([, candidate]) => candidate === iface)
  return entry?.[0] || normalizePortName(fallback, interfaces)
}

function normalizePortName(portName: string | undefined, interfaces?: Record<string, any>): string {
  const name = String(portName || '').trim()
  if (!name) return ''
  if (interfaces?.[name]) return name
  if (name === '→') {
    if (interfaces?.execOut) return 'execOut'
    if (interfaces?.execIn) return 'execIn'
    if (interfaces?.controlOut) return 'controlOut'
  }
  if (name === 'controlOut') return interfaces?.execOut ? 'execOut' : 'controlOut'

  const characterControlMatch = name.match(/(?:◆|characterControl)\s*(\d+)/i)
  if (characterControlMatch) {
    const key = `characterControl${characterControlMatch[1]}`
    if (!interfaces || interfaces[key]) return key
  }

  return name
}

function resolveConnectionEndpoint(editor: Editor, endpoint: any, direction: 'input' | 'output') {
  const collectionKey = direction === 'input' ? 'inputs' : 'outputs'
  const nodeByEndpointId = editor.graph.nodes.find((node) => node.id === endpoint?.nodeId) as any
  if (nodeByEndpointId) {
    const portKey = findInterfaceKey(nodeByEndpointId[collectionKey], endpoint, endpoint?.name || endpoint?.port)
    return { nodeId: nodeByEndpointId.id, portKey }
  }

  for (const node of editor.graph.nodes as any[]) {
    const interfaces = node?.[collectionKey]
    const match = Object.entries(interfaces || {}).find(([, candidate]) => candidate === endpoint)
    if (match) {
      return { nodeId: node.id, portKey: match[0] }
    }
  }

  return {
    nodeId: String(endpoint?.nodeId || ''),
    portKey: normalizePortName(endpoint?.name || endpoint?.port),
  }
}

function setInterfaceValue(iface: any, value: any) {
  if (!iface || value === undefined) return

  if (typeof iface.setValue === 'function') {
    iface.setValue(value)
    return
  }

  iface.value = value
}

function resolvePort(ports: Record<string, any> | undefined, requestedName: string | undefined, direction: 'input' | 'output') {
  if (!ports) return undefined
  if (requestedName && ports[requestedName]) return ports[requestedName]

  const normalizedName = normalizePortName(requestedName, ports)
  if (normalizedName && ports[normalizedName]) return ports[normalizedName]

  const aliases = direction === 'input'
    ? ['execIn', 'exec_in']
    : ['execOut', 'exec_out', 'controlOut']

  for (const alias of aliases) {
    if (ports[alias]) return ports[alias]
  }

  return undefined
}

function normalizeBlueprintPosition(position: { x: number; y: number } | undefined) {
  return {
    x: Math.max(0, Number(position?.x ?? 0)),
    y: Number(position?.y ?? 0),
  }
}

function getBlueprintOriginY(): number {
  const editorElement = document.querySelector('.baklava-editor-wrapper, .baklava-editor') as HTMLElement | null
  return (editorElement?.clientHeight ?? window.innerHeight ?? 0) / 2
}

function editorToBlueprintPosition(position: { x: number; y: number } | undefined) {
  const normalized = normalizeBlueprintPosition(position)
  return {
    x: normalized.x,
    y: normalized.y - getBlueprintOriginY(),
  }
}

function blueprintToEditorPosition(position: { x: number; y: number } | undefined) {
  const normalized = normalizeBlueprintPosition(position)
  return {
    x: normalized.x,
    y: normalized.y + getBlueprintOriginY(),
  }
}

function serializeEditorGraph(editor: Editor): SerializedSceneGraph {
  const nodes: SerializedNode[] = editor.graph.nodes.map((node) => {
    const nodeType = normalizeNodeType(extractNodeType(node))
    const properties = normalizeAssetProperties(extractNodeProperties(node))
    if (nodeType === 'CharacterControlNode') {
      const characterControls = getCharacterControlsFromNode(node)
      properties.characterControls = characterControls
      properties.characterControlsJson = JSON.stringify(characterControls)
      properties.tlorFormatVersion = '1.1'
    }
    if (nodeType === 'DialogueNode') {
      const characterControls = buildCharacterControlsForDialogue(editor, node)
      if (characterControls.length > 0) {
        properties.characterControls = characterControls
      }
    }

    return {
      uuid: node.id,
      id: node.id,
      nodeType,
      subType: extractSubType(node),
      position: editorToBlueprintPosition(node.position),
      properties,
      nextNodeUuids: (node as any).nextNodeUuids || []
    }
  })
  
  const connections: SerializedConnection[] = editor.graph.connections.map((conn) => {
    const source = resolveConnectionEndpoint(editor, conn.from, 'output')
    const target = resolveConnectionEndpoint(editor, conn.to, 'input')
    const id = `${source.nodeId}:${source.portKey}->${target.nodeId}:${target.portKey}`

    return {
      uuid: id,
      id,
      sourceNodeUuid: source.nodeId,
      source: source.nodeId,
      sourcePort: source.portKey,
      targetNodeUuid: target.nodeId,
      target: target.nodeId,
      targetPort: target.portKey
    }
  })
  
  return {
    id: '',
    nodes,
    connections
  }
}

async function restoreNodeProperties(
  editor: Editor,
  nodeId: string,
  _nodeType: string,
  subType: string | undefined,
  properties: Record<string, any>
) {
  await new Promise((resolve) => setTimeout(resolve, 0))
  
  const node = editor.graph.nodes.find((n) => n.id === nodeId)
  if (!node) return
  
  if (subType && node.inputs?.subType) {
    const subTypeInput = node.inputs.subType as any
    if (subTypeInput) {
      setInterfaceValue(subTypeInput, subType)
    }
  }

  ;(node as any).data = {
    ...((node as any).data || {}),
    ...(properties || {}),
    subType,
  }
  
  await new Promise((resolve) => setTimeout(resolve, 50))
  
  const updatedNode = editor.graph.nodes.find((n) => n.id === nodeId)
  if (!updatedNode || !updatedNode.inputs) return

  const normalizedType = normalizeNodeType(_nodeType)
  const normalizedProperties = { ...properties }
  if (normalizedType === 'CharacterControlNode') {
    const controls = parseCharacterControls(normalizedProperties.characterControls || normalizedProperties.characterControlsJson)
      .map((control, index) => normalizeCharacterControl(control, String(index + 1)))
    if (controls.length > 0) {
      normalizedProperties.characterControls = controls
      normalizedProperties.characterControlsJson = JSON.stringify(controls)
      ;(updatedNode as any).data = {
        ...((updatedNode as any).data || {}),
        ...normalizedProperties,
      }
    }
  }
  
  Object.entries(normalizedProperties).forEach(([propName, propValue]) => {
    const iface = (updatedNode.inputs as any)[propName]
    if (iface && propValue !== undefined) {
      try {
        setInterfaceValue(iface, propValue)
      } catch (e) {
        console.warn(`[useNodeOperations] Failed to restore property "${propName}":`, e)
      }
    }
  })
}

async function deserializeToEditor(editor: Editor, data: SerializedSceneGraph) {
  const graph = editor.graph as any
  
  if (typeof graph.clear === 'function') {
    graph.clear()
  } else {
    if (Array.isArray(graph.nodes)) graph.nodes.length = 0
    if (Array.isArray(graph.connections)) graph.connections.length = 0
  }
  
  for (const nodeData of data.nodes) {
    const normalizedNodeType = normalizeNodeType(nodeData.nodeType)
    const nodeTypeInfo = editor.nodeTypes.get(normalizedNodeType)
    if (!nodeTypeInfo) {
      console.warn(`[useNodeOperations] Unknown node type: ${nodeData.nodeType}`)
      continue
    }
    
    const node = new nodeTypeInfo.type()
    node.id = nodeData.uuid || nodeData.id
    node.position = blueprintToEditorPosition(nodeData.position)
    
    // 保存 nextNodeUuids 到节点对象上，供后续使用
    ;(node as any).nextNodeUuids = nodeData.nextNodeUuids || []
    
    editor.graph.addNode(node as any)
  }
  
  await new Promise((resolve) => setTimeout(resolve, 100))
  
  for (const nodeData of data.nodes) {
    const nodeId = nodeData.uuid || nodeData.id
    await restoreNodeProperties(
      editor,
      nodeId,
      normalizeNodeType(nodeData.nodeType),
      nodeData.subType,
      nodeData.properties
    )
  }
  
  await new Promise((resolve) => setTimeout(resolve, 100))
  
  console.log('[deserializeToEditor] Starting to restore', data.connections?.length || 0, 'connections')
  
  for (const connData of data.connections) {
    try {
      const sourceUuid = connData.sourceNodeUuid || connData.source
      const targetUuid = connData.targetNodeUuid || connData.target
      
      const fromNode = editor.graph.nodes.find((n) => n.id === sourceUuid)
      const toNode = editor.graph.nodes.find((n) => n.id === targetUuid)
      
      if (!fromNode) {
        console.warn(`[deserializeToEditor] From node not found: ${sourceUuid}`)
        continue
      }
      if (!toNode) {
        console.warn(`[deserializeToEditor] To node not found: ${targetUuid}`)
        continue
      }
      
      console.log(`[deserializeToEditor] Connecting: ${sourceUuid}:${connData.sourcePort} -> ${targetUuid}:${connData.targetPort}`)
      console.log(`[deserializeToEditor] fromNode.outputs keys:`, Object.keys(fromNode.outputs || {}))
      console.log(`[deserializeToEditor] toNode.inputs keys:`, Object.keys(toNode.inputs || {}))
      
      const fromPort = resolvePort(fromNode.outputs, connData.sourcePort, 'output')
      const toPort = resolvePort(toNode.inputs, connData.targetPort, 'input')
      
      if (fromPort && toPort) {
        editor.graph.addConnection(fromPort, toPort)
        console.log(`[deserializeToEditor] Connection added successfully`)
      } else {
        console.warn(`[deserializeToEditor] Ports not found: fromPort=${!!fromPort}, toPort=${!!toPort}`)
      }
    } catch (e) {
      console.error(`[useNodeOperations] Failed to restore connection:`, e)
    }
  }
}

export function useNodeOperations() {
  const nodeGraphStore = useNodeGraphStore()
  const editorStore = useEditorStore()
  const characterStore = useCharacterStore()

  function normalizeSceneId(sceneId: string): string {
    return sceneId.replace(/\\/g, '/').split('/').pop()?.replace(/\.lor$/i, '') || sceneId
  }
  
  async function saveSceneGraph(sceneId: string): Promise<boolean> {
    const normalizedSceneId = normalizeSceneId(sceneId)
    const editor = getEditorInstance()
    
    if (!editor) {
      console.error('[useNodeOperations] Editor instance not found, cannot save')
      return false
    }
    
    try {
      const serializedData = serializeEditorGraph(editor)
      serializedData.id = normalizedSceneId
      
      await sceneGraphApi.put(normalizedSceneId, serializedData)
      
      nodeGraphStore.currentSceneId = normalizedSceneId
      editorStore.currentFileName = `${normalizedSceneId}.lor`
      nodeGraphStore.markClean()
      
      console.log('[useNodeOperations] Scene graph saved successfully:', normalizedSceneId)
      return true
    } catch (error) {
      // 异常可能来源：网络请求失败、后端服务不可用、数据格式校验不通过
      console.error('[useNodeOperations] Failed to save scene graph:', error)
      return false
    }
  }
  
  async function loadSceneGraph(sceneId: string): Promise<boolean> {
    const normalizedSceneId = normalizeSceneId(sceneId)
    const editor = getEditorInstance()
    
    if (!editor) {
      console.error('[useNodeOperations] Editor instance not found, cannot load')
      return false
    }
    
    try {
      await characterStore.refreshFromAssets()

      const response = await sceneGraphApi.get(normalizedSceneId)
      
      let graphData: SerializedSceneGraph
      
      if (response.data?.data) {
        graphData = response.data.data
      } else if (response.data?.nodes) {
        graphData = response.data
      } else {
        throw new Error('Invalid response format: missing node data')
      }
      
      await deserializeToEditor(editor, graphData)
      
      nodeGraphStore.currentSceneId = normalizedSceneId
      editorStore.currentFileName = `${normalizedSceneId}.lor`
      nodeGraphStore.markClean()
      
      console.log('[useNodeOperations] Scene graph loaded successfully:', normalizedSceneId)
      return true
    } catch (error) {
      // 异常可能来源：网络请求失败、场景 ID 不存在、响应数据格式异常
      console.error('[useNodeOperations] Failed to load scene graph:', error)
      return false
    }
  }
  
  async function saveGraph() {
    if (!editorStore.currentFileName) {
      console.warn('[useNodeOperations] No file is currently open, cannot save')
      return
    }
    
    await saveSceneGraph(editorStore.currentFileName)
  }
  
  async function loadGraph(sceneId: string) {
    await loadSceneGraph(sceneId)
  }
  
  function newGraph() {
    const editor = getEditorInstance()
    
    if (!editor) {
      console.error('[useNodeOperations] Editor instance not found')
      return
    }
    
    const graph = editor.graph as any
    if (typeof graph.clear === 'function') {
      graph.clear()
    } else {
      graph.nodes = []
      graph.connections = []
    }
    
    const startNodeTypeInfo = editor.nodeTypes.get('StartNode')
    if (startNodeTypeInfo) {
      const startNode = new startNodeTypeInfo.type()
      startNode.position = { x: 300, y: 200 }
      editor.graph.addNode(startNode as any)
    }
    
    editorStore.currentFileName = 'Untitled'
    nodeGraphStore.currentSceneId = null
    nodeGraphStore.markClean()
    
    console.log('[useNodeOperations] New graph created')
  }
  
  return {
    saveSceneGraph,
    loadSceneGraph,
    saveGraph,
    loadGraph,
    newGraph,
    serializeEditorGraph,
    deserializeToEditor
  }
}
