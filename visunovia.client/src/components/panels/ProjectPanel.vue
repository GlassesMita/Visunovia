<template>
  <div class="project-panel">
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
            <div class="folder-item" @click="toggleFolder(node.path)">
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

    <div v-if="previewFile" class="preview-overlay" @click.self="closePreview">
      <div class="preview-dialog" role="dialog" aria-modal="true">
        <div class="preview-header">
          <div class="preview-title" :title="previewFile.path">{{ previewFile.name }}</div>
          <button class="preview-close" type="button" @click="closePreview">✕</button>
        </div>
        <pre class="preview-content">{{ previewFile.content }}</pre>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onMounted, ref, watch } from 'vue'
import type { VNode } from 'vue'
import { getCurrentProject, getProjectFileContent, getProjectFolderTree } from '@/api/projectApi'
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

const rootChildren = computed(() => folderTree.value?.children ?? [])
const displayNodes = computed(() => buildDisplayNodes(rootChildren.value))

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
  selectedFile.value = selectedFile.value === path ? null : path
}

function openNode(node: DisplayNode) {
  if (node.isDirectory) return

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

function getFileIcon(node: DisplayNode): string {
  if (node.isDirectory) return isExpanded(node.path) ? '📂' : '📁'

  const extension = node.extension?.toLowerCase()
  if (['.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.svg'].includes(extension)) return '🖼️'
  if (['.mp3', '.wav', '.ogg', '.flac', '.m4a'].includes(extension)) return '🎵'
  if (['.lor', '.vn', '.yaml', '.yml'].includes(extension)) return '📄'
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
  emits: ['toggle', 'select'],
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

.preview-overlay {
  position: fixed;
  inset: 0;
  z-index: 3000;
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
