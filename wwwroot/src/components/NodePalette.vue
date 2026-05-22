<template>
  <div class="node-palette">
    <div class="palette-header">
      <h3>{{ t('palette.title') || 'Nodes' }}</h3>
    </div>
    
    <!-- 搜索框 -->
    <div class="palette-search">
      <div class="search-icon">🔍</div>
      <input 
        type="text" 
        v-model="searchQuery" 
        :placeholder="t('common.search') || 'Search nodes...'"
        @input="onSearch"
      />
      <button 
        v-if="searchQuery"
        class="clear-search-btn"
        @click="searchQuery = ''"
        title="Clear search"
      >
        ✕
      </button>
    </div>

    <!-- 节点分类列表 -->
    <div class="palette-categories">
      <div 
        v-for="category in filteredCategories" 
        :key="category.name"
        class="category"
      >
        <!-- 分类标题（可折叠） -->
        <div 
          class="category-header" 
          :class="{ expanded: isCategoryExpanded(category.name) }"
          @click="toggleCategory(category.name)"
        >
          <ChevronRight :size="14" class="chevron-icon" />
          <span class="category-label">{{ getCategoryLabel(category.labelKey) }}</span>
          <span class="node-count-badge">{{ category.nodes.length }}</span>
        </div>

        <!-- 节点列表 -->
        <div v-if="isCategoryExpanded(category.name)" class="category-nodes">
          <div 
            v-for="node in category.nodes" 
            :key="node.type"
            class="node-item"
            :class="node.type.replace('Node', '').toLowerCase()"
            draggable="true"
            @dragstart="onDragStart($event, node.type)"
            @dragend="onDragEnd"
            :title="getNodeDescription(node.type)"
          >
            <span class="node-icon">{{ getNodeIcon(node.type) }}</span>
            <span class="node-label">{{ getNodeTitle(node.type) }}</span>
            <!-- 子类型数量提示 -->
            <span v-if="getNodeSubTypeCount(node.type) > 0" class="subtypes-hint">
              ({{ getNodeSubTypeCount(node.type) }} {{ t('palette.types') || 'types' }})
            </span>
          </div>
          
          <!-- 搜索无结果提示 -->
          <div v-if="category.nodes.length === 0 && searchQuery" class="no-results">
            {{ t('common.noResults') || 'No results' }}
          </div>
        </div>
      </div>

      <!-- 全局无结果提示 -->
      <div v-if="filteredCategories.length === 0 && searchQuery" class="global-no-results">
        <span>🔍</span>
        <p>{{ t('palette.noMatch') || 'No nodes match your search' }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { ChevronRight } from 'lucide-vue-next'

const { t } = useI18n()

const searchQuery = ref('')
const expandedCategories = ref<string[]>(['flow', 'event', 'dialogue', 'logic', 'resources'])

// 6 种大类型节点配置，按分类展示
const nodeCategories = [
  {
    name: 'flow',
    labelKey: 'categories.flow' || 'Flow',
    nodes: [
      { type: 'StartNode' },
      { type: 'EndNode' }
    ]
  },
  {
    name: 'event',
    labelKey: 'categories.event' || 'Event',
    nodes: [
      { type: 'EventNode' }
    ]
  },
  {
    name: 'dialogue',
    labelKey: 'categories.dialogue' || 'Dialogue',
    nodes: [
      { type: 'DialogueNode' },
      { type: 'BranchNode' }
    ]
  },
  {
    name: 'logic',
    labelKey: 'categories.logic' || 'Logic',
    nodes: [
      { type: 'LogicNode' }
    ]
  },
  {
    name: 'resources',
    labelKey: 'categories.resources' || 'Resources',
    nodes: [
      { type: 'ResourceNode', icon: '📦', subTypes: 0 }
    ]
  },
]

// 根据搜索关键词过滤分类和节点
const filteredCategories = computed(() => {
  if (!searchQuery.value.trim()) return nodeCategories
  
  const query = searchQuery.value.toLowerCase().trim()
  
  return nodeCategories
    .map(category => ({
      ...category,
      // 过滤每个分类下的节点
      nodes: category.nodes.filter(node => {
        const typeName = node.type.toLowerCase()
        const titleName = getNodeTitle(node.type).toLowerCase()
        return typeName.includes(query) || titleName.includes(query)
      })
    }))
    // 只保留有匹配节点的分类
    .filter(category => category.nodes.length > 0)
})

function isCategoryExpanded(name: string): boolean {
  return expandedCategories.value.includes(name)
}

function toggleCategory(name: string) {
  const index = expandedCategories.value.indexOf(name)
  if (index >= 0) {
    expandedCategories.value.splice(index, 1)
  } else {
    expandedCategories.value.push(name)
  }
}

function onSearch() {
  // 搜索时自动展开所有有结果的分类
  if (searchQuery.value.trim()) {
    filteredCategories.value.forEach(cat => {
      if (!expandedCategories.value.includes(cat.name)) {
        expandedCategories.value.push(cat.name)
      }
    })
  }
}

function getCategoryLabel(key: string): string {
  const labels: Record<string, string> = {
    flow: t('categories.flow') || 'Flow Control',
    event: t('categories.event') || 'Events',
    dialogue: t('categories.dialogue') || 'Dialogue',
    logic: t('categories.logic') || 'Logic',
    resources: t('categories.resources') || 'Resources',
  }
  return labels[key] || key
}

function getNodeTitle(type: string): string {
  const titleKeys: Record<string, string> = {
    StartNode: 'nodes.start',
    EndNode: 'nodes.end',
    EventNode: 'nodes.event',
    DialogueNode: 'nodes.dialogue',
    BranchNode: 'nodes.branch',
    LogicNode: 'nodes.logic',
    ResourceNode: 'nodes.resources',
  }
  return t(titleKeys[type] || `nodes.${type}`) || type.replace('Node', '')
}

function getNodeDescription(type: string): string {
  const descriptions: Record<string, string> = {
    StartNode: 'Entry point of the visual novel scene',
    EndNode: 'End point / terminal node',
    EventNode: 'Trigger events (BGM, SFX, backgrounds, etc.) - 9 sub-types available',
    DialogueNode: 'Display character dialogue with speaker and text',
    BranchNode: 'Create conditional branches in the story',
    LogicNode: 'Variables, conditions, delays - 3 sub-types available',
    ResourceNode: 'Declare and initialize project resources (images, audio, data files)',
  }
  return descriptions[type] || ''
}

function getNodeIcon(type: string): string {
  const icons: Record<string, string> = {
    StartNode: '▶️',
    EndNode: '⏹️',
    EventNode: '⚡',
    DialogueNode: '💬',
    BranchNode: '❓',
    LogicNode: '🔧',
    ResourceNode: '📦',
  }
  return icons[type] || '📦'
}

// EventNode 有 9 种子类型，LogicNode 有 3 种子类型
function getNodeSubTypeCount(type: string): number {
  if (type === 'EventNode') return 9
  if (type === 'LogicNode') return 3
  return 0
}

// 拖拽开始：设置拖拽数据
function onDragStart(event: DragEvent, nodeType: string) {
  if (!event.dataTransfer) return
  
  event.dataTransfer.setData('application/node-type', nodeType)
  event.dataTransfer.effectAllowed = 'copy'
  
  // 添加拖拽样式类
  const target = event.target as HTMLElement
  target.classList.add('dragging')
}

// 拖拽结束：清理样式
function onDragEnd(event: DragEvent) {
  const target = event.target as HTMLElement
  target?.classList.remove('dragging')
}
</script>

<style scoped>
.node-palette {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #252526;
  overflow: hidden;
}

.palette-header {
  padding: 10px 12px;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.palette-header h3 {
  margin: 0;
  font-size: 11px;
  font-weight: 600;
  color: #cccccc;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.palette-search {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  border-bottom: 1px solid #3e3e42;
  flex-shrink: 0;
}

.search-icon {
  font-size: 12px;
  opacity: 0.5;
  flex-shrink: 0;
}

.palette-search input {
  flex: 1;
  min-width: 0;
  padding: 5px 8px;
  background: #3c3c3c;
  border: 1px solid #555555;
  border-radius: 3px;
  color: #e0e0e0;
  font-size: 11px;
  outline: none;
  transition: border-color 0.15s;
}

.palette-search input::placeholder {
  color: #606060;
}

.palette-search input:focus {
  border-color: #007acc;
}

.clear-search-btn {
  width: 18px;
  height: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: none;
  color: #808080;
  cursor: pointer;
  font-size: 10px;
  border-radius: 50%;
  transition: all 0.15s;
  flex-shrink: 0;
}

.clear-search-btn:hover {
  background: #4e4e4e;
  color: #cccccc;
}

.palette-categories {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}

.category {
  margin-bottom: 2px;
}

.category-header {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 8px;
  color: #a0a0a0;
  cursor: pointer;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 500;
  user-select: none;
  transition: all 0.1s;
}

.category-header:hover {
  background: rgba(255, 255, 255, 0.05);
  color: #cccccc;
}

.category-header .chevron-icon {
  transition: transform 0.2s ease;
  color: #707070;
  flex-shrink: 0;
}

.category-header.expanded .chevron-icon {
  transform: rotate(90deg);
}

.category-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.node-count-badge {
  font-size: 9px;
  color: #606060;
  background: #333333;
  padding: 1px 6px;
  border-radius: 8px;
}

.category-nodes {
  padding-left: 16px;
  padding-top: 2px;
  padding-bottom: 4px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  margin: 1px 0;
  color: #b0b0b0;
  background: transparent;
  border-radius: 4px;
  cursor: grab;
  font-size: 11px;
  transition: all 0.12s;
  user-select: none;
  border: 1px solid transparent;
}

.node-item:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #ffffff;
  border-color: rgba(255, 255, 255, 0.08);
}

.node-item:active {
  cursor: grabbing;
}

.node-item.dragging {
  opacity: 0.5;
  background: #094771;
}

/* 不同节点类型的颜色标识 */
.node-item.start { border-left: 2px solid #4ec9b0; }
.node-item.end { border-left: 2px solid #f48771; }
.node-item.event { border-left: 2px solid #dcdcaa; }
.node-item.dialogue { border-left: 2px solid #569cd6; }
.node-item.branch { border-left: 2px solid #c586c0; }
.node-item.logic { border-left: 2px solid #ce9178; }
.node-item.resources { border-left: 2px solid #9E9E9E; }

.node-icon {
  font-size: 13px;
  width: 16px;
  text-align: center;
  flex-shrink: 0;
}

.node-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 400;
}

.subtypes-hint {
  color: #606060;
  font-size: 9px;
  padding: 1px 5px;
  background: #1e1e1e;
  border-radius: 3px;
  white-space: nowrap;
}

.no-results {
  padding: 8px;
  text-align: center;
  color: #606060;
  font-size: 11px;
}

.global-no-results {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px 16px;
  color: #606060;
  text-align: center;
}

.global-no-results span {
  font-size: 28px;
  margin-bottom: 10px;
  opacity: 0.4;
}

.global-no-results p {
  margin: 0;
  font-size: 12px;
}
</style>
