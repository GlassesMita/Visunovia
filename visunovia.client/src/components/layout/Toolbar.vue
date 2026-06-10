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
import { computed } from 'vue'
import { FilePlus, FolderOpen, RefreshCw, Save, Undo2, Redo2, Users, Play } from 'lucide-vue-next'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'

const { t } = useLocalization()
const uiStore = useUIStore()
const editorStore = useEditorStore()
const undoRedoStore = useUndoRedoStore()
const nodeGraphStore = useNodeGraphStore()

const undoDescription = computed(() => undoRedoStore.getUndoDescription())
const redoDescription = computed(() => undoRedoStore.getRedoDescription())

function handleNew() {
  uiStore.openNewProjectModal()
}

function handleSave() {
  editorStore.save()
}

function handleRefreshProjectTree() {
  uiStore.openProjectPopup()
  uiStore.refreshProjectTree()
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
