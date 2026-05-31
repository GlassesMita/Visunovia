import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { NODE_DEFAULT_DATA } from '@/components/node-graph/nodeRegistries'

export function useNodeOperations() {
  const selectedNodeId = useEditorStore((s) => s.selectedNodeId)

  function addNodeToCenter(type: string, position?: { x: number; y: number }): void {
    const ctx = useNodeGraphStore.getState().flowGramContext
    if (!ctx) return
    const pos = position || { x: 400, y: 300 }
    const defaultData = NODE_DEFAULT_DATA[type] ?? {}
    ctx.document.createWorkflowNode({
      id: crypto.randomUUID(),
      type,
      meta: { position: pos },
      data: { ...defaultData },
    })
  }

  function deleteSelectedNode(): void {
    const ctx = useNodeGraphStore.getState().flowGramContext
    if (!ctx || !selectedNodeId) return
    const node = ctx.document.getAllNodes().find((n) => n.id === selectedNodeId)
    if (node) {
      node.dispose()
    }
  }

  return { addNodeToCenter, deleteSelectedNode }
}
