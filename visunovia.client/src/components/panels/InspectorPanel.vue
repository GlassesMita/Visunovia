<template>
  <div class="inspector-panel">
    <!-- 无选中节点时的空状态 -->
    <div v-if="!selectedNode" class="empty-state">
      <div class="empty-icon">📋</div>
      <p>{{ t('common.noSelection') || 'No Selection' }}</p>
      <span class="empty-hint">{{ t('inspector.hint') || 'Select a node to view properties' }}</span>
    </div>

    <!-- 选中节点时显示属性检查器 -->
    <div v-else class="node-inspector" @keydown.tab.prevent="handleInspectorTab">
      <!-- 节点标题栏 -->
      <div class="node-header">
        <span class="node-type-icon">{{ getNodeIcon(selectedNode.type) }}</span>
        <h3>{{ getNodeTitle(selectedNode.type) }}</h3>
        <span class="node-id">{{ selectedNode.id.slice(0, 8) }}...</span>
      </div>

      <!-- EventNode 子类型选择器（9种事件类型） -->
      <div v-if="selectedNode.type === 'EventNode'" class="property-group subtype-selector">
        <label>
          <span class="label-icon">⚡</span>
          {{ t('props.subType') || 'Event Type' }}
        </label>
        <select 
          v-model="eventSubType" 
          @change="onEventSubTypeChange"
          class="subtype-select"
        >
          <option 
            v-for="(labelKey, type) in eventTypeLabels" 
            :key="type"
            :value="type"
          >
            {{ t(labelKey) }}
          </option>
        </select>
      </div>

      <!-- LogicNode 子类型选择器（3种逻辑类型） -->
      <div v-if="selectedNode.type === 'LogicNode'" class="property-group subtype-selector">
        <label>
          <span class="label-icon">🔧</span>
          {{ t('props.subType') || 'Logic Type' }}
        </label>
        <select 
          v-model="logicSubType" 
          @change="onLogicSubTypeChange"
          class="subtype-select"
        >
          <option 
            v-for="(labelKey, type) in logicTypeLabels" 
            :key="type"
            :value="type"
          >
            {{ t(labelKey) }}
          </option>
        </select>
      </div>

      <!-- 动态属性渲染区域 -->
      <div class="properties-section">
        <div class="section-title">{{ t('properties.title') || 'Properties' }}</div>
        
        <div 
          v-for="prop in dynamicProperties" 
          :key="prop.name"
          class="property-group"
        >
          <label :title="getPropertyDescription(prop.name)">
            {{ getPropertyLabel(prop.name) }}
          </label>
          
          <!-- String 类型 → 文本输入框 -->
          <input 
            v-if="prop.type === 'string'"
            type="text"
            :value="getPropertyValue(prop.name)"
            :placeholder="getPlaceholder(prop.name)"
            @input="updateProperty(prop.name, ($event.target as HTMLInputElement).value)"
            class="prop-input"
          />
          <div
            v-if="prop.type === 'string' && getPropertyValue(prop.name)"
            class="markdown-preview"
            v-html="renderSafeMarkdown(getPropertyValue(prop.name))"
          />
          
          <!-- Number 类型 → 数字输入框 -->
          <input 
            v-else-if="prop.type === 'number'"
            type="number"
            step="0.01"
            :value="getPropertyValue(prop.name)"
            @input="updateProperty(prop.name, parseFloat(($event.target as HTMLInputElement).value))"
            class="prop-input prop-number"
          />
          
          <!-- Boolean 类型 → 复选框 -->
          <label v-else-if="prop.type === 'boolean'" class="checkbox-label">
            <input 
              type="checkbox"
              :checked="getPropertyValue(prop.name)"
              @change="updateProperty(prop.name, ($event.target as HTMLInputElement).checked)"
              class="prop-checkbox"
            />
            <span class="checkbox-text">
              {{ getPropertyValue(prop.name) ? t('common.yes') || 'Yes' : t('common.no') || 'No' }}
            </span>
          </label>
          
          <!-- Select 类型 → 下拉选择框 -->
          <select 
            v-else-if="prop.type === 'select'"
            :value="getPropertyValue(prop.name)"
            @change="updateProperty(prop.name, ($event.target as HTMLSelectElement).value)"
            class="prop-input prop-select"
          >
            <option 
              v-for="opt in (prop.options || [])" 
              :key="opt.value"
              :value="opt.value"
            >
              {{ opt.label }}
            </option>
          </select>

          <!-- Character 类型 → 角色管理器快捷选择 -->
          <div v-else-if="prop.type === 'character'" class="character-select-wrapper">
            <select
              :value="getPropertyValue(prop.name)"
              class="prop-input prop-select"
              @change="updateProperty(prop.name, ($event.target as HTMLSelectElement).value)"
            >
              <option value="">未选择角色</option>
              <option
                v-for="character in characterStore.sortedCharacters"
                :key="character.id"
                 :value="character.id"
              >
                {{ character.name }} ({{ character.displayId || character.id }})
              </option>
            </select>
            <button class="character-manager-btn" type="button" title="打开角色管理器" @click="uiStore.openCharacterManager()">👥</button>
          </div>
          
          <!-- Resource 类型 → Unity ObjectField 风格只读输入框 + 浏览按钮 -->
          <div v-else-if="prop.type === 'resource'" class="resource-input-wrapper">
            <input 
              type="text"
              readonly
              :value="getPropertyValue(prop.name)"
              :placeholder="t('properties.resourcePath') || '点击选择资源...'"
              @click="handleResourceBrowse(prop.name)"
              class="prop-input prop-resource prop-readonly"
            />
            <button 
              class="browse-btn"
              :title="t('common.browse') || 'Browse'"
              @click="handleResourceBrowse(prop.name)"
            >
              📂
            </button>
          </div>
        </div>

        <!-- 无动态属性时的提示 -->
        <div v-if="dynamicProperties.length === 0 && !isSpecialNode" class="no-properties">
          <span>{{ t('inspector.noProperties') || 'No editable properties' }}</span>
        </div>
      </div>

      <!-- 节点位置信息 -->
      <div class="node-position">
        <div class="position-item">
          <span>X:</span>
          <span>{{ Math.round(selectedNode.position?.x || 0) }}</span>
        </div>
        <div class="position-item">
          <span>Y:</span>
          <span>{{ Math.round(selectedNode.position?.y || 0) }}</span>
        </div>
      </div>
    </div>

    <!-- 资源选择模态框 -->
    <ResourcePickerModal
      :visible="showResourcePicker"
      :resource-type="currentResourceType"
      :current-path="getPropertyValue(currentResourcePropName) || ''"
      :files="currentResourceFiles"
      @confirm="onResourcePicked"
      @cancel="showResourcePicker = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useCharacterStore } from '@/stores/useCharacterStore'
