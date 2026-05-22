import { onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'

interface ShortcutConfig {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  handler: () => void
  description: string
}

export function useKeyboardShortcuts() {
  const router = useRouter()
  const editorStore = useEditorStore()
  const nodeGraphStore = useNodeGraphStore()
  const undoRedoStore = useUndoRedoStore()

  const shortcuts: ShortcutConfig[] = [
    {
      key: 'z',
      ctrl: true,
      handler: () => {
        handleUndo()
      },
      description: 'Undo'
    },
    {
      key: 'y',
      ctrl: true,
      handler: () => {
        handleRedo()
      },
      description: 'Redo'
    },
    {
      key: 'z',
      ctrl: true,
      shift: true,
      handler: () => {
        handleRedo()
      },
      description: 'Redo (Alternative)'
    },
    {
      key: 's',
      ctrl: true,
      handler: () => {
        editorStore.save()
      },
      description: 'Save'
    },
    {
      key: 'n',
      ctrl: true,
      handler: () => {
        if (confirm('Create new project? Unsaved changes will be lost.')) {
          nodeGraphStore.deserializeGraph({ nodes: [], connections: [] })
          undoRedoStore.clear()
          editorStore.selectNode(null)
        }
      },
      description: 'New Project'
    },
    {
      key: 'Delete',
      handler: () => {
        if (editorStore.selectedNodeId) {
          undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, 'Delete Node')
          nodeGraphStore.removeNode(editorStore.selectedNodeId)
          editorStore.selectNode(null)
        }
      },
      description: 'Delete Selected'
    },
    {
      key: 'Backspace',
      handler: () => {
        if (editorStore.selectedNodeId && !isInInputElement()) {
          undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, 'Delete Node')
          nodeGraphStore.removeNode(editorStore.selectedNodeId)
          editorStore.selectNode(null)
        }
      },
      description: 'Delete Selected (Backspace)'
    },
    {
      key: 'Escape',
      handler: () => {
        editorStore.selectNode(null)
      },
      description: 'Deselect'
    },
    {
      key: ',',
      ctrl: true,
      handler: () => {
        router.push('/preferences')
      },
      description: 'Open Preferences'
    },
    {
      key: '=',
      ctrl: true,
      shift: false,
      handler: () => {
        console.log('Zoom in')
      },
      description: 'Zoom In'
    },
    {
      key: '-',
      ctrl: true,
      handler: () => {
        console.log('Zoom out')
      },
      description: 'Zoom Out'
    },
    {
      key: '0',
      ctrl: true,
      handler: () => {
        console.log('Reset zoom')
      },
      description: 'Reset Zoom'
    }
  ]

  function handleUndo() {
    if (!undoRedoStore.canUndo) return

    const state = undoRedoStore.undo()
    if (state) {
      nodeGraphStore.deserializeGraph({
        nodes: state.nodes,
        connections: state.connections
      })
    }
  }

  function handleRedo() {
    if (!undoRedoStore.canRedo) return

    const state = undoRedoStore.redo()
    if (state) {
      nodeGraphStore.deserializeGraph({
        nodes: state.nodes,
        connections: state.connections
      })
    }
  }

  function isInInputElement(): boolean {
    const activeElement = document.activeElement
    if (!activeElement) return false

    const tagName = activeElement.tagName
    return (
      tagName === 'INPUT' ||
      tagName === 'TEXTAREA' ||
      tagName === 'SELECT' ||
      (activeElement as HTMLElement).isContentEditable
    )
  }

  function matchesShortcut(event: KeyboardEvent, shortcut: ShortcutConfig): boolean {
    if (event.key.toLowerCase() !== shortcut.key.toLowerCase()) return false
    if (shortcut.ctrl !== undefined && event.ctrlKey !== shortcut.ctrl) return false
    if (shortcut.shift !== undefined && event.shiftKey !== shortcut.shift) return false
    if (shortcut.alt !== undefined && event.altKey !== shortcut.alt) return false

    return true
  }

  function handleKeyDown(event: KeyboardEvent) {
    if (isInInputElement()) return

    for (const shortcut of shortcuts) {
      if (matchesShortcut(event, shortcut)) {
        event.preventDefault()
        event.stopPropagation()
        shortcut.handler()
        return
      }
    }
  }

  onMounted(() => {
    window.addEventListener('keydown', handleKeyDown, true)
  })

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown, true)
  })

  return {
    shortcuts
  }
}
