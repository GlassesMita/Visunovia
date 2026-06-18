<template>
  <div class="project-panel" @click="closeAssetContextMenu">
    <div class="project-panel-toolbar">
      <button class="refresh-button" :disabled="isLoading" title="刷新资源列表" @click="loadProjectTree(true)">
        <span :class="{ spinning: isLoading }">↻</span>
        <span>刷新</span>
      </button>
    </div>
    <div class="panel-content">
      <div v-if="isLoading" class="panel-state">正在加载项目文件...</div>
      <div v-else-if="errorMessage" class="panel-state panel-state-error">{{ errorMessage }}</div>
      <div v-else-if="!folderTree" class="panel-state">当前没有打开的项目</div>
      <div v-else class="folder-tree">
        <template v-for="node in displayNodes" :key="node.path">
          <div
            v-if="node.isDirectory"
            class="folder-node"
          >
            <div
              class="folder-item"
              @click="toggleFolder(node.path)"
              @contextmenu.prevent.stop="openAssetContextMenu($event, node)"
            >
              <span class="folder-icon">{{ isExpanded(node.path) ? '📂' : '📁' }}</span>
              <span class="folder-label" :title="node.path">{{ node.label }}</span>
              <span v-if="(node.children?.length ?? 0) > 0" class="folder-count">{{ node.children?.length ?? 0 }}</span>
            </div>
            <div v-if="isExpanded(node.path)" class="folder-content">
              <ProjectTreeEntry
                v-for="child in node.children ?? []"
                :key="child.path"
                :node="child"
                :depth="1"
                :expanded-paths="expandedFolders"
                :selected-path="selectedFile"
                @toggle="toggleFolder"
                @select="selectFile"
                @context="openAssetContextMenu"
              />
            </div>
          </div>
          <div
            v-else
            class="file-item"
            :class="{ selected: selectedFile === node.path }"
            :title="node.path"
            @click="selectFile(node.path)"
            @dblclick="openNode(node)"
          >
            <span class="file-icon">{{ getFileIcon(node) }}</span>
            <span class="file-label">{{ node.label }}</span>
          </div>
        </template>
      </div>
    </div>

    <Transition name="asset-menu-fade">
      <div
        v-if="assetContextMenu"
        class="asset-context-menu"
        :style="{ left: `${assetContextMenu.x}px`, top: `${assetContextMenu.y}px` }"
        @click.stop
      >
        <button type="button" @click="openAssetManager(assetContextMenu.node)">打开资产管理器</button>
        <button type="button" @click="openAssetManager(assetContextMenu.node, true)">导入资产到此目录</button>
        <button v-if="canRenameAssetDirectory(assetContextMenu.node)" type="button" @click="openRenameAssetDialog(assetContextMenu.node)">重命名文件夹</button>
      </div>
    </Transition>

    <Transition name="asset-modal-fade">
      <div v-if="assetManagerOpen && assetDirectory" class="asset-manager-overlay" @click.self="closeAssetManager">
      <section class="asset-manager-dialog" role="dialog" aria-modal="true">
        <header class="asset-manager-header">
          <div>
            <h3>Asset Manager</h3>
            <p :title="assetDirectory.path">{{ assetDirectory.label }}</p>
          </div>
          <button class="preview-close" type="button" @click="closeAssetManager">✕</button>
        </header>

        <div class="asset-manager-toolbar">
          <button type="button" :disabled="assetBusy" @click="triggerAssetImport">导入文件</button>
          <button type="button" :disabled="assetBusy" @click="triggerAssetFolderImport">导入文件夹</button>
          <button type="button" :disabled="assetBusy || selectedAssetPaths.size === 0" @click="deleteSelectedAssets">删除所选</button>
          <button type="button" :disabled="assetBusy" @click="loadProjectTree(true)">刷新</button>
          <span v-if="selectedAssetPaths.size > 0" class="asset-selection-count">已选 {{ selectedAssetPaths.size }} 项</span>
          <span v-if="assetError" class="asset-error">{{ assetError }}</span>
          <input ref="assetImportInput" type="file" class="hidden-input" multiple @change="handleAssetImport" />
          <input ref="assetFolderImportInput" type="file" class="hidden-input" webkitdirectory multiple @change="handleAssetImport" />
        </div>

        <div class="asset-manager-body">
          <aside class="asset-manager-tree">
            <div class="asset-tree-title">Assets</div>
            <button
              v-for="directory in assetDirectories"
              :key="directory.path"
              type="button"
              class="asset-tree-item"
              :class="{ active: assetDirectory.path === directory.node.path }"
              :style="{ paddingLeft: `${8 + directory.depth * 14}px` }"
              :title="directory.node.path"
              @click="selectAssetDirectory(directory.node)"
            >
              {{ directory.node.label }}
            </button>
          </aside>

          <main class="asset-manager-content">
            <div v-if="assetDirectoryChildren.length === 0" class="asset-empty">该目录暂无资产</div>
            <button
              v-for="asset in assetDirectoryChildren"
              :key="asset.path"
              type="button"
              class="asset-card"
              :class="{ active: selectedAssetPaths.has(asset.path) }"
              :title="asset.path"
              @click="selectAsset(asset, $event)"
              @contextmenu.prevent.stop="openAssetManagerContextMenu($event, asset)"
              @dblclick="asset.isDirectory ? selectAssetDirectory(asset) : previewAsset(asset)"
            >
              <span class="asset-card-icon">{{ getFileIcon(asset) }}</span>
              <span class="asset-card-name">{{ asset.label }}</span>
              <span class="asset-card-meta">{{ asset.isDirectory ? `${asset.children?.length ?? 0} 项` : formatFileSize(asset.size) }}</span>
            </button>
          </main>

          <aside class="asset-preview-pane">
            <div class="asset-preview-title">预览</div>
            <div v-if="!selectedAsset" class="asset-preview-empty">选择一个资产进行预览</div>
            <template v-else>
              <div class="asset-preview-name" :title="selectedAsset.path">{{ selectedAsset.name }}</div>
              <img v-if="assetPreviewKind === 'image'" class="asset-preview-media" :src="assetPreviewUrl" :alt="selectedAsset.name" />
              <audio v-else-if="assetPreviewKind === 'audio'" class="asset-preview-audio" :src="assetPreviewUrl" controls />
              <button v-else-if="assetPreviewKind === 'text'" type="button" class="asset-preview-action" @click="previewAsset(selectedAsset)">查看文本内容</button>
              <div v-else class="asset-preview-empty">该类型暂无内联预览</div>
              <dl class="asset-preview-meta">
                <dt>类型</dt><dd>{{ selectedAsset.isDirectory ? '目录' : selectedAsset.extension || '文件' }}</dd>
                <dt>大小</dt><dd>{{ selectedAsset.isDirectory ? '-' : formatFileSize(selectedAsset.size) }}</dd>
              </dl>
            </template>
          </aside>
        </div>
      </section>
      </div>
    </Transition>

    <Transition name="asset-menu-fade">
      <div
        v-if="assetManagerContextMenu"
        class="asset-context-menu asset-manager-context-menu"
        :style="{ left: `${assetManagerContextMenu.x}px`, top: `${assetManagerContextMenu.y}px` }"
        @click.stop
      >
        <button type="button" @click="importFileToContextDirectory">导入文件到此目录</button>
        <button type="button" @click="importFolderToContextDirectory">导入文件夹到此目录</button>
        <button v-if="canRenameAssetDirectory(assetManagerContextMenu.node)" type="button" @click="openRenameAssetDialog(assetManagerContextMenu.node)">重命名文件夹</button>
        <button type="button" :disabled="selectedAssetPaths.size === 0" @click="deleteSelectedAssets">删除所选</button>
      </div>
    </Transition>

    <Transition name="asset-modal-fade">
      <div v-if="assetRenameTarget" class="asset-dialog-overlay" @click.self="closeRenameAssetDialog">
        <section class="asset-small-dialog" role="dialog" aria-modal="true" aria-labelledby="asset-rename-title" @click.stop>
          <header class="asset-small-dialog-header">
            <h3 id="asset-rename-title">重命名文件夹</h3>
            <button class="preview-close" type="button" @click="closeRenameAssetDialog">✕</button>
          </header>
          <form class="asset-dialog-form" @submit.prevent="submitRenameAsset">
            <label class="asset-dialog-label" for="asset-rename-input">新文件夹名称</label>
            <input id="asset-rename-input" v-model.trim="assetRenameName" class="asset-dialog-input" type="text" autocomplete="off" />
            <p v-if="assetRenameError" class="asset-error">{{ assetRenameError }}</p>
            <div class="asset-dialog-actions">
              <button type="button" @click="closeRenameAssetDialog">取消</button>
              <button class="primary" type="submit" :disabled="assetBusy || !assetRenameName">确认重命名</button>
            </div>
          </form>
        </section>
      </div>
    </Transition>

    <Transition name="asset-modal-fade">
      <div v-if="assetConfirmDialog" class="asset-dialog-overlay" @click.self="cancelAssetConfirm">
        <section class="asset-small-dialog" role="dialog" aria-modal="true" aria-labelledby="asset-confirm-title" @click.stop>
          <header class="asset-small-dialog-header">
            <h3 id="asset-confirm-title">{{ assetConfirmDialog.title }}</h3>
            <button class="preview-close" type="button" @click="cancelAssetConfirm">✕</button>
          </header>
          <div class="asset-dialog-message">{{ assetConfirmDialog.message }}</div>
          <div class="asset-dialog-actions">
            <button type="button" @click="cancelAssetConfirm">取消</button>
            <button type="button" :class="{ danger: assetConfirmDialog.danger }" @click="acceptAssetConfirm">{{ assetConfirmDialog.confirmText }}</button>
          </div>
        </section>
      </div>
    </Transition>

    <Transition name="asset-modal-fade">
      <div v-if="previewFile" class="preview-overlay" @click.self="closePreview">
      <div class="preview-dialog" role="dialog" aria-modal="true">
        <div class="preview-header">
          <div class="preview-title" :title="previewFile.path">{{ previewFile.name }}</div>
          <button class="preview-close" type="button" @click="closePreview">✕</button>
        </div>
        <pre class="preview-content">{{ previewFile.content }}</pre>
      </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onMounted, ref, watch } from 'vue'
