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
          @pointerenter="(ev: PointerEvent) => showNodeHover(ev, node)"
          @pointerleave="hideNodeHover"
          @dblclick.stop="() => openNodeDetails(node)"
          @select="onSelect"
          @start-drag="onStartDrag"
        >
          <template #title>
            <div
              class="__title visunovia-node-title"
              @pointerdown.stop="(ev: PointerEvent) => handleNodeTitlePointerDown(ev, onSelect, onStartDrag)"
              @dblclick.stop="() => openNodeDetails(node)"
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
    <aside
      v-if="hoverPanel.visible && hoverPanel.node"
      class="visunovia-node-hover-panel"
      @pointerdown.stop
    >
      <div class="visunovia-node-hover-kicker">节点信息</div>
      <div class="visunovia-node-hover-title">{{ getHoverNodeTitle(hoverPanel.node) }}</div>
      <div class="visunovia-node-hover-id">{{ hoverPanel.node.id }}</div>
      <div class="visunovia-node-hover-grid">
        <template v-for="item in getHoverNodeItems(hoverPanel.node)" :key="item.key">
          <span>{{ item.label }}</span>
          <strong>{{ item.value }}</strong>
        </template>
      </div>
      <div class="visunovia-node-hover-hint">双击节点打开完整配置</div>
    </aside>
    <div v-else-if="isInitialized && !baklava" class="loading">
      <p>Failed to initialize editor</p>
    </div>
    <div v-else-if="!isInitialized" class="loading">
      <p>Initializing Editor...</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, reactive, shallowRef, watch } from 'vue'
import { BaklavaEditor, Components, useBaklava } from '@baklavajs/renderer-vue'
import { registerAllNodes } from '@/baklava/nodeRegistry'
import { characterSelectOptions } from '@/services/characterOptions'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'
import { useUIStore } from '@/stores/useUIStore'
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
const uiStore = useUIStore()
const nodeMenu = reactive({
  visible: false,
  x: 0,
  y: 0,
  node: null as any,
})
const hoverPanel = reactive({
  visible: false,
  node: null as any,
})

watch(
  () => {
    const graph = baklava.value?.displayedGraph
    if (!graph) return []

    return graph.nodes.map((node: any) => ({
      id: node.id,
      x: node.position.x,
      y: node.position.y,
    }))
  },
  (positions, previousPositions) => {
    if (!isInitialized.value || positions.length !== previousPositions.length) return

    const previousById = new Map(
      previousPositions.map(position => [position.id, position])
    )
    const hasSameNodes = positions.every(position => previousById.has(position.id))
    if (!hasSameNodes) return

    const positionChanged = positions.some((position) => {
      const previousPosition = previousById.get(position.id)
      return previousPosition &&
        (position.x !== previousPosition.x || position.y !== previousPosition.y)
    })
    if (!positionChanged) return

    nodeGraphStore.syncNodes()
    nodeGraphStore.markDirty()
  },
  { flush: 'post' }
)

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

function showNodeHover(_ev: PointerEvent, node: any) {
  hoverPanel.node = node
  hoverPanel.visible = true
}

function hideNodeHover() {
  hoverPanel.visible = false
  hoverPanel.node = null
}

function getInputValue(node: any, key: string) {
  return node?.inputs?.[key]?.value ?? node?.data?.[key] ?? ''
}

function getCharacterLabel(value: string) {
  const id = String(value || '').trim()
  if (!id) return '未选择'
  const option = characterSelectOptions.find((item: any) => item.value === id || item.text === id)
  return option?.text || id
}

function parseCharacterControls(node: any) {
  const raw = getInputValue(node, 'characterControlsJson') || node?.data?.characterControlsJson || node?.data?.characterControls
  if (Array.isArray(raw)) return raw
  if (!raw) return []
  try {
    const parsed = JSON.parse(String(raw))
    return Array.isArray(parsed) ? parsed : []
  } catch {
    return []
  }
}

function getHoverNodeTitle(node: any) {
  return String(node?.title || node?.type || 'Node')
}

