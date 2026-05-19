import { onMounted, onUnmounted } from 'vue'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'

export function useKeyboardShortcuts() {
  const editorStore = useEditorStore()
  const nodeGraphStore = useNodeGraphStore()

  function handleKeyDown(event: KeyboardEvent) {
    if (isInInputElement(event)) return
    
    const ctrl = event.ctrlKey || event.metaKey
    
    if (ctrl && event.key === 's') {
      event.preventDefault()
      editorStore.save()
      return
    }
    
    if (ctrl && event.key === 'z' && !event.shiftKey) {
      event.preventDefault()
      editorStore.undo()
      return
    }
    
    if (ctrl && event.key === 'y') {
      event.preventDefault()
      editorStore.redo()
      return
    }
    
    if (ctrl && event.shiftKey && event.key === 'z') {
      event.preventDefault()
      editorStore.redo()
      return
    }
    
    if (event.key === 'Delete') {
      if (editorStore.selectedNodeId) {
        nodeGraphStore.removeNode(editorStore.selectedNodeId)
        editorStore.selectNode(null)
        editorStore.saveState()
      }
      return
    }
    
    if (event.key === 'Escape') {
      editorStore.selectNode(null)
      return
    }
  }

  function isInInputElement(event: KeyboardEvent): boolean {
    const target = event.target as HTMLElement
    return (
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.tagName === 'SELECT' ||
      target.isContentEditable
    )
  }

  onMounted(() => {
    window.addEventListener('keydown', handleKeyDown)
  })

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown)
  })
}
