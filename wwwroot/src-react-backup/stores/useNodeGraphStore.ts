import { create } from 'zustand'
import type { VNNode, VNEdge, SceneConfig } from '@/types'
import type { FreeLayoutPluginContext } from '@flowgram.ai/free-layout-editor'

interface FlowGramNode {
  id: string
  type: string | number
  meta?: { position?: { x: number; y: number } }
  data?: Record<string, unknown>
}

interface FlowGramEdge {
  sourceNodeID: string
  targetNodeID: string
  sourcePortID?: string | number
  targetPortID?: string | number
}

export interface FlowGramJSON {
  nodes: FlowGramNode[]
  edges: FlowGramEdge[]
}

interface NodeGraphState {
  nodes: VNNode[]
  edges: VNEdge[]
  sceneConfig: SceneConfig
  currentSceneId: string | null
  isDirty: boolean
  pendingNodeCreation: { type: string; position?: { x: number; y: number } } | null
  flowGramData: FlowGramJSON | null
  flowGramContext: FreeLayoutPluginContext | null
  viewportZoom: number

  syncFromFlowGram: (json: FlowGramJSON) => void
  toFlowGramJSON: () => FlowGramJSON
  addNode: (node: VNNode) => void
  removeNode: (nodeId: string) => void
  updateNode: (nodeId: string, patch: Partial<VNNode>) => void
  addEdge: (edge: VNEdge) => void
  removeEdge: (edgeId: string) => void
  setPendingNodeCreation: (value: { type: string; position?: { x: number; y: number } } | null) => void
  setFlowGramContext: (ctx: FreeLayoutPluginContext | null) => void
  setViewportZoom: (zoom: number) => void
  markClean: () => void
}

export const useNodeGraphStore = create<NodeGraphState>((set, get) => ({
  nodes: [],
  edges: [],
  sceneConfig: {},
  currentSceneId: null,
  isDirty: false,
  pendingNodeCreation: null,
  flowGramData: null,
  flowGramContext: null,
  viewportZoom: 1,

  syncFromFlowGram: (json) => {
    set({
      nodes: json.nodes.map((n): VNNode => ({
        id: n.id,
        type: String(n.type) as VNNode['type'],
        position: n.meta?.position ? { ...n.meta.position } : { x: 0, y: 0 },
        properties: { ...(n.data ?? {}) },
        inputs: [],
        outputs: [],
      })),
      edges: json.edges.map((e, i): VNEdge => ({
        id: `edge-${i}`,
        source: e.sourceNodeID,
        sourcePort: e.sourcePortID != null ? String(e.sourcePortID) : '',
        target: e.targetNodeID,
        targetPort: e.targetPortID != null ? String(e.targetPortID) : '',
        type: 'exec',
      })),
      isDirty: false,
      flowGramData: json,
    })
  },

  toFlowGramJSON: () => {
    const { nodes, edges } = get()
    return {
      nodes: nodes.map((n) => ({
        id: n.id,
        type: n.type,
        meta: { position: { x: n.position.x, y: n.position.y } },
        data: { ...n.properties },
      })),
      edges: edges.map((e) => ({
        sourceNodeID: e.source,
        targetNodeID: e.target,
        ...(e.sourcePort ? { sourcePortID: e.sourcePort } : {}),
        ...(e.targetPort ? { targetPortID: e.targetPort } : {}),
      })),
    }
  },

  addNode: (node) => {
    set((state) => ({ nodes: [...state.nodes, node], isDirty: true }))
  },

  removeNode: (nodeId) => {
    set((state) => ({
      nodes: state.nodes.filter((n) => n.id !== nodeId),
      edges: state.edges.filter((e) => e.source !== nodeId && e.target !== nodeId),
      isDirty: true,
    }))
  },

  updateNode: (nodeId, patch) => {
    set((state) => ({
      nodes: state.nodes.map((n) => (n.id === nodeId ? { ...n, ...patch } : n)),
      isDirty: true,
    }))
  },

  addEdge: (edge) => {
    set((state) => ({ edges: [...state.edges, edge], isDirty: true }))
  },

  removeEdge: (edgeId) => {
    set((state) => ({
      edges: state.edges.filter((e) => e.id !== edgeId),
      isDirty: true,
    }))
  },

  setPendingNodeCreation: (value) => {
    set({ pendingNodeCreation: value })
  },

  setFlowGramContext: (ctx) => {
    set({ flowGramContext: ctx })
  },

  setViewportZoom: (zoom) => {
    set({ viewportZoom: zoom })
  },

  markClean: () => {
    set({ isDirty: false })
  },
}))