function getHoverNodeItems(node: any) {
  const type = String(node?.type || '')
  if (type === 'CharacterControlNode') {
    const controls = parseCharacterControls(node).filter((control: any) => String(control?.mode || control?.action || 'none') !== 'none')
    if (controls.length > 0) {
      return [
        { key: 'count', label: '修改 Slot', value: String(controls.length) },
        { key: 'slots', label: 'Slot', value: controls.map((control: any) => `${control.slot}:${control.mode || control.action}`).join(', ') },
        { key: 'characters', label: '角色', value: controls.map((control: any) => control.slot === '6'
          ? (control.unmanagedCharacter || control.character || '未设置')
          : getCharacterLabel(String(control.character || ''))).join(' / ') },
      ]
    }

    return [
      { key: 'count', label: '修改 Slot', value: '0' },
      { key: 'hint', label: '状态', value: '全部无修改' },
    ]
  }

  if (type === 'DialogueNode') {
    return [
      { key: 'speakerSlot', label: '说话 Slot', value: getInputValue(node, 'speakerSlot') || '无' },
      { key: 'voiceCount', label: '语音数量', value: getInputValue(node, 'voiceCount') || '0' },
      { key: 'text', label: '文本', value: String(getInputValue(node, 'text') || '无').slice(0, 80) },
    ]
  }

  return Object.entries(node?.inputs || {})
    .filter(([, iface]: any) => iface?.value !== undefined)
    .slice(0, 8)
    .map(([key, iface]: any) => ({ key, label: key, value: String(iface.value || '无') }))
}

function openNodeDetails(node: any) {
  if (!node?.id) return
  hideNodeHover()
  editorStore.selectNode(node.id)
  uiStore.openNodeDetailsModal()
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

    // 初始化图数据存储，用于属性检查器、层级面板和自动保存
    nodeGraphStore.initializeEditor(baklavaInstance.editor)

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
  background-color: var(--md-sys-color-background);
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
  border-radius: var(--md-sys-shape-corner-small);
  color: var(--md-sys-color-on-surface-variant);
  background: transparent;
  cursor: pointer;
  font-size: 18px;
  line-height: 1;
}

:deep(.visunovia-node-menu-button:hover) {
  background: var(--md-sys-color-secondary-container);
}

.visunovia-node-context-menu {
  position: absolute;
  z-index: 10000;
  min-width: 120px;
  padding: 4px;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: var(--md-sys-shape-corner-medium);
  background: var(--md-sys-color-surface-container-high);
  box-shadow: var(--md-sys-elevation-2);
}

.visunovia-node-context-item {
  width: 100%;
  padding: 6px 10px;
  border: 0;
  border-radius: var(--md-sys-shape-corner-small);
  color: var(--md-sys-color-on-surface);
  background: transparent;
  text-align: left;
  cursor: pointer;
  font-size: 12px;
}

.visunovia-node-context-item:hover {
  background: var(--md-sys-color-secondary-container);
}

.visunovia-node-hover-panel {
  position: absolute;
  top: 72px;
  right: 18px;
  z-index: 9000;
  width: min(340px, 32vw);
  max-height: calc(100% - 96px);
  overflow: auto;
  padding: 14px;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: var(--md-sys-shape-corner-large);
  background: color-mix(in srgb, var(--md-sys-color-surface-container-high) 94%, transparent);
  color: var(--md-sys-color-on-surface);
  box-shadow: var(--md-sys-elevation-2);
  backdrop-filter: blur(10px);
  pointer-events: none;
}

.visunovia-node-hover-kicker {
  color: var(--md-sys-color-primary);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
}

.visunovia-node-hover-title {
  margin-top: 4px;
  font-size: 16px;
  font-weight: 700;
}

.visunovia-node-hover-id {
  margin-top: 3px;
  color: var(--md-sys-color-on-surface-variant);
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 10px;
  word-break: break-all;
}

.visunovia-node-hover-grid {
  display: grid;
  grid-template-columns: minmax(72px, auto) minmax(0, 1fr);
  gap: 8px 10px;
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid rgba(148, 163, 184, 0.22);
  font-size: 12px;
}

.visunovia-node-hover-grid span {
  color: #94a3b8;
}

.visunovia-node-hover-grid strong {
  overflow: hidden;
  color: #f8fafc;
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.visunovia-node-hover-hint {
  margin-top: 12px;
  color: #bfdbfe;
  font-size: 11px;
}

.loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #888;
}
</style>
