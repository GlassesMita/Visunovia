<template>
  <div class="menu-bar">
    <div class="menu-items">
      <div 
        v-for="menu in menus" 
        :key="menu.key" 
        class="menu-item"
        :class="{ active: activeMenu === menu.key }"
        @click="toggleMenu(menu.key)"
      >
        <span class="menu-label">{{ t(menu.labelKey) }}</span>
        <div v-if="activeMenu === menu.key" class="menu-dropdown">
          <template v-for="item in menu.items" :key="item.key">
            <div v-if="item.divider" class="menu-divider"></div>
            <div 
              v-else
              class="menu-dropdown-item"
              :class="{ disabled: item.disabled }"
              @click.stop="!item.disabled && executeAction(item.action!)"
            >
              <span class="item-label">{{ t(item.labelKey) }}</span>
              <span v-if="item.shortcut" class="shortcut">{{ item.shortcut }}</span>
              <span v-if="item.checked !== undefined" class="checkmark">
                {{ item.checked ? '✓' : '' }}
              </span>
            </div>
          </template>
        </div>
      </div>
    </div>
    <div class="menu-title" :title="windowTitle">
      {{ windowTitle }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useNodeOperations } from '@/composables/useNodeOperations'
import { getCurrentProject } from '@/api/projectApi'
import { quitApplication } from '@/api/systemApi'

interface MenuItem {
  key: string
  labelKey: string
  action?: string
  shortcut?: string
  divider?: boolean
  disabled?: boolean
  checked?: boolean
}

interface Menu {
  key: string
  labelKey: string
  items: MenuItem[]
}

const { t } = useLocalization()
const uiStore = useUIStore()
const editorStore = useEditorStore()
const nodeGraphStore = useNodeGraphStore()
const { saveSceneGraph, loadSceneGraph, newGraph } = useNodeOperations()

const activeMenu = ref<string | null>(null)
const projectName = ref('Visunovia')
const sceneName = computed(() => nodeGraphStore.currentSceneId || editorStore.currentFileName.replace(/\.lor$/i, '') || 'Untitled')
const windowTitle = computed(() => `${projectName.value} - ${sceneName.value}`)

const menus = computed<Menu[]>(() => [
  {
    key: 'file',
    labelKey: 'menu.file',
    items: [
      { key: 'new', labelKey: 'menu.new', action: 'newFile', shortcut: 'Ctrl+N' },
      { key: 'open', labelKey: 'menu.open', action: 'openFile', shortcut: 'Ctrl+O' },
      { key: 'save', labelKey: 'menu.save', action: 'saveFile', shortcut: 'Ctrl+S' },
      { key: 'saveAs', labelKey: 'menu.saveAs', action: 'saveFileAs', shortcut: 'Ctrl+Shift+S' },
      { key: 'divider1', labelKey: '', divider: true },
      { key: 'projectPreferences', labelKey: 'Menu.ProjectPreferences', action: 'openProjectPreferences' },
      { key: 'sceneManager', labelKey: '场景管理', action: 'openSceneManager' },
      { key: 'lorImport', labelKey: 'lorImport.menuImport', action: 'openLorImport' },
      { key: 'divider2', labelKey: '', divider: true },
      { key: 'exit', labelKey: 'menu.exit', action: 'exitApp' },
    ],
  },
  {
    key: 'edit',
    labelKey: 'menu.edit',
    items: [
      { key: 'undo', labelKey: 'menu.undo', action: 'undo', shortcut: 'Ctrl+Z', disabled: !editorStore.canUndo },
      { key: 'redo', labelKey: 'menu.redo', action: 'redo', shortcut: 'Ctrl+Y', disabled: !editorStore.canRedo },
    ],
  },
  {
    key: 'view',
    labelKey: 'menu.view',
    items: [
      { 
        key: 'project', 
        labelKey: 'panels.project', 
        action: 'toggleProject',
        checked: uiStore.showProjectPopup
      },
      { 
        key: 'console', 
        labelKey: 'panels.console', 
        action: 'toggleConsole',
        checked: uiStore.showConsolePanel 
      },
      { key: 'divider1', labelKey: '', divider: true },
      { key: 'fullscreen', labelKey: '全屏', action: 'toggleFullscreen', shortcut: 'F11', checked: Boolean(document.fullscreenElement) },
      { key: 'divider2', labelKey: '', divider: true },
      { key: 'preferences', labelKey: 'menu.preferences', action: 'openPreferences' },
    ],
  },
  {
    key: 'help',
    labelKey: 'menu.help',
    items: [
      { key: 'about', labelKey: 'menu.about', action: 'showAbout' },
    ],
  },
])

function toggleMenu(key: string) {
  activeMenu.value = activeMenu.value === key ? null : key
}

