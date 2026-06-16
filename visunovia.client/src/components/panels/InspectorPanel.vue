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

        <div v-if="selectedNode.type === 'CharacterControlNode'" class="character-control-editor">
          <div class="character-control-summary">
            <div>
              <strong>{{ t('characterControl.preview') || 'Stage Preview' }}</strong>
              <span>{{ modifiedCharacterControls.length }} {{ t('characterControl.modifiedSlots') || 'modified slots' }}</span>
            </div>
            <button type="button" class="character-manager-btn" title="打开角色管理器" @click="uiStore.openCharacterManager()">👥</button>
          </div>

          <div class="character-stage-preview" aria-label="角色控制预览">
            <div class="character-stage-grid"></div>
            <img
              v-for="character in characterControlPreviewCharacters"
              :key="character.slot"
              class="character-stage-sprite"
              :style="getCharacterPreviewStyle(character)"
              :src="character.spriteUrl"
              :alt="character.character || `slot-${character.slot}`"
            />
            <div
              v-for="expression in characterControlPreviewExpressions"
              :key="expression.slot"
              class="character-stage-expression"
              :style="getExpressionPreviewStyle(expression)"
            >
              <img v-if="expression.balloonUrl" class="character-stage-expression-balloon" :src="expression.balloonUrl" alt="balloon" />
              <img v-if="expression.iconUrl" class="character-stage-expression-icon" :src="expression.iconUrl" alt="expression" />
            </div>
            <div v-if="characterControlPreviewCharacters.length === 0" class="character-stage-empty">
              {{ t('characterControl.noPreview') || 'No visible character changes' }}
            </div>
          </div>

          <div class="character-slot-tabs" role="tablist" aria-label="角色 Slot">
            <button
              v-for="slot in characterControlSlots"
              :key="slot"
              type="button"
              class="character-slot-tab"
              :class="{ active: activeCharacterSlot === slot, modified: isCharacterSlotModified(slot) }"
              @click="activeCharacterSlot = slot"
            >
              Slot {{ slot }}
            </button>
          </div>

          <div class="character-slot-form">
            <div class="property-group">
              <label>{{ t('characterControl.action') || 'Action' }}</label>
              <select class="prop-input prop-select" :value="activeCharacterControl.mode" @change="updateCharacterControlField('mode', ($event.target as HTMLSelectElement).value)">
                <option value="none">{{ t('characterControl.noChange') || 'No Change' }}</option>
                <option value="show">{{ t('characterControl.show') || 'Show Character' }}</option>
                <option value="hide">{{ t('characterControl.hide') || 'Hide Character' }}</option>
                <option value="update">{{ t('characterControl.update') || 'Update Character' }}</option>
                <option value="move">{{ t('characterControl.move') || 'Move Character' }}</option>
                <option value="rotate180">平面旋转 180°</option>
                <option value="moveReturn">左右移动后回到原位</option>
                <option value="exitLeft">从左侧移出舞台</option>
                <option value="exitRight">从右侧移出舞台</option>
                <option value="shakeFallExit">左右晃动后倒地退出</option>
                <option value="expression">表情表达</option>
              </select>
            </div>

            <template v-if="activeCharacterControl.mode !== 'none'">
              <div v-if="activeCharacterSlot === '6'" class="property-group">
                <label>{{ t('characterControl.unmanagedCharacter') || 'Slot 6 Character Name' }}</label>
                <input class="prop-input" type="text" :value="activeCharacterControl.unmanagedCharacter" @input="updateCharacterControlField('unmanagedCharacter', ($event.target as HTMLInputElement).value)" />
              </div>

              <div v-else class="property-group">
                <label>{{ t('eventProps.characterId') || 'Character' }}</label>
                <div class="character-select-wrapper">
                  <select class="prop-input prop-select" :value="activeCharacterControl.character" @change="updateCharacterControlField('character', ($event.target as HTMLSelectElement).value)">
                    <option value="">{{ t('characterControl.unselected') || 'Unselected' }}</option>
                    <option v-for="character in characterStore.sortedCharacters" :key="character.id" :value="character.id">
                      {{ character.name }} ({{ character.displayId || character.id }})
                    </option>
                  </select>
                  <button class="character-manager-btn" type="button" title="打开角色管理器" @click="uiStore.openCharacterManager()">👥</button>
                </div>
              </div>

              <div v-if="activeCharacterSlot !== '6' && shouldShowSpritePicker(activeCharacterControl.mode)" class="property-group">
                <label>{{ t('characterControl.sprite') || 'Sprite' }}</label>
                <div class="resource-input-wrapper">
                  <input class="prop-input prop-resource prop-readonly" type="text" readonly :value="activeCharacterControl.sprite" @click="handleCharacterControlResourceBrowse('sprite')" />
                  <button class="browse-btn" type="button" @click="handleCharacterControlResourceBrowse('sprite')">📂</button>
                </div>
              </div>

              <div v-if="shouldShowPositionControls(activeCharacterControl.mode)" class="property-group">
                <label>{{ t('eventProps.position') || 'Position' }}</label>
                <select class="prop-input prop-select" :value="activeCharacterControl.position" @change="updateCharacterControlField('position', ($event.target as HTMLSelectElement).value)">
                  <option value="left">{{ t('eventOptions.left') || 'Left' }}</option>
                  <option value="center">{{ t('eventOptions.center') || 'Center' }}</option>
                  <option value="right">{{ t('eventOptions.right') || 'Right' }}</option>
                </select>
              </div>

              <div v-if="activeCharacterControl.mode === 'move'" class="character-control-grid">
                <div class="property-group">
                  <label>{{ t('characterControl.fromPosition') || 'From Position' }}</label>
                  <select class="prop-input prop-select" :value="activeCharacterControl.fromPosition" @change="updateCharacterControlField('fromPosition', ($event.target as HTMLSelectElement).value)">
                    <option value="">{{ t('characterControl.currentPosition') || 'Current Position' }}</option>
                    <option value="left">{{ t('eventOptions.left') || 'Left' }}</option>
                    <option value="center">{{ t('eventOptions.center') || 'Center' }}</option>
                    <option value="right">{{ t('eventOptions.right') || 'Right' }}</option>
                  </select>
                </div>
                <div class="property-group">
                  <label>{{ t('characterControl.toPosition') || 'To Position' }}</label>
                  <select class="prop-input prop-select" :value="activeCharacterControl.toPosition" @change="updateCharacterControlField('toPosition', ($event.target as HTMLSelectElement).value)">
                    <option value="none">{{ t('characterControl.noMove') || 'No Move' }}</option>
                    <option value="left">{{ t('eventOptions.left') || 'Left' }}</option>
                    <option value="center">{{ t('eventOptions.center') || 'Center' }}</option>
                    <option value="right">{{ t('eventOptions.right') || 'Right' }}</option>
                  </select>
                </div>
              </div>

              <div v-if="activeCharacterControl.mode === 'expression'" class="expression-control-section">
                <div class="property-group">
                  <label>表情成品</label>
                  <div class="character-select-wrapper">
                    <select class="prop-input prop-select" :value="activeCharacterControl.expressionPreset" @change="applyExpressionPreset(($event.target as HTMLSelectElement).value)">
                      <option value="">自定义组合</option>
                      <option v-for="expression in expressionStore.sortedExpressions" :key="expression.id" :value="expression.id">
                        {{ expression.name || expression.id }}
                      </option>
                    </select>
                    <button class="character-manager-btn" type="button" title="打开表情管理器" @click="uiStore.openExpressionManager()">✨</button>
                  </div>
                </div>
                <div class="character-control-grid">
                  <div class="property-group">
                    <label>显示位置</label>
                    <select class="prop-input prop-select" :value="activeCharacterControl.expressionCorner" @change="updateCharacterControlField('expressionCorner', ($event.target as HTMLSelectElement).value)">
                      <option value="top-left">立绘左上角</option>
                      <option value="top-right">立绘右上角</option>
                    </select>
                  </div>
                  <div class="property-group">
                    <label>持续秒数</label>
                    <input class="prop-input prop-number" type="number" step="0.1" min="0.1" :value="activeCharacterControl.expressionDuration" @input="updateCharacterControlField('expressionDuration', normalizeNumberProperty('duration', parseFloat(($event.target as HTMLInputElement).value)))" />
                  </div>
                </div>
                <div class="property-group">
                  <label>Balloon 基底图像</label>
                  <div class="resource-input-wrapper">
                    <input class="prop-input prop-resource prop-readonly" type="text" readonly :value="activeCharacterControl.expressionBalloon" @click="handleCharacterControlResourceBrowse('expressionBalloon')" />
                    <button class="browse-btn" type="button" @click="handleCharacterControlResourceBrowse('expressionBalloon')">📂</button>
                  </div>
                </div>
                <div class="property-group">
                  <label>内部情绪图像</label>
                  <div class="resource-input-wrapper">
                    <input class="prop-input prop-resource prop-readonly" type="text" readonly :value="activeCharacterControl.expressionIcon" @click="handleCharacterControlResourceBrowse('expressionIcon')" />
                    <button class="browse-btn" type="button" @click="handleCharacterControlResourceBrowse('expressionIcon')">📂</button>
                  </div>
                </div>
              </div>

              <div v-if="shouldShowAnimationControls(activeCharacterControl.mode)" class="character-control-grid">
                <div class="property-group">
                  <label>{{ t('characterControl.animation') || 'Animation' }}</label>
                  <select class="prop-input prop-select" :value="activeCharacterControl.animation" @change="updateCharacterControlField('animation', ($event.target as HTMLSelectElement).value)">
                    <option value="none">{{ t('characterControl.animation.none') || 'None' }}</option>
                    <option value="fade">{{ t('eventOptions.fade') || 'Fade' }}</option>
                    <option value="slide">{{ t('eventOptions.slide') || 'Slide' }}</option>
                    <option value="pop">{{ t('characterControl.animation.pop') || 'Pop' }}</option>
                    <option value="move">{{ t('characterControl.animation.move') || 'Move' }}</option>
                  </select>
                </div>
                <div class="property-group">
                  <label>{{ t('eventProps.duration') || 'Duration' }}</label>
                  <input class="prop-input prop-number" type="number" step="0.05" min="0" :value="activeCharacterControl.duration" @input="updateCharacterControlField('duration', normalizeNumberProperty('duration', parseFloat(($event.target as HTMLInputElement).value)))" />
                </div>
              </div>

              <div class="property-group">
                <label>{{ t('characterControl.sfx') || 'Sound Effect' }}</label>
                <div class="resource-input-wrapper">
                  <input class="prop-input prop-resource prop-readonly" type="text" readonly :value="activeCharacterControl.sfx" @click="handleCharacterControlResourceBrowse('sfx')" />
                  <button class="browse-btn" type="button" @click="handleCharacterControlResourceBrowse('sfx')">📂</button>
                </div>
              </div>
            </template>
          </div>
        </div>

        <div v-else-if="selectedNode.type === 'DialogueNode'" class="dialogue-preview-toolbar">
          <div class="dialogue-preview-title">文本预览</div>
          <div class="dialogue-preview-box">
            <span v-if="dialoguePreviewSpeaker" class="dialogue-preview-speaker">{{ dialoguePreviewSpeaker }}</span>
            <span class="dialogue-preview-text markdown-body" v-html="renderSafeMarkdown(getPropertyValue('text') || '')"></span>
          </div>
        </div>
        
        <div 
          v-for="prop in dynamicProperties" 
          :key="prop.name"
          class="property-group"
          v-show="selectedNode.type !== 'CharacterControlNode' && shouldShowProperty(prop.name)"
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
            :min="prop.name === 'voiceCount' ? 0 : undefined"
            :max="prop.name === 'voiceCount' ? 5 : undefined"
            :value="getPropertyValue(prop.name)"
            @input="updateProperty(prop.name, normalizeNumberProperty(prop.name, parseFloat(($event.target as HTMLInputElement).value)))"
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
import DOMPurify from 'dompurify'
import { useLocalization } from '@/composables/useLocalization'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useCharacterStore } from '@/stores/useCharacterStore'
import { useExpressionStore } from '@/stores/useExpressionStore'
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
import { getCurrentProject } from '@/api/projectApi'
import { renderDialogueMarkdown } from '@/utils/dialogueMarkdown'
import { resolveAssetUrl } from '@/utils/assetPaths'

