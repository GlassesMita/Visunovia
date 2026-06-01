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
    
    <!-- 自定义输入对话框 (替代原生 prompt) -->
    <Teleport to="body">
      <div v-if="showInputDialog" class="input-dialog-overlay" @click.self="handleInputCancel">
        <div class="input-dialog">
          <div class="input-dialog-header">
            <h3>{{ inputDialogTitle }}</h3>
          </div>
          <div class="input-dialog-body">
            <input 
              ref="inputRef"
              v-model="inputDialogDefaultValue"
              type="text"
              :placeholder="inputDialogPlaceholder"
              @keyup.enter="handleInputConfirm(inputDialogDefaultValue)"
              @keyup.escape="handleInputCancel"
            />
          </div>
          <div class="input-dialog-footer">
            <button class="cancel-btn" @click="handleInputCancel">取消</button>
            <button class="confirm-btn" @click="handleInputConfirm(inputDialogDefaultValue)">确定</button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeOperations } from '@/composables/useNodeOperations'

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
const { saveSceneGraph, loadSceneGraph, newGraph } = useNodeOperations()

const activeMenu = ref<string | null>(null)

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
      { key: 'exit', labelKey: 'menu.exit', action: 'exitApp' },
    ],
  },
  {
    key: 'edit',
    labelKey: 'menu.edit',
    items: [
      { key: 'undo', labelKey: 'menu.undo', action: 'undo', shortcut: 'Ctrl+Z', disabled: !editorStore.canUndo },
      { key: 'redo', labelKey: 'menu.redo', action: 'redo', shortcut: 'Ctrl+Y', disabled: !editorStore.canRedo },
      { key: 'divider1', labelKey: '', divider: true },
      { key: 'cut', labelKey: 'menu.cut', action: 'cut', shortcut: 'Ctrl+X' },
      { key: 'copy', labelKey: 'menu.copy', action: 'copy', shortcut: 'Ctrl+C' },
      { key: 'paste', labelKey: 'menu.paste', action: 'paste', shortcut: 'Ctrl+V' },
      { key: 'delete', labelKey: 'menu.delete', action: 'delete', shortcut: 'Del' },
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
        key: 'inspector', 
        labelKey: 'panels.inspector', 
        action: 'toggleInspector',
        checked: uiStore.showInspectorPanel 
      },
      { 
        key: 'hierarchy', 
        labelKey: 'panels.hierarchy', 
        action: 'toggleHierarchy',
        checked: uiStore.showHierarchyPanel 
      },
      { 
        key: 'console', 
        labelKey: 'panels.console', 
        action: 'toggleConsole',
        checked: uiStore.showConsolePanel 
      },
      { key: 'divider1', labelKey: '', divider: true },
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

const showInputDialog = ref(false)
const inputDialogTitle = ref('')
const inputDialogPlaceholder = ref('')
const inputDialogDefaultValue = ref('')
const inputDialogCallback = ref<((value: string | null) => void) | null>(null)

function openInputDialog(title: string, placeholder: string, defaultValue: string, callback: (value: string | null) => void) {
  inputDialogTitle.value = title
  inputDialogPlaceholder.value = placeholder
  inputDialogDefaultValue.value = defaultValue
  inputDialogCallback.value = callback
  showInputDialog.value = true
}

function handleInputConfirm(value: string) {
  showInputDialog.value = false
  if (inputDialogCallback.value) {
    inputDialogCallback.value(value)
    inputDialogCallback.value = null
  }
}

function handleInputCancel() {
  showInputDialog.value = false
  if (inputDialogCallback.value) {
    inputDialogCallback.value(null)
    inputDialogCallback.value = null
  }
}

function executeAction(action: string) {
  activeMenu.value = null
  
  switch (action) {
    case 'newFile':
      uiStore.openNewProjectExplorer()
      break
    case 'openFile':
      uiStore.openFileExplorer()
      break
    case 'saveFile':
      if (editorStore.currentFileName && editorStore.currentFileName !== 'Untitled') {
        saveSceneGraph(editorStore.currentFileName)
      } else {
        executeAction('saveFileAs')
      }
      break
    case 'saveFileAs':
      openInputDialog('另存为', '请输入文件名', editorStore.currentFileName || 'Untitled', (fileName) => {
        if (fileName) {
          editorStore.currentFileName = fileName
          saveSceneGraph(fileName)
        }
      })
      break
    case 'exitApp':
      console.log('Exit app')
      break
    case 'undo':
      editorStore.undo()
      break
    case 'redo':
      editorStore.redo()
      break
    case 'cut':
      console.log('Cut action')
      break
    case 'copy':
      console.log('Copy action')
      break
    case 'paste':
      console.log('Paste action')
      break
    case 'delete':
      console.log('Delete action')
      break
    case 'toggleProject':
      if (uiStore.showProjectPopup) {
        uiStore.closeProjectPopup()
      } else {
        uiStore.openProjectPopup()
      }
      break
    case 'toggleInspector':
      uiStore.togglePanel('inspector')
      break
    case 'toggleHierarchy':
      uiStore.togglePanel('hierarchy')
      break
    case 'toggleConsole':
      uiStore.togglePanel('console')
      break
    case 'openPreferences':
      // 使用 window.open 打开独立的 Preferences 窗口（Popup）
      window.open('/Preferences', 'Preferences', 'width=800,height=600,scrollbars=yes,resizable=yes')
      break
    case 'showAbout':
      // 使用 window.open 打开独立的 About 窗口（Popup）
      window.open('/About', 'About', 'width=600,height=500,scrollbars=yes,resizable=yes')
      break
    default:
      console.log('Action not implemented:', action)
  }
}

function handleClickOutside(event: MouseEvent) {
  const target = event.target as HTMLElement
  if (!target.closest('.menu-bar')) {
    activeMenu.value = null
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<style scoped>
.menu-bar {
  display: flex;
  align-items: center;
  height: 100%;
  padding: 0 8px;
  user-select: none;
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

/* 输入对话框样式 */
.input-dialog-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.input-dialog {
  background: #252526;
  border: 1px solid #454545;
  border-radius: 6px;
  width: 360px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
}

.input-dialog-header {
  padding: 16px 20px 12px;
  border-bottom: 1px solid #3e3e42;
}

.input-dialog-header h3 {
  margin: 0;
  font-size: 14px;
  color: #ffffff;
  font-weight: 500;
}

.input-dialog-body {
  padding: 20px;
}

.input-dialog-body input {
  width: 100%;
  padding: 10px 12px;
  background: #3c3c3c;
  border: 1px solid #555;
  border-radius: 4px;
  color: #ffffff;
  font-size: 14px;
  box-sizing: border-box;
}

.input-dialog-body input:focus {
  outline: none;
  border-color: #0078d4;
}

.input-dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 12px 20px 16px;
}

.cancel-btn,
.confirm-btn {
  padding: 8px 20px;
  border-radius: 4px;
  font-size: 13px;
  cursor: pointer;
  border: none;
}

.cancel-btn {
  background: #3c3c3c;
  color: #cccccc;
}

.cancel-btn:hover {
  background: #4a4a4a;
}

.confirm-btn {
  background: #0078d4;
  color: #ffffff;
}

.confirm-btn:hover {
  background: #1084d8;
}
</style>
