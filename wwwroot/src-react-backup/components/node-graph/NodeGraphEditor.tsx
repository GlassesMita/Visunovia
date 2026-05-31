import { useMemo } from 'react'
import {
  FreeLayoutEditorProvider,
  EditorRenderer,
  FreeLayoutProps,
  FreeLayoutPluginContext,
  WorkflowNodeEntity,
  WorkflowPortEntity,
  WorkflowLineEntity,
} from '@flowgram.ai/free-layout-editor'
import '@flowgram.ai/free-layout-editor/index.css'
import { createMinimapPlugin } from '@flowgram.ai/minimap-plugin'
import { createFreeSnapPlugin } from '@flowgram.ai/free-snap-plugin'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { nodeRegistries, getNodeDefaultRegistry, NODE_DEFAULT_DATA } from './nodeRegistries'
import StartNode from './nodes/StartNode'
import EndNode from './nodes/EndNode'
import DialogueNode from './nodes/DialogueNode'
import BranchNode from './nodes/BranchNode'
import EventNode from './nodes/EventNode'
import LogicNode from './nodes/LogicNode'
import ResourceNode from './nodes/ResourceNode'

let _zoomDisposable: { dispose(): void } | null = null

export default function NodeGraphEditor() {
  const flowGramData = useNodeGraphStore((s) => s.flowGramData)
  const initialData = flowGramData ?? undefined

  const editorProps = useMemo<FreeLayoutProps>(
    () => ({
      nodeRegistries,
      getNodeDefaultRegistry,
      materials: {
        renderNodes: {
          Start: StartNode,
          End: EndNode,
          Dialogue: DialogueNode,
          Branch: BranchNode,
          PlayBGM: EventNode,
          StopBGM: EventNode,
          PlaySFX: EventNode,
          PlayVoice: EventNode,
          ChangeBackground: EventNode,
          ShowCharacter: EventNode,
          HideCharacter: EventNode,
          CameraShake: EventNode,
          FadeScreen: EventNode,
          CustomEvent: EventNode,
          SetVariable: LogicNode,
          Conditional: LogicNode,
          Delay: LogicNode,
          SubGraph: ResourceNode,
        },
      },
      lineColor: {
        hidden: 'transparent',
        default: '#4d53e8',
        drawing: '#5DD6E3',
        hovered: '#37d0ff',
        selected: '#fbbf24',
        error: '#ef4444',
        flowing: '#4d53e8',
      },
      canAddLine(
        _ctx: FreeLayoutPluginContext,
        fromPort: WorkflowPortEntity,
        toPort: WorkflowPortEntity
      ) {
        if (fromPort.node === toPort.node) return false
        if (fromPort.portType === toPort.portType) return false
        return true
      },
      canDeleteLine() {
        return true
      },
      canResetLine() {
        return true
      },
      canDeleteNode() {
        return true
      },
      plugins: () => [
        createMinimapPlugin({}),
        createFreeSnapPlugin({ edgeColor: '#22d3ee', alignColor: '#22d3ee' }),
      ],
      background: true,
      onContentChange(ctx: FreeLayoutPluginContext) {
        const json = ctx.document.toJSON()
        const nodeStore = useNodeGraphStore.getState()
        if (nodeStore.syncFromFlowGram) {
          nodeStore.syncFromFlowGram(json)
        }

        const editorStore = useEditorStore.getState()
        const selectedEntities = ctx.selection.selection
        const selectedNodes = selectedEntities.filter(
          (e): e is WorkflowNodeEntity => 'flowNodeType' in e
        )
        if (selectedNodes.length > 0) {
          editorStore.selectNode(selectedNodes[0].id)
        } else {
          editorStore.selectNode(null)
        }
      },
      onAllLayersRendered(ctx: FreeLayoutPluginContext) {
        const nodes = ctx.document.getAllNodes()
        if (nodes.length === 0) {
          ctx.document.createWorkflowNode({
            id: 'start',
            type: 'Start',
            meta: { position: { x: 400, y: 300 } },
            data: { ...NODE_DEFAULT_DATA.Start },
          })
        }
      },
      onInit(ctx: FreeLayoutPluginContext) {
        useNodeGraphStore.getState().setFlowGramContext(ctx)
        _zoomDisposable = ctx.playground.onZoom((zoom) => {
          useNodeGraphStore.getState().setViewportZoom(zoom)
        })
      },
      onDispose() {
        _zoomDisposable?.dispose()
        _zoomDisposable = null
        useNodeGraphStore.getState().setFlowGramContext(null)
      },
      initialData,
      history: { enable: true, enableChangeNode: true },
    }),
    [initialData]
  )

  return (
    <div className="w-full h-full" style={{ background: '#0a0a0f' }}>
      <FreeLayoutEditorProvider {...editorProps}>
        <EditorRenderer />
      </FreeLayoutEditorProvider>
    </div>
  )
}