import { useUIStore } from '@/stores/useUIStore'
import { 
  EventType, 
  LogicType,
  eventTypeLabels, 
  logicTypeLabels,
  eventTypeConfig,
  logicTypeConfig,
  PropertyConfig 
} from '@/types'
import ResourcePickerModal from '@/components/modals/ResourcePickerModal.vue'
import { RESOURCE_TYPE_EXTENSIONS, type ResourceType } from '@/stores/useResourceRegistry'
import { getEntries, type DirEntry } from '@/api/fileBrowser'

const { t } = useLocalization()
const editorStore = useEditorStore()
const nodeGraphStore = useNodeGraphStore()
const characterStore = useCharacterStore()
const uiStore = useUIStore()

const showResourcePicker = ref(false)
const currentResourcePropName = ref('')
const currentResourceType = ref<ResourceType>('image')
const currentResourceFiles = ref<Array<{ name: string; path?: string }>>([])

const selectedNode = computed(() => editorStore.selectedNode)

const eventSubType = ref<EventType>(EventType.PlayBGM)
const logicSubType = ref<LogicType>(LogicType.SetVariable)

onMounted(() => {
  characterStore.load().catch(error => {
    console.warn('[InspectorPanel] Failed to load characters:', error)
  })
})

// 判断是否为特殊节点（仅 StartNode 无额外属性，EndNode 有 eventType/sceneId 需要编辑）
const isSpecialNode = computed(() => {
  if (!selectedNode.value) return false
  return selectedNode.value.type === 'StartNode'
})

// 监听选中节点变化，更新子类型
watch(selectedNode, (node) => {
  if (!node) return
  
  if (node.type === 'EventNode') {
    eventSubType.value = node.data?.subType || EventType.PlayBGM
  } else if (node.type === 'LogicNode') {
    logicSubType.value = node.data?.subType || LogicType.SetVariable
  }
}, { immediate: true })