import type { VNode } from 'vue'
import { deleteProjectAsset, getCurrentProject, getProjectFileContent, getProjectFolderTree, importProjectAsset, renameProjectAsset } from '@/api/projectApi'
import type { FolderNode } from '@/api/projectApi'
import { useUIStore } from '@/stores/useUIStore'

const uiStore = useUIStore()

type DisplayNode = Omit<FolderNode, 'children'> & {
  label: string
  children?: DisplayNode[] | null
}

type PreviewFile = {
  name: string
  path: string
  content: string
}

type AssetContextMenu = {
  x: number
  y: number
  node: DisplayNode
}

type AssetDirectoryEntry = {
  node: DisplayNode
  depth: number
  path: string
}

type AssetConfirmDialog = {
  title: string
  message: string
  confirmText: string
  danger?: boolean
  resolve: (confirmed: boolean) => void
}

const CATEGORY_LABELS: Record<string, string> = {
  assets: '资产',
  characters: '立绘',
  backgrounds: '背景',
  musics: '音乐',
  music: '音乐',
  bgm: '音乐',
  voices: '语音',
  voice: '语音',
  sfx: '音效',
  sfxs: '音效',
  fonts: '字体',
}

const folderTree = ref<FolderNode | null>(null)
const expandedFolders = ref<string[]>([])
const selectedFile = ref<string | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')
const previewFile = ref<PreviewFile | null>(null)
const assetContextMenu = ref<AssetContextMenu | null>(null)
const assetManagerOpen = ref(false)
const assetDirectory = ref<DisplayNode | null>(null)
const selectedAsset = ref<DisplayNode | null>(null)
const assetError = ref('')
const assetBusy = ref(false)
const assetImportInput = ref<HTMLInputElement | null>(null)
const assetFolderImportInput = ref<HTMLInputElement | null>(null)
const selectedAssetPaths = ref<Set<string>>(new Set())
const lastSelectedAssetPath = ref<string | null>(null)
const assetManagerContextMenu = ref<AssetContextMenu | null>(null)
const pendingImportDirectory = ref<string | null>(null)
const assetRenameTarget = ref<DisplayNode | null>(null)
const assetRenameName = ref('')
const assetRenameError = ref('')
const assetConfirmDialog = ref<AssetConfirmDialog | null>(null)

