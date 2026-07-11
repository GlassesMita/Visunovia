<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { getDrives, getEntries, getSpecialFolders, createFolder } from '@/api/fileBrowser'
import type { DriveInfo, DirEntry, SpecialFolder } from '@/api/fileBrowser'
import { resolveBackendUrl } from '@/utils/backendUrl'

const props = defineProps<{
  visible: boolean
  fileFilter?: string[]
  title?: string
  allowSelectDirectory?: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'select', path: string, isDirectory: boolean): void
}>()

const drives = ref<DriveInfo[]>([])
const specialFolders = ref<SpecialFolder[]>([])
const currentPath = ref('')
const entries = ref<DirEntry[]>([])
const selectedEntry = ref<DirEntry | null>(null)
const isLoading = ref(false)
const error = ref<string | null>(null)
const isCreatingFolder = ref(false)
const newFolderName = ref('')
const newFolderInputRef = ref<HTMLInputElement | null>(null)
const folderCreated = ref(false)
const isEditingAddress = ref(false)
const addressInputValue = ref('')
const addressInputRef = ref<HTMLInputElement | null>(null)

// Image preview state
const previewEntry = ref<DirEntry | null>(null)
const previewPosition = ref({ x: 0, y: 0 })
const previewVisible = ref(false)
const previewTimeout = ref<ReturnType<typeof setTimeout> | null>(null)
const previewLoaded = ref(false)
const previewError = ref(false)

const imageExtensions = new Set(['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.svg', '.ico', '.tga', '.dds'])

const title = computed(() => props.title ?? '打开项目')
const fileFilter = computed(() => props.fileFilter ?? [])
const allowSelectDirectory = computed(() => props.allowSelectDirectory ?? false)

const filteredEntries = computed(() => {
  return [...entries.value].sort((a, b) => {
    if (a.isDirectory !== b.isDirectory) return a.isDirectory ? -1 : 1
    return a.name.localeCompare(b.name)
  })
})

const canSelectEntry = (entry: DirEntry): boolean => {
  if (entry.isDirectory) return true
  if (fileFilter.value.length === 0) return true
  return fileFilter.value.includes(entry.extension.toLowerCase())
}

const canConfirm = computed(() => {
  if (!selectedEntry.value) return false
  if (selectedEntry.value.isDirectory) return allowSelectDirectory.value
  return canSelectEntry(selectedEntry.value)
})

const isComputerView = computed(() => !currentPath.value)

const breadcrumbs = computed(() => {
  if (!currentPath.value) return []
  const parts = currentPath.value.split(/[/\\]/).filter(Boolean)
  const crumbs: { name: string; path: string }[] = []
  let path = ''
  for (const part of parts) {
    path = path ? `${path}\\${part}` : part
    crumbs.push({ name: part, path })
  }
  return crumbs
})

const folderCount = computed(() => filteredEntries.value.filter(e => e.isDirectory).length)
const fileCount = computed(() => filteredEntries.value.filter(e => !e.isDirectory).length)

async function loadDrives() {
  try {
    isLoading.value = true
    error.value = null
    const [drivesResult, foldersResult] = await Promise.all([
      getDrives(),
      getSpecialFolders()
    ])
    drives.value = drivesResult
    specialFolders.value = foldersResult
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载驱动器失败'
  } finally {
    isLoading.value = false
  }
}

async function navigateTo(path: string) {
  currentPath.value = ''
  selectedEntry.value = null
  if (!path) {
    entries.value = []
    return
  }
  try {
    isLoading.value = true
    error.value = null
    const result = await getEntries(path)
    currentPath.value = result.currentPath || path
    entries.value = result.entries || []
    selectedEntry.value = null
  } catch (e) {
    error.value = e instanceof Error ? e.message : '无法访问该目录'
  } finally {
    isLoading.value = false
  }
}

async function navigateUp() {
  if (!currentPath.value) return
  const parts = currentPath.value.split(/[/\\]/).filter(Boolean)
  if (parts.length <= 1) {
    currentPath.value = ''
    entries.value = []
    return
  }
  parts.pop()
  await navigateTo(parts.join('\\'))
}

function handleDriveClick(drive: DriveInfo) {
  navigateTo(drive.letter + '\\')
}

function handleComputerItemClick(item: { type: 'drive'; drive: DriveInfo } | { type: 'folder'; folder: SpecialFolder }) {
  if (item.type === 'drive') {
    handleDriveClick(item.drive)
  } else {
    handleSpecialFolderClick(item.folder)
  }
}

function handleSpecialFolderClick(folder: SpecialFolder) {
  if (folder.path) {
    // 快速访问：仅导航到目标文件夹，不触发项目打开
    navigateTo(folder.path)
  }
}

function handleEntryClick(entry: DirEntry) {
  if (!canSelectEntry(entry)) return
  selectedEntry.value = entry
}

function handleEntryDblClick(entry: DirEntry) {
  if (entry.isDirectory) {
    navigateTo(currentPath.value ? `${currentPath.value}\\${entry.name}` : entry.name)
  } else {
    if (!canSelectEntry(entry)) return
    handleSelect()
  }
}

function handleSelect() {
  if (!selectedEntry.value) return
  const fullPath = currentPath.value ? `${currentPath.value}\\${selectedEntry.value.name}` : selectedEntry.value.name
  emit('select', fullPath, selectedEntry.value.isDirectory)
}

