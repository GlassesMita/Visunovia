<template>
  <div class="project-panel">
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
              <span class="folder-count">{{ node.children?.length ?? 0 }}</span>
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
  </div>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getCurrentProject, getProjectFolderTree } from '@/api/projectApi'
import type { FolderNode } from '@/api/projectApi'

const router = useRouter()

type DisplayNode = Omit<FolderNode, 'children'> & {
  label: string
  children?: DisplayNode[] | null
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
  scripts: '脚本',
  main: '主线',
  scenes: '场景',
  resources: '资源',
}

const folderTree = ref<FolderNode | null>(null)
const expandedFolders = ref<string[]>([])
const selectedFile = ref<string | null>(null)
const isLoading = ref(false)
const errorMessage = ref('')

const rootChildren = computed(() => folderTree.value?.children ?? [])
const displayNodes = computed(() => buildDisplayNodes(rootChildren.value))

onMounted(loadProjectTree)

async function loadProjectTree() {
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
    folderTree.value = tree
    expandedFolders.value = collectInitialExpandedPaths(buildDisplayNodes(tree.children ?? []))
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
    router.push({ name: 'project-settings' })
  }
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
  return CATEGORY_LABELS[node.name.trim().toLowerCase()] ?? node.name
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

    if (!node.isDirectory) {
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
  return buildDisplayNodes([...left, ...right])
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

const ProjectTreeEntry = defineComponent({
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

    return () => props.node.isDirectory
      ? h('div', { class: 'folder-node' }, [
          h('div', {
            class: 'folder-item',
            style: { paddingLeft: `${8 + props.depth * 12}px` },
            title: props.node.path,
            onClick: () => emit('toggle', props.node.path),
          }, [
            h('span', { class: 'folder-icon' }, isNodeExpanded() ? '📂' : '📁'),
            h('span', { class: 'folder-label' }, props.node.label),
            h('span', { class: 'folder-count' }, String(props.node.children?.length ?? 0)),
          ]),
          isNodeExpanded()
            ? h('div', { class: 'folder-content' }, props.node.children?.map(child => h(ProjectTreeEntry, {
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
</style>
