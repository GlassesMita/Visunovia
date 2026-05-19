import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { Editor } from '@baklavajs/core'

export interface GraphNode {
  id: string
  type: string
  position: { x: number; y: number }
  data: Record<string, any>
}

export interface GraphConnection {
  id: string
  from: { nodeId: string; port: string }
  to: { nodeId: string; port: string }
}

export const useNodeGraphStore = defineStore('nodeGraph', () => {
  const editor = ref<Editor | null>(null)
  const nodes = ref<GraphNode[]>([])
  const connections = ref<GraphConnection[]>([])
  const currentSceneId = ref<string | null>(null)
  const isDirty = ref(false)

  const nodeCount = computed(() => nodes.value.length)
  const connectionCount = computed(() => connections.value.length)

  function initializeEditor(editorInstance: Editor) {
    editor.value = editorInstance
    
    editorInstance.graph.events.addNode.subscribe(editorInstance, () => {
      syncNodes()
      isDirty.value = true
    })
    
    editorInstance.graph.events.removeNode.subscribe(editorInstance, () => {
      syncNodes()
      isDirty.value = true
    })
    
    editorInstance.graph.events.addConnection.subscribe(editorInstance, () => {
      syncConnections()
      isDirty.value = true
    })
    
    editorInstance.graph.events.removeConnection.subscribe(editorInstance, () => {
      syncConnections()
      isDirty.value = true
    })
  }

  function syncNodes() {
    if (!editor.value) return
    
    nodes.value = editor.value.graph.nodes.map(node => ({
      id: node.id,
      type: node.type,
      position: { x: node.position.x, y: node.position.y },
      data: {}
    }))
  }

  function syncConnections() {
    if (!editor.value) return
    
    connections.value = editor.value.graph.connections.map(conn => ({
      id: `${conn.from.nodeId}:${conn.from.name}->${conn.to.nodeId}:${conn.to.name}`,
      from: {
        nodeId: conn.from.nodeId,
        port: conn.from.name
      },
      to: {
        nodeId: conn.to.nodeId,
        port: conn.to.name
      }
    }))
  }

  function addNode(type: string, position?: { x: number; y: number }) {
    if (!editor.value) return null
    
    const nodeTypeInfo = editor.value.nodeTypes.get(type)
    if (nodeTypeInfo) {
      const node = new nodeTypeInfo.type()
      if (position) {
        node.position = position
      }
      editor.value.graph.addNode(node)
      return node
    }
    
    return null
  }
  
  function removeNode(nodeId: string) {
    if (!editor.value) return
    
    const node = editor.value.graph.nodes.find(n => n.id === nodeId)
    if (node) {
      editor.value.graph.removeNode(node as any)
    }
  }
  
  function updateNodeProperty(_nodeId: string, _propertyName: string, _value: any) {
    // Property updates are not directly supported in BaklavaJS 2.x
    // This function is kept for API compatibility
    isDirty.value = true
  }

  function serializeGraph() {
    if (!editor.value) return null
    
    syncNodes()
    syncConnections()
    
    return {
      nodes: nodes.value,
      connections: connections.value
    }
  }

  function deserializeGraph(data: { nodes: GraphNode[]; connections: GraphConnection[] }) {
    if (!editor.value) return
    
    editor.value.graph.nodes.slice().forEach(node => {
      editor.value!.graph.removeNode(node as any)
    })
    
    data.nodes.forEach(nodeData => {
      const nodeTypeInfo = editor.value!.nodeTypes.get(nodeData.type)
      if (nodeTypeInfo) {
        const node = new nodeTypeInfo.type()
        node.id = nodeData.id
        node.position = nodeData.position
        editor.value!.graph.addNode(node as any)
      }
    })
    
    data.connections.forEach(connData => {
      try {
        const fromNode = editor.value!.graph.nodes.find(n => n.id === connData.from.nodeId)
        const toNode = editor.value!.graph.nodes.find(n => n.id === connData.to.nodeId)
        
        if (fromNode && toNode) {
          editor.value!.graph.addConnection(
            fromNode.outputs[connData.from.port],
            toNode.inputs[connData.to.port]
          )
        }
      } catch (e) {
        console.error('Failed to restore connection:', e)
      }
    })
    
    isDirty.value = false
  }

  function markClean() {
    isDirty.value = false
  }

  return {
    editor,
    nodes,
    connections,
    currentSceneId,
    isDirty,
    
    nodeCount,
    connectionCount,
    
    initializeEditor,
    syncNodes,
    syncConnections,
    addNode,
    removeNode,
    updateNodeProperty,
    serializeGraph,
    deserializeGraph,
    markClean
  }
})