function handleCancel() {
  emit('close')
}

function handleBreadcrumbClick(crumb: { name: string; path: string }) {
  navigateTo(crumb.path)
}

function handleAddressBarClick() {
  if (isEditingAddress.value) return
  addressInputValue.value = currentPath.value
  isEditingAddress.value = true
  nextTick(() => {
    addressInputRef.value?.focus()
    addressInputRef.value?.select()
  })
}

function handleAddressGo() {
  if (isEditingAddress.value) {
    confirmAddressEdit()
  } else {
    navigateTo(currentPath.value)
  }
}

function confirmAddressEdit() {
  const rawPath = addressInputValue.value.trim()
  if (!rawPath) {
    isEditingAddress.value = false
    return
  }
  // Normalize: replace / with \, collapse multiple backslashes
  const normalized = rawPath.replace(/\//g, '\\').replace(/\\{2,}/g, '\\')
  isEditingAddress.value = false
  navigateTo(normalized)
}

function cancelAddressEdit() {
  isEditingAddress.value = false
  addressInputValue.value = currentPath.value
}

function handleAddressKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter') {
    event.preventDefault()
    confirmAddressEdit()
  } else if (event.key === 'Escape') {
    event.preventDefault()
    cancelAddressEdit()
  }
}

function handleNewFolderInput() {
  isCreatingFolder.value = true
  newFolderName.value = ''
  nextTick(() => {
    newFolderInputRef.value?.focus()
  })
}

async function confirmCreateFolder() {
  const name = newFolderName.value.trim()
  if (!name) {
    cancelCreateFolder()
    return
  }
  try {
    const parentPath = currentPath.value || ''
    const result = await createFolder(parentPath, name)
    folderCreated.value = true
    setTimeout(() => { folderCreated.value = false }, 2000)
    if (result?.path) {
      // 新建文件夹后选中该文件夹，而不是进入
      selectedEntry.value = null
      entries.value = []
      currentPath.value = parentPath
      emit('select', result.path, true)
    } else if (parentPath) {
      await navigateTo(parentPath)
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : '创建文件夹失败'
  }
  cancelCreateFolder()
}

function cancelCreateFolder() {
  isCreatingFolder.value = false
  newFolderName.value = ''
}

function formatSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes <= 0) return '未知'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let unitIndex = 0
  let size = bytes
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex++
  }
  return `${size.toFixed(unitIndex > 0 ? 1 : 0)} ${units[unitIndex]}`
}

function getDriveDisplayName(drive: DriveInfo): string {
  const name = drive.name.trim()
  return !name || name.toUpperCase() === drive.letter.toUpperCase()
    ? drive.letter
    : `${name} (${drive.letter})`
}

function getDriveSpaceText(drive: DriveInfo): string {
  const capacity = `${formatSize(drive.freeSpace)} 可用，共 ${formatSize(drive.totalSpace)}`
  return drive.fileSystem ? `${drive.fileSystem} - ${capacity}` : capacity
}

function formatDate(dateStr: string): string {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function getDriveUsagePercent(drive: DriveInfo): number {
  if (drive.totalSpace === 0) return 0
  return Math.round(((drive.totalSpace - drive.freeSpace) / drive.totalSpace) * 100)
}

function getFileExtension(entry: DirEntry): string {
  if (entry.isDirectory) return '文件夹'
  if (!entry.extension) return '文件'
  return `${entry.extension.toUpperCase().replace('.', '')} 文件`
}

function isImageFile(entry: DirEntry): boolean {
  if (entry.isDirectory) return false
  return imageExtensions.has(entry.extension.toLowerCase())
}

function handlePreviewEnter(entry: DirEntry, event: MouseEvent) {
  if (!isImageFile(entry)) {
    handlePreviewLeave()
    return
  }
  previewEntry.value = entry
  previewPosition.value = { x: event.clientX, y: event.clientY }
  previewLoaded.value = false
  previewError.value = false
  previewTimeout.value = setTimeout(() => {
    previewVisible.value = true
  }, 400)
}

function handlePreviewMove(event: MouseEvent) {
  previewPosition.value = { x: event.clientX, y: event.clientY }
}

function handlePreviewLeave() {
  if (previewTimeout.value) {
    clearTimeout(previewTimeout.value)
    previewTimeout.value = null
  }
  previewVisible.value = false
  previewEntry.value = null
  previewLoaded.value = false
  previewError.value = false
}

function getPreviewUrl(entry: DirEntry): string {
  // Convert file path to a URL that can be served by the backend
  return resolveBackendUrl(`/api/FileBrowser/preview?path=${encodeURIComponent(entry.path)}`)
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    if (isCreatingFolder.value) {
      cancelCreateFolder()
    } else {
      handleCancel()
    }
  }
}

function handleKeyDown(event: KeyboardEvent) {
  if (isCreatingFolder.value) return
  if (event.key === 'Backspace') {
    event.preventDefault()
    navigateUp()
  }
  if (event.key === 'Enter' && selectedEntry.value) {
    handleEntryDblClick(selectedEntry.value)
  }
}

onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown)
})