const rootChildren = computed(() => folderTree.value?.children ?? [])
const displayNodes = computed(() => buildDisplayNodes(rootChildren.value))
const assetDirectories = computed<AssetDirectoryEntry[]>(() => collectAssetDirectories(displayNodes.value))
const assetDirectoryChildren = computed(() => assetDirectory.value?.children ?? [])
const assetPreviewKind = computed(() => selectedAsset.value ? getPreviewKind(selectedAsset.value) : 'none')
const assetPreviewUrl = computed(() => selectedAsset.value ? getAssetFileUrl(selectedAsset.value) : '')

onMounted(loadProjectTree)

watch(() => uiStore.projectTreeRefreshToken, () => {
  loadProjectTree(true)
})

async function loadProjectTree(forceRefresh = false) {
  isLoading.value = true
  errorMessage.value = ''

  try {
    const currentProject = await getCurrentProject()
    const projectPath = currentProject.data?.projectPath

    if (!projectPath) {
      folderTree.value = null
      expandedFolders.value = []
      return
    }

    const tree = await getProjectFolderTree(projectPath)
    const previouslyExpanded = new Set(expandedFolders.value)
    folderTree.value = tree
    const nextDisplayNodes = buildDisplayNodes(tree.children ?? [])
    expandedFolders.value = forceRefresh && previouslyExpanded.size > 0
      ? collectExistingExpandedPaths(nextDisplayNodes, previouslyExpanded)
      : collectInitialExpandedPaths(nextDisplayNodes)
  } catch (error) {
    console.error('[ProjectPanel] Failed to load project tree:', error)
    errorMessage.value = error instanceof Error ? error.message : '项目文件加载失败'
  } finally {
    isLoading.value = false
  }
}

function toggleFolder(folder: string) {
  const index = expandedFolders.value.indexOf(folder)
  if (index >= 0) {
    expandedFolders.value.splice(index, 1)
  } else {
    expandedFolders.value.push(folder)
  }
}

function isExpanded(path: string) {
  return expandedFolders.value.includes(path)
}

function selectFile(path: string) {
  closeAssetContextMenu()
  selectedFile.value = selectedFile.value === path ? null : path
}

function openNode(node: DisplayNode) {
  if (node.isDirectory) return

  if (node.name.toLowerCase().endsWith('.lor')) {
    uiStore.closeProjectPopup()
    uiStore.openFileByPath(getPrimaryPath(node.path))
    return
  }

  if (node.name.toLowerCase() === 'project.tlor') {
    uiStore.closeProjectPopup()
    uiStore.openProjectPreferences()
    return
  }

  if (node.name.toLowerCase() === 'index.resona') {
    openReadOnlyPreview(node)
  }
}

async function openReadOnlyPreview(node: DisplayNode) {
  try {
    previewFile.value = {
      name: node.name,
      path: node.path,
      content: '正在加载...',
    }
    previewFile.value = await getProjectFileContent(node.path)
  } catch (error) {
    console.error('[ProjectPanel] Failed to preview file:', error)
    previewFile.value = {
      name: node.name,
      path: node.path,
      content: error instanceof Error ? error.message : '文件内容加载失败',
    }
  }
}

function closePreview() {
  previewFile.value = null
}

function openAssetContextMenu(event: MouseEvent, node: DisplayNode) {
  if (!node.isDirectory || !isUnderAssets(getPrimaryPath(node.path))) return
  assetContextMenu.value = {
    x: event.clientX,
    y: event.clientY,
    node,
  }
}

function closeAssetContextMenu() {
  assetContextMenu.value = null
  assetManagerContextMenu.value = null
}

