<template>
  <div class="baklava-editor-wrapper" @contextmenu.prevent>
    <BaklavaEditor 
      v-if="isInitialized && baklava" 
      :view-model="baklava" 
    >
      <template #node="{ node, selected, dragging, onStartDrag, onSelect }">
        <Components.Node
          :node="node"
          :selected="selected"
          :dragging="dragging"
          @select="onSelect"
          @start-drag="onStartDrag"
        >
          <template #title>
            <div
              class="__title visunovia-node-title"
              @pointerdown.stop="(ev: PointerEvent) => handleNodeTitlePointerDown(ev, onSelect, onStartDrag)"
              @contextmenu.prevent.stop="(ev: MouseEvent) => openNodeMenu(ev, node)"
            >
              <div class="__title-label">{{ node.title }}</div>
              <button
                type="button"
                class="visunovia-node-menu-button"
                aria-label="Node menu"
                @pointerdown.stop
                @click.stop="(ev: MouseEvent) => openNodeMenu(ev, node)"
              >
                ⋮
              </button>
            </div>
          </template>
        </Components.Node>
      </template>
    </BaklavaEditor>
    <div
      v-if="nodeMenu.visible"
      class="visunovia-node-context-menu"
      :style="{ left: `${nodeMenu.x}px`, top: `${nodeMenu.y}px` }"
      @contextmenu.prevent.stop
      @pointerdown.stop
    >
      <button type="button" class="visunovia-node-context-item" @click="deleteContextNode">
        Delete
      </button>
    </div>
    <div v-else-if="isInitialized && !baklava" class="loading">
      <p>Failed to initialize editor</p>
    </div>
    <div v-else-if="!isInitialized" class="loading">
      <p>Initializing Editor...</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, reactive, shallowRef } from 'vue'
import { BaklavaEditor, Components, useBaklava } from '@baklavajs/renderer-vue'
import { registerAllNodes } from '@/baklava/nodeRegistry'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'
import { useConnectionColors } from '@/composables/useConnectionColors'
import { useFixConnectionRedraw } from '@/composables/useFixConnectionRedraw'
import { useFixNodeSelection } from '@/composables/useFixNodeSelection'
import StartNode from '@/components/baklava-nodes/StartNode'

const isInitialized = shallowRef(false)
const baklava = shallowRef<any>(null)
const localizationStore = useLocalizationStore()
const editorStore = useEditorStore()
const nodeGraphStore = useNodeGraphStore()
const undoRedoStore = useUndoRedoStore()
const nodeMenu = reactive({
  visible: false,
  x: 0,
  y: 0,
  node: null as any,
})

function handleNodeTitlePointerDown(ev: PointerEvent, onSelect: (event?: any) => void, onStartDrag: (event: PointerEvent) => void) {
  closeNodeMenu()
  onSelect(ev)
  onStartDrag(ev)
}

function openNodeMenu(ev: MouseEvent, node: any) {
  ev.preventDefault()
  ev.stopPropagation()

  const wrapper = document.querySelector('.baklava-editor-wrapper')?.getBoundingClientRect()
  nodeMenu.x = wrapper ? ev.clientX - wrapper.left : ev.clientX
  nodeMenu.y = wrapper ? ev.clientY - wrapper.top : ev.clientY
  nodeMenu.node = node
  nodeMenu.visible = true
}

function closeNodeMenu() {
  nodeMenu.visible = false
  nodeMenu.node = null
}

function deleteContextNode() {
  const graph = baklava.value?.displayedGraph
  if (!graph || !nodeMenu.node) return

  undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, 'Delete Node')
  graph.removeNode(nodeMenu.node)
  graph.selectedNodes = graph.selectedNodes.filter((node: any) => node !== nodeMenu.node)
  editorStore.selectNode(null)
  nodeGraphStore.isDirty = true
  closeNodeMenu()
}

onMounted(async () => {
  try {
    // 初始化本地化不应阻塞 Baklava 编辑器挂载，避免启动时返回空内容。
    void localizationStore.initialize().catch((error) => {
      console.warn('[BaklavaEditor] Localization initialization failed:', error)
    })

    // 使用 useBaklava hook
    const baklavaInstance = useBaklava()
    baklava.value = baklavaInstance

    // 注册所有节点
    registerAllNodes(baklavaInstance.editor)

    // 初始化连接线颜色系统
    useConnectionColors(baklavaInstance.editor)

    // 修复节点拖拽时连接线未跟随更新的问题
    useFixConnectionRedraw(baklavaInstance)

    // 修复节点选中行为：左键点击可累加多选，背景点击清空选择
    useFixNodeSelection(baklavaInstance)
    
    // 将 editor 实例暴露到 window，供 useNodeOperations 和 useLorImport 使用
    ;(window as any).__editor = baklavaInstance.editor
    ;(window as any).__baklavaViewModel = baklavaInstance

    // 创建一个默认 Start 节点
    setTimeout(() => {
      if (baklavaInstance.editor && baklavaInstance.editor.graph.nodes.length === 0) {
        const startNode = new StartNode()
        startNode.position = { x: 250, y: 50 }
        baklavaInstance.editor.graph.addNode(startNode)
      }
    }, 100)
    
    isInitialized.value = true
  } catch (error) {
    console.error('Failed to initialize BaklavaJS:', error)
  }

  document.addEventListener('pointerdown', closeNodeMenu)
})

onUnmounted(() => {
  document.removeEventListener('pointerdown', closeNodeMenu)
})
</script>

<style scoped>
.baklava-editor-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  background-color: #1e1e1e;
}

:deep(.visunovia-node-title) {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

:deep(.visunovia-node-menu-button) {
  width: 22px;
  height: 22px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 0;
  border-radius: 3px;
  color: #d4d4d4;
  background: transparent;
  cursor: pointer;
  font-size: 18px;
  line-height: 1;
}

:deep(.visunovia-node-menu-button:hover) {
  background: rgba(255, 255, 255, 0.12);
}

.visunovia-node-context-menu {
  position: absolute;
  z-index: 10000;
  min-width: 120px;
  padding: 4px;
  border: 1px solid #454545;
  border-radius: 4px;
  background: #252526;
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.35);
}

.visunovia-node-context-item {
  width: 100%;
  padding: 6px 10px;
  border: 0;
  border-radius: 3px;
  color: #f0f0f0;
  background: transparent;
  text-align: left;
  cursor: pointer;
  font-size: 12px;
}

.visunovia-node-context-item:hover {
  background: #094771;
}

.loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #888;
}
</style>