watch(() => props.visible, (newVal) => {
  if (newVal) {
    loadDrives()
    currentPath.value = ''
    entries.value = []
    selectedEntry.value = null
    error.value = null
    isCreatingFolder.value = false
    newFolderName.value = ''
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition name="fb-modal">
      <div v-if="visible" class="fb-modal-overlay" @click.self="handleCancel">
        <div class="fb-window" @keydown="handleKeyDown" tabindex="-1">
        <!-- Title Bar -->
        <div class="fb-titlebar">
          <div class="fb-titlebar-icon">
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M1 3.5C1 2.67 1.67 2 2.5 2H6L7.5 3.5H13.5C14.33 3.5 15 4.17 15 5V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V3.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.5"/><path d="M1 6H15V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V6Z" fill="#FFD54F" stroke="#E0A800" stroke-width="0.5"/></svg>
          </div>
          <span class="fb-titlebar-text">{{ title }}</span>
          <div class="fb-titlebar-buttons">
            <button class="fb-titlebar-btn fb-titlebar-close" @click="handleCancel" title="关闭">
              <svg width="10" height="10" viewBox="0 0 10 10"><path d="M1 1L9 9M9 1L1 9" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/></svg>
            </button>
          </div>
        </div>

        <!-- Toolbar -->
        <div class="fb-toolbar">
          <div class="fb-toolbar-left">
            <button class="fb-toolbar-btn" @click="navigateUp" :disabled="!currentPath" title="向上 (Backspace)">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M10 12L6 8L10 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/><path d="M6 8H14" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
              <span>向上</span>
            </button>
            <div class="fb-toolbar-separator"></div>
            <button class="fb-toolbar-btn" @click="handleNewFolderInput" title="新建文件夹">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M1 4.5C1 3.67 1.67 3 2.5 3H6L7.5 4.5H13.5C14.33 4.5 15 5.17 15 6V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V4.5Z" stroke="currentColor" stroke-width="1.2"/><path d="M8 7V11M6 9H10" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/></svg>
              <span>新建文件夹</span>
            </button>
          </div>
          <div class="fb-toolbar-right">
            <button class="fb-toolbar-btn fb-toolbar-btn-icon" title="视图选项">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><rect x="1" y="2" width="14" height="3" rx="0.5" stroke="currentColor" stroke-width="1"/><rect x="1" y="6.5" width="14" height="3" rx="0.5" stroke="currentColor" stroke-width="1"/><rect x="1" y="11" width="14" height="3" rx="0.5" stroke="currentColor" stroke-width="1"/></svg>
            </button>
          </div>
        </div>

        <!-- Address Bar -->
        <div class="fb-addressbar">
          <span class="fb-addressbar-label">地址</span>
          <div class="fb-addressbar-combo" :class="{ 'fb-addressbar-editing': isEditingAddress }">
            <template v-if="isEditingAddress">
              <input
                ref="addressInputRef"
                type="text"
                class="fb-addressbar-input"
                v-model="addressInputValue"
                @keydown="handleAddressKeydown"
                @blur="cancelAddressEdit"
              />
            </template>
            <template v-else>
              <div class="fb-addressbar-path" @click="handleAddressBarClick">
                <template v-if="currentPath">
                  <span
                    v-for="(crumb, idx) in breadcrumbs"
                    :key="crumb.path"
                    class="fb-breadcrumb"
                  >
                    <span class="fb-breadcrumb-text" @click.stop="handleBreadcrumbClick(crumb)">{{ crumb.name }}</span>
                    <svg v-if="idx < breadcrumbs.length - 1" class="fb-breadcrumb-arrow" width="8" height="8" viewBox="0 0 8 8"><path d="M3 1.5L5.5 4L3 6.5" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/></svg>
                  </span>
                </template>
                <span v-else class="fb-breadcrumb-text fb-breadcrumb-root" @click.stop="navigateTo('')">此电脑</span>
              </div>
            </template>
            <button class="fb-addressbar-go" @click="handleAddressGo" title="转到">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M9 7L4 10.5V3.5L9 7Z" fill="currentColor"/></svg>
            </button>
          </div>
        </div>

        <!-- Main Content -->
        <div class="fb-content">
          <!-- Navigation Pane (Sidebar) -->
          <div class="fb-navpane">
            <div class="fb-navpane-section">
              <div class="fb-navpane-header">
                <svg class="fb-navpane-arrow" width="10" height="10" viewBox="0 0 10 10"><path d="M3 2L7 5L3 8" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/></svg>
                <span>快速访问</span>
              </div>
              <div class="fb-navpane-items">
                <div
                  v-for="folder in specialFolders"
                  :key="folder.id"
                  class="fb-navpane-item"
                  :class="{ active: currentPath === folder.path }"
                  @click="handleSpecialFolderClick(folder)"
                >
                  <svg class="fb-navpane-icon" width="16" height="16" viewBox="0 0 16 16" fill="none">
                    <path d="M1 4.5C1 3.67 1.67 3 2.5 3H6L7.5 4.5H13.5C14.33 4.5 15 5.17 15 6V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V4.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.5"/>
                  </svg>
                  <span>{{ folder.name }}</span>
                </div>
              </div>
            </div>
            <div class="fb-navpane-section">
              <div class="fb-navpane-header">
                <svg class="fb-navpane-arrow" width="10" height="10" viewBox="0 0 10 10"><path d="M3 2L7 5L3 8" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"/></svg>
                <span>此电脑</span>
              </div>
              <div class="fb-navpane-items">
                <div
                  v-for="drive in drives"
                  :key="drive.letter"
                  class="fb-navpane-item"
                  :class="{ active: currentPath.toUpperCase().startsWith(drive.letter.toUpperCase()) }"
                  @click="handleDriveClick(drive)"
                  :title="`${drive.name} (${drive.letter}) - ${drive.fileSystem}`"
                >
                  <svg class="fb-navpane-icon" width="16" height="16" viewBox="0 0 16 16" fill="none">
                    <rect x="1" y="5" width="14" height="8" rx="1" fill="#90A4AE" stroke="#607D8B" stroke-width="0.5"/>
                    <rect x="3" y="7" width="4" height="2" rx="0.5" fill="#B0BEC5"/>
                    <rect x="10" y="7" width="3" height="2" rx="0.5" fill="#607D8B"/>
                    <rect x="12" y="10" width="1" height="1" rx="0.3" fill="#4CAF50"/>
                  </svg>
                  <div class="fb-navpane-item-info">
                    <span class="fb-navpane-item-name">{{ getDriveDisplayName(drive) }}</span>
                    <div class="fb-drive-bar">
                      <div class="fb-drive-bar-fill" :style="{ width: getDriveUsagePercent(drive) + '%' }"></div>
                    </div>
                    <span class="fb-drive-space">{{ formatSize(drive.freeSpace) }} 可用，共 {{ formatSize(drive.totalSpace) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- File List -->
          <div class="fb-filepane">
            <!-- Column Headers (hidden in computer view) -->
            <div v-if="!isComputerView" class="fb-column-headers">
              <div class="fb-col-header fb-col-name" @click="navigateTo(currentPath)">
                <span>名称</span>
              </div>
              <div class="fb-col-header fb-col-date">
                <span>修改日期</span>
              </div>
              <div class="fb-col-header fb-col-type">
                <span>类型</span>
              </div>
              <div class="fb-col-header fb-col-size">
                <span>大小</span>
              </div>
            </div>

            <!-- File List Content -->
            <div class="fb-file-list">
              <!-- This Computer View -->
              <template v-if="isComputerView">
                <!-- Quick Access Section -->
                <div v-if="specialFolders.length > 0" class="fb-computer-section">
                  <div class="fb-computer-section-header">快速访问</div>
                  <div
                    v-for="folder in specialFolders"
                    :key="'sf-' + folder.id"
                    class="fb-computer-item"
                    @click="handleComputerItemClick({ type: 'folder', folder })"
                  >
                    <svg class="fb-computer-item-icon" width="24" height="24" viewBox="0 0 20 20" fill="none">
                      <path d="M1 5.5C1 4.67 1.67 4 2.5 4H7.5L9.5 6H17.5C18.33 6 19 6.67 19 7.5V15.5C19 16.33 18.33 17 17.5 17H2.5C1.67 17 1 16.33 1 15.5V5.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.7"/>
                    </svg>
                    <div class="fb-computer-item-info">
                      <span class="fb-computer-item-name">{{ folder.name }}</span>
                      <span class="fb-computer-item-path">{{ folder.path }}</span>
                    </div>
                  </div>
                </div>
                <!-- Drives Section -->
                <div v-if="drives.length > 0" class="fb-computer-section">
                  <div class="fb-computer-section-header">驱动器</div>
                  <div
                    v-for="drive in drives"
                    :key="'dr-' + drive.letter"
                    class="fb-computer-item"
                    @click="handleComputerItemClick({ type: 'drive', drive })"
                  >
                    <svg class="fb-computer-item-icon" width="24" height="24" viewBox="0 0 20 20" fill="none">
                      <rect x="1" y="6" width="18" height="10" rx="1.5" fill="#90A4AE" stroke="#607D8B" stroke-width="0.7"/>
                      <rect x="3" y="8" width="5" height="3" rx="0.5" fill="#B0BEC5"/>
                      <rect x="12" y="8" width="5" height="3" rx="0.5" fill="#607D8B"/>
                      <rect x="15" y="12" width="1.5" height="1.5" rx="0.3" fill="#4CAF50"/>
                    </svg>
                    <div class="fb-computer-item-info">
                      <span class="fb-computer-item-name">{{ getDriveDisplayName(drive) }}</span>
                      <div class="fb-computer-drive-bar">
                        <div class="fb-computer-drive-bar-fill" :style="{ width: getDriveUsagePercent(drive) + '%' }"></div>
                      </div>
                      <span class="fb-computer-drive-space">{{ getDriveSpaceText(drive) }}</span>
                    </div>
                  </div>
                </div>
                <div v-else-if="!isLoading" class="fb-empty">
                  <svg width="48" height="48" viewBox="0 0 48 48" fill="none"><path d="M6 8C6 6.34 7.34 5 9 5H18L22 9H41C42.66 9 44 10.34 44 12V34C44 35.66 42.66 37 41 37H9C7.34 37 6 35.66 6 34V8Z" fill="#2a2a2a" stroke="#444" stroke-width="1"/></svg>
                  <span>没有可用的驱动器</span>
                </div>
              </template>
              <!-- Normal Folder View -->
              <template v-else>
                <div v-if="isLoading" class="fb-loading">
                  <div class="fb-loading-spinner"></div>
                  <span>正在加载...</span>
                </div>
                <div v-else-if="error" class="fb-error">
                  <svg width="32" height="32" viewBox="0 0 32 32" fill="none"><circle cx="16" cy="16" r="14" stroke="#f44336" stroke-width="2"/><path d="M16 8V18M16 22V24" stroke="#f44336" stroke-width="2" stroke-linecap="round"/></svg>
                  <span>{{ error }}</span>
                </div>
                <div v-else-if="filteredEntries.length === 0" class="fb-empty">
                  <svg width="48" height="48" viewBox="0 0 48 48" fill="none"><path d="M4 12C4 10.34 5.34 9 7 9H16L20 13H41C42.66 13 44 14.34 44 16V38C44 39.66 42.66 41 41 41H7C5.34 41 4 39.66 4 38V12Z" fill="#2a2a2a" stroke="#444" stroke-width="1"/></svg>
                  <span>此文件夹为空</span>
                </div>
                <div
                  v-else
                  v-for="entry in filteredEntries"
                  :key="entry.name"
                  class="fb-file-item"
                  :class="{
                    selected: selectedEntry?.name === entry.name,
                    'fb-file-item--disabled': !canSelectEntry(entry),
                    'fb-file-item--image': isImageFile(entry)
                  }"
                  @click="handleEntryClick(entry)"
                  @dblclick="handleEntryDblClick(entry)"
                  @mouseenter="handlePreviewEnter(entry, $event)"
                  @mousemove="handlePreviewMove($event)"
                  @mouseleave="handlePreviewLeave"
                >
                  <div class="fb-file-col fb-col-name">
                    <div class="fb-file-icon-wrap">
                      <svg v-if="entry.isDirectory" class="fb-file-icon-svg" width="20" height="20" viewBox="0 0 20 20" fill="none">
                        <path d="M1 5.5C1 4.67 1.67 4 2.5 4H7.5L9.5 6H17.5C18.33 6 19 6.67 19 7.5V15.5C19 16.33 18.33 17 17.5 17H2.5C1.67 17 1 16.33 1 15.5V5.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.7"/>
                      </svg>
                      <svg v-else class="fb-file-icon-svg" width="20" height="20" viewBox="0 0 20 20" fill="none">
                        <path d="M4 1C3.45 1 3 1.45 3 2V18C3 18.55 3.45 19 4 19H16C16.55 19 17 18.55 17 18V6L13 1H4Z" fill="#90CAF9" stroke="#42A5F5" stroke-width="0.7"/>
                        <path d="M13 1V5C13 5.55 13.45 6 14 6H17" stroke="#42A5F5" stroke-width="0.7"/>
                        <rect x="6" y="9" width="8" height="1" rx="0.5" fill="#42A5F5" opacity="0.5"/>
                        <rect x="6" y="11.5" width="8" height="1" rx="0.5" fill="#42A5F5" opacity="0.5"/>
                        <rect x="6" y="14" width="5" height="1" rx="0.5" fill="#42A5F5" opacity="0.5"/>
                      </svg>
                    </div>
                    <span class="fb-file-name-text">{{ entry.name }}</span>
                  </div>
                  <div class="fb-file-col fb-col-date">
                    <span>{{ formatDate(entry.lastModified) }}</span>
                  </div>
                  <div class="fb-file-col fb-col-type">
                    <span>{{ getFileExtension(entry) }}</span>
                  </div>
                  <div class="fb-file-col fb-col-size">
                    <span>{{ entry.isDirectory ? '' : formatSize(entry.size) }}</span>
                  </div>
                </div>
              </template>
            </div>

            <!-- New Folder Inline Input -->
            <div v-if="isCreatingFolder" class="fb-new-folder-inline">
              <svg class="fb-file-icon-svg" width="20" height="20" viewBox="0 0 20 20" fill="none">
                <path d="M1 5.5C1 4.67 1.67 4 2.5 4H7.5L9.5 6H17.5C18.33 6 19 6.67 19 7.5V15.5C19 16.33 18.33 17 17.5 17H2.5C1.67 17 1 16.33 1 15.5V5.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.7"/>
              </svg>
              <input
                ref="newFolderInputRef"
                type="text"
                class="fb-new-folder-input"
                v-model="newFolderName"
                placeholder="新建文件夹"
                @keydown.enter="confirmCreateFolder"
                @keydown.escape="cancelCreateFolder"
              />
            </div>
          </div>
        </div>

        <!-- Status Bar -->
        <div class="fb-statusbar">
          <span v-if="folderCreated" class="fb-statusbar-msg fb-statusbar-success">
            ✓ 文件夹已创建
          </span>
          <span v-else-if="isComputerView" class="fb-statusbar-msg">
            {{ drives.length }} 个驱动器
          </span>
          <span v-else-if="filteredEntries.length > 0" class="fb-statusbar-msg">
            {{ folderCount }} 个文件夹，{{ fileCount }} 个文件
          </span>
          <span v-else class="fb-statusbar-msg">就绪</span>
        </div>

        <!-- Image Preview Popup -->
        <div
          v-if="previewVisible && previewEntry"
          class="fb-preview-popup"
          :style="{
            left: (previewPosition.x + 16) + 'px',
            top: (previewPosition.y - 20) + 'px'
          }"
        >
          <div class="fb-preview-box">
            <div v-if="!previewLoaded && !previewError" class="fb-preview-loading">
              <div class="fb-preview-spinner"></div>
            </div>
            <div v-if="previewError" class="fb-preview-error">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
                <circle cx="12" cy="12" r="10" stroke="#f44336" stroke-width="2"/>
                <path d="M12 7V13M12 15V16" stroke="#f44336" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <span>无法预览</span>
            </div>
            <img
              v-show="previewLoaded"
              :src="getPreviewUrl(previewEntry)"
              class="fb-preview-image"
              @load="previewLoaded = true"
              @error="previewError = true"
            />
            <div class="fb-preview-name">{{ previewEntry.name }}</div>
          </div>
        </div>

        <!-- Dialog Buttons -->
        <div class="fb-dialog-footer">
          <button class="fb-btn-cancel" @click="handleCancel">取消</button>
          <button class="fb-btn-confirm" @click="handleSelect" :disabled="!canConfirm">确定</button>
        </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
/* ========== Modal Overlay ========== */
.fb-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.6);
  z-index: 9998;
  display: flex;
  justify-content: center;
  align-items: center;
  backdrop-filter: blur(2px);
}

/* ========== Window Frame ========== */
.fb-window {
  width: 900px;
  height: 600px;
  background: #1e1e1e;
  border: 1px solid #3c3c3c;
  border-radius: 8px 8px 0 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.6), 0 0 0 1px rgba(255, 255, 255, 0.05);
  font-family: 'Segoe UI', 'Microsoft YaHei', system-ui, -apple-system, sans-serif;
  font-size: 12px;
  color: #cccccc;
  outline: none;
  user-select: none;
  -webkit-user-select: none;
  -moz-user-select: none;
  -ms-user-select: none;
}