function openAssetManager(node: DisplayNode, importImmediately = false) {
  closeAssetContextMenu()
  assetDirectory.value = normalizeDisplayNodePath(node)
  selectedAsset.value = null
  clearAssetSelection()
  assetError.value = ''
  assetManagerOpen.value = true
  if (importImmediately) requestAnimationFrame(() => triggerAssetImport())
}

function closeAssetManager() {
  assetManagerOpen.value = false
  assetDirectory.value = null
  selectedAsset.value = null
  clearAssetSelection()
  assetError.value = ''
}

function selectAssetDirectory(node: DisplayNode) {
  assetDirectory.value = normalizeDisplayNodePath(node)
  selectedAsset.value = null
  clearAssetSelection()
  assetError.value = ''
}

function selectAsset(node: DisplayNode, event?: MouseEvent) {
  selectedAsset.value = normalizeDisplayNodePath(node)
  const nextSelection = new Set(selectedAssetPaths.value)

  if (event?.shiftKey && lastSelectedAssetPath.value) {
    const children = assetDirectoryChildren.value
    const startIndex = children.findIndex(item => item.path === lastSelectedAssetPath.value)
    const endIndex = children.findIndex(item => item.path === node.path)
    if (startIndex >= 0 && endIndex >= 0) {
      const [start, end] = startIndex < endIndex ? [startIndex, endIndex] : [endIndex, startIndex]
      for (const item of children.slice(start, end + 1)) nextSelection.add(item.path)
    }
  } else if (event?.ctrlKey || event?.metaKey) {
    if (nextSelection.has(node.path)) nextSelection.delete(node.path)
    else nextSelection.add(node.path)
  } else {
    nextSelection.clear()
    nextSelection.add(node.path)
  }

  selectedAssetPaths.value = nextSelection
  lastSelectedAssetPath.value = node.path
  assetError.value = ''
}

function previewAsset(node: DisplayNode) {
  if (node.isDirectory) return
  if (getPreviewKind(node) === 'text') {
    openReadOnlyPreview(node)
    return
  }
  selectedAsset.value = normalizeDisplayNodePath(node)
}

function triggerAssetImport() {
  assetError.value = ''
  pendingImportDirectory.value = assetDirectory.value ? getPrimaryPath(assetDirectory.value.path) : null
  assetImportInput.value?.click()
}

function triggerAssetFolderImport() {
  assetError.value = ''
  pendingImportDirectory.value = assetDirectory.value ? getPrimaryPath(assetDirectory.value.path) : null
  assetFolderImportInput.value?.click()
}

async function handleAssetImport(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  const targetDirectory = pendingImportDirectory.value || (assetDirectory.value ? getPrimaryPath(assetDirectory.value.path) : '')
  pendingImportDirectory.value = null
  if (files.length === 0 || !targetDirectory) return

  assetBusy.value = true
  assetError.value = ''
  const currentDirectoryPath = assetDirectory.value?.path
  try {
    for (const file of files) {
      await importProjectAsset(targetDirectory, file, file.webkitRelativePath || file.name)
    }
    await loadProjectTree(true)
    restoreAssetDirectoryAfterRefresh(currentDirectoryPath)
  } catch (error) {
    assetError.value = error instanceof Error ? error.message : '导入资产失败'
  } finally {
    assetBusy.value = false
  }
}

async function deleteSelectedAssets() {
  const selectedItems = assetDirectoryChildren.value.filter(item => selectedAssetPaths.value.has(item.path))
  if (selectedItems.length === 0) return
  const confirmed = await requestAssetConfirm({
    title: '删除资产',
    message: `将删除所选 ${selectedItems.length} 个资产。文件夹及其内容会一并删除，此操作不可撤销。`,
    confirmText: '删除所选',
    danger: true,
  })
  if (!confirmed) return

  assetBusy.value = true
  assetError.value = ''
  const currentDirectoryPath = assetDirectory.value?.path
  try {
    for (const item of selectedItems) {
      await deleteProjectAsset(getPrimaryPath(item.path))
    }
    selectedAsset.value = null
    clearAssetSelection()
    await loadProjectTree(true)
    restoreAssetDirectoryAfterRefresh(currentDirectoryPath)
  } catch (error) {
    assetError.value = error instanceof Error ? error.message : '删除资产失败'
  } finally {
    assetBusy.value = false
  }
}

function openRenameAssetDialog(node: DisplayNode) {
  const normalizedNode = normalizeDisplayNodePath(node)
  if (!canRenameAssetDirectory(normalizedNode)) return
  closeAssetContextMenu()
  assetRenameTarget.value = normalizedNode
  assetRenameName.value = normalizedNode.name
  assetRenameError.value = ''
}

function closeRenameAssetDialog() {
  assetRenameTarget.value = null
  assetRenameName.value = ''
  assetRenameError.value = ''
}

async function submitRenameAsset() {
  if (!assetRenameTarget.value) return
  const nextName = assetRenameName.value.trim()
  if (!nextName) {
    assetRenameError.value = '请输入新文件夹名称'
    return
  }

  assetBusy.value = true
  assetRenameError.value = ''
  assetError.value = ''
  const currentDirectoryPath = assetDirectory.value?.path
  const renamePath = getPrimaryPath(assetRenameTarget.value.path)
  const renamedPath = getSiblingPathWithName(renamePath, nextName)
  try {
    await renameProjectAsset(renamePath, nextName)
    closeRenameAssetDialog()
    await loadProjectTree(true)
    restoreAssetDirectoryAfterRefresh(renamePath === getPrimaryPath(currentDirectoryPath ?? '') ? renamedPath : currentDirectoryPath)
  } catch (error) {
    assetRenameError.value = error instanceof Error ? error.message : '重命名文件夹失败'
  } finally {
    assetBusy.value = false
  }
}

