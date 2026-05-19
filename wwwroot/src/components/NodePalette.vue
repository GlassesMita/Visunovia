<template>
  <div class="node-palette">
    <div class="palette-header">
      <h3>{{ t('palette.title') || 'Nodes' }}</h3>
    </div>
    
    <!-- 搜索框 -->
    <div class="palette-search">
      <input 
        type="text" 
        v-model="searchQuery" 
        :placeholder="t('common.search')"
      />
    </div>
    
    <!-- 节点分类 -->
    <div class="palette-categories">
      <div 
        v-for="category in filteredCategories" 
        :key="category.name"
        class="category"
      >
        <div class="category-header" @click="toggleCategory(category.name)">
          <ChevronDown 
            :size="16" 
            :class="{ rotated: !expandedCategories.includes(category.name) }"
          />
          <span>{{ getCategoryLabel(category.labelKey) }}</span>
        </div>
        
        <div 
          v-if="expandedCategories.includes(category.name)"
          class="category-nodes"
        >
          <div 
            v-for="node in category.nodes" 
            :key="node.type"
            class="node-item"
            draggable="true"
            @dragstart="onDragStart($event, node.type)"
          >
            <span class="node-icon">{{ getNodeIcon(node.type) }}</span>
            <span class="node-label">{{ getNodeTitle(node.type) }}</span>
            <span v-if="getNodeSubTypeCount(node.type)" class="node-subtypes">
              ({{ getNodeSubTypeCount(node.type) }})
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { ChevronDown } from 'lucide-vue-next'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

const searchQuery = ref('')
const expandedCategories = ref(['flow', 'event', 'dialogue', 'logic'])

// 6 种大类型节点配置
const nodeCategories = [
  {
    name: 'flow',
    labelKey: 'Flow',
    nodes: [
      { type: 'StartNode' },
      { type: 'EndNode' }
    ]
  },
  {
    name: 'event',
    labelKey: 'Event',
    nodes: [
      { type: 'EventNode' }
    ]
  },
  {
    name: 'dialogue',
    labelKey: 'Dialogue',
    nodes: [
      { type: 'DialogueNode' },
      { type: 'BranchNode' }
    ]
  },
  {
    name: 'logic',
    labelKey: 'Logic',
    nodes: [
      { type: 'LogicNode' }
    ]
  },
]

const filteredCategories = computed(() => {
  if (!searchQuery.value) return nodeCategories
  
  const query = searchQuery.value.toLowerCase()
  return nodeCategories.map(category => ({
    ...category,
    nodes: category.nodes.filter(node => 
      node.type.toLowerCase().includes(query) ||
      getNodeTitle(node.type).toLowerCase().includes(query)
    )
  })).filter(category => category.nodes.length > 0)
})

function toggleCategory(name: string) {
  const index = expandedCategories.value.indexOf(name)
  if (index >= 0) {
    expandedCategories.value.splice(index, 1)
  } else {
    expandedCategories.value.push(name)
  }
}

function getCategoryLabel(key: string): string {
  return t(`categories.${key}`) || key
}

function getNodeTitle(type: string): string {
  const titleKeys: Record<string, string> = {
    StartNode: 'nodes.start',
    EndNode: 'nodes.end',
    EventNode: 'nodes.event',
    DialogueNode: 'nodes.dialogue',
    BranchNode: 'nodes.branch',
    LogicNode: 'nodes.logic',
  }
  return t(titleKeys[type] || type)
}

function getNodeIcon(type: string): string {
  const icons: Record<string, string> = {
    StartNode: '▶',
    EndNode: '■',
    EventNode: '⚡',
    DialogueNode: '💬',
    BranchNode: '❓',
    LogicNode: '🔧',
  }
  return icons[type] || '●'
}

function getNodeSubTypeCount(type: string): number {
  // EventNode 有 9 种事件子类型
  if (type === 'EventNode') return 9
  // LogicNode 有 3 种逻辑子类型
  if (type === 'LogicNode') return 3
  return 0
}

function onDragStart(event: DragEvent, nodeType: string) {
  event.dataTransfer?.setData('application/node-type', nodeType)
  console.log('Dragging node:', nodeType)
}
</script>

<style scoped>
.node-palette {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #252526;
}

.palette-header {
  padding: 12px;
  border-bottom: 1px solid #3e3e42;
}

.palette-header h3 {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  color: #ffffff;
  text-transform: uppercase;
}

.palette-search {
  padding: 8px;
}

.palette-search input {
  width: 100%;
  padding: 6px 8px;
  background: #3c3c3c;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  color: #ffffff;
  font-size: 12px;
}

.palette-search input::placeholder {
  color: #808080;
}

.palette-categories {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.category {
  margin-bottom: 8px;
}

.category-header {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 500;
}

.category-header:hover {
  background: rgba(255, 255, 255, 0.1);
}

.category-header svg {
  transition: transform 0.2s;
}

.category-header svg.rotated {
  transform: rotate(-90deg);
}

.category-nodes {
  padding-left: 20px;
  margin-top: 4px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  margin: 2px 0;
  color: #cccccc;
  background: #2d2d30;
  border-radius: 4px;
  cursor: grab;
  font-size: 12px;
  transition: background 0.15s;
}

.node-item:hover {
  background: #3e3e42;
}

.node-item:active {
  cursor: grabbing;
}

.node-icon {
  font-size: 14px;
}

.node-label {
  flex: 1;
}

.node-subtypes {
  color: #808080;
  font-size: 11px;
  padding: 2px 6px;
  background: #1e1e1e;
  border-radius: 3px;
}
</style>
