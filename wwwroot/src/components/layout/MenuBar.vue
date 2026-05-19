<template>
  <div class="menu-bar">
    <div class="menu-items">
      <div 
        v-for="menu in menus" 
        :key="menu.key" 
        class="menu-item"
        @click="toggleMenu(menu.key)"
      >
        <span class="menu-label">{{ t(menu.labelKey) }}</span>
        <div v-if="activeMenu === menu.key" class="menu-dropdown">
          <template v-for="item in menu.items" :key="item.key">
            <div v-if="item.divider" class="menu-divider"></div>
            <div 
              v-else
              class="menu-dropdown-item"
              @click.stop="executeAction(item.action!)"
            >
              <span>{{ t(item.labelKey) }}</span>
              <span v-if="item.shortcut" class="shortcut">{{ item.shortcut }}</span>
            </div>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

interface MenuItem {
  key: string
  labelKey: string
  action?: string
  shortcut?: string
  divider?: boolean
}

interface Menu {
  key: string
  labelKey: string
  items: MenuItem[]
}

const { t } = useLocalization()

const activeMenu = ref<string | null>(null)

const menus: Menu[] = [
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
      { key: 'undo', labelKey: 'menu.undo', action: 'undo', shortcut: 'Ctrl+Z' },
      { key: 'redo', labelKey: 'menu.redo', action: 'redo', shortcut: 'Ctrl+Y' },
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
]

function toggleMenu(key: string) {
  activeMenu.value = activeMenu.value === key ? null : key
}

function executeAction(action: string) {
  activeMenu.value = null
  
  switch (action) {
    case 'saveFile':
      console.log('Save file action')
      break
    case 'undo':
      console.log('Undo action')
      break
    case 'redo':
      console.log('Redo action')
      break
    case 'openPreferences':
      console.log('Open preferences')
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
}

.menu-items {
  display: flex;
  gap: 4px;
}

.menu-item {
  position: relative;
  padding: 4px 12px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
}

.menu-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.menu-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 200px;
  background: #252526;
  border: 1px solid #3e3e42;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  z-index: 1000;
}

.menu-dropdown-item {
  display: flex;
  justify-content: space-between;
  padding: 8px 16px;
  color: #cccccc;
  cursor: pointer;
}

.menu-dropdown-item:hover {
  background: #094771;
}

.menu-divider {
  height: 1px;
  background: #3e3e42;
  margin: 4px 0;
}

.shortcut {
  color: #808080;
  font-size: 12px;
}
</style>
