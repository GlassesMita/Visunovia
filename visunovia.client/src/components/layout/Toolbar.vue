<template>
  <div class="toolbar">
    <div class="toolbar-group">
      <button
        class="toolbar-button"
        :title="t('panels.project')"
        @click="uiStore.openProjectPopup()"
      >
        <FolderOpen :size="18" />
      </button>
      <button
        class="toolbar-button"
        title="刷新项目资源"
        @click="handleRefreshProjectTree"
      >
        <RefreshCw :size="18" />
      </button>
      <button
        class="toolbar-button"
        title="角色管理器"
        @click="uiStore.openCharacterManager()"
      >
        <Users :size="18" />
      </button>
      <button
        class="toolbar-button"
        title="表情管理器"
        @click="uiStore.openExpressionManager()"
      >
        <Sparkles :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="t('lorImport.menuImport', 'Import Lor/LRC to Blueprint')"
        @click="uiStore.openLorImportDialog()"
      >
        <Upload :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="t('menu.new')"
        @click="handleNew"
      >
        <FilePlus :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="t('menu.save')"
        @click="handleSave"
      >
        <Save :size="18" />
      </button>
      <label class="scene-quick-open" title="快速打开场景">
        <span>场景</span>
        <NativeFreeSelect
          v-model="selectedSceneId"
          class="scene-quick-open-control"
          :options="sceneQuickOpenOptions"
          :disabled="scenesLoading || sceneOptions.length === 0 || editorStore.isLoading"
          @change="handleSceneSelected"
        />
      </label>
      <button
        class="toolbar-button"
        title="预览项目"
        @click="uiStore.openPreviewPopup()"
      >
        <Play :size="18" />
      </button>
    </div>

    <div class="toolbar-divider"></div>

    <div class="toolbar-group">
      <button
        class="toolbar-button"
        title="将选中节点对齐到网格；未选中时对齐全部节点"
        :disabled="nodeGraphStore.nodeCount === 0"
        @click="handleAlignNodesToGrid"
      >
        <Grid3X3 :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="`${t('menu.undo')} (${undoDescription || 'Ctrl+Z'})`"
        :disabled="!undoRedoStore.canUndo"
        @click="handleUndo"
      >
        <Undo2 :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="`${t('menu.redo')} (${redoDescription || 'Ctrl+Y'})`"
        :disabled="!undoRedoStore.canRedo"
        @click="handleRedo"
      >
        <Redo2 :size="18" />
      </button>
    </div>

    <!-- Spacer -->
    <div class="toolbar-spacer"></div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted } from 'vue'
import { FilePlus, FolderOpen, RefreshCw, Save, Undo2, Redo2, Users, Play, Grid3X3, Sparkles, Upload } from 'lucide-vue-next'
import NativeFreeSelect from '@/components/NativeFreeSelect.vue'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useNodeOperations } from '@/composables/useNodeOperations'
import { getCurrentProject, getProjectScenes, type SceneListItem } from '@/api/projectApi'

const { t } = useLocalization()
const uiStore = useUIStore()
const editorStore = useEditorStore()
const undoRedoStore = useUndoRedoStore()
const nodeGraphStore = useNodeGraphStore()
const { loadSceneGraph } = useNodeOperations()

const undoDescription = computed(() => undoRedoStore.getUndoDescription())
const redoDescription = computed(() => undoRedoStore.getRedoDescription())
const sceneOptions = ref<SceneListItem[]>([])
const selectedSceneId = ref('')
const scenesLoading = ref(false)
const sceneQuickOpenOptions = computed(() => sceneOptions.value.length === 0
  ? [{ value: '', label: '无场景', disabled: true }]
  : sceneOptions.value.map(scene => ({ value: scene.id, label: scene.id }))
)

onMounted(() => {
  refreshSceneOptions()
})

watch(
  () => nodeGraphStore.currentSceneId,
  (sceneId) => {
    selectedSceneId.value = sceneId || ''
    refreshSceneOptions()
  }
)

function handleNew() {
  uiStore.openNewProjectModal()
}

function handleSave() {
  editorStore.save()
}

async function handleRefreshProjectTree() {
  uiStore.openProjectPopup()
  uiStore.refreshProjectTree()
  await refreshSceneOptions()
}

async function refreshSceneOptions() {
  scenesLoading.value = true
  try {
    const currentProject = await getCurrentProject()
    if (!currentProject.data?.projectPath) {
      sceneOptions.value = []
      selectedSceneId.value = ''
      return
    }

    sceneOptions.value = await getProjectScenes(currentProject.data.projectPath)
    selectedSceneId.value = nodeGraphStore.currentSceneId || sceneOptions.value[0]?.id || ''
  } catch (error) {
    console.warn('[Toolbar] 获取场景列表失败:', error)
    sceneOptions.value = []
  } finally {
    scenesLoading.value = false
  }
}

async function handleSceneSelected() {
  const targetSceneId = selectedSceneId.value
  const currentSceneId = nodeGraphStore.currentSceneId

  if (!targetSceneId || targetSceneId === currentSceneId) return

  editorStore.isLoading = true
  try {
    if (currentSceneId) {
      await editorStore.save()
      if (editorStore.error) {
        selectedSceneId.value = currentSceneId
        return
      }
    }

    const loaded = await loadSceneGraph(targetSceneId)
    if (!loaded) {
      selectedSceneId.value = currentSceneId || ''
    }
  } finally {
    editorStore.isLoading = false
  }
}

function handleAlignNodesToGrid() {
  nodeGraphStore.alignNodesToGrid()
  ;(window as any).__baklavaViewModel?.editor?.hooks?.load?.execute?.({})
}

function handleUndo() {
  const state = undoRedoStore.undo()
  if (state) {
    nodeGraphStore.deserializeGraph({
      nodes: state.nodes,
      connections: state.connections
    })
  }
}

function handleRedo() {
  const state = undoRedoStore.redo()
  if (state) {
    nodeGraphStore.deserializeGraph({
      nodes: state.nodes,
      connections: state.connections
    })
  }
}
</script>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  height: 100%;
  padding: 0 12px;
  gap: 4px;
}

.toolbar-group {
  display: flex;
  gap: 2px;
}

.toolbar-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  background: transparent;
  border: none;
  border-radius: 4px;
  color: #cccccc;
  cursor: pointer;
  transition: all 0.15s;
}

.toolbar-button:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.1);
  color: #ffffff;
}

.toolbar-button:active:not(:disabled) {
  background: rgba(255, 255, 255, 0.15);
  transform: scale(0.96);
}

.toolbar-button:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.scene-quick-open {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 6px;
  color: #cccccc;
  font-size: 12px;
}

.scene-quick-open-control {
  min-width: 140px;
  height: 26px;
  background: #1e1e1e;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  color: #eeeeee;
}

:deep(.scene-quick-open-control .native-free-select__value) {
  min-height: 24px;
  padding: 0 8px;
}

.scene-quick-open-control.disabled {
  opacity: 0.5;
}

.toolbar-divider {
  width: 1px;
  height: 22px;
  background: #3e3e42;
  margin: 0 6px;
}

.toolbar-spacer {
  flex: 1;
}
</style>
