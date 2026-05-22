<template>
  <div class="hierarchy-panel">
    <div class="panel-header">
      <h3>{{ t('panels.hierarchy') }}</h3>
      <span class="node-count-badge">{{ nodes.length }}</span>
    </div>
    <div class="panel-content">
      <!-- 节点列表 -->
      <div 
        v-for="node in nodes" 
        :key="node.id"
        class="node-item"
        :class="{ 
          selected: isSelected(node.id),
          [node.type]: true 
        }"
        @click="selectNode(node.id)"
        @dblclick="focusNode(node.id)"
      >
        <span class="node-icon">{{ getNodeIcon(node.type) }}</span>
        <span class="node-label">{{ getNodeDisplayName(node) }}</span>
        <span class="node-type-badge">{{ node.type.replace('Node', '') }}</span>
      </div>

      <!-- 空状态 -->
      <div v-if="nodes.length === 0" class="empty-state">
        <div class="empty-icon">🔷</div>
        <p>{{ t('common.noNodes') || 'No nodes yet' }}</p>
        <span class="empty-hint">{{ t('hierarchy.hint') || 'Add nodes from the palette' }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'

const { t } = useI18n()
const nodeGraphStore = useNodeGraphStore()
const editorStore = useEditorStore()

// 从 store 获取节点列表
const nodes = computed(() => nodeGraphStore.nodes)

function isSelected(nodeId: string): boolean {
  return editorStore.selectedNodeId === nodeId
}

function selectNode(id: string) {
  editorStore.selectNode(editorStore.selectedNodeId === id ? null : id)
}

function focusNode(_id: string) {
  // 聚焦到编辑器中的节点（可扩展）
  console.log('Focus node:', _id)
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

function getNodeDisplayName(node: any): string {
  // 根据节点类型和数据显示不同的名称
  if (node.type === 'DialogueNode') {
    const speaker = node.data?.speaker
    const text = node.data?.text
    if (speaker && text) {
      return `${speaker}: ${text.slice(0, 20)}${text.length > 20 ? '...' : ''}`
    }
    if (speaker) return speaker
  }

  if (node.type === 'EventNode') {
    const subType = node.data?.subType
    if (subType) {
      // 将 subType key 转为 i18n label
      const typeKey = `eventTypes.${subType}`
      const translated = t(typeKey)
      return translated !== typeKey ? translated : subType
    }
  }

  return getNodeTitle(node.type)
}
</script>

<style scoped>
.hierarchy-panel {
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

.node-count-badge {
  font-size: 10px;
  color: #808080;
  background: #333333;
  padding: 1px 7px;
  border-radius: 8px;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}

.node-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 11px;
  transition: all 0.1s;
  user-select: none;
}

.node-item:hover {
  background: rgba(255, 255, 255, 0.06);
}

.node-item.selected {
  background: #094771;
  color: #ffffff;
}

/* 不同类型节点的图标颜色 */
.node-item.StartNode .node-icon { color: #4ec9b0; }
.node-item.EndNode .node-icon { color: #f48771; }
.node-item.EventNode .node-icon { color: #dcdcaa; }
.node-item.DialogueNode .node-icon { color: #569cd6; }
.node-item.BranchNode .node-icon { color: #c586c0; }
.node-item.LogicNode .node-icon { color: #ce9178; }

.node-icon {
  font-size: 13px;
  width: 18px;
  text-align: center;
  flex-shrink: 0;
}

.node-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.node-type-badge {
  font-size: 9px;
  color: #606060;
  background: #333333;
  padding: 1px 5px;
  border-radius: 3px;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #606060;
  padding: 24px;
  text-align: center;
}

.empty-icon {
  font-size: 32px;
  margin-bottom: 10px;
  opacity: 0.5;
}

.empty-state p {
  margin: 0 0 6px;
  font-size: 12px;
  color: #808080;
}

.empty-hint {
  font-size: 10px;
  opacity: 0.6;
}
</style>
