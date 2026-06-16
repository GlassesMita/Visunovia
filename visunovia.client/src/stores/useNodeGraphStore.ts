import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { Editor } from '@baklavajs/core'
import { sceneGraphApi } from '@/api'
import { useUndoRedoStore } from './useUndoRedoStore'
import { normalizeAssetProperties } from '@/utils/assetPaths'

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
  if (name === 'exec_out') return interfaces?.execOut ? 'execOut' : 'execOut'
  if (name === 'exec_in') return interfaces?.execIn ? 'execIn' : 'execIn'
  if (name === 'controlOut') return interfaces?.execOut ? 'execOut' : 'controlOut'

  const characterControlMatch = name.match(/(?:◆|characterControl)\s*(\d+)/i)
  if (characterControlMatch) {
    const key = `characterControl${characterControlMatch[1]}`
    if (!interfaces || interfaces[key]) return key
  }

  return name
}

function resolveConnectionEndpoint(editorInstance: Editor, endpoint: any, direction: 'input' | 'output') {
  const collectionKey = direction === 'input' ? 'inputs' : 'outputs'
  const nodeByEndpointId = editorInstance.graph.nodes.find((node) => node.id === endpoint?.nodeId) as any
  if (nodeByEndpointId) {
    const portKey = findInterfaceKey(nodeByEndpointId[collectionKey], endpoint, endpoint?.name || endpoint?.port)
    return { nodeId: nodeByEndpointId.id, portKey }
  }

  for (const node of editorInstance.graph.nodes as any[]) {
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
    expression: String(control?.expression ?? 'default').trim() || 'default',
    fromPosition: String(control?.fromPosition ?? '').trim(),
    toPosition: String(control?.toPosition ?? 'none').trim() || 'none',
    position: String(control?.position ?? 'center').trim() || 'center',
    animation: String(control?.animation ?? 'fade').trim() || 'fade',
    easing: String(control?.easing ?? 'easeOutCubic').trim() || 'easeOutCubic',
    duration: Number(control?.duration ?? 0.3),
    expressionBalloon: String(control?.expressionBalloon ?? '').trim(),
    expressionIcon: String(control?.expressionIcon ?? '').trim(),
    expressionPreset: String(control?.expressionPreset ?? '').trim(),
    expressionCorner: String(control?.expressionCorner ?? 'top-right').trim() || 'top-right',
    expressionDuration: Number(control?.expressionDuration ?? 2),
  }
}