function executeAction(action: string) {
  activeMenu.value = null
  
  switch (action) {
    case 'newFile':
      newGraph()
      break
    case 'openFile':
      // 打开文件：需要通过文件浏览器选择场景 ID 后调用 loadSceneGraph
      // 当前使用默认场景 ID 作为演示，实际应配合文件浏览器组件使用
      const openSceneId = prompt('请输入要打开的场景 ID:')
      if (openSceneId) {
        loadSceneGraph(openSceneId)
      }
      break
    case 'saveFile':
      if (editorStore.currentFileName && editorStore.currentFileName !== 'Untitled') {
        saveSceneGraph(editorStore.currentFileName)
      } else {
        executeAction('saveFileAs')
      }
      break
    case 'saveFileAs': {
      const saveAsName = prompt('请输入文件名:', editorStore.currentFileName || 'Untitled')
      if (saveAsName) {
        editorStore.currentFileName = saveAsName
        saveSceneGraph(saveAsName)
      }
      break
    }
    case 'exitApp':
      quitApplication().finally(() => {
        window.close()
      })
      break
    case 'undo':
      editorStore.undo()
      break
    case 'redo':
      editorStore.redo()
      break
    case 'toggleProject':
      if (uiStore.showProjectPopup) {
        uiStore.closeProjectPopup()
      } else {
        uiStore.openProjectPopup()
      }
      break
    case 'openProjectPreferences':
      uiStore.openProjectPreferences()
      break
    case 'openSceneManager':
      uiStore.openSceneManager()
      break
    case 'openLorImport':
      uiStore.openLorImportDialog()
      break
    case 'toggleConsole':
      uiStore.togglePanel('console')
      break
    case 'toggleFullscreen':
      toggleFullscreen()
      break
    case 'openPreferences':
      // 使用 window.open 打开独立的 Preferences 窗口（Popup）
      // 使用 hash 路径以确保在构建后也能正确路由
        window.open('/Preferences', 'Preferences', 'width=800,height=600,scrollbars=yes,resizable=yes')
      break
    case 'showAbout':
      // 使用 window.open 打开独立的 About 窗口（Popup）
      // 使用 hash 路径以确保在构建后也能正确路由
        window.open('/About', 'About', 'width=600,height=500,scrollbars=yes,resizable=yes')
      break
    default:
      console.log('Action not implemented:', action)
  }
}

function toggleFullscreen() {
  if (document.fullscreenElement) {
    document.exitFullscreen().catch(error => console.warn('Exit fullscreen failed:', error))
  } else {
    document.documentElement.requestFullscreen().catch(error => console.warn('Enter fullscreen failed:', error))
  }
}

function handleClickOutside(event: MouseEvent) {
  const target = event.target as HTMLElement
  if (!target.closest('.menu-bar')) {
    activeMenu.value = null
  }
}

async function refreshProjectName() {
  try {
    const currentProject = await getCurrentProject()
    projectName.value = currentProject.data?.projectName || 'Visunovia'
  } catch {
    projectName.value = 'Visunovia'
  }
}

watch(
  () => nodeGraphStore.currentSceneId,
  () => {
    refreshProjectName()
  }
)

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
  refreshProjectName()
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<style scoped>
.menu-bar {
  position: relative;
  display: flex;
  align-items: center;
  height: 100%;
  padding: 0 8px;
  user-select: none;
}

.menu-title {
  position: absolute;
  left: 50%;
  top: 50%;
  max-width: 52vw;
  transform: translate(-50%, -50%);
  color: #d6d6d6;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  pointer-events: none;
}

.menu-items {
  display: flex;
  gap: 2px;
}

.menu-item {
  position: relative;
  padding: 4px 10px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 13px;
  transition: all 0.1s;
}

.menu-item:hover,
.menu-item.active {
  background: rgba(255, 255, 255, 0.08);
  color: #ffffff;
}

.menu-label {
  white-space: nowrap;
}

.menu-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  min-width: 220px;
  background: #252526;
  border: 1px solid #454545;
  border-radius: 4px;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.4);
  z-index: 1000;
  padding: 4px 0;
}

.menu-dropdown-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 7px 16px;
  color: #cccccc;
  cursor: pointer;
  font-size: 12px;
  transition: background 0.1s;
}

.menu-dropdown-item:hover:not(.disabled) {
  background: #094771;
  color: #ffffff;
}

.menu-dropdown-item.disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.item-label {
  flex: 1;
}

.shortcut {
  color: #808080;
  font-size: 11px;
  margin-left: 24px;
}

.checkmark {
  color: #ffffff;
  font-size: 12px;
  margin-left: 8px;
  width: 16px;
  text-align: center;
}

.menu-divider {
  height: 1px;
  background: #3e3e42;
  margin: 4px 12px;
}
</style>