// 动态计算当前节点应显示的属性列表
const dynamicProperties = computed((): PropertyConfig[] => {
  if (!selectedNode.value) return []
  
  // EventNode 根据子类型返回对应属性
  if (selectedNode.value.type === 'EventNode') {
    return eventTypeConfig[eventSubType.value]?.properties || []
  }
  
  // LogicNode 根据子类型返回对应属性
  if (selectedNode.value.type === 'LogicNode') {
    return logicTypeConfig[logicSubType.value]?.properties || []
  }
  
  // 其他节点类型的默认属性
  const defaultProps: Record<string, PropertyConfig[]> = {
    EndNode: [
      {
        name: 'eventType',
        type: 'select',
        defaultValue: 'end_game',
        options: [
          { value: 'end_game', label: '结束游戏' },
          { value: 'return_to_menu', label: '返回主菜单' },
          { value: 'jump_to_scene', label: '跳转场景' },
        ],
      },
      {
        name: 'sceneId',
        type: 'string',
        defaultValue: '',
      },
    ],
    DialogueNode: [
      { name: 'text', type: 'string', defaultValue: '' },
    ],
    CharacterControlNode: [
      { name: 'character', type: 'character', defaultValue: '', label: 'CharacterID' },
      { name: 'unmanagedCharacter', type: 'string', defaultValue: '', label: 'Slot 6 角色名' },
      {
        name: 'slot',
        type: 'select',
        defaultValue: '1',
        label: '控制角色槽位',
        options: [
          { value: '1', label: '角色 1' },
          { value: '2', label: '角色 2' },
          { value: '3', label: '角色 3' },
          { value: '4', label: '角色 4' },
          { value: '5', label: '角色 5' },
          { value: '6', label: '角色 6（无立绘）' },
        ],
      },
      {
        name: 'action',
        type: 'select',
        defaultValue: 'show',
        label: '显示类型',
        options: [
          { value: 'show', label: '角色显示' },
          { value: 'hide', label: '角色消失' },
          { value: 'update', label: '更新角色' },
        ],
      },
      { name: 'sprite', type: 'resource', defaultValue: '', label: '立绘' },
      { name: 'sfx', type: 'resource', defaultValue: '', label: '音效' },
      { name: 'expression', type: 'string', defaultValue: 'default', label: '表情' },
      {
        name: 'position',
        type: 'select',
        defaultValue: 'center',
        label: '位置',
        options: [
          { value: 'left', label: '左侧' },
          { value: 'center', label: '中间' },
          { value: 'right', label: '右侧' },
        ],
      },
      {
        name: 'animation',
        type: 'select',
        defaultValue: 'fade',
        label: '动画效果',
        options: [
          { value: 'none', label: '无' },
          { value: 'fade', label: '淡入淡出' },
          { value: 'slide', label: '滑入滑出' },
          { value: 'pop', label: '弹出' },
        ],
      },
      { name: 'duration', type: 'number', defaultValue: 0.3, label: '动画时长' },
    ],
    BranchNode: [
      { name: 'condition', type: 'string', defaultValue: '' },
      { name: 'options', type: 'string', defaultValue: '' },
    ],
  }
  
  return defaultProps[selectedNode.value.type] || []
})

function getNodeTitle(type: string): string {
  const titleKeys: Record<string, string> = {
    StartNode: 'nodes.start',
    EndNode: 'nodes.end',
    EventNode: 'nodes.event',
    DialogueNode: 'nodes.dialogue',
    CharacterControlNode: 'nodes.characterControl',
    BranchNode: 'nodes.branch',
    LogicNode: 'nodes.logic',
  }
  return t(titleKeys[type] || type)
}

function getNodeIcon(type: string): string {
  const icons: Record<string, string> = {
    StartNode: '▶️',
    EndNode: '⏹️',
    EventNode: '⚡',
    DialogueNode: '💬',
    CharacterControlNode: '🎭',
    BranchNode: '❓',
    LogicNode: '🔧',
  }
  return icons[type] || '📦'
}

function getPropertyLabel(name: string): string {
  const prop = dynamicProperties.value.find(item => item.name === name)
  if (prop?.label) return prop.label

  const label = t(`properties.${name}`).value
  return label !== `properties.${name}` ? label : name
}

function getPropertyDescription(_name: string): string {
  return ''
}

function getPlaceholder(name: string): string {
  const placeholders: Record<string, string> = {
    speaker: t('properties.speaker').value || 'Speaker name...',
    text: t('properties.text').value || 'Dialogue text...',
    varName: t('properties.varName').value || 'Variable name...',
    value: t('properties.value').value || 'Value...',
    characterId: t('properties.characterId').value || 'Character ID...',
    expression: t('properties.expression').value || 'Expression...',
  }
  return placeholders[name] || ''
}

function getPropertyValue(name: string): any {
  if (!selectedNode.value?.data) return undefined
  return selectedNode.value.data[name]
}