function canRenameAssetDirectory(node: DisplayNode | null | undefined): boolean {
  if (!node?.isDirectory) return false
  const path = getPrimaryPath(node.path)
  return isUnderAssets(path) && !/[\\/]assets$/i.test(path.replace(/[\\/]+$/, ''))
}

function getSiblingPathWithName(path: string, newName: string): string {
  const normalizedPath = path.replace(/[\\/]+$/, '')
  const separatorIndex = Math.max(normalizedPath.lastIndexOf('\\'), normalizedPath.lastIndexOf('/'))
  if (separatorIndex < 0) return newName
  return `${normalizedPath.slice(0, separatorIndex + 1)}${newName}`
}

function requestAssetConfirm(options: Omit<AssetConfirmDialog, 'resolve'>): Promise<boolean> {
  return new Promise(resolve => {
    assetConfirmDialog.value = { ...options, resolve }
  })
}

function acceptAssetConfirm() {
  assetConfirmDialog.value?.resolve(true)
  assetConfirmDialog.value = null
}

function cancelAssetConfirm() {
  assetConfirmDialog.value?.resolve(false)
  assetConfirmDialog.value = null
}

function clearAssetSelection() {
  selectedAssetPaths.value = new Set()
  lastSelectedAssetPath.value = null
}

function openAssetManagerContextMenu(event: MouseEvent, node: DisplayNode) {
  selectAsset(node, event)
  const directory = node.isDirectory ? node : assetDirectory.value
  if (!directory) return
  assetManagerContextMenu.value = {
    x: event.clientX,
    y: event.clientY,
    node: normalizeDisplayNodePath(directory),
  }
}

function importFileToContextDirectory() {
  if (!assetManagerContextMenu.value) return
  pendingImportDirectory.value = getPrimaryPath(assetManagerContextMenu.value.node.path)
  assetManagerContextMenu.value = null
  assetImportInput.value?.click()
}

function importFolderToContextDirectory() {
  if (!assetManagerContextMenu.value) return
  pendingImportDirectory.value = getPrimaryPath(assetManagerContextMenu.value.node.path)
  assetManagerContextMenu.value = null
  assetFolderImportInput.value?.click()
}

function restoreAssetDirectoryAfterRefresh(path = assetDirectory.value?.path) {
  if (!path) return
  const normalizedPath = getPrimaryPath(path)
  const directory = assetDirectories.value.find(item => getPrimaryPath(item.path) === normalizedPath)
  if (directory) assetDirectory.value = normalizeDisplayNodePath(directory.node)
}

function collectAssetDirectories(nodes: DisplayNode[], depth = 0): AssetDirectoryEntry[] {
  const directories: AssetDirectoryEntry[] = []
  for (const node of nodes) {
    if (!node.isDirectory) continue
    if (isUnderAssets(getPrimaryPath(node.path))) {
      directories.push({ node, depth, path: node.path })
    }
    directories.push(...collectAssetDirectories(node.children ?? [], depth + 1))
  }
  return directories
}

function normalizeDisplayNodePath<T extends DisplayNode>(node: T): T {
  return {
    ...node,
    path: getPrimaryPath(node.path),
  }
}

function getPrimaryPath(path: string): string {
  return path.split('|')[0]
}

function getPreviewKind(node: DisplayNode): 'image' | 'audio' | 'text' | 'none' {
  if (node.isDirectory) return 'none'
  const extension = node.extension?.toLowerCase()
  if (['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.svg'].includes(extension)) return 'image'
  if (['.mp3', '.wav', '.ogg', '.flac', '.m4a'].includes(extension)) return 'audio'
  if (['.resona', '.json', '.xml', '.txt', '.md', '.po', '.css', '.js', '.ts', '.html', '.lor'].includes(extension)) return 'text'
  return 'none'
}

function getAssetFileUrl(node: DisplayNode): string {
  const relativePath = getRelativeProjectPath(getPrimaryPath(node.path))
  if (!relativePath) return ''
  return `/api/resources/file/${relativePath.split('/').map(encodeURIComponent).join('/')}`
}

function getRelativeProjectPath(path: string): string {
  const rootPath = folderTree.value?.path
  if (!rootPath) return ''
  const normalizedRoot = rootPath.replace(/\\/g, '/').replace(/\/$/, '')
  const normalizedPath = path.replace(/\\/g, '/')
  if (!normalizedPath.toLowerCase().startsWith(`${normalizedRoot.toLowerCase()}/`)) return ''
  return normalizedPath.slice(normalizedRoot.length + 1)
}

function formatFileSize(size: number): string {
  if (!Number.isFinite(size) || size <= 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB']
  let value = size
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }
  return `${value.toFixed(value >= 10 || unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`
}

function getFileIcon(node: DisplayNode): string {
  if (node.isDirectory) return isExpanded(node.path) ? '📂' : '📁'

  const extension = node.extension?.toLowerCase()
  if (['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.svg'].includes(extension)) return '🖼️'
  if (['.mp3', '.wav', '.ogg', '.flac', '.m4a'].includes(extension)) return '🎵'
  if (['.lor', '.vn'].includes(extension)) return '📄'
  if (['.tlor', '.json', '.xml'].includes(extension)) return '⚙️'
  if (['.ttf', '.otf', '.woff', '.woff2'].includes(extension)) return '🔤'
  return '📄'
}

