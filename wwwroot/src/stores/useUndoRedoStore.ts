import { defineStore } from 'pinia'
import { ref } from 'vue'

interface HistoryState {
  nodes: any[]
  edges: any[]
  timestamp: number
}

export const useUndoRedoStore = defineStore('undoRedo', () => {
  const undoStack = ref<HistoryState[]>([])
  const redoStack = ref<HistoryState[]>([])
  const maxHistorySize = ref(50)

  function pushState(state: { nodes: any[], edges: any[] }) {
    undoStack.value.push({
      ...state,
      timestamp: Date.now()
    })
    if (undoStack.value.length > maxHistorySize.value) {
      undoStack.value.shift()
    }
    redoStack.value = []
  }

  function undo(): HistoryState | null {
    if (undoStack.value.length === 0) return null
    const state = undoStack.value.pop()!
    redoStack.value.push(state)
    return undoStack.value[undoStack.value.length - 1] || null
  }

  function redo(): HistoryState | null {
    if (redoStack.value.length === 0) return null
    const state = redoStack.value.pop()!
    undoStack.value.push(state)
    return state
  }

  function canUndo(): boolean {
    return undoStack.value.length > 1
  }

  function canRedo(): boolean {
    return redoStack.value.length > 0
  }

  function clearHistory() {
    undoStack.value = []
    redoStack.value = []
  }

  return {
    undoStack,
    redoStack,
    maxHistorySize,
    pushState,
    undo,
    redo,
    canUndo,
    canRedo,
    clearHistory
  }
})
