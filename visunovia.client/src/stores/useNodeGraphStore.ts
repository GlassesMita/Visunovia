import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { Editor } from '@baklavajs/core'
import { sceneGraphApi } from '@/api'
import { useUndoRedoStore } from './useUndoRedoStore'

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

  const autoSaveEnabled = ref(true)
  const autoSaveInterval = ref(30000)
  let autoSaveTimer: ReturnType<typeof setInterval> | null = null
  let pendingSave = false

  const nodeCount = computed(() => nodes.value.length)
  const connectionCount = computed(() => connections.value.length)

  function initializeEditor(editorInstance: Editor) {
    editor.value = editorInstance

    editorInstance.graph.events.addNode.subscribe(editorInstance, () => {
      syncNodes()
      isDirty.value = true
      captureUndoState('Add Node')
      scheduleAutoSave()
    })

    editorInstance.graph.events.removeNode.subscribe(editorInstance, () => {
      syncNodes()
      isDirty.value = true
      captureUndoState('Remove Node')
      scheduleAutoSave()
    })

    editorInstance.graph.events.addConnection.subscribe(editorInstance, () => {
      syncConnections()
      isDirty.value = true
      captureUndoState('Add Connection')
      scheduleAutoSave()
    })

    editorInstance.graph.events.removeConnection.subscribe(editorInstance, () => {
      syncConnections()
      isDirty.value = true
      captureUndoState('Remove Connection')
      scheduleAutoSave()
    })

    startAutoSaveTimer()
  }

  function captureUndoState(description: string) {
    const undoRedoStore = useUndoRedoStore()
    const serialized = serializeGraph()
    if (serialized) {
      undoRedoStore.pushState(serialized, description)
    }
  }

  function startAutoSaveTimer() {
    if (autoSaveTimer !== null) return

    autoSaveTimer = setInterval(() => {
      if (autoSaveEnabled.value && isDirty.value && currentSceneId.value && !pendingSave) {
        performAutoSave()
      }
    }, autoSaveInterval.value)
  }

  function stopAutoSaveTimer() {
    if (autoSaveTimer !== null) {
      clearInterval(autoSaveTimer)
      autoSaveTimer = null
    }
  }

  function scheduleAutoSave() {
    if (!autoSaveEnabled.value || !currentSceneId.value || !isDirty.value) return

    if (autoSaveTimer === null) {
      startAutoSaveTimer()
    }

    pendingSave = false
  }

  async function performAutoSave() {
    if (!currentSceneId.value || !editor.value || pendingSave) return

    pendingSave = true

    try {
      const graphData = serializeGraph()
      if (!graphData) return

      await sceneGraphApi.put(currentSceneId.value, {
        id: currentSceneId.value,
        nodes: graphData.nodes.map(node => ({
          id: node.id,
          nodeType: node.type,
          subType: extractSubTypeFromNode(editor.value as Editor, node.id),
          position: node.position,
          properties: extractPropertiesFromNode(editor.value as Editor, node.id)
        })),
        connections: graphData.connections
      })

      isDirty.value = false
      console.log('[NodeGraphStore] Auto-save completed:', currentSceneId.value)
    } catch (error) {
      // 异常可能来源：网络请求失败、后端服务不可用、自动保存期间数据被修改
      console.warn('[NodeGraphStore] Auto-save failed, will retry on next interval:', error)
    } finally {
      pendingSave = false
    }
  }

  function extractSubTypeFromNode(editorInstance: Editor, nodeId: string): string | undefined {
    const node = editorInstance.graph.nodes.find(n => n.id === nodeId)
    if (!node?.inputs?.subType) return undefined

    const subTypeInput = (node.inputs as any).subType
    return subTypeInput?.value ?? undefined
  }

  function extractPropertiesFromNode(editorInstance: Editor, nodeId: string): Record<string, any> {
    const node = editorInstance.graph.nodes.find(n => n.id === nodeId)
    if (!node?.inputs) return {}

    const properties: Record<string, any> = {}

    Object.entries(node.inputs as Record<string, any>).forEach(([key, iface]) => {
      // 跳过执行端口和子类型选择器，只提取动态属性值
      if (key === 'exec_in' || key === 'subType') return

      if (iface?.value !== undefined) {
        properties[key] = iface.value
      }
    })

    return properties
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
    isDirty.value = true
    captureUndoState('Update Property')
    scheduleAutoSave()
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

    const undoRedoStore = useUndoRedoStore()
    undoRedoStore.setRecording(false)

    try {
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
          // 异常可能来源：连接端口不存在、节点已被删除、数据格式不匹配
          console.error('[NodeGraphStore] Failed to restore connection:', e)
        }
      })

      syncNodes()
      syncConnections()
      isDirty.value = false
    } catch (error) {
      // 异常可能来源：反序列化过程中节点创建失败、图结构损坏
      console.error('[NodeGraphStore] Failed to deserialize graph:', error)
    } finally {
      undoRedoStore.setRecording(true)
    }
  }

  function markClean() {
    isDirty.value = false
  }

  function setAutoSave(enabled: boolean, intervalMs?: number) {
    autoSaveEnabled.value = enabled
    if (intervalMs !== undefined) {
      autoSaveInterval.value = intervalMs
    }

    if (enabled) {
      startAutoSaveTimer()
    } else {
      stopAutoSaveTimer()
    }
  }

  function cleanup() {
    stopAutoSaveTimer()
  }

  return {
    editor,
    nodes,
    connections,
    currentSceneId,
    isDirty,
    autoSaveEnabled,
    autoSaveInterval,

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
    markClean,
    setAutoSave,
    cleanup
  }
})