function getCategoryKey(name: string): string {
  const normalized = name.trim().toLowerCase()
  return CATEGORY_LABELS[normalized] ? normalized.replace(/s$/, '') : normalized
}

function getDisplayLabel(node: FolderNode): string {
  if (!node.isDirectory) return node.name
  if (!isUnderAssets(node.path)) return node.name
  return CATEGORY_LABELS[node.name.trim().toLowerCase()] ?? node.name
}

function isUnderAssets(path: string): boolean {
  return /(^|[\\/])assets([\\/]|$)/i.test(path)
}

function shouldGroupDirectory(node: FolderNode): boolean {
  return node.isDirectory && isUnderAssets(node.path)
}

function toDisplayNode(node: FolderNode): DisplayNode {
  return {
    ...node,
    label: getDisplayLabel(node),
    children: node.children ? buildDisplayNodes(node.children) : node.children,
  }
}

function buildDisplayNodes(nodes: FolderNode[]): DisplayNode[] {
  const result: DisplayNode[] = []
  const directoryGroups = new Map<string, DisplayNode>()

  for (const node of nodes) {
    const displayNode = toDisplayNode(node)

    if (!shouldGroupDirectory(node)) {
      result.push(displayNode)
      continue
    }

    const key = getCategoryKey(node.name)
    const existing = directoryGroups.get(key)

    if (existing) {
      existing.children = mergeDisplayChildren(existing.children ?? [], displayNode.children ?? [])
      existing.path = `${existing.path}|${displayNode.path}`
      continue
    }

    directoryGroups.set(key, displayNode)
    result.push(displayNode)
  }

  return result
}

function mergeDisplayChildren(left: DisplayNode[], right: DisplayNode[]): DisplayNode[] {
  return buildDisplayNodes([...(left as FolderNode[]), ...(right as FolderNode[])])
}

function collectInitialExpandedPaths(nodes: DisplayNode[], depth = 0): string[] {
  const paths: string[] = []
  for (const node of nodes) {
    if (!node.isDirectory) continue
    if (depth < 2) paths.push(node.path)
    paths.push(...collectInitialExpandedPaths(node.children ?? [], depth + 1))
  }
  return paths
}

function collectExistingExpandedPaths(nodes: DisplayNode[], expanded: Set<string>): string[] {
  const paths: string[] = []
  for (const node of nodes) {
    if (!node.isDirectory) continue
    if (expanded.has(node.path)) paths.push(node.path)
    paths.push(...collectExistingExpandedPaths(node.children ?? [], expanded))
  }
  return paths.length > 0 ? paths : collectInitialExpandedPaths(nodes)
}

let ProjectTreeEntry: ReturnType<typeof defineComponent>

ProjectTreeEntry = defineComponent({
  name: 'ProjectTreeEntry',
  props: {
    node: { type: Object as () => DisplayNode, required: true },
    depth: { type: Number, required: true },
    expandedPaths: { type: Array as () => string[], required: true },
    selectedPath: { type: String, default: null },
  },
  emits: ['toggle', 'select', 'context'],
  setup(props, { emit }) {
    const isNodeExpanded = () => props.expandedPaths.includes(props.node.path)
    const icon = () => getFileIcon(props.node)

    return (): VNode => props.node.isDirectory
      ? h('div', { class: 'folder-node' }, [
          h('div', {
            class: 'folder-item',
            style: { paddingLeft: `${8 + props.depth * 12}px` },
            title: props.node.path,
            onClick: () => emit('toggle', props.node.path),
            onContextmenu: (event: MouseEvent) => {
              event.preventDefault()
              event.stopPropagation()
              emit('context', event, props.node)
            },
          }, [
            h('span', { class: 'folder-icon' }, isNodeExpanded() ? '📂' : '📁'),
            h('span', { class: 'folder-label' }, props.node.label),
            (props.node.children?.length ?? 0) > 0
              ? h('span', { class: 'folder-count' }, String(props.node.children?.length ?? 0))
              : null,
          ]),
          isNodeExpanded()
            ? h('div', { class: 'folder-content' }, props.node.children?.map((child): VNode => h(ProjectTreeEntry, {
                key: child.path,
                node: child,
                depth: props.depth + 1,
                expandedPaths: props.expandedPaths,
                selectedPath: props.selectedPath,
                onToggle: (path: string) => emit('toggle', path),
                onSelect: (path: string) => emit('select', path),
                onContext: (event: MouseEvent, node: DisplayNode) => emit('context', event, node),
              })) ?? [])
            : null,
        ])
      : h('div', {
          class: ['file-item', { selected: props.selectedPath === props.node.path }],
          style: { paddingLeft: `${8 + props.depth * 12}px` },
          title: props.node.path,
          onClick: () => emit('select', props.node.path),
          onDblclick: () => openNode(props.node),
        }, [
          h('span', { class: 'file-icon' }, icon()),
          h('span', { class: 'file-label' }, props.node.label),
        ])
  },
})
</script>

<style scoped>
.project-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #252526;
}

.project-panel-toolbar {
  display: flex;
  justify-content: flex-end;
  padding: 6px 8px;
  border-bottom: 1px solid #3e3e42;
  background: #252526;
  flex-shrink: 0;
}