/* ========== Title Bar ========== */
.fb-titlebar {
  height: 32px;
  background: #2d2d2d;
  display: flex;
  align-items: center;
  padding: 0 8px;
  flex-shrink: 0;
  user-select: none;
  -webkit-user-select: none;
}

.fb-titlebar-icon {
  width: 16px;
  height: 16px;
  margin-right: 8px;
  flex-shrink: 0;
}

.fb-titlebar-text {
  flex: 1;
  font-size: 12px;
  color: #cccccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.fb-titlebar-buttons {
  display: flex;
  flex-shrink: 0;
}

.fb-titlebar-btn {
  width: 46px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: none;
  border: none;
  color: #cccccc;
  cursor: pointer;
  transition: background 0.1s;
}

.fb-titlebar-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.fb-titlebar-close:hover {
  background: #e81123 !important;
  color: #fff;
}

/* ========== Toolbar ========== */
.fb-toolbar {
  height: 38px;
  background: #2d2d2d;
  border-bottom: 1px solid #3c3c3c;
  display: flex;
  align-items: center;
  padding: 0 8px;
  flex-shrink: 0;
  gap: 4px;
}

.fb-toolbar-left {
  display: flex;
  align-items: center;
  gap: 2px;
}

.fb-toolbar-right {
  margin-left: auto;
  display: flex;
  align-items: center;
}