function updateProperty(name: string, value: any) {
  if (!selectedNode.value) return
  
  // 更新本地数据引用（用于响应式显示）
  if (selectedNode.value.data) {
    selectedNode.value.data[name] = value
  }

  const editorNode = nodeGraphStore.editor?.graph.nodes.find(node => node.id === selectedNode.value?.id) as any
  const iface = editorNode?.inputs?.[name]
  if (iface) {
    if (typeof iface.setValue === 'function') {
      iface.setValue(value)
    } else {
      iface.value = value
    }
  }
  
  // 同步到 store（标记为已修改）
  nodeGraphStore.isDirty = true
}

function handleInspectorTab(event: KeyboardEvent) {
  const container = event.currentTarget as HTMLElement
  const focusable = Array.from(container.querySelectorAll<HTMLElement>(
    'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])'
  )).filter(el => el.offsetParent !== null)

  if (focusable.length === 0) return

  const currentIndex = focusable.indexOf(document.activeElement as HTMLElement)
  const nextIndex = event.shiftKey
    ? (currentIndex <= 0 ? focusable.length - 1 : currentIndex - 1)
    : (currentIndex < 0 || currentIndex >= focusable.length - 1 ? 0 : currentIndex + 1)

  focusable[nextIndex]?.focus()
}

function renderSafeMarkdown(value: any): string {
  const escaped = escapeHtml(String(value ?? ''))

  return escaped
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/__([^_]+)__/g, '<strong>$1</strong>')
    .replace(/(^|[^*])\*([^*]+)\*(?!\*)/g, '$1<em>$2</em>')
    .replace(/(^|[^_])_([^_]+)_(?!_)/g, '$1<em>$2</em>')
    .replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_match, label, url) => renderSafeLink(label, url))
    .replace(/\n/g, '<br>')
}