type CharacterControlMode = 'none' | 'show' | 'hide' | 'update' | 'move' | 'rotate180' | 'moveReturn' | 'exitLeft' | 'exitRight' | 'shakeFallExit' | 'expression'

type CharacterControlEntry = {
  slot: string
  mode: CharacterControlMode
  action?: string
  character: string
  unmanagedCharacter: string
  sprite: string
  sfx: string
  expression: string
  fromPosition: string
  toPosition: string
  position: string
  animation: string
  easing: string
  duration: number
  expressionBalloon: string
  expressionIcon: string
  expressionPreset: string
  expressionCorner: string
  expressionDuration: number
}

const { t } = useLocalization()
const editorStore = useEditorStore()
const nodeGraphStore = useNodeGraphStore()
const characterStore = useCharacterStore()
const expressionStore = useExpressionStore()
const uiStore = useUIStore()

const showResourcePicker = ref(false)
const currentResourcePropName = ref('')
const currentResourceType = ref<ResourceType>('image')
const currentResourceFiles = ref<Array<{ name: string; path?: string }>>([])
const currentCharacterControlResourceSlot = ref('')

const selectedNode = computed(() => editorStore.selectedNode)

const eventSubType = ref<EventType>(EventType.PlayBGM)
const logicSubType = ref<LogicType>(LogicType.SetVariable)
const activeCharacterSlot = ref('1')
const characterControlSlots = ['1', '2', '3', '4', '5', '6']