.fb-toolbar-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  background: none;
  border: 1px solid transparent;
  color: #cccccc;
  cursor: pointer;
  border-radius: 3px;
  font-size: 12px;
  font-family: inherit;
  transition: background 0.1s;
  height: 28px;
}

.fb-toolbar-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.1);
}

.fb-toolbar-btn:active:not(:disabled) {
  background: rgba(255, 255, 255, 0.12);
}

.fb-toolbar-btn:disabled {
  opacity: 0.4;
  cursor: default;
}

.fb-toolbar-btn-icon {
  padding: 4px;
  width: 28px;
  justify-content: center;
}

.fb-toolbar-separator {
  width: 1px;
  height: 20px;
  background: #555;
  margin: 0 4px;
}

/* ========== Address Bar ========== */
.fb-addressbar {
  height: 34px;
  background: #2d2d2d;
  border-bottom: 1px solid #3c3c3c;
  display: flex;
  align-items: center;
  padding: 0 8px;
  flex-shrink: 0;
  gap: 8px;
}

.fb-addressbar-label {
  font-size: 12px;
  color: #999;
  flex-shrink: 0;
}

.fb-addressbar-combo {
  flex: 1;
  display: flex;
  align-items: center;
  background: #3c3c3c;
  border: 1px solid #555;
  border-radius: 2px;
  height: 24px;
  overflow: hidden;
}