function getCharacterControlsFromNode(node: any, fallbackSlot = '1') {
  const rawControls = parseCharacterControls(getInterfaceValue(node, 'characterControlsJson') ?? node?.data?.characterControlsJson ?? node?.data?.characterControls)
  if (rawControls.length > 0) {
    return rawControls
      .map((control, index) => normalizeCharacterControl(control, String(index + 1)))
      .filter(control => control.action !== 'none')
  }

  const selectedSlot = String(getInterfaceValue(node, 'slot') ?? fallbackSlot).trim() || fallbackSlot
  const legacyAction = String(getInterfaceValue(node, 'action') ?? '').trim()
  const legacyCharacter = getInterfaceValue(node, 'character')
  const legacySprite = getInterfaceValue(node, 'sprite')
  if (!legacyAction && !legacyCharacter && !legacySprite) return []

  return [normalizeCharacterControl({
    slot: selectedSlot,
    character: legacyCharacter,
    unmanagedCharacter: getInterfaceValue(node, 'unmanagedCharacter'),
    action: legacyAction || 'show',
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
          uuid: node.id,
          id: node.id,
          nodeType: node.type,
          subType: extractSubTypeFromNode(editor.value as Editor, node.id),
          position: node.position,
                properties: normalizeAssetProperties(extractPropertiesFromNode(editor.value as Editor, node.id))
        })),
        connections: graphData.connections.map(connection => ({
          uuid: connection.id,
          id: connection.id,
          sourceNodeUuid: connection.from.nodeId,
          source: connection.from.nodeId,
          sourcePort: connection.from.port,
          targetNodeUuid: connection.to.nodeId,
          target: connection.to.nodeId,
          targetPort: connection.to.port,
        }))
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
      if (key === 'exec_in' || key === 'execIn' || key.startsWith('characterControl') || key === 'subType') return

      if (iface?.value !== undefined) {
        properties[key] = iface.value
      }
    })

    if (normalizeNodeType(extractNodeType(node)) === 'CharacterControlNode') {
      const controls = getCharacterControlsFromNode(node)
      properties.characterControls = controls
      properties.characterControlsJson = JSON.stringify(controls)
    }

    if (normalizeNodeType(extractNodeType(node)) === 'DialogueNode') {
      const characterControls = buildCharacterControlsForDialogue(editorInstance, node)
      if (characterControls.length > 0) {
        properties.characterControls = characterControls
      }
    }

    return properties
  }

  function buildCharacterControlsForDialogue(editorInstance: Editor, dialogueNode: any) {
    return editorInstance.graph.connections
      .map((connection) => {
        if (connection.to.nodeId !== dialogueNode.id) return null

        const controlNode = editorInstance.graph.nodes.find((node) => node.id === connection.from.nodeId) as any
        if (!controlNode || normalizeNodeType(extractNodeType(controlNode)) !== 'CharacterControlNode') return null

        const sourcePort = normalizePortName(String(connection.from?.name || connection.from?.port || ''))
        const targetPort = findInterfaceKey(dialogueNode.inputs, connection.to, connection.to.name)
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

  function syncNodes() {
    if (!editor.value) return

    nodes.value = editor.value.graph.nodes.map(node => ({
      id: node.id,
      type: node.type,
      subType: extractSubTypeFromNode(editor.value as Editor, node.id),
      position: { x: node.position.x, y: node.position.y },
      data: {
        ...normalizeAssetProperties(extractPropertiesFromNode(editor.value as Editor, node.id)),
        subType: extractSubTypeFromNode(editor.value as Editor, node.id),
      }
    } as any))
  }

  function syncConnections() {
    if (!editor.value) return

    connections.value = editor.value.graph.connections.map(conn => {
      const source = resolveConnectionEndpoint(editor.value as Editor, conn.from, 'output')
      const target = resolveConnectionEndpoint(editor.value as Editor, conn.to, 'input')

      return {
        id: `${source.nodeId}:${source.portKey}->${target.nodeId}:${target.portKey}`,
        from: {
          nodeId: source.nodeId,
          port: source.portKey
        },
        to: {
          nodeId: target.nodeId,
          port: target.portKey
        }
      }
    })
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

  function alignNodesToGrid(gridSize = 40) {
    if (!editor.value) return 0

    const normalizedGridSize = Math.max(1, Number(gridSize) || 40)
    const graph = editor.value.graph as any
    const selectedNodes = Array.isArray(graph.selectedNodes) && graph.selectedNodes.length > 0
      ? graph.selectedNodes
      : graph.nodes

    const targetNodes = selectedNodes.filter((node: any) => node?.position)
    if (targetNodes.length === 0) return 0

    const movedNodes = targetNodes.filter((node: any) => {
      const x = Math.round(Number(node.position.x || 0) / normalizedGridSize) * normalizedGridSize
      const y = Math.round(Number(node.position.y || 0) / normalizedGridSize) * normalizedGridSize
      return x !== node.position.x || y !== node.position.y
    })

    if (movedNodes.length === 0) return 0

    captureUndoState(movedNodes.length > 1 ? 'Align Nodes to Grid' : 'Align Node to Grid')

    movedNodes.forEach((node: any) => {
      node.position = {
        x: Math.round(Number(node.position.x || 0) / normalizedGridSize) * normalizedGridSize,
        y: Math.round(Number(node.position.y || 0) / normalizedGridSize) * normalizedGridSize,
      }
    })

    syncNodes()
    isDirty.value = true
    scheduleAutoSave()
    return movedNodes.length
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

          Object.entries(nodeData.data || {}).forEach(([key, value]) => {
            const iface = (node.inputs as any)?.[key]
            if (!iface || value === undefined) return

            if (typeof iface.setValue === 'function') {
              iface.setValue(value)
            } else {
              iface.value = value
            }
          })

          ;(node as any).data = {
            ...(node as any).data || {},
            ...(nodeData.data || {}),
          }
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
    alignNodesToGrid,
    serializeGraph,
    deserializeGraph,
    markClean,
    setAutoSave,
    cleanup
  }
})
