<template>
  <div class="app-layout">
    <header class="app-header">
      <MenuBar />
    </header>

    <div class="app-toolbar">
      <Toolbar />
    </div>

    <div class="app-content">
      <!-- 主编辑区域 -->
      <main class="app-editor" @click="closeProjectPanel">
        <BaklavaEditor />
      </main>

      <!-- 右侧面板（Inspector / Hierarchy） -->
      <div
        v-if="uiStore.showInspectorPanel || uiStore.showHierarchyPanel"
        class="resize-handle resize-handle-right"
        @mousedown="startResizeRight"
      ></div>

      <aside
        class="app-right-panel"
        :class="{ collapsed: !(uiStore.showInspectorPanel || uiStore.showHierarchyPanel) }"
      >
        <div class="panel-tabs-right">
          <button
            :class="{ active: rightActiveTab === 'inspector' }"
            :title="t('panels.inspector')"
            @click="rightActiveTab = 'inspector'; uiStore.showInspectorPanel = true"
          >
            {{ t('panels.inspector') }}
          </button>
          <button
            :class="{ active: rightActiveTab === 'hierarchy' }"
            :title="t('panels.hierarchy')"
            @click="rightActiveTab = 'hierarchy'; uiStore.showHierarchyPanel = true"
          >
            {{ t('panels.hierarchy') }}
          </button>
        </div>
        <div v-if="uiStore.showInspectorPanel || uiStore.showHierarchyPanel" class="right-panel-content">
          <InspectorPanel v-if="rightActiveTab === 'inspector' && uiStore.showInspectorPanel" />
          <HierarchyPanel v-else-if="rightActiveTab === 'hierarchy' && uiStore.showHierarchyPanel" />
        </div>
        <button
          class="panel-toggle-btn toggle-right"
          :title="(uiStore.showInspectorPanel || uiStore.showHierarchyPanel) ? (t('common.close') || 'Close') : (t('panels.inspector'))"
          @click="toggleRightPanel"
        >
          {{ (uiStore.showInspectorPanel || uiStore.showHierarchyPanel) ? '▶' : '◀' }}
        </button>
      </aside>
    </div>

    <footer class="app-footer">
      <StatusBar />
      <button
        class="console-toggle-btn"
        :class="{ active: uiStore.showConsolePanel }"
        :title="t('panels.console')"
        @click="uiStore.togglePanel('console')"
      >
        {{ t('panels.console') }}
      </button>
    </footer>

    <div
      class="app-console"
      :class="{ expanded: uiStore.showConsolePanel }"
    >
      <ConsolePanel />
    </div>

    <!-- 文件浏览器模态框 -->
    <FileExplorer
      :visible="uiStore.showFileExplorer"
      title="打开项目"
      :file-filter="['.tlor']"
      @close="uiStore.closeFileExplorer()"
      @select="(path: string, isDir: boolean) => { uiStore.closeFileExplorer(); if (path) loadSceneGraph(path); }"
    />

    <!-- 新建项目模态框 -->
    <NewProjectModal />

    <!-- 项目首选项模态框 -->
    <ProjectPreferencesModal />

    <!-- Project 面板 — 右侧弹出模态框 -->
    <Transition name="slide-fade">
      <div
        v-if="uiStore.showProjectPopup"
        class="project-popup-overlay"
        @click.self="closeProjectPanel"
      >
        <aside class="project-popup" @click.stop>
          <div class="popup-header">
            <h3>{{ t('panels.project') }}</h3>
            <button class="popup-close-btn" @click="closeProjectPanel">✕</button>
          </div>
          <ProjectPanel />
        </aside>
      </div>
    </Transition>

    <!-- Welcome Modal (shown when no project is open) -->
    <WelcomeModal v-if="uiStore.showWelcomeModal" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { useShortcuts } from '@/composables/useShortcuts'
import { useProjectImport } from '@/composables/useProjectImport'
import { useNodeOperations } from '@/composables/useNodeOperations'
import MenuBar from './MenuBar.vue'
import Toolbar from './Toolbar.vue'
import StatusBar from './StatusBar.vue'
import ProjectPanel from '@/components/panels/ProjectPanel.vue'
import InspectorPanel from '@/components/panels/InspectorPanel.vue'
import HierarchyPanel from '@/components/panels/HierarchyPanel.vue'
import ConsolePanel from '@/components/panels/ConsolePanel.vue'
import BaklavaEditor from '@/components/BaklavaEditor.vue'
import FileExplorer from '@/components/FileExplorer.vue'
import NewProjectModal from '@/components/NewProjectModal.vue'
import ProjectPreferencesModal from '@/components/ProjectPreferencesModal.vue'
import WelcomeModal from '@/components/WelcomeModal.vue'
import { getCurrentProject } from '@/api/projectApi'