.fb-addressbar-combo:focus-within {
  border-color: #0078d4;
}

.fb-addressbar-path {
  flex: 1;
  display: flex;
  align-items: center;
  padding: 0 8px;
  overflow: hidden;
  height: 100%;
  cursor: text;
}

.fb-addressbar-input {
  flex: 1;
  height: 100%;
  width: 100%;
  padding: 0 8px;
  background: #3c3c3c;
  color: #fff;
  border: none;
  outline: none;
  font-size: 12px;
  font-family: 'Consolas', 'Courier New', monospace;
  user-select: text;
  -webkit-user-select: text;
  -moz-user-select: text;
  -ms-user-select: text;
  box-sizing: border-box;
}

.fb-addressbar-editing {
  border-color: #0078d4;
}

.fb-breadcrumb {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.fb-breadcrumb-text {
  font-size: 12px;
  color: #cccccc;
  cursor: pointer;
  padding: 1px 3px;
  border-radius: 2px;
  white-space: nowrap;
}

.fb-breadcrumb-text:hover {
  background: rgba(255, 255, 255, 0.08);
  color: #fff;
}

.fb-breadcrumb-root {
  font-weight: 600;
}

.fb-breadcrumb-arrow {
  color: #888;
  flex-shrink: 0;
  margin: 0 2px;
}

.fb-addressbar-go {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #505050;
  border: none;
  border-left: 1px solid #555;
  color: #ccc;
  cursor: pointer;
  flex-shrink: 0;
  transition: background 0.1s;
}

.fb-addressbar-go:hover {
  background: #606060;
}

/* ========== Main Content Area ========== */
.fb-content {
  display: flex;
  flex: 1;
  overflow: hidden;
  min-height: 0;
}

/* ========== Navigation Pane ========== */
.fb-navpane {
  width: 220px;
  min-width: 180px;
  flex-shrink: 0;
  background: #252525;
  border-right: 1px solid #3c3c3c;
  overflow-y: auto;
  padding: 4px 0;
}

.fb-navpane-section {
  margin-bottom: 2px;
}

.fb-navpane-header {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  font-size: 11px;
  font-weight: 700;
  color: #888;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  cursor: default;
  user-select: none;
}

.fb-navpane-arrow {
  color: #888;
  flex-shrink: 0;
}

.fb-navpane-items {
  padding: 0 4px;
}

.fb-navpane-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 5px 8px;
  color: #cccccc;
  font-size: 12px;
  cursor: pointer;
  border-radius: 3px;
  transition: background 0.1s;
  min-height: 28px;
}

