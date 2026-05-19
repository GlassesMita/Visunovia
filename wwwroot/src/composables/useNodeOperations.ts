import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { sceneGraphApi } from '@/api'

export function useNodeOperations() {
  const nodeGraphStore = useNodeGraphStore()
  const editorStore = useEditorStore()
  
  async function saveGraph() {
    const editor = (window as any).__editor
    
    if (!editor) {
      console.error('Editor instance not found')
      return
    }
    
    const data = {
      nodes: editor.graph.nodes.map((n: any) => ({
        id: n.id,
        type: n.type,
        position: n.position,
        data: n.data
      })),
      edges: editor.graph.edges.map((e: any) => ({
        id: e.id,
        source: e.source,
        target: e.target,
        sourceOutput: e.sourceOutput,
        targetInput: e.targetInput
      }))
    }
    
    if (editorStore.currentFileName) {
      try {
        await sceneGraphApi.save(editorStore.currentFileName, data)
        nodeGraphStore.markClean()
        console.log('[NodeOperations] Graph saved successfully')
      } catch (error) {
        console.error('[NodeOperations] Failed to save graph:', error)
      }
    } else {
      console.warn('[NodeOperations] No file is currently open')
    }
  }
  
  async function loadGraph(sceneId: string) {
    const editor = (window as any).__editor
    
    if (!editor) {
      console.error('Editor instance not found')
      return
    }
    
    try {
      const response = await sceneGraphApi.get(sceneId)
      const data = response.data.data
      
      editor.graph.clear()
      
      for (const node of data.nodes) {
        editor.graph.addNode(node)
      }
      
      for (const edge of data.edges) {
        editor.graph.addConnection(
          edge.sourceOutput,
          edge.targetInput,
          edge.source,
          edge.target
        )
      }
      
      nodeGraphStore.markClean()
      console.log('[NodeOperations] Graph loaded successfully')
    } catch (error) {
      console.error('[NodeOperations] Failed to load graph:', error)
    }
  }
  
  function newGraph() {
    const editor = (window as any).__editor
    
    if (!editor) {
      console.error('Editor instance not found')
      return
    }
    
    editor.graph.clear()
    editor.graph.addNode({
      type: 'StartNode',
      position: { x: 300, y: 200 }
    })
    
    editorStore.currentFileName = 'Untitled'
    nodeGraphStore.markClean()
    console.log('[NodeOperations] New graph created')
  }
  
  return {
    saveGraph,
    loadGraph,
    newGraph
  }
}
