import { create } from 'zustand'

interface EditorState {
  currentFile: string | null
  selectedNodeId: string | null
  selectedEdgeId: string | null
  isDirty: boolean

  setCurrentFile: (path: string | null) => void
  selectNode: (id: string | null) => void
  selectEdge: (id: string | null) => void
  setDirty: (dirty: boolean) => void
}

export const useEditorStore = create<EditorState>((set) => ({
  currentFile: null,
  selectedNodeId: null,
  selectedEdgeId: null,
  isDirty: false,

  setCurrentFile: (path) => set({ currentFile: path }),

  selectNode: (id) =>
    set({ selectedNodeId: id, ...(id !== null ? { selectedEdgeId: null } : {}) }),

  selectEdge: (id) =>
    set({ selectedEdgeId: id, ...(id !== null ? { selectedNodeId: null } : {}) }),

  setDirty: (dirty) => set({ isDirty: dirty }),
}))
