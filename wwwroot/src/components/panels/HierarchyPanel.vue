<template>
  <div class="hierarchy-panel">
    <div class="panel-header">
      <h3>{{ t('panels.hierarchy') }}</h3>
    </div>
    <div class="panel-content">
      <div 
        v-for="node in nodes" 
        :key="node.id"
        class="node-item"
        :class="{ selected: selectedNodeId === node.id }"
        @click="selectNode(node.id)"
      >
        <span class="node-icon">{{ getNodeIcon(node.type) }}</span>
        <span class="node-label">{{ getNodeTitle(node.type) }}</span>
      </div>
      <div v-if="nodes.length === 0" class="empty-state">
        <p>{{ t('common.noNodes') }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

// 模拟节点数据，实际应该从 store 获取
const nodes = ref<any[]>([])
const selectedNodeId = ref<string | null>(null)

function selectNode(id: string) {
  selectedNodeId.value = id
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
</script>

<style scoped>
.hierarchy-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.panel-header {
  padding: 12px;
  border-bottom: 1px solid #3e3e42;
}

.panel-header h3 {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  color: #ffffff;
  text-transform: uppercase;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 13px;
}

.node-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.node-item.selected {
  background: #094771;
  color: #ffffff;
}

.node-icon {
  font-size: 14px;
}

.node-label {
  flex: 1;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #808080;
  font-size: 12px;
}
</style>