.fb-navpane-item:hover {
  background: rgba(255, 255, 255, 0.06);
}

.fb-navpane-item.active {
  background: rgba(0, 120, 212, 0.3);
}

.fb-navpane-icon {
  flex-shrink: 0;
  width: 16px;
  height: 16px;
}

.fb-navpane-item-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.fb-navpane-item-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.fb-drive-bar {
  height: 3px;
  background: #444;
  border-radius: 2px;
  overflow: hidden;
}

.fb-drive-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, #0078d4, #00bcf2);
  border-radius: 2px;
  transition: width 0.3s ease;
}

.fb-drive-space {
  font-size: 10px;
  color: #888;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ========== File Pane ========== */
.fb-filepane {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
}

/* Column Headers */
.fb-column-headers {
  display: flex;
  height: 28px;
  background: #2d2d2d;
  border-bottom: 1px solid #3c3c3c;
  flex-shrink: 0;
  user-select: none;
}

.fb-col-header {
  display: flex;
  align-items: center;
  padding: 0 8px;
  font-size: 11px;
  font-weight: 600;
  color: #999;
  cursor: default;
  border-right: 1px solid #3c3c3c;
}

.fb-col-header:last-child {
  border-right: none;
}

.fb-col-name {
  flex: 1;
  min-width: 200px;
}

.fb-col-date {
  width: 150px;
  flex-shrink: 0;
}

.fb-col-type {
  width: 120px;
  flex-shrink: 0;
}

.fb-col-size {
  width: 80px;
  flex-shrink: 0;
  justify-content: flex-end;
}

/* File List */
.fb-file-list {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
}

.fb-file-item {
  display: flex;
  align-items: center;
  height: 26px;
  cursor: pointer;
  color: #cccccc;
  font-size: 12px;
  border-bottom: 1px solid transparent;
  transition: background 0.05s;
}

.fb-file-item:hover {
  background: rgba(255, 255, 255, 0.04);
}

.fb-file-item.selected {
  background: rgba(0, 120, 212, 0.35);
}

.fb-file-item.selected:hover {
  background: rgba(0, 120, 212, 0.45);
}

.fb-file-item--disabled {
  opacity: 0.4;
  cursor: default;
}

.fb-file-item--disabled:hover {
  background: transparent;
}

.fb-file-item--disabled.selected {
  background: rgba(255, 255, 255, 0.08);
}

.fb-file-col {
  display: flex;
  align-items: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  padding: 0 8px;
  height: 100%;
}

.fb-col-name {
  flex: 1;
  min-width: 0;
}

.fb-col-date {
  color: #999;
  font-size: 11px;
}

.fb-col-type {
  color: #999;
  font-size: 11px;
}

.fb-col-size {
  color: #999;
  font-size: 11px;
  justify-content: flex-end;
}

.fb-file-icon-wrap {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 6px;
}

.fb-file-icon-svg {
  width: 20px;
  height: 20px;
}

.fb-file-name-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Loading / Error / Empty States */
.fb-loading,
.fb-empty,
.fb-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #666;
  font-size: 13px;
  gap: 12px;
}

.fb-loading-spinner {
  width: 24px;
  height: 24px;
  border: 2px solid #3c3c3c;
  border-top-color: #0078d4;
  border-radius: 50%;
  animation: fb-spin 0.8s linear infinite;
}

@keyframes fb-spin {
  to { transform: rotate(360deg); }
}

.fb-error {
  color: #f48771;
}

.fb-empty svg {
  opacity: 0.3;
}

/* New Folder Inline */
.fb-new-folder-inline {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  height: 32px;
  background: #1e1e1e;
  border-top: 1px solid #3c3c3c;
  flex-shrink: 0;
}

