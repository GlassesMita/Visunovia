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
      @select="handleProjectSelected"
    />

    <div v-if="projectOpenError" class="project-open-error" role="alert">
      <span>{{ projectOpenError }}</span>
      <button type="button" @click="projectOpenError = ''">✕</button>
    </div>

    <!-- 新建项目模态框 -->
    <NewProjectModal />

    <!-- 项目首选项模态框 -->
    <ProjectPreferencesModal />

    <!-- 角色管理器 -->
    <CharacterManagerModal />

    <!-- 项目预览 -->
    <PreviewPopup
      :visible="uiStore.showPreviewPopup"
      :reload-token="uiStore.previewReloadToken"
      @close="uiStore.closePreviewPopup()"
    />

    <NodeDetailsModal
      :visible="uiStore.showNodeDetailsModal"
      @close="uiStore.closeNodeDetailsModal()"
    />

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
    <WelcomeModal
      v-if="uiStore.showWelcomeModal"
      @project-opened="handleRecentProjectOpened"
      @project-open-failed="handleRecentProjectOpenFailed"
    />
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
import ConsolePanel from '@/components/panels/ConsolePanel.vue'
import BaklavaEditor from '@/components/BaklavaEditor.vue'
import FileExplorer from '@/components/FileExplorer.vue'
import NewProjectModal from '@/components/NewProjectModal.vue'
import ProjectPreferencesModal from '@/components/ProjectPreferencesModal.vue'
import CharacterManagerModal from '@/components/CharacterManagerModal.vue'
import PreviewPopup from '@/components/PreviewPopup.vue'
import NodeDetailsModal from '@/components/NodeDetailsModal.vue'
import WelcomeModal from '@/components/WelcomeModal.vue'
import { getCurrentProject } from '@/api/projectApi'

const { t } = useLocalization()
const uiStore = useUIStore()
const projectImport = useProjectImport()
const { loadSceneGraph, newGraph } = useNodeOperations()
const editorReady = ref(false)

// Track whether a project is currently open
const hasOpenProject = ref(false)
const projectOpenError = ref('')

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
      const response = await fetch('/api/system/health', { cache: 'no-store' })
      if (!response.ok) {
        throw new Error(`Backend health check failed: ${response.status}`)
      }
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

      // If no URL import, always show the welcome modal on startup.
      // The backend may still hold a previous CurrentProject from the same process,
      // but startup should not automatically open a scene graph from that state.
      if (!imported) {
        hasOpenProject.value = false
        uiStore.openWelcomeModal()
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
      const normalizedPath = filePath.replace(/\\/g, '/')
      const scriptsMainIndex = normalizedPath.toLowerCase().lastIndexOf('/scripts/main/')
      if (scriptsMainIndex >= 0) {
        const projectPath = filePath.slice(0, scriptsMainIndex)
        await projectImport.importProjectPath(projectPath)
      } else {
        await loadSceneGraph(filePath)
      }
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

async function handleProjectSelected(path: string, isDir: boolean) {
  uiStore.closeFileExplorer()
  if (!path) return
  projectOpenError.value = ''

  const normalizedProjectPath = isDir
    ? path
    : path.toLowerCase().endsWith('.tlor')
      ? path.replace(/[\\/][^\\/]*$/, '')
      : path

  const success = await projectImport.importProjectPath(normalizedProjectPath)
  if (success) {
    hasOpenProject.value = true
    uiStore.closeWelcomeModal()
  } else {
    projectOpenError.value = projectImport.error.value || '项目打开失败，请检查 Project.tlor 和 Scripts/Main 目录。'
    console.error('[AppLayout] Failed to open project:', projectOpenError.value)
  }
}

function closeProjectPanel() {
  uiStore.closeProjectPopup()
}

function handleRecentProjectOpened() {
  hasOpenProject.value = true
  projectOpenError.value = ''
}

function handleRecentProjectOpenFailed(message: string) {
  hasOpenProject.value = false
  projectOpenError.value = message
}

onUnmounted(() => {
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

.project-open-error {
  position: fixed;
  left: 50%;
  bottom: 28px;
  z-index: 5000;
  display: flex;
  align-items: center;
  gap: 12px;
  max-width: min(680px, calc(100vw - 48px));
  padding: 10px 14px;
  color: #f8d7da;
  background: rgba(96, 24, 32, 0.96);
  border: 1px solid rgba(248, 113, 113, 0.5);
  border-radius: 8px;
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.35);
  transform: translateX(-50%);
}

.project-open-error button {
  color: inherit;
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 14px;
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
