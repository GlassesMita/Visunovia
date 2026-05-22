import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { sceneGraphApi } from '@/api'
import type { Editor } from '@baklavajs/core'

export interface SerializedNode {
  id: string
  nodeType: string
  subType?: string
  position: { x: number; y: number }
  properties: Record<string, any>
}

export interface SerializedConnection {
  id: string
  source: string
  sourcePort: string
  target: string
  targetPort: string
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
    if (key === 'exec_in' || key === 'subType') return
    
    if (iface && iface.value !== undefined) {
      properties[key] = iface.value
    }
  })
  
  return properties
}

function serializeEditorGraph(editor: Editor): SerializedSceneGraph {
  const nodes: SerializedNode[] = editor.graph.nodes.map((node) => ({
    id: node.id,
    nodeType: extractNodeType(node),
    subType: extractSubType(node),
    position: { x: node.position.x, y: node.position.y },
    properties: extractNodeProperties(node)
  }))
  
  const connections: SerializedConnection[] = editor.graph.connections.map((conn) => ({
    id: `${conn.from.nodeId}:${conn.from.name}->${conn.to.nodeId}:${conn.to.name}`,
    source: conn.from.nodeId,
    sourcePort: conn.from.name,
    target: conn.to.nodeId,
    targetPort: conn.to.name
  }))
  
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
    if (subTypeInput && subTypeInput.setValue) {
      subTypeInput.setValue(subType)
    }
  }
  
  await new Promise((resolve) => setTimeout(resolve, 50))
  
  const updatedNode = editor.graph.nodes.find((n) => n.id === nodeId)
  if (!updatedNode || !updatedNode.inputs) return
  
  Object.entries(properties).forEach(([propName, propValue]) => {
    const iface = (updatedNode.inputs as any)[propName]
    if (iface && iface.setValue && propValue !== undefined) {
      try {
        iface.setValue(propValue)
      } catch (e) {
        console.warn(`[useNodeOperations] Failed to restore property "${propName}":`, e)
      }
    }
  })
}

async function deserializeToEditor(editor: Editor, data: SerializedSceneGraph) {
  const graph = editor.graph as any
  graph.clear?.() || (graph.nodes = [] && (graph.connections = []))
  
  for (const nodeData of data.nodes) {
    const nodeTypeInfo = editor.nodeTypes.get(nodeData.nodeType)
    if (!nodeTypeInfo) {
      console.warn(`[useNodeOperations] Unknown node type: ${nodeData.nodeType}`)
      continue
    }
    
    const node = new nodeTypeInfo.type()
    node.id = nodeData.id
    node.position = nodeData.position
    
    editor.graph.addNode(node as any)
  }
  
  await new Promise((resolve) => setTimeout(resolve, 100))
  
  for (const nodeData of data.nodes) {
    await restoreNodeProperties(
      editor,
      nodeData.id,
      nodeData.nodeType,
      nodeData.subType,
      nodeData.properties
    )
  }
  
  await new Promise((resolve) => setTimeout(resolve, 100))
  
  for (const connData of data.connections) {
    try {
      const fromNode = editor.graph.nodes.find((n) => n.id === connData.source)
      const toNode = editor.graph.nodes.find((n) => n.id === connData.target)
      
      if (!fromNode || !toNode) continue
      
      const fromPort = fromNode.outputs?.[connData.sourcePort]
      const toPort = toNode.inputs?.[connData.targetPort]
      
      if (fromPort && toPort) {
        editor.graph.addConnection(fromPort, toPort)
      }
    } catch (e) {
      console.error(`[useNodeOperations] Failed to restore connection:`, e)
    }
  }
}

export function useNodeOperations() {
  const nodeGraphStore = useNodeGraphStore()
  const editorStore = useEditorStore()
  
  async function saveSceneGraph(sceneId: string): Promise<boolean> {
    const editor = getEditorInstance()
    
    if (!editor) {
      console.error('[useNodeOperations] Editor instance not found, cannot save')
      return false
    }
    
    try {
      const serializedData = serializeEditorGraph(editor)
      serializedData.id = sceneId
      
      await sceneGraphApi.put(sceneId, serializedData)
      
      nodeGraphStore.markClean()
      
      console.log('[useNodeOperations] Scene graph saved successfully:', sceneId)
      return true
    } catch (error) {
      // 异常可能来源：网络请求失败、后端服务不可用、数据格式校验不通过
      console.error('[useNodeOperations] Failed to save scene graph:', error)
      return false
    }
  }
  
  async function loadSceneGraph(sceneId: string): Promise<boolean> {
    const editor = getEditorInstance()
    
    if (!editor) {
      console.error('[useNodeOperations] Editor instance not found, cannot load')
      return false
    }
    
    try {
      const response = await sceneGraphApi.get(sceneId)
      
      let graphData: SerializedSceneGraph
      
      if (response.data?.data) {
        graphData = response.data.data
      } else if (response.data?.nodes) {
        graphData = response.data
      } else {
        throw new Error('Invalid response format: missing node data')
      }
      
      await deserializeToEditor(editor, graphData)
      
      nodeGraphStore.currentSceneId = sceneId
      nodeGraphStore.markClean()
      
      console.log('[useNodeOperations] Scene graph loaded successfully:', sceneId)
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