.refresh-button {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 3px 8px;
  border-radius: 4px;
  background: #333337;
  color: #cccccc;
  font-size: 11px;
  cursor: pointer;
}

.refresh-button:hover:not(:disabled) {
  background: #3e3e42;
  color: #ffffff;
}

.refresh-button:disabled {
  opacity: 0.6;
  cursor: wait;
}

.spinning {
  display: inline-block;
  animation: spin 0.8s linear infinite;
}

.panel-header {
  padding: 10px 12px;
  border-bottom: 1px solid #3e3e42;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.panel-header h3 {
  margin: 0;
  font-size: 11px;
  font-weight: 600;
  color: #cccccc;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}

.folder-tree {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.folder-node {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.panel-state {
  padding: 12px 10px;
  color: #9ca3af;
  font-size: 12px;
}

.panel-state-error {
  color: #fca5a5;
}

.folder-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 12px;
  user-select: none;
  transition: background 0.1s;
}

.folder-item:hover {
  background: rgba(255, 255, 255, 0.08);
}

.folder-icon {
  font-size: 13px;
  width: 16px;
  text-align: center;
  flex-shrink: 0;
}

.folder-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.folder-count {
  font-size: 10px;
  color: #606060;
  background: #333333;
  padding: 1px 6px;
  border-radius: 8px;
}

.folder-content {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  color: #a0a0a0;
  cursor: pointer;
  border-radius: 3px;
  font-size: 11px;
  transition: all 0.1s;
}

.file-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-item:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #cccccc;
}

.file-item.selected {
  background: #094771;
  color: #ffffff;
}

.file-icon {
  font-size: 12px;
  width: 16px;
  text-align: center;
  flex-shrink: 0;
}

.asset-context-menu {
  position: fixed;
  z-index: 3500;
  min-width: 168px;
  padding: 5px;
  background: #252526;
  border: 1px solid #454545;
  border-radius: 6px;
  box-shadow: 0 12px 28px rgba(0, 0, 0, 0.4);
}

.asset-context-menu button {
  width: 100%;
  padding: 7px 9px;
  text-align: left;
  color: #d4d4d4;
  background: transparent;
  border: 0;
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
}

.asset-context-menu button:hover {
  background: #094771;
  color: #ffffff;
}

.asset-context-menu button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.asset-context-menu button:disabled:hover {
  background: transparent;
  color: #d4d4d4;
}

.asset-manager-overlay {
  position: fixed;
  inset: 0;
  z-index: 3200;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.58);
}

.asset-manager-dialog {
  width: min(1080px, calc(100vw - 48px));
  height: min(720px, calc(100vh - 48px));
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  border: 1px solid #454545;
  border-radius: 10px;
  box-shadow: 0 22px 60px rgba(0, 0, 0, 0.5);
  overflow: hidden;
}

.asset-dialog-overlay {
  position: fixed;
  inset: 0;
  z-index: 3800;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: rgba(0, 0, 0, 0.58);
}

.asset-small-dialog {
  width: min(420px, calc(100vw - 48px));
  overflow: hidden;
  color: #d4d4d4;
  background: #1f1f23;
  border: 1px solid #454545;
  border-radius: 10px;
  box-shadow: 0 22px 60px rgba(0, 0, 0, 0.5);
}

.asset-small-dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
}

.asset-small-dialog-header h3 {
  margin: 0;
  color: #ffffff;
  font-size: 14px;
}

.asset-dialog-form {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px;
}

.asset-dialog-label {
  color: #cbd5e1;
  font-size: 12px;
}

.asset-dialog-input {
  width: 100%;
  padding: 8px 10px;
  color: #ffffff;
  background: #252526;
  border: 1px solid #454545;
  border-radius: 6px;
  outline: none;
  font-size: 13px;
}

.asset-dialog-input:focus {
  border-color: #0e639c;
  box-shadow: 0 0 0 2px rgba(14, 99, 156, 0.28);
}

.asset-dialog-message {
  padding: 16px 14px 4px;
  color: #d4d4d4;
  font-size: 13px;
  line-height: 1.55;
}

.asset-dialog-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 12px 14px 14px;
}

.asset-dialog-form .asset-dialog-actions {
  padding: 4px 0 0;
}

.asset-dialog-actions button {
  padding: 7px 12px;
  color: #e5e7eb;
  background: #333337;
  border: 1px solid #454545;
  border-radius: 5px;
  cursor: pointer;
  font-size: 12px;
}

.asset-dialog-actions button:hover:not(:disabled),
.asset-dialog-actions button.primary {
  background: #0e639c;
  border-color: #1177bb;
}

.asset-dialog-actions button.danger {
  background: #a31515;
  border-color: #c42b1c;
}

.asset-dialog-actions button.danger:hover {
  background: #c42b1c;
}

.asset-dialog-actions button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.asset-modal-fade-enter-active,
.asset-modal-fade-leave-active {
  transition: opacity 0.18s ease;
}

.asset-modal-fade-enter-from,
.asset-modal-fade-leave-to {
  opacity: 0;
}

.asset-modal-fade-enter-active .asset-manager-dialog,
.asset-modal-fade-leave-active .asset-manager-dialog,
.asset-modal-fade-enter-active .asset-small-dialog,
.asset-modal-fade-leave-active .asset-small-dialog,
.asset-modal-fade-enter-active .preview-dialog,
.asset-modal-fade-leave-active .preview-dialog {
  transition: transform 0.18s ease, opacity 0.18s ease;
}

