import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { sceneGraphApi } from '@/api'
import type { Editor } from '@baklavajs/core'

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
    if (key === 'exec_in' || key === 'execIn' || key === 'subType') return
    
    if (iface && iface.value !== undefined) {
      properties[key] = iface.value
    }
  })
  
  return properties
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
  }

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
    : ['execOut', 'exec_out']

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
  const nodes: SerializedNode[] = editor.graph.nodes.map((node) => ({
    uuid: node.id,
    id: node.id,
    nodeType: normalizeNodeType(extractNodeType(node)),
    subType: extractSubType(node),
    position: editorToBlueprintPosition(node.position),
    properties: extractNodeProperties(node),
    nextNodeUuids: (node as any).nextNodeUuids || []
  }))
  
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
  
  await new Promise((resolve) => setTimeout(resolve, 50))
  
  const updatedNode = editor.graph.nodes.find((n) => n.id === nodeId)
  if (!updatedNode || !updatedNode.inputs) return
  
  Object.entries(properties).forEach(([propName, propValue]) => {
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
    graph.clear?.() || (graph.nodes = [] && (graph.connections = []))
    
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