.fb-new-folder-inline .fb-new-folder-input {
  flex: 1;
  padding: 4px 8px;
  background: #3c3c3c;
  color: #fff;
  border: 1px solid #0078d4;
  border-radius: 2px;
  outline: none;
  font-size: 12px;
  font-family: inherit;
  height: 24px;
  user-select: text;
  -webkit-user-select: text;
  -moz-user-select: text;
  -ms-user-select: text;
}

/* ========== Status Bar ========== */
.fb-statusbar {
  height: 24px;
  background: #0078d4;
  display: flex;
  align-items: center;
  padding: 0 12px;
  flex-shrink: 0;
}

.fb-statusbar-msg {
  font-size: 11px;
  color: #fff;
}

.fb-statusbar-success {
  font-weight: 600;
}

/* ========== Dialog Footer ========== */
.fb-dialog-footer {
  padding: 12px 16px;
  background: #2d2d2d;
  border-top: 1px solid #3c3c3c;
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}

.fb-btn-cancel {
  background: #505050;
  color: #fff;
  border: 1px solid #666;
  padding: 6px 24px;
  border-radius: 3px;
  cursor: pointer;
  font-size: 12px;
  font-family: inherit;
  transition: background 0.1s;
}

.fb-btn-cancel:hover {
  background: #606060;
}

.fb-btn-confirm {
  background: #0078d4;
  color: #fff;
  border: 1px solid #1a86d9;
  padding: 6px 24px;
  border-radius: 3px;
  cursor: pointer;
  font-size: 12px;
  font-family: inherit;
  transition: background 0.1s;
}

.fb-btn-confirm:hover:not(:disabled) {
  background: #1084d8;
}

.fb-btn-confirm:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

/* ========== Scrollbar Styling ========== */
.fb-navpane::-webkit-scrollbar,
.fb-file-list::-webkit-scrollbar {
  width: 8px;
}

.fb-navpane::-webkit-scrollbar-track,
.fb-file-list::-webkit-scrollbar-track {
  background: transparent;
}

.fb-navpane::-webkit-scrollbar-thumb,
.fb-file-list::-webkit-scrollbar-thumb {
  background: #555;
  border-radius: 4px;
}

.fb-navpane::-webkit-scrollbar-thumb:hover,
.fb-file-list::-webkit-scrollbar-thumb:hover {
  background: #777;
}

/* ========== This Computer View ========== */
.fb-computer-section {
  margin-bottom: 8px;
}

.fb-computer-section-header {
  font-size: 11px;
  font-weight: 600;
  color: #888;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  padding: 4px 8px 6px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  margin-bottom: 4px;
}

.fb-computer-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 8px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.15s;
}

.fb-computer-item:hover {
  background: rgba(255, 255, 255, 0.06);
}

.fb-computer-item-icon {
  flex-shrink: 0;
}

.fb-computer-item-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.fb-computer-item-name {
  font-size: 12px;
  color: #e0e0e0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.fb-computer-item-path {
  font-size: 10px;
  color: #777;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.fb-computer-drive-bar {
  width: 180px;
  height: 4px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
  overflow: hidden;
}

.fb-computer-drive-bar-fill {
  height: 100%;
  background: #2196F3;
  border-radius: 2px;
  transition: width 0.3s;
}

.fb-computer-drive-space {
  font-size: 10px;
  color: #777;
}

/* ========== Image Preview Popup ========== */
.fb-file-item--image {
  cursor: default;
}

.fb-preview-popup {
  position: fixed;
  z-index: 99999;
  pointer-events: none;
  animation: fb-preview-fadein 0.2s ease;
}

@keyframes fb-preview-fadein {
  from { opacity: 0; transform: scale(0.9); }
  to { opacity: 1; transform: scale(1); }
}

.fb-preview-box {
  background: #1e1e1e;
  border: 1px solid #555;
  border-radius: 8px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6), 0 0 0 1px rgba(255, 255, 255, 0.05);
  overflow: hidden;
  max-width: 320px;
  max-height: 320px;
  display: flex;
  flex-direction: column;
}

.fb-preview-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 120px;
  height: 120px;
}

.fb-preview-spinner {
  width: 24px;
  height: 24px;
  border: 2px solid #3c3c3c;
  border-top-color: #0078d4;
  border-radius: 50%;
  animation: fb-spin 0.8s linear infinite;
}

.fb-preview-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 120px;
  height: 120px;
  color: #f44336;
  font-size: 11px;
}

.fb-preview-image {
  max-width: 300px;
  max-height: 260px;
  object-fit: contain;
  display: block;
  background: repeating-conic-gradient(#2a2a2a 0% 25%, #333 0% 50%) 50% / 16px 16px;
}

.fb-preview-name {
  padding: 6px 10px;
  font-size: 11px;
  color: #cccccc;
  background: #2d2d2d;
  border-top: 1px solid #3c3c3c;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  text-align: center;
}

/* ========== Modal Transition (fade + scale) ========== */
.fb-modal-enter-active,
.fb-modal-leave-active {
  transition: opacity 0.2s ease;
}
.fb-modal-enter-active .fb-window,
.fb-modal-leave-active .fb-window {
  transition: transform 0.2s ease;
}
.fb-modal-enter-from,
.fb-modal-leave-to {
  opacity: 0;
}
.fb-modal-enter-from .fb-window,
.fb-modal-leave-to .fb-window {
  transform: scale(0.92);
}
</style>