.asset-modal-fade-enter-from .asset-manager-dialog,
.asset-modal-fade-leave-to .asset-manager-dialog,
.asset-modal-fade-enter-from .asset-small-dialog,
.asset-modal-fade-leave-to .asset-small-dialog,
.asset-modal-fade-enter-from .preview-dialog,
.asset-modal-fade-leave-to .preview-dialog {
  opacity: 0;
  transform: translateY(8px) scale(0.98);
}

.asset-menu-fade-enter-active,
.asset-menu-fade-leave-active {
  transition: opacity 0.12s ease, transform 0.12s ease;
}

.asset-menu-fade-enter-from,
.asset-menu-fade-leave-to {
  opacity: 0;
  transform: translateY(-4px) scale(0.98);
}

.asset-manager-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  background: linear-gradient(180deg, #303033, #252526);
  border-bottom: 1px solid #3e3e42;
}

.asset-manager-header h3 {
  margin: 0;
  color: #ffffff;
  font-size: 14px;
  letter-spacing: 0.3px;
}

.asset-manager-header p {
  margin: 4px 0 0;
  color: #9ca3af;
  font-size: 11px;
}

.asset-manager-toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #252526;
  border-bottom: 1px solid #333337;
}

.asset-manager-toolbar button,
.asset-preview-action {
  padding: 6px 10px;
  color: #e5e7eb;
  background: #333337;
  border: 1px solid #454545;
  border-radius: 5px;
  cursor: pointer;
  font-size: 12px;
}

.asset-manager-toolbar button:hover:not(:disabled),
.asset-preview-action:hover {
  background: #0e639c;
  border-color: #1177bb;
}

.asset-manager-toolbar button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.asset-error {
  color: #fca5a5;
  font-size: 12px;
}

.asset-selection-count {
  color: #93c5fd;
  font-size: 12px;
}

.hidden-input {
  display: none;
}

.asset-manager-body {
  min-height: 0;
  flex: 1;
  display: grid;
  grid-template-columns: 220px 1fr 260px;
}

.asset-manager-tree,
.asset-preview-pane {
  min-width: 0;
  overflow: auto;
  background: #252526;
}

.asset-manager-tree {
  border-right: 1px solid #333337;
  padding: 8px;
}

.asset-tree-title,
.asset-preview-title {
  margin-bottom: 8px;
  color: #9ca3af;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.6px;
}

.asset-tree-item {
  width: 100%;
  display: block;
  padding: 6px 8px;
  margin-bottom: 2px;
  color: #d4d4d4;
  background: transparent;
  border: 0;
  border-radius: 4px;
  text-align: left;
  cursor: pointer;
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.asset-tree-item:hover,
.asset-tree-item.active {
  background: #094771;
  color: #ffffff;
}

.asset-manager-content {
  min-width: 0;
  overflow: auto;
  padding: 14px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(116px, 1fr));
  align-content: start;
  gap: 10px;
  background: #1e1e1e;
}

.asset-empty,
.asset-preview-empty {
  color: #8b949e;
  font-size: 12px;
}

.asset-card {
  min-height: 112px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 7px;
  padding: 10px;
  color: #d4d4d4;
  background: #252526;
  border: 1px solid #333337;
  border-radius: 8px;
  cursor: pointer;
}

.asset-card:hover,
.asset-card.active {
  border-color: #0e639c;
  background: #26313a;
}

.asset-card-icon {
  font-size: 26px;
}

.asset-card-name {
  width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: center;
  font-size: 12px;
}

.asset-card-meta {
  color: #8b949e;
  font-size: 10px;
}

.asset-preview-pane {
  border-left: 1px solid #333337;
  padding: 12px;
}

.asset-preview-name {
  margin-bottom: 12px;
  color: #ffffff;
  font-size: 13px;
  overflow-wrap: anywhere;
}

.asset-preview-media {
  width: 100%;
  max-height: 220px;
  object-fit: contain;
  background: #111827;
  border: 1px solid #333337;
  border-radius: 6px;
}

.asset-preview-audio {
  width: 100%;
}

.asset-preview-meta {
  display: grid;
  grid-template-columns: 56px 1fr;
  gap: 6px 8px;
  margin-top: 14px;
  color: #cbd5e1;
  font-size: 12px;
}

.asset-preview-meta dt {
  color: #8b949e;
}

.asset-preview-meta dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.preview-overlay {
  position: fixed;
  inset: 0;
  z-index: 3700;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.55);
}

.preview-dialog {
  width: min(720px, calc(100vw - 48px));
  max-height: min(620px, calc(100vh - 48px));
  display: flex;
  flex-direction: column;
  background: #1e1e1e;
  border: 1px solid #454545;
  border-radius: 8px;
  box-shadow: 0 18px 48px rgba(0, 0, 0, 0.45);
  overflow: hidden;
}

.preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
}

.preview-title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #ffffff;
  font-size: 13px;
}

.preview-close {
  color: #cccccc;
  background: transparent;
  cursor: pointer;
  font-size: 13px;
  padding: 2px 6px;
  border-radius: 4px;
}

.preview-close:hover {
  background: #c42b1c;
  color: #ffffff;
}

.preview-content {
  margin: 0;
  padding: 14px;
  overflow: auto;
  color: #d4d4d4;
  background: #1e1e1e;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.55;
  white-space: pre-wrap;
  user-select: text;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