const { t } = useLocalization()
const uiStore = useUIStore()
const projectImport = useProjectImport()
const { loadSceneGraph, newGraph } = useNodeOperations()
const editorReady = ref(false)

// Track whether a project is currently open
const hasOpenProject = ref(false)

// Backend shutdown detection
let shutdownCheckInterval: ReturnType<typeof setInterval> | null = null

useShortcuts()

/**
 * Check if a project is currently open.
 * Returns true if a project was opened, false otherwise.
 */
async function checkHasOpenProject(): Promise<boolean> {
  try {
    const result = await getCurrentProject()
    return result.data !== null && result.data !== undefined
  } catch {
    return false
  }
}

/**
 * Start polling the backend to detect shutdown.
 * When the backend becomes unreachable, auto-close the frontend.
 */
function startShutdownDetection() {
  shutdownCheckInterval = setInterval(async () => {
    try {
      await getCurrentProject()
    } catch {
      // Backend is unreachable — it's shutting down
      console.log('[AppLayout] Backend is shutting down, closing frontend...')
      if (shutdownCheckInterval) {
        clearInterval(shutdownCheckInterval)
        shutdownCheckInterval = null
      }
      // Attempt to close the window
      window.close()
      // Fallback: navigate to about:blank after 3 seconds
      setTimeout(() => {
        if (!window.closed) {
          window.location.href = 'about:blank'
        }
      }, 3000)
    }
  }, 2000)
}

onMounted(async () => {
  // Wait for editor to be ready, then check for project
  const checkEditorReady = async () => {
    if ((window as any).__editor) {
      editorReady.value = true

      // First try to import from URL
      const imported = await importProjectIfNeeded()

      // If no URL import, check if there's already an open project
      if (!imported) {
        const hasProject = await checkHasOpenProject()
        hasOpenProject.value = hasProject

        if (!hasProject) {
          // No project open — show the welcome modal
          uiStore.openWelcomeModal()
        }
      } else {
        hasOpenProject.value = true
      }

      // Start backend shutdown detection
      startShutdownDetection()
    } else {
      setTimeout(checkEditorReady, 100)
    }
  }
  checkEditorReady()
})

// 监听 openingFilePath，当创建项目后自动打开 start.lor
watch(
  () => uiStore.openingFilePath,
  async (filePath) => {
    if (filePath) {
      await nextTick()
      await loadSceneGraph(filePath)
      uiStore.openingFilePath.value = null
      // A project has been opened — close the welcome modal
      hasOpenProject.value = true
      uiStore.closeWelcomeModal()
    }
  }
)

// When the file explorer selects a .tlor file, close the welcome modal
watch(
  () => uiStore.showFileExplorer,
  (visible) => {
    // If the file explorer was closed and we had the welcome modal open,
    // check if a project was opened
    if (!visible && uiStore.showWelcomeModal) {
      // Give a moment for the project to load, then check
      setTimeout(async () => {
        const hasProject = await checkHasOpenProject()
        if (hasProject) {
          hasOpenProject.value = true
          uiStore.closeWelcomeModal()
        }
      }, 500)
    }
  }
)

async function importProjectIfNeeded(): Promise<boolean> {
  if (editorReady.value) {
    const success = await projectImport.importFromUrl()
    if (success) {
      console.log(`[AppLayout] 项目已导入: ${projectImport.importedSceneId.value}`)
      return true
    }
  }
  return false
}

const rightActiveTab = ref<'inspector' | 'hierarchy'>('inspector')

let isResizingRight = false

function closeProjectPanel() {
  uiStore.closeProjectPopup()
}

function startResizeRight() {
  isResizingRight = true
  document.addEventListener('mousemove', handleResizeRight)
  document.addEventListener('mouseup', stopResize)
}

function handleResizeRight(e: MouseEvent) {
  if (!isResizingRight) return
  const windowWidth = window.innerWidth
  const newWidth = Math.max(200, Math.min(500, windowWidth - e.clientX))
  document.documentElement.style.setProperty('--right-panel-width', `${newWidth}px`)
}

function stopResize() {
  isResizingRight = false
  document.removeEventListener('mousemove', handleResizeRight)
  document.removeEventListener('mouseup', stopResize)
}

function toggleRightPanel() {
  if (uiStore.showInspectorPanel || uiStore.showHierarchyPanel) {
    uiStore.showInspectorPanel = false
    uiStore.showHierarchyPanel = false
  } else {
    uiStore.showInspectorPanel = true
  }
}

onUnmounted(() => {
  document.removeEventListener('mousemove', handleResizeRight)
  document.removeEventListener('mouseup', stopResize)
  if (shutdownCheckInterval) {
    clearInterval(shutdownCheckInterval)
    shutdownCheckInterval = null
  }
})
</script>

<style scoped>
.app-layout {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100vh;
  overflow: hidden;
  background: #1e1e1e;
}

