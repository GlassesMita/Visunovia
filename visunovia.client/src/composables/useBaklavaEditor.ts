import { ref, shallowRef, onUnmounted } from 'vue'
import { Editor } from '@baklavajs/core'
import { createBaklavaEditor } from '@/baklava'
import { registerAllNodes } from '@/baklava/nodeRegistry'
import StartNode from '@/components/baklava-nodes/StartNode'

export function useBaklavaEditor() {
  const editor = shallowRef<Editor | null>(null)
  const isInitialized = ref(false)
  
  function initialize() {
    if (editor.value) return
    
    editor.value = createBaklavaEditor()
    registerAllNodes(editor.value)
    
    // StartNode 唯一性检查：每个场景只能有一个 Start 节点
    // 使用 beforeAddNode 可在节点添加前阻止操作，避免先添加再移除的开销
    editor.value.graph.events.beforeAddNode.subscribe(editor.value, (node, prevent) => {
      if (node.type === 'StartNode') {
        const hasStartNode = editor.value!.graph.nodes.some(
          (n) => n.type === 'StartNode'
        )
        if (hasStartNode) {
          prevent()
          const message = '每个场景只能有一个 Start 节点'
          console.warn('[useBaklavaEditor]', message)
          alert(message)
        }
      }
    })

    // 使用 graph.events 来监听节点添加和移除
    editor.value.graph.events.addNode.subscribe(editor.value, () => {
      // Node added, sync will happen automatically
    })

    editor.value.graph.events.removeNode.subscribe(editor.value, () => {
      // Node removed, sync will happen automatically
    })
    
    if (editor.value.graph.nodes.length === 0) {
      const startNode = new StartNode()
      startNode.position = { x: 300, y: 200 }
      editor.value.graph.addNode(startNode)
    }
    
    isInitialized.value = true
  }
  
  function destroy() {
    editor.value = null
    isInitialized.value = false
  }
  
  onUnmounted(() => {
    destroy()
  })
  
  return {
    editor,
    isInitialized,
    initialize,
    destroy
  }
}
