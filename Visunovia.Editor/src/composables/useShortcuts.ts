import { onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'

export interface ShortcutAction {
  action: string
  handler: () => void
  description: string
  preventDefault?: boolean
}

interface ShortcutConfig {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  action: ShortcutAction
}

let clipboardNodes: any[] = []

export function useShortcuts() {
  const router = useRouter()
  const editorStore = useEditorStore()
  const nodeGraphStore = useNodeGraphStore()
  const undoRedoStore = useUndoRedoStore()

  const shortcuts: ShortcutConfig[] = [
    {
      key: 's',
      ctrl: true,
      action: {
        action: 'save',
        handler: () => editorStore.save(),
        description: 'Save',
        preventDefault: true
      }
    },
    {
      key: 'z',
      ctrl: true,
      shift: false,
      action: {
        action: 'undo',
        handler: handleUndo,
        description: 'Undo',
        preventDefault: true
      }
    },
    {
      key: 'y',
      ctrl: true,
      action: {
        action: 'redo',
        handler: handleRedo,
        description: 'Redo',
        preventDefault: true
      }
    },
    {
      key: 'z',
      ctrl: true,
      shift: true,
      action: {
        action: 'redo',
        handler: handleRedo,
        description: 'Redo (Alternative)',
        preventDefault: true
      }
    },
    {
      key: 'c',
      ctrl: true,
      action: {
        action: 'copy',
        handler: copySelectedNodes,
        description: 'Copy Selected Nodes',
        preventDefault: false
      }
    },
    {
      key: 'v',
      ctrl: true,
      action: {
        action: 'paste',
        handler: pasteNodes,
        description: 'Paste Nodes',
        preventDefault: false
      }
    },
    {
      key: 'x',
      ctrl: true,
      action: {
        action: 'cut',
        handler: cutSelectedNodes,
        description: 'Cut Selected Nodes',
        preventDefault: true
      }
    },
    {
      key: 'Delete',
      action: {
        action: 'delete',
        handler: deleteSelectedNodes,
        description: 'Delete Selected Nodes',
        preventDefault: true
      }
    },
    {
      key: 'Backspace',
      action: {
        action: 'delete',
        handler: deleteSelectedNodes,
        description: 'Delete Selected Nodes (Backspace)',
        preventDefault: true
      }
    },
    {
      key: 'a',
      ctrl: true,
      action: {
        action: 'selectAll',
        handler: selectAllNodes,
        description: 'Select All Nodes',
        preventDefault: true
      }
    },
    {
      key: 'n',
      ctrl: true,
      action: {
        action: 'new',
        handler: handleNew,
        description: 'New Project',
        preventDefault: true
      }
    },
    {
      key: 'Escape',
      action: {
        action: 'deselect',
        handler: () => editorStore.selectNode(null),
        description: 'Deselect Node',
        preventDefault: false
      }
    },
    {
      key: ',',
      ctrl: true,
      action: {
        action: 'preferences',
        handler: () => router.push('/preferences'),
        description: 'Open Preferences',
        preventDefault: true
      }
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

  function copySelectedNodes() {
    if (!editorStore.selectedNodeId) return

    const selectedNode = nodeGraphStore.nodes.find(n => n.id === editorStore.selectedNodeId)
    if (selectedNode) {
      clipboardNodes = [JSON.parse(JSON.stringify(selectedNode))]
      console.log('[Shortcuts] Copied node:', selectedNode.id)
    }
  }

  function cutSelectedNodes() {
    if (!editorStore.selectedNodeId) return

    copySelectedNodes()
    deleteSelectedNodes()
  }

  function pasteNodes() {
    if (clipboardNodes.length === 0) return

    undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, 'Paste Node')

    clipboardNodes.forEach((nodeData, index) => {
      const newNode = nodeGraphStore.addNode(nodeData.type, {
        x: nodeData.position.x + 30 * (index + 1),
        y: nodeData.position.y + 30 * (index + 1)
      })

      if (newNode && editorStore.selectedNodeId) {
        editorStore.selectNode(newNode.id)
      }
    })
  }

  function deleteSelectedNodes() {
    if (isInInputElement()) return

    const graph = getBaklavaGraph()
    const selectedNodes = getSelectedBaklavaNodes()

    if (graph && selectedNodes.length > 0) {
      undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, selectedNodes.length > 1 ? 'Delete Nodes' : 'Delete Node')

      for (const node of selectedNodes) {
        if (graph.nodes?.includes(node)) {
          graph.removeNode(node)
        }
      }

      graph.selectedNodes = []
      editorStore.selectNode(null)
      nodeGraphStore.isDirty = true
      return
    }

    if (!editorStore.selectedNodeId) return

    undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, 'Delete Node')
    nodeGraphStore.removeNode(editorStore.selectedNodeId)
    editorStore.selectNode(null)
  }

  function selectAllNodes() {
    const graph = getBaklavaGraph()
    if (graph?.nodes && graph?.selectedNodes) {
      graph.selectedNodes = [...graph.nodes]
      return
    }

    if (nodeGraphStore.nodes.length > 0) {
      editorStore.selectNode(nodeGraphStore.nodes[0].id)
    }
  }

  function getBaklavaGraph(): any | null {
    return (window as any).__baklavaViewModel?.displayedGraph || (window as any).__editor?.graph || null
  }

  function getSelectedBaklavaNodes(): any[] {
    const graph = getBaklavaGraph()
    if (!graph?.selectedNodes) return []
    return [...graph.selectedNodes]
  }

  function handleNew() {
    if (confirm('Create new project? Unsaved changes will be lost.')) {
      nodeGraphStore.deserializeGraph({ nodes: [], connections: [] })
      undoRedoStore.clear()
      editorStore.selectNode(null)
    }
  }

  function isInInputElement(): boolean {
    const activeElement = document.activeElement
    if (!activeElement) return false

    const tagName = activeElement.tagName
    const isInput = tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT'
    const isContentEditable = (activeElement as HTMLElement).isContentEditable

    return isInput || isContentEditable
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
    if ((event.ctrlKey || event.metaKey) && ['c', 'x'].includes(event.key.toLowerCase()) && !window.getSelection()?.isCollapsed) return

    for (const shortcut of shortcuts) {
      if (matchesShortcut(event, shortcut)) {
        if (shortcut.action.preventDefault) {
          event.preventDefault()
        }
        event.stopPropagation()

        try {
          shortcut.action.handler()
        } catch (error) {
          console.error(`[Shortcuts] Error executing ${shortcut.action.action}:`, error)
        }

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
    shortcuts,
    copySelectedNodes,
    pasteNodes,
    cutSelectedNodes,
    deleteSelectedNodes
  }
}