.app-header {
  height: 32px;
  min-height: 32px;
  background: #252526;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.app-toolbar {
  height: 40px;
  min-height: 40px;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.app-content {
  display: flex;
  flex: 1;
  overflow: hidden;
  min-height: 0;
}

.app-left-panel,
.app-right-panel {
  width: var(--left-panel-width, 280px);
  background: #252526;
  display: flex;
  overflow: hidden;
  transition: width 0.15s ease;
  flex-shrink: 0;
}

.app-right-panel {
  width: var(--right-panel-width, 280px);
  border-left: 1px solid #3e3e42;
}

.app-left-panel.collapsed {
  width: 36px;
  border-right: none;
}

.app-right-panel.collapsed {
  width: 36px;
  border-left: none;
}

.panel-tabs-left,
.panel-tabs-right {
  display: flex;
  flex-direction: column;
  width: 32px;
  background: #2d2d30;
  border-right: 1px solid #3e3e42;
  flex-shrink: 0;
}

.panel-tabs-right {
  flex-direction: row;
  width: auto;
  border-right: none;
  border-bottom: 1px solid #3e3e42;
}

.panel-tabs-left button,
.panel-tabs-right button {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 8px 4px;
  background: transparent;
  border: none;
  color: #808080;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.15s;
}

.panel-tabs-left button {
  height: 40px;
  border-bottom: 1px solid #3e3e42;
}

.panel-tabs-right button {
  flex: 1;
  font-size: 11px;
  text-transform: uppercase;
}

.panel-tabs-left button:hover,
.panel-tabs-right button:hover {
  color: #cccccc;
  background: rgba(255, 255, 255, 0.05);
}

.panel-tabs-left button.active,
.panel-tabs-right button.active {
  color: #ffffff;
  background: #094771;
}

.left-panel-content,
.right-panel-content {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.panel-toggle-btn {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  width: 16px;
  height: 48px;
  background: #2d2d30;
  border: 1px solid #3e3e42;
  color: #808080;
  cursor: pointer;
  font-size: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10;
  transition: all 0.15s;
}

.panel-toggle-btn:hover {
  background: #3e3e42;
  color: #ffffff;
}

.app-left-panel .panel-toggle-btn {
  right: 0;
  border-radius: 0 4px 4px 0;
}

.toggle-right {
  left: 0;
  border-radius: 4px 0 0 4px;
}

.resize-handle {
  width: 4px;
  background: #3e3e42;
  cursor: col-resize;
  flex-shrink: 0;
  transition: background 0.15s;
  z-index: 5;
}

.resize-handle:hover {
  background: #007acc;
}

.app-editor {
  flex: 1;
  background: #1e1e1e;
  overflow: hidden;
  min-width: 0;
}

.app-footer {
  height: 24px;
  min-height: 24px;
  background: #007acc;
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-shrink: 0;
  position: relative;
}

.console-toggle-btn {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  padding: 2px 10px;
  background: rgba(0, 0, 0, 0.2);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 3px;
  color: #ffffff;
  cursor: pointer;
  font-size: 11px;
  transition: all 0.15s;
}

.console-toggle-btn:hover {
  background: rgba(0, 0, 0, 0.4);
}

.console-toggle-btn.active {
  background: rgba(255, 255, 255, 0.2);
}

.app-console {
  height: 0;
  background: #1e1e1e;
  border-top: 1px solid #3e3e42;
  overflow: hidden;
  transition: height 0.2s ease;
  flex-shrink: 0;
}

.app-console.expanded {
  height: 200px;
}

/* Project 弹出模态框 */
.project-popup-overlay {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  justify-content: flex-end;
  background: rgba(0, 0, 0, 0.35);
}

.project-popup {
  width: 320px;
  height: 100%;
  background: #252526;
  border-left: 1px solid #3e3e42;
  display: flex;
  flex-direction: column;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.4);
  overflow: hidden;
}

.popup-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.popup-header h3 {
  margin: 0;
  font-size: 13px;
  color: #cccccc;
  font-weight: 500;
}

.popup-close-btn {
  background: transparent;
  border: none;
  color: #808080;
  cursor: pointer;
  font-size: 14px;
  padding: 2px 6px;
  border-radius: 3px;
  transition: all 0.15s;
}

.popup-close-btn:hover {
  color: #ffffff;
  background: rgba(255, 255, 255, 0.1);
}

/* 滑入滑出动画 (fade + slide + scale) */
.slide-fade-enter-active,
.slide-fade-leave-active {
  transition: opacity 0.2s ease;
}

.slide-fade-enter-active .project-popup,
.slide-fade-leave-active .project-popup {
  transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1);
}

.slide-fade-enter-from,
.slide-fade-leave-to {
  opacity: 0;
}

.slide-fade-enter-from .project-popup,
.slide-fade-leave-to .project-popup {
  transform: translateX(60px) scale(0.96);
}
</style>
