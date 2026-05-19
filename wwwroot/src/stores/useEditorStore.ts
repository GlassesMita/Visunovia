import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { useNodeGraphStore } from './useNodeGraphStore'
import { sceneGraphApi } from '@/api'

export const useEditorStore = defineStore('editor', () => {
  const nodeGraphStore = useNodeGraphStore()
  
  const currentFileName = ref<string>('Untitled')
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const selectedNodeId = ref<string | null>(null)
  
  const undoStack = ref<any[]>([])
  const redoStack = ref<any[]>([])
  const maxHistorySize = 50

  const selectedNode = computed(() => {
    if (!selectedNodeId.value) return null
    return nodeGraphStore.nodes.find(n => n.id === selectedNodeId.value) || null
  })
  
  const canUndo = computed(() => undoStack.value.length > 0)
  const canRedo = computed(() => redoStack.value.length > 0)
  
  const isModified = computed(() => nodeGraphStore.isDirty)

  function selectNode(nodeId: string | null) {
    selectedNodeId.value = nodeId
  }

  function saveState() {
    const state = nodeGraphStore.serializeGraph()
    if (!state) return
    
    undoStack.value.push(JSON.stringify(state))
    if (undoStack.value.length > maxHistorySize) {
      undoStack.value.shift()
    }
    
    redoStack.value = []
  }

  function undo() {
    if (undoStack.value.length === 0) return
    
    const currentState = nodeGraphStore.serializeGraph()
    if (currentState) {
      redoStack.value.push(JSON.stringify(currentState))
    }
    
    const previousState = undoStack.value.pop()
    if (previousState) {
      nodeGraphStore.deserializeGraph(JSON.parse(previousState))
    }
  }

  function redo() {
    if (redoStack.value.length === 0) return
    
    const currentState = nodeGraphStore.serializeGraph()
    if (currentState) {
      undoStack.value.push(JSON.stringify(currentState))
    }
    
    const nextState = redoStack.value.pop()
    if (nextState) {
      nodeGraphStore.deserializeGraph(JSON.parse(nextState))
    }
  }

  async function save() {
    if (!nodeGraphStore.currentSceneId) {
      console.error('No scene ID to save')
      return
    }
    
    isLoading.value = true
    error.value = null
    
    try {
      const graphData = nodeGraphStore.serializeGraph()
      await sceneGraphApi.save(nodeGraphStore.currentSceneId, {
        nodes: graphData?.nodes || [],
        connections: graphData?.connections || []
      })
      
      nodeGraphStore.markClean()
    } catch (err: any) {
      error.value = err.message || 'Failed to save'
      console.error('Save error:', err)
    } finally {
      isLoading.value = false
    }
  }

  async function load(sceneId: string) {
    isLoading.value = true
    error.value = null
    
    try {
      const response = await sceneGraphApi.get(sceneId)
      nodeGraphStore.currentSceneId = sceneId
      nodeGraphStore.deserializeGraph(response.data)
      
      undoStack.value = []
      redoStack.value = []
    } catch (err: any) {
      error.value = err.message || 'Failed to load'
      console.error('Load error:', err)
    } finally {
      isLoading.value = false
    }
  }

  return {
    currentFileName,
    isLoading,
    error,
    selectedNodeId,
    
    selectedNode,
    canUndo,
    canRedo,
    isModified,
    
    selectNode,
    saveState,
    undo,
    redo,
    save,
    load
  }
})