onMounted(() => {
  characterStore.load().catch(error => {
    console.warn('[InspectorPanel] Failed to load characters:', error)
  })
  expressionStore.load().catch(error => {
    console.warn('[InspectorPanel] Failed to load expressions:', error)
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
    eventSubType.value = node.subType || node.data?.subType || EventType.PlayBGM
  } else if (node.type === 'LogicNode') {
    logicSubType.value = node.subType || node.data?.subType || LogicType.SetVariable
  }
}, { immediate: true })

// 动态计算当前节点应显示的属性列表
const dynamicProperties = computed((): PropertyConfig[] => {
  if (!selectedNode.value) return []

  if (selectedNode.value.type === 'CharacterControlNode') return []
  
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
      {
        name: 'speakerSlot',
        type: 'select',
        defaultValue: '',
        label: '说话角色槽位',
        options: [
          { value: '', label: '无说话角色' },
          { value: '1', label: '角色 1' },
          { value: '2', label: '角色 2' },
          { value: '3', label: '角色 3' },
          { value: '4', label: '角色 4' },
          { value: '5', label: '角色 5' },
          { value: '6', label: '角色 6' },
          { value: 'all', label: '全员' },
        ],
      },
      { name: 'unmanagedCharacter', type: 'string', defaultValue: '', label: 'Slot 6 角色名' },
      { name: 'text', type: 'string', defaultValue: '', label: '输入内容' },
      { name: 'voiceCount', type: 'number', defaultValue: 1, label: '语音数量' },
      { name: 'voice1', type: 'resource', defaultValue: '', label: '语音 1' },
      { name: 'voice2', type: 'resource', defaultValue: '', label: '语音 2' },
      { name: 'voice3', type: 'resource', defaultValue: '', label: '语音 3' },
      { name: 'voice4', type: 'resource', defaultValue: '', label: '语音 4' },
      { name: 'voice5', type: 'resource', defaultValue: '', label: '语音 5' },
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
          { value: 'move', label: '移动角色' },
        ],
      },
      { name: 'sprite', type: 'resource', defaultValue: '', label: '立绘' },
      { name: 'sfx', type: 'resource', defaultValue: '', label: '音效' },
      { name: 'expression', type: 'string', defaultValue: 'default', label: '表情' },
      {
        name: 'fromPosition',
        type: 'select',
        defaultValue: '',
        label: '移动起点',
        options: [
          { value: '', label: '当前位置' },
          { value: 'left', label: '左侧' },
          { value: 'center', label: '中间' },
          { value: 'right', label: '右侧' },
        ],
      },
      {
        name: 'toPosition',
        type: 'select',
        defaultValue: 'none',
        label: '移动目标',
        options: [
          { value: 'none', label: '不移动' },
          { value: 'left', label: '左侧' },
          { value: 'center', label: '中间' },
          { value: 'right', label: '右侧' },
        ],
      },
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
          { value: 'move', label: '移动' },
        ],
      },
      {
        name: 'easing',
        type: 'select',
        defaultValue: 'easeOutCubic',
        label: '缓动曲线',
        options: [
          { value: 'easeOutCubic', label: 'Ease Out Cubic' },
          { value: 'easeInOutCubic', label: 'Ease In Out Cubic' },
          { value: 'easeOutBack', label: 'Ease Out Back' },
          { value: 'linear', label: 'Linear' },
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

function createDefaultCharacterControl(slot: string): CharacterControlEntry {
  return {
    slot,
    mode: 'none',
    action: 'none',
    character: '',
    unmanagedCharacter: '',
    sprite: '',
    sfx: '',
    expression: 'default',
    fromPosition: '',
    toPosition: 'none',
    position: 'center',
    animation: 'fade',
    easing: 'easeOutCubic',
    duration: 0.3,
    expressionBalloon: '',
    expressionIcon: '',
    expressionPreset: '',
    expressionCorner: 'top-right',
    expressionDuration: 2,
  }
}

function normalizeCharacterControlEntry(value: any, fallbackSlot: string): CharacterControlEntry {
  const slot = String(value?.slot || fallbackSlot || '1')
  const rawMode = String(value?.mode || value?.action || 'none').toLowerCase()
  const modeAlias: Record<string, CharacterControlMode> = {
    show: 'show',
    hide: 'hide',
    update: 'update',
    move: 'move',
    rotate180: 'rotate180',
    movereturn: 'moveReturn',
    exitleft: 'exitLeft',
    exitright: 'exitRight',
    shakefallexit: 'shakeFallExit',
    expression: 'expression',
  }
  const mode = modeAlias[rawMode] || 'none'
  return {
    ...createDefaultCharacterControl(slot),
    ...value,
    slot,
    mode,
    action: mode,
    character: String(value?.character || ''),
    unmanagedCharacter: String(value?.unmanagedCharacter || ''),
    sprite: slot === '6' ? '' : String(value?.sprite || ''),
    sfx: String(value?.sfx || ''),
    expression: String(value?.expression || 'default'),
    fromPosition: String(value?.fromPosition || ''),
    toPosition: String(value?.toPosition || 'none'),
    position: String(value?.position || 'center'),
    animation: String(value?.animation || 'fade'),
    easing: String(value?.easing || 'easeOutCubic'),
    duration: Number(value?.duration ?? 0.3) || 0.3,
    expressionBalloon: String(value?.expressionBalloon || ''),
    expressionIcon: String(value?.expressionIcon || ''),
    expressionPreset: String(value?.expressionPreset || ''),
    expressionCorner: String(value?.expressionCorner || 'top-right'),
    expressionDuration: Number(value?.expressionDuration ?? 2) || 2,
  }
}

function parseCharacterControlsJson(value: any): CharacterControlEntry[] {
  if (Array.isArray(value)) return value.map((entry, index) => normalizeCharacterControlEntry(entry, String(index + 1)))
  const raw = String(value || '').trim()
  if (!raw) return []
  try {
    const parsed = JSON.parse(raw)
    return Array.isArray(parsed) ? parsed.map((entry, index) => normalizeCharacterControlEntry(entry, String(index + 1))) : []
  } catch {
    return []
  }
}

function getLegacyCharacterControl(): CharacterControlEntry | null {
  if (!selectedNode.value?.data) return null
  const data = selectedNode.value.data
  if (!data.slot && !data.action && !data.character && !data.sprite) return null
  return normalizeCharacterControlEntry(data, String(data.slot || '1'))
}

const characterControls = computed(() => {
  const controls = parseCharacterControlsJson(getPropertyValue('characterControlsJson') || getPropertyValue('characterControls'))
  const legacy = controls.length === 0 ? getLegacyCharacterControl() : null
  return legacy ? [legacy] : controls
})

const activeCharacterControl = computed(() => {
  return characterControls.value.find(control => control.slot === activeCharacterSlot.value) || createDefaultCharacterControl(activeCharacterSlot.value)
})

const BACKGROUND_VIDEO_EXTENSIONS = ['.mp4', '.webm', '.m4v', '.mov', '.ogv', '.avi', '.mkv']

const modifiedCharacterControls = computed(() => characterControls.value.filter(control => control.mode !== 'none'))

const characterControlPreviewCharacters = computed(() => modifiedCharacterControls.value
  .filter(control => control.slot !== '6' && !['hide', 'expression'].includes(control.mode) && Boolean(control.sprite))
  .map(control => ({
    ...control,
    spriteUrl: resolveAssetUrl(control.sprite, 'Characters'),
  }))
  .filter(control => Boolean(control.spriteUrl))
)

const characterControlPreviewExpressions = computed(() => modifiedCharacterControls.value
  .filter(control => control.mode === 'expression' && (control.expressionBalloon || control.expressionIcon))
  .map(control => ({
    ...control,
    balloonUrl: control.expressionBalloon ? resolveAssetUrl(control.expressionBalloon, 'Emoji') : '',
    iconUrl: control.expressionIcon ? resolveAssetUrl(control.expressionIcon, 'Emoji') : '',
  }))
)

function serializeCharacterControls(controls: CharacterControlEntry[]) {
  return JSON.stringify(controls
    .map(control => normalizeCharacterControlEntry(control, control.slot))
    .filter(control => control.mode !== 'none')
    .sort((a, b) => Number(a.slot) - Number(b.slot))
  )
}

function setCharacterControls(controls: CharacterControlEntry[]) {
  const serialized = serializeCharacterControls(controls)
  updateProperty('characterControlsJson', serialized)
  if (selectedNode.value?.data) {
    selectedNode.value.data.characterControls = parseCharacterControlsJson(serialized)
    selectedNode.value.data.characterControlsJson = serialized
    selectedNode.value.data.tlorFormatVersion = '1.1'
  }

  const editorNode = nodeGraphStore.editor?.graph.nodes.find(node => node.id === selectedNode.value?.id) as any
  if (editorNode) {
    editorNode.data = {
      ...(editorNode.data || {}),
      characterControls: parseCharacterControlsJson(serialized),
      characterControlsJson: serialized,
      tlorFormatVersion: '1.1',
    }
  }
}

function updateCharacterControlField(field: keyof CharacterControlEntry, value: any) {
  const next = characterControlSlots.map(slot => {
    const existing = characterControls.value.find(control => control.slot === slot) || createDefaultCharacterControl(slot)
    if (slot !== activeCharacterSlot.value) return existing
    const updated = normalizeCharacterControlEntry({ ...existing, [field]: value }, slot)
    if (field === 'mode') updated.action = updated.mode
    if (updated.mode === 'hide') updated.sprite = ''
    if (updated.slot === '6') updated.sprite = ''
    return updated
  })
  setCharacterControls(next)
}

function shouldShowSpritePicker(mode: CharacterControlMode) {
  return ['show', 'update', 'move'].includes(mode)
}

function shouldShowPositionControls(mode: CharacterControlMode) {
  return ['show', 'update', 'move'].includes(mode)
}

function shouldShowAnimationControls(mode: CharacterControlMode) {
  return ['show', 'update', 'move'].includes(mode)
}

function applyExpressionPreset(expressionId: string) {
  const preset = expressionStore.findExpression(expressionId)
  updateCharacterControlField('expressionPreset', expressionId)
  if (!preset) return
  const layers = [...preset.layers].sort((a, b) => a.zIndex - b.zIndex)
  const baseLayer = layers[0]
  const iconLayer = [...layers].reverse().find(layer => layer.id !== baseLayer?.id)
  updateCharacterControlField('expressionBalloon', baseLayer?.image || '')
  updateCharacterControlField('expressionIcon', iconLayer?.image || '')
  updateCharacterControlField('expressionDuration', preset.duration)
}

function isCharacterSlotModified(slot: string) {
  return characterControls.value.some(control => control.slot === slot && control.mode !== 'none')
}

function getCharacterPreviewStyle(character: CharacterControlEntry & { spriteUrl: string }) {
  const left: Record<string, string> = { left: '15%', center: '50%', right: '85%' }
  const transform: Record<string, string> = { left: 'translateX(0)', center: 'translateX(-50%)', right: 'translateX(-100%)' }
  const position = character.toPosition && character.toPosition !== 'none' ? character.toPosition : character.position
  return {
    left: left[position] || left.center,
    transform: transform[position] || transform.center,
  }
}

function getExpressionPreviewStyle(expression: CharacterControlEntry) {
  const base = getCharacterPreviewStyle(expression as CharacterControlEntry & { spriteUrl: string })
  return {
    left: base.left,
    transform: `${base.transform} translateY(-12%)`,
    justifyContent: expression.expressionCorner === 'top-left' ? 'flex-start' : 'flex-end',
  }
}

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
  const editorNode = nodeGraphStore.editor?.graph.nodes.find(node => node.id === selectedNode.value?.id) as any
  const interfaceValue = editorNode?.inputs?.[name]?.value
  if (interfaceValue !== undefined) return interfaceValue
  if (name === 'subType') return selectedNode.value?.subType
  if (!selectedNode.value?.data) return undefined
  return selectedNode.value.data[name]
}

const dialoguePreviewSpeaker = computed(() => {
  const slot = String(getPropertyValue('speakerSlot') || '')
  if (!slot) return ''
  if (slot === 'all') return '全员'
  if (slot === '6') return String(getPropertyValue('unmanagedCharacter') || '旁白')
  return `角色 ${slot}`
})

function getDialogueVoiceCount() {
  return Math.max(0, Math.min(5, Number(getPropertyValue('voiceCount') ?? 1) || 0))
}

function shouldShowProperty(name: string): boolean {
  if (selectedNode.value?.type !== 'DialogueNode') return true
  if (name === 'unmanagedCharacter') return String(getPropertyValue('speakerSlot') || '') === '6'
  const voiceMatch = name.match(/^voice(\d)$/)
  if (voiceMatch) return Number(voiceMatch[1]) <= getDialogueVoiceCount()
  return true
}

function normalizeNumberProperty(name: string, value: number) {
  if (name !== 'voiceCount') return Number.isFinite(value) ? value : 0
  if (!Number.isFinite(value)) return 0
  return Math.max(0, Math.min(5, Math.round(value)))
}

function updateProperty(name: string, value: any) {
  if (!selectedNode.value) return
  if (name === 'speakerSlot' && value !== '6') {
    updateProperty('unmanagedCharacter', '')
  }
  
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

  if (editorNode) {
    editorNode.data = {
      ...(editorNode.data || {}),
      [name]: value,
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
  return DOMPurify.sanitize(renderDialogueMarkdown(String(value ?? '')), {
    ADD_TAGS: ['ruby', 'rp', 'rt', 'i', 'u', 'mark', 'span'],
    ADD_ATTR: ['class', 'title'],
  })
}

function onEventSubTypeChange() {
  if (!selectedNode.value) return

  if (selectedNode.value.data) {
    selectedNode.value.data.subType = eventSubType.value
  }

  const editorNode = nodeGraphStore.editor?.graph.nodes.find(node => node.id === selectedNode.value?.id) as any
  const subTypeInput = editorNode?.inputs?.subType
  if (subTypeInput) {
    if (typeof subTypeInput.setValue === 'function') {
      subTypeInput.setValue(eventSubType.value)
    } else {
      subTypeInput.value = eventSubType.value
    }
    editorNode.data = {
      ...(editorNode.data || {}),
      subType: eventSubType.value,
    }
  }

  nodeGraphStore.syncNodes()
  nodeGraphStore.isDirty = true
}

function onLogicSubTypeChange() {
  if (!selectedNode.value) return

  if (selectedNode.value.data) {
    selectedNode.value.data.subType = logicSubType.value
  }

  const editorNode = nodeGraphStore.editor?.graph.nodes.find(node => node.id === selectedNode.value?.id) as any
  const subTypeInput = editorNode?.inputs?.subType
  if (subTypeInput) {
    if (typeof subTypeInput.setValue === 'function') {
      subTypeInput.setValue(logicSubType.value)
    } else {
      subTypeInput.value = logicSubType.value
    }
    editorNode.data = {
      ...(editorNode.data || {}),
      subType: logicSubType.value,
    }
  }

  nodeGraphStore.syncNodes()
  nodeGraphStore.isDirty = true
}

function getSelectedCharacterProfile() {
  const selectedCharacterName = selectedNode.value?.type === 'CharacterControlNode'
    ? String(activeCharacterControl.value.character || '').trim()
    : String(getPropertyValue('character') || '').trim()
  if (!selectedCharacterName) return null

  return characterStore.sortedCharacters.find(character => character.id === selectedCharacterName) || null
}

function getDialogueVoiceCharacterId(propName: string) {
  const slot = propName.match(/^voice(\d)$/)?.[1] || ''
  if (!slot || !selectedNode.value) return ''

  const editor = nodeGraphStore.editor
  const dialogueNode = editor?.graph.nodes.find(node => node.id === selectedNode.value?.id) as any
  const connection = editor?.graph.connections.find((candidate: any) => {
    if (candidate.to.nodeId !== selectedNode.value?.id) return false
    const targetPort = Object.entries(dialogueNode?.inputs || {}).find(([, iface]: any) => iface === candidate.to || iface?.id === candidate.to?.id || iface?.name === candidate.to?.name)?.[0]
    return targetPort === `characterControl${slot}`
  })
  const controlNode = connection ? editor?.graph.nodes.find(node => node.id === connection.from.nodeId) as any : null
  if (!controlNode) return ''

  const controlSlot = String(controlNode.inputs?.slot?.value || slot)
  return controlSlot === '6'
    ? String(controlNode.inputs?.unmanagedCharacter?.value || controlNode.inputs?.character?.value || '').trim()
    : String(controlNode.inputs?.character?.value || '').trim()
}

function toResourcePickerFiles(entries: DirEntry[]) {
  return entries
    .filter(entry => !entry.isDirectory)
    .map(entry => ({ name: entry.name, path: entry.path }))
}

function getAssetFolderForResource(propName: string, resourceType: ResourceType): string {
  const propFolderMap: Record<string, string> = {
    imagePath: 'Backgrounds',
    background: 'Backgrounds',
    bg: 'Backgrounds',
    bgFile: 'Backgrounds',
    sprite: 'Characters',
    sprite1: 'Characters',
    sprite2: 'Characters',
    sprite3: 'Characters',
    sprite4: 'Characters',
    sprite5: 'Characters',
    bgm: 'Musics',
    bgmPath: 'Musics',
    bgmFile: 'Musics',
    musicFile: 'Musics',
    sfx: 'Sfx',
    sfxPath: 'Sfx',
    soundFile: 'Sfx',
    voice: 'Voices',
    voicePath: 'Voices',
    voiceFile: 'Voices',
  }

  if (propFolderMap[propName]) return propFolderMap[propName]

  const typeFolderMap: Partial<Record<ResourceType, string>> = {
    image: 'Backgrounds',
    audio: 'Sfx',
    bgm: 'Musics',
    voice: 'Voices',
  }

  return typeFolderMap[resourceType] || ''
}

async function loadProjectAssetFiles(propName: string, resourceType: ResourceType) {
  const currentProject = await getCurrentProject()
  const root = currentProject.data?.projectPath
  const folder = getAssetFolderForResource(propName, resourceType)
  if (!root || !folder) {
    currentResourceFiles.value = []
    return
  }

  try {
    const result = await getEntries(`${root}\\Assets\\${folder}`)
    const exts = folder === 'Backgrounds' && resourceType === 'image'
      ? Array.from(new Set([...(RESOURCE_TYPE_EXTENSIONS.image || []), ...BACKGROUND_VIDEO_EXTENSIONS]))
      : RESOURCE_TYPE_EXTENSIONS[resourceType] || []
    currentResourceFiles.value = toResourcePickerFiles(result.entries)
      .filter(file => exts.includes(file.name.slice(file.name.lastIndexOf('.')).toLowerCase()))
  } catch (error) {
    console.warn(`[InspectorPanel] Failed to load project asset folder: ${folder}`, error)
    currentResourceFiles.value = []
  }
}

async function loadCharacterSpriteFiles() {
  const character = getSelectedCharacterProfile()
  const spriteFolder = character?.spriteFolder?.trim()
  if (!character || !spriteFolder) {
    currentResourceFiles.value = []
    return
  }

  if (character.sprites.length > 0) {
    currentResourceFiles.value = character.sprites.map(path => ({
      name: path.replace(/\\/g, '/').split('/').pop() || path,
      path,
    }))
    return
  }

  try {
    const result = await getEntries(spriteFolder)
    const imageExts = RESOURCE_TYPE_EXTENSIONS.image
    currentResourceFiles.value = toResourcePickerFiles(result.entries)
      .filter(file => imageExts.includes(file.name.slice(file.name.lastIndexOf('.')).toLowerCase()))
      .filter(file => !/[\\/]Avatars[\\/]/i.test(file.path || ''))
    if (currentResourceFiles.value.length > 0) {
      await characterStore.updateCharacter(character.id, {
        sprites: currentResourceFiles.value.map(file => file.path || file.name),
      })
    }
  } catch (error) {
    console.warn('[InspectorPanel] Failed to load character sprite folder:', error)
    currentResourceFiles.value = []
  }
}

async function loadDialogueVoiceFiles(propName: string) {
  const characterId = getDialogueVoiceCharacterId(propName)
  if (!characterId) {
    currentResourceFiles.value = []
    return
  }

  const currentProject = await getCurrentProject()
  const root = currentProject.data?.projectPath
  if (!root) {
    currentResourceFiles.value = []
    return
  }

  try {
    const result = await getEntries(`${root}\\Assets\\Voices\\${characterId}`)
    const voiceExts = RESOURCE_TYPE_EXTENSIONS.voice
    currentResourceFiles.value = toResourcePickerFiles(result.entries)
      .filter(file => voiceExts.includes(file.name.slice(file.name.lastIndexOf('.')).toLowerCase()))
  } catch (error) {
    console.warn('[InspectorPanel] Failed to load character voice folder:', error)
    currentResourceFiles.value = []
  }
}

async function loadEmojiResourceFiles() {
  const currentProject = await getCurrentProject()
  const root = currentProject.data?.projectPath
  if (!root) {
    currentResourceFiles.value = []
    return
  }

  try {
    const result = await getEntries(`${root}\\Assets\\Emoji\\Resources`)
    const imageExts = RESOURCE_TYPE_EXTENSIONS.image
    currentResourceFiles.value = toResourcePickerFiles(result.entries)
      .filter(file => imageExts.includes(file.name.slice(file.name.lastIndexOf('.')).toLowerCase()))
  } catch (error) {
    console.warn('[InspectorPanel] Failed to load emoji resource folder:', error)
    currentResourceFiles.value = []
  }
}

async function handleResourceBrowse(propName: string) {
  currentResourcePropName.value = propName
  currentCharacterControlResourceSlot.value = ''
  currentResourceFiles.value = []

  if (selectedNode.value?.type === 'ResourceNode') {
    const nodeResourceType = ((selectedNode.value as any)?.state?.resourceType
      || (selectedNode.value as any)?.options?.resourceType
      || 'image') as ResourceType
    currentResourceType.value = nodeResourceType
  } else {
    const typeMap: Partial<Record<string, ResourceType>> = {
      voice: 'voice',
      voicePath: 'voice',
      voice1: 'voice',
      voice2: 'voice',
      voice3: 'voice',
      voice4: 'voice',
      voice5: 'voice',
      voice6: 'voice',
      sfx: 'audio',
      sfxPath: 'audio',
      bgm: 'bgm',
      bgmPath: 'bgm',
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
  } else if (selectedNode.value?.type === 'DialogueNode' && /^voice\d$/.test(propName)) {
    await loadDialogueVoiceFiles(propName)
  } else {
    await loadProjectAssetFiles(propName, currentResourceType.value)
  }

  showResourcePicker.value = true
}

async function handleCharacterControlResourceBrowse(propName: 'sprite' | 'sfx' | 'expressionBalloon' | 'expressionIcon') {
  currentResourcePropName.value = propName
  currentCharacterControlResourceSlot.value = activeCharacterSlot.value
  currentResourceFiles.value = []
  currentResourceType.value = propName === 'sfx' ? 'audio' : 'image'

  if (propName === 'sprite') {
    await loadCharacterSpriteFiles()
  } else if (propName === 'expressionBalloon' || propName === 'expressionIcon') {
    await loadEmojiResourceFiles()
  } else {
    await loadProjectAssetFiles(propName, currentResourceType.value)
  }

  showResourcePicker.value = true
}

function onResourcePicked(path: string) {
  if (selectedNode.value?.type === 'CharacterControlNode' && currentCharacterControlResourceSlot.value) {
    updateCharacterControlField(currentResourcePropName.value as keyof CharacterControlEntry, path)
    currentCharacterControlResourceSlot.value = ''
    showResourcePicker.value = false
    return
  }

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

.dialogue-preview-toolbar {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 8px;
  padding: 10px;
  border: 1px solid rgba(96, 165, 250, 0.35);
  border-radius: 8px;
  background: linear-gradient(180deg, #172033 0%, #111827 100%);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.28);
}

.dialogue-preview-title {
  color: #93c5fd;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
}

.dialogue-preview-box {
  min-height: 54px;
  padding: 10px 12px;
  border-radius: 8px;
  background: rgba(15, 23, 42, 0.92);
  color: #e5e7eb;
  font-family: Gadugi, "Segoe UI", sans-serif !important;
  font-size: 18px;
  font-weight: 400;
  line-height: 1.32;
  text-shadow: 0 2px 6px rgba(0, 0, 0, 0.45);
  word-break: break-word;
}

.dialogue-preview-speaker {
  display: inline-flex;
  margin-right: 8px;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(37, 99, 235, 0.28);
  color: #bfdbfe;
  font-size: 12px;
  font-weight: 700;
}

.dialogue-preview-text {
  color: rgba(255, 255, 255, 0.94) !important;
  font: inherit !important;
  line-height: inherit !important;
  white-space: pre-wrap !important;
}

.dialogue-preview-text :deep(p) {
  margin: 0 !important;
  color: inherit !important;
  font: inherit !important;
  line-height: inherit !important;
}

.dialogue-preview-text :deep(*) {
  color: inherit !important;
  line-height: inherit !important;
}

.dialogue-preview-text :deep(p + p) {
  margin-top: 0.35em !important;
}

.dialogue-preview-text :deep(h1),
.dialogue-preview-text :deep(h2),
.dialogue-preview-text :deep(h3),
.dialogue-preview-text :deep(h4),
.dialogue-preview-text :deep(h5),
.dialogue-preview-text :deep(h6) {
  margin: 0 0 0.16em !important;
  color: #ffffff !important;
  font-family: inherit !important;
  font-weight: 700 !important;
  line-height: 1.08 !important;
}

.dialogue-preview-text :deep(h1) { font-size: 1.28em !important; }
.dialogue-preview-text :deep(h2) { font-size: 1.18em !important; }
.dialogue-preview-text :deep(h3),
.dialogue-preview-text :deep(h4),
.dialogue-preview-text :deep(h5),
.dialogue-preview-text :deep(h6) { font-size: 1.08em !important; }

.dialogue-preview-text :deep(strong) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: inherit !important;
  font-weight: 700 !important;
  line-height: inherit !important;
}

.dialogue-preview-text :deep(em),
.dialogue-preview-text :deep(i) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: inherit !important;
  font-style: oblique 12deg !important;
  font-synthesis: style !important;
  font-synthesis-style: auto !important;
  line-height: inherit !important;
  display: inline-block !important;
  transform: skewX(-9deg) !important;
  transform-origin: left bottom !important;
}

.dialogue-preview-text :deep(em::after),
.dialogue-preview-text :deep(i::after) {
  content: "" !important;
  display: inline-block !important;
  width: 0.14em !important;
}

.dialogue-preview-text :deep(ruby) {
  color: inherit !important;
  font: inherit !important;
  ruby-position: over !important;
  ruby-align: center !important;
  text-emphasis: none !important;
}

.dialogue-preview-text :deep(rt) {
  color: inherit !important;
  font-family: inherit !important;
  font-size: 0.44em !important;
  font-weight: 600 !important;
  line-height: 1 !important;
  text-align: center !important;
  text-shadow: inherit !important;
}

.dialogue-preview-text :deep(rp) {
  display: none !important;
}

.dialogue-preview-text :deep(.dialog-inside),
.dialogue-preview-text :deep(.dialog-inside a),
.dialogue-preview-text :deep(a .dialog-inside),
.dialogue-preview-text :deep(.dialog-inside a.new) {
  background-color: #252525 !important;
  color: rgba(255, 255, 255, 0) !important;
  -webkit-text-fill-color: rgba(255, 255, 255, 0) !important;
  text-shadow: none !important;
  transition: background-color 0.5s ease, color 0.5s ease, -webkit-text-fill-color 0.5s ease, text-shadow 0.5s ease !important;
}

.dialogue-preview-text :deep(.dialog-inside:hover),
.dialogue-preview-text :deep(.dialog-inside:active),
.dialogue-preview-text :deep(.dialog-inside:hover .dialog-inside),
.dialogue-preview-text :deep(.dialog-inside:active .dialog-inside) {
  background-color: transparent !important;
  color: inherit !important;
  -webkit-text-fill-color: currentColor !important;
  text-shadow: inherit !important;
}

.dialogue-preview-box :deep(code) {
  padding: 1px 4px;
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.1);
}

.dialogue-preview-box :deep(a) {
  color: #93c5fd;
}

.character-control-editor {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.character-control-summary {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 10px;
  border: 1px solid rgba(168, 85, 247, 0.35);
  border-radius: 8px;
  background: linear-gradient(180deg, rgba(88, 28, 135, 0.42), rgba(30, 41, 59, 0.82));
}

.character-control-summary div {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.character-control-summary strong {
  color: #e9d5ff;
  font-size: 12px;
}

.character-control-summary span {
  color: #c4b5fd;
  font-size: 11px;
}

.character-stage-preview {
  position: relative;
  aspect-ratio: 16 / 9;
  overflow: hidden;
  border: 1px solid #3e3e42;
  border-radius: 8px;
  background: radial-gradient(circle at center, #263044 0%, #111827 72%);
}

.character-stage-grid {
  position: absolute;
  inset: 0;
  opacity: 0.22;
  background-image: linear-gradient(rgba(255, 255, 255, 0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(255, 255, 255, 0.08) 1px, transparent 1px);
  background-size: 24px 24px;
}

.character-stage-sprite {
  position: absolute;
  bottom: -4%;
  z-index: 2;
  max-width: 44%;
  max-height: 104%;
  object-fit: contain;
  filter: drop-shadow(0 14px 20px rgba(0, 0, 0, 0.48));
}

.character-stage-expression {
  position: absolute;
  top: 15%;
  z-index: 3;
  width: 28%;
  aspect-ratio: 1;
  display: flex;
  align-items: center;
  pointer-events: none;
}

.character-stage-expression-balloon,
.character-stage-expression-icon {
  position: absolute;
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}

.character-stage-expression-icon {
  inset: 24%;
  width: 52%;
  height: 52%;
  margin: auto;
}

.expression-control-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.character-stage-empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #94a3b8;
  font-size: 12px;
}

.character-slot-tabs {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 6px;
}

.character-slot-tab {
  padding: 7px 6px;
  border: 1px solid #3e3e42;
  border-radius: 6px;
  color: #cbd5e1;
  background: #2d2d30;
  cursor: pointer;
  font-size: 11px;
}

.character-slot-tab:hover {
  border-color: #7c3aed;
  background: #35313d;
}

.character-slot-tab.active {
  border-color: #a855f7;
  color: #ffffff;
  background: #6d28d9;
}

.character-slot-tab.modified::after {
  content: "";
  display: inline-block;
  width: 6px;
  height: 6px;
  margin-left: 5px;
  border-radius: 999px;
  background: #22c55e;
  vertical-align: middle;
}

.character-slot-form {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.character-control-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 8px;
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
