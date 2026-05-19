<template>
  <div class="inspector-panel">
    <div v-if="!selectedNode" class="empty-state">
      <p>{{ t('common.noSelection') }}</p>
    </div>
    <div v-else class="node-inspector">
      <div class="node-header">
        <h3>{{ getNodeTitle(selectedNode.type) }}</h3>
      </div>
      
      <div class="node-properties">
        <!-- EventNode 子类型选择器 -->
        <div v-if="selectedNode.type === 'EventNode'" class="property-group">
          <label>{{ t('properties.subType') || 'Sub Type' }}</label>
          <select v-model="eventSubType" @change="onEventSubTypeChange">
            <option 
              v-for="(label, type) in eventTypeLabels" 
              :key="type"
              :value="type"
            >
              {{ t(label) }}
            </option>
          </select>
        </div>
        
        <!-- LogicNode 子类型选择器 -->
        <div v-if="selectedNode.type === 'LogicNode'" class="property-group">
          <label>{{ t('properties.subType') || 'Sub Type' }}</label>
          <select v-model="logicSubType" @change="onLogicSubTypeChange">
            <option 
              v-for="(label, type) in logicTypeLabels" 
              :key="type"
              :value="type"
            >
              {{ t(label) }}
            </option>
          </select>
        </div>
        
        <!-- 动态属性渲染 -->
        <div 
          v-for="prop in dynamicProperties" 
          :key="prop.name"
          class="property-group"
        >
          <label>{{ getPropertyLabel(prop.name) }}</label>
          
          <!-- String 类型 -->
          <input 
            v-if="prop.type === 'string'"
            type="text"
            :value="getPropertyValue(prop.name)"
            @input="updateProperty(prop.name, ($event.target as HTMLInputElement).value)"
          />
          
          <!-- Number 类型 -->
          <input 
            v-else-if="prop.type === 'number'"
            type="number"
            :value="getPropertyValue(prop.name)"
            @input="updateProperty(prop.name, parseFloat(($event.target as HTMLInputElement).value))"
          />
          
          <!-- Boolean 类型 -->
          <input 
            v-else-if="prop.type === 'boolean'"
            type="checkbox"
            :checked="getPropertyValue(prop.name)"
            @change="updateProperty(prop.name, ($event.target as HTMLInputElement).checked)"
          />
          
          <!-- Select 类型 -->
          <select 
            v-else-if="prop.type === 'select'"
            :value="getPropertyValue(prop.name)"
            @change="updateProperty(prop.name, ($event.target as HTMLSelectElement).value)"
          >
            <option 
              v-for="opt in prop.options" 
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </option>
          </select>
          
          <!-- Resource 类型 -->
          <input 
            v-else-if="prop.type === 'resource'"
            type="text"
            :value="getPropertyValue(prop.name)"
            @input="updateProperty(prop.name, ($event.target as HTMLInputElement).value)"
            placeholder="Resource path..."
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { 
  EventType, 
  LogicType,
  eventTypeLabels, 
  logicTypeLabels,
  eventTypeConfig,
  logicTypeConfig,
  PropertyConfig 
} from '@/types'

const { t } = useLocalization()

// 模拟选中节点，实际应该从 store 获取
const selectedNode = ref<any>(null)

const eventSubType = ref<EventType>(EventType.PlayBGM)
const logicSubType = ref<LogicType>(LogicType.SetVariable)

// 监听选中节点变化，更新子类型
watch(selectedNode, (node) => {
  if (node?.type === 'EventNode') {
    eventSubType.value = node.data?.subType || EventType.PlayBGM
  } else if (node?.type === 'LogicNode') {
    logicSubType.value = node.data?.subType || LogicType.SetVariable
  }
})

const dynamicProperties = computed((): PropertyConfig[] => {
  if (!selectedNode.value) return []
  
  if (selectedNode.value.type === 'EventNode') {
    return eventTypeConfig[eventSubType.value]?.properties || []
  }
  
  if (selectedNode.value.type === 'LogicNode') {
    return logicTypeConfig[logicSubType.value]?.properties || []
  }
  
  return []
})

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

function getPropertyLabel(name: string): string {
  return t(`properties.${name}`) || name
}

function getPropertyValue(name: string): any {
  return selectedNode.value?.data?.[name]
}

function updateProperty(name: string, value: any) {
  if (selectedNode.value) {
    // 实际应该调用 store 的更新方法
    selectedNode.value.data = {
      ...selectedNode.value.data,
      [name]: value
    }
  }
}

function onEventSubTypeChange() {
  if (selectedNode.value) {
    selectedNode.value.data = {
      ...selectedNode.value.data,
      subType: eventSubType.value
    }
  }
}

function onLogicSubTypeChange() {
  if (selectedNode.value) {
    selectedNode.value.data = {
      ...selectedNode.value.data,
      subType: logicSubType.value
    }
  }
}
</script>

<style scoped>
.inspector-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow-y: auto;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #808080;
}

.node-inspector {
  padding: 16px;
}

.node-header {
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid #3e3e42;
}

.node-header h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: #ffffff;
}

.property-group {
  margin-bottom: 12px;
}

.property-group label {
  display: block;
  margin-bottom: 4px;
  font-size: 12px;
  color: #cccccc;
}

.property-group input[type="text"],
.property-group input[type="number"],
.property-group select {
  width: 100%;
  padding: 6px 8px;
  background: #3c3c3c;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  color: #ffffff;
  font-size: 12px;
}

.property-group input[type="checkbox"] {
  width: 16px;
  height: 16px;
}
</style>