function renderSafeLink(label: string, rawUrl: string): string {
  const url = rawUrl.replace(/&amp;/g, '&').trim()
  if (!/^(https?:|mailto:)/i.test(url)) {
    return label
  }

  return `<a href="${escapeAttribute(url)}" target="_blank" rel="noopener noreferrer">${label}</a>`
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

function escapeAttribute(value: string): string {
  return escapeHtml(value).replace(/`/g, '&#96;')
}

function onEventSubTypeChange() {
  if (selectedNode.value && selectedNode.value.data) {
    selectedNode.value.data.subType = eventSubType.value
    nodeGraphStore.isDirty = true
  }
}

function onLogicSubTypeChange() {
  if (selectedNode.value && selectedNode.value.data) {
    selectedNode.value.data.subType = logicSubType.value
    nodeGraphStore.isDirty = true
  }
}

function getSelectedCharacterProfile() {
  const selectedCharacterName = String(getPropertyValue('character') || '').trim()
  if (!selectedCharacterName) return null

  return characterStore.sortedCharacters.find(character => character.id === selectedCharacterName) || null
}

function toResourcePickerFiles(entries: DirEntry[]) {
  return entries
    .filter(entry => !entry.isDirectory)
    .map(entry => ({ name: entry.name, path: entry.path }))
}

async function loadCharacterSpriteFiles() {
  const character = getSelectedCharacterProfile()
  const spriteFolder = character?.spriteFolder?.trim()
  if (!spriteFolder) {
    currentResourceFiles.value = []
    return
  }

  try {
    const result = await getEntries(spriteFolder)
    const imageExts = RESOURCE_TYPE_EXTENSIONS.image
    currentResourceFiles.value = toResourcePickerFiles(result.entries)
      .filter(file => imageExts.includes(file.name.slice(file.name.lastIndexOf('.')).toLowerCase()))
      .filter(file => !/[\\/]Avatars[\\/]/i.test(file.path || ''))
  } catch (error) {
    console.warn('[InspectorPanel] Failed to load character sprite folder:', error)
    currentResourceFiles.value = []
  }
}

async function handleResourceBrowse(propName: string) {
  currentResourcePropName.value = propName
  currentResourceFiles.value = []

  if (selectedNode.value?.type === 'ResourceNode') {
    const nodeResourceType = ((selectedNode.value as any)?.state?.resourceType
      || (selectedNode.value as any)?.options?.resourceType
      || 'image') as ResourceType
    currentResourceType.value = nodeResourceType
  } else {
    const typeMap: Partial<Record<string, ResourceType>> = {
      voice: 'voice',
      voice2: 'voice',
      voice3: 'voice',
      voice4: 'voice',
      voice5: 'voice',
      sfx: 'audio',
      bgm: 'bgm',
      imagePath: 'image',
      background: 'image',
      sprite1: 'image',
      sprite2: 'image',
      sprite3: 'image',
      sprite4: 'image',
      sprite5: 'image',
      sprite: 'image',
    }
    currentResourceType.value = typeMap[propName] || 'image'
  }

  if (selectedNode.value?.type === 'CharacterControlNode' && propName === 'sprite') {
    await loadCharacterSpriteFiles()
  }

  showResourcePicker.value = true
}

function onResourcePicked(path: string) {
  if (currentResourcePropName.value) {
    updateProperty(currentResourcePropName.value, path)
  }
  showResourcePicker.value = false
}
</script>

<style scoped>
.inspector-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow-y: auto;
  background: #252526;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #808080;
  padding: 24px;
  text-align: center;
}

.empty-icon {
  font-size: 36px;
  margin-bottom: 12px;
  opacity: 0.6;
}

.empty-state p {
  margin: 0 0 8px;
  font-size: 13px;
  color: #a0a0a0;
}

.empty-hint {
  font-size: 11px;
  opacity: 0.7;
}

.node-inspector {
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.node-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-bottom: 10px;
  border-bottom: 1px solid #3e3e42;
}

.node-type-icon {
  font-size: 16px;
}

.node-header h3 {
  margin: 0;
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: #ffffff;
}

.node-id {
  font-size: 10px;
  color: #606060;
  font-family: monospace;
}

.properties-section {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.section-title {
  font-size: 11px;
  font-weight: 600;
  color: #808080;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 4px;
}

.property-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.subtype-selector {
  background: #2d2d30;
  padding: 10px;
  border-radius: 4px;
  border: 1px solid #3e3e42;
}

.property-group > label {
  font-size: 11px;
  color: #9d9d9d;
  display: flex;
  align-items: center;
  gap: 4px;
}

.label-icon {
  font-size: 12px;
}

.subtype-select,
.prop-input,
.prop-select {
  width: 100%;
  padding: 6px 8px;
  background: #3c3c3c;
  border: 1px solid #555555;
  border-radius: 3px;
  color: #e0e0e0;
  font-size: 12px;
  outline: none;
  transition: border-color 0.15s;
}

.subtype-select:focus,
.prop-input:focus,
.prop-select:focus {
  border-color: #007acc;
}

.character-select-wrapper {
  display: flex;
  gap: 6px;
}

.character-select-wrapper .prop-select {
  flex: 1;
}

.character-manager-btn {
  width: 32px;
  border: 1px solid #555555;
  border-radius: 3px;
  color: #d4d4d4;
  background: #3c3c3c;
  cursor: pointer;
}

.character-manager-btn:hover {
  border-color: #007acc;
  background: #4e4e4e;
}

.prop-number {
  -moz-appearance: textfield;
}

.markdown-preview {
  padding: 8px;
  border: 1px solid #3e3e42;
  border-radius: 3px;
  background: #1e1e1e;
  color: #d4d4d4;
  font-size: 12px;
  line-height: 1.5;
  word-break: break-word;
}

.markdown-preview :deep(code) {
  padding: 1px 4px;
  border-radius: 3px;
  background: #2d2d30;
  color: #f0f0f0;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
}

.markdown-preview :deep(a) {
  color: #4ea1ff;
}

.prop-number::-webkit-inner-spin-button,
.prop-number::-webkit-outer-spin-button {
  opacity: 0.5;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  padding: 4px 0;
}

.prop-checkbox {
  width: 14px;
  height: 14px;
  accent-color: #007acc;
  cursor: pointer;
}

.checkbox-text {
  font-size: 12px;
  color: #cccccc;
}

.resource-input-wrapper {
  display: flex;
  gap: 4px;
}

.prop-resource {
  flex: 1;
}

.browse-btn {
  width: 32px;
  background: #3c3c3c;
  border: 1px solid #555555;
  border-radius: 3px;
  cursor: pointer;
  font-size: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
}

.browse-btn:hover {
  background: #4e4e4e;
  border-color: #007acc;
}

.prop-readonly {
  cursor: pointer;
  background: #2d2d30;
  color: #a0a0a0;
}

.prop-readonly:hover {
  border-color: #007acc;
  color: #e0e0e0;
}

.no-properties {
  padding: 16px;
  text-align: center;
  color: #606060;
  font-size: 12px;
}

.node-position {
  display: flex;
  gap: 16px;
  padding-top: 10px;
  border-top: 1px solid #3e3e42;
  font-size: 11px;
  color: #707070;
  font-family: monospace;
}

.position-item {
  display: flex;
  gap: 4px;
}

.position-item span:last-child {
  color: #a0a0a0;
}
</style>
