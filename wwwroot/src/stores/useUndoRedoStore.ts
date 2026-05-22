import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { GraphNode, GraphConnection } from './useNodeGraphStore'
import { useNodeGraphStore } from './useNodeGraphStore'

export interface HistoryState {
  nodes: GraphNode[]
  connections: GraphConnection[]
  timestamp: number
  description?: string
}

const MAX_HISTORY = 50

export const useUndoRedoStore = defineStore('undoRedo', () => {
  const undoStack = ref<HistoryState[]>([])
  const redoStack = ref<HistoryState[]>([])
  const isRecording = ref(true)

  const canUndo = computed(() => undoStack.value.length > 1)
  const canRedo = computed(() => redoStack.value.length > 0)

  function pushState(state: { nodes: GraphNode[]; connections: GraphConnection[] }, description?: string) {
    if (!isRecording.value) return

    const historyState: HistoryState = {
      nodes: JSON.parse(JSON.stringify(state.nodes)),
      connections: JSON.parse(JSON.stringify(state.connections)),
      timestamp: Date.now(),
      description
    }

    undoStack.value.push(historyState)

    if (undoStack.value.length > MAX_HISTORY) {
      undoStack.value.shift()
    }

    redoStack.value = []
  }

  function undo(): HistoryState | null {
    if (undoStack.value.length <= 1) return null

    const currentState = undoStack.value.pop()!
    redoStack.value.push(currentState)

    return currentState
  }

  function redo(): HistoryState | null {
    if (redoStack.value.length === 0) return null

    const state = redoStack.value.pop()!
    undoStack.value.push(state)

    return state
  }

  function clear() {
    undoStack.value = []
    redoStack.value = []
  }

  function clearHistory() {
    clear()
  }

  function captureSnapshot(description?: string) {
    const nodeGraphStore = useNodeGraphStore()
    pushState(
      { nodes: nodeGraphStore.nodes, connections: nodeGraphStore.connections },
      description
    )
  }

  function initializeWithState(state: { nodes: GraphNode[]; connections: GraphConnection[] }) {
    clear()
    const initialState: HistoryState = {
      nodes: JSON.parse(JSON.stringify(state.nodes)),
      connections: JSON.parse(JSON.stringify(state.connections)),
      timestamp: Date.now(),
      description: 'Initial State'
    }
    undoStack.value.push(initialState)
  }

  function setRecording(enabled: boolean) {
    isRecording.value = enabled
  }

  function getUndoDescription(): string | undefined {
    if (undoStack.value.length <= 1) return undefined
    return undoStack.value[undoStack.value.length - 2]?.description
  }

  function getRedoDescription(): string | undefined {
    if (redoStack.value.length === 0) return undefined
    return redoStack.value[redoStack.value.length - 1]?.description
  }

  return {
    undoStack,
    redoStack,
    isRecording,
    canUndo,
    canRedo,
    pushState,
    undo,
    redo,
    clear,
    clearHistory,
    captureSnapshot,
    initializeWithState,
    setRecording,
    getUndoDescription,
    getRedoDescription
  }
})
