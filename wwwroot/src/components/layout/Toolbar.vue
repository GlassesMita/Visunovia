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

    <div class="toolbar-divider"></div>

    <div class="toolbar-group">
      <button
        class="toolbar-button"
        :title="t('menu.cut')"
        @click="handleCut"
      >
        <Scissors :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="t('menu.copy')"
        @click="handleCopy"
      >
        <Copy :size="18" />
      </button>
      <button
        class="toolbar-button"
        :title="t('menu.paste')"
        @click="handlePaste"
      >
        <Clipboard :size="18" />
      </button>
    </div>

    <div class="toolbar-divider"></div>

    <div class="toolbar-group">
      <button
        class="toolbar-button"
        :title="t('menu.delete')"
        @click="handleDelete"
      >
        <Trash2 :size="18" />
      </button>
    </div>

    <!-- Spacer to push window controls to the right -->
    <div class="toolbar-spacer"></div>

    <!-- Window controls -->
    <div class="toolbar-group toolbar-window-controls">
      <button
        class="toolbar-button toolbar-btn-window"
        :title="isFullscreen ? 'Exit Fullscreen' : 'Fullscreen'"
        @click="toggleFullscreen"
      >
        <Maximize v-if="!isFullscreen" :size="14" />
        <Minimize v-else :size="14" />
      </button>
      <button
        class="toolbar-button toolbar-btn-close"
        title="Close"
        @click="handleCloseApp"
      >
        <X :size="16" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { FilePlus, FolderOpen, Save, Undo2, Redo2, Scissors, Copy, Clipboard, Trash2, Maximize, Minimize, X } from 'lucide-vue-next'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { quitApplication } from '@/api/systemApi'

const { t } = useLocalization()
const uiStore = useUIStore()
const editorStore = useEditorStore()
const undoRedoStore = useUndoRedoStore()
const nodeGraphStore = useNodeGraphStore()

const undoDescription = computed(() => undoRedoStore.getUndoDescription())
const redoDescription = computed(() => undoRedoStore.getRedoDescription())

const isFullscreen = ref(false)

function toggleFullscreen() {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen().catch(() => {})
  } else {
    document.exitFullscreen().catch(() => {})
  }
}

function handleFullscreenChange() {
  isFullscreen.value = !!document.fullscreenElement
}

async function handleCloseApp() {
  try {
    await quitApplication()
  } catch {
    // If the API call fails, still attempt to close
  }
  // Close the window after requesting shutdown
  window.close()
  // Fallback: if window.close() doesn't work (e.g. opened directly in browser),
  // navigate to about:blank after 3 seconds
  setTimeout(() => {
    if (!window.closed) {
      window.location.href = 'about:blank'
    }
  }, 3000)
}

onMounted(() => {
  document.addEventListener('fullscreenchange', handleFullscreenChange)
  isFullscreen.value = !!document.fullscreenElement
})

onUnmounted(() => {
  document.removeEventListener('fullscreenchange', handleFullscreenChange)
})

function handleNew() {
  uiStore.openNewProjectModal()
}

function handleSave() {
  editorStore.save()
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

function handleCut() {
  console.log('Cut')
}

function handleCopy() {
  console.log('Copy')
}

function handlePaste() {
  console.log('Paste')
}

function handleDelete() {
  if (editorStore.selectedNodeId) {
    undoRedoStore.pushState(nodeGraphStore.serializeGraph()!, 'Delete Node')
    nodeGraphStore.removeNode(editorStore.selectedNodeId)
    editorStore.selectNode(null)
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

.toolbar-window-controls {
  gap: 0;
  margin-left: 4px;
}

.toolbar-btn-window {
  width: 36px;
}

.toolbar-btn-close {
  width: 36px;
  border-radius: 0 4px 4px 0;
}

.toolbar-btn-close:hover {
  background: #e81123 !important;
  color: #ffffff !important;
}

.toolbar-btn-close:active {
  background: #c50f1f !important;
}
</style>
