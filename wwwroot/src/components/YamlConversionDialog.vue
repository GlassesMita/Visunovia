<template>
  <div v-if="visible" class="yaml-dialog-overlay" @click.self="handleClose">
    <div class="yaml-dialog" :class="{ 'yaml-dialog--expanded': activeTab === 'preview' && previewContent }">
      <!-- 标题栏 -->
      <div class="yaml-dialog__header">
        <div class="yaml-dialog__title">
          <span class="yaml-dialog__icon">⟷</span>
          <h2>{{ t('yaml.title', 'YAML ↔ Blueprint') }}</h2>
        </div>
        <button class="yaml-dialog__close" @click="handleClose" :title="t('common.close', 'Close')">✕</button>
      </div>

      <!-- 标签页 -->
      <div class="yaml-dialog__tabs">
        <button
          class="yaml-dialog__tab"
          :class="{ 'yaml-dialog__tab--active': activeTab === 'export' }"
          @click="activeTab = 'export'"
        >
          <span class="yaml-dialog__tab-icon">📤</span>
          {{ t('yaml.export', 'Export') }}
        </button>
        <button
          class="yaml-dialog__tab"
          :class="{ 'yaml-dialog__tab--active': activeTab === 'import' }"
          @click="activeTab = 'import'"
        >
          <span class="yaml-dialog__tab-icon">📥</span>
          {{ t('yaml.import', 'Import') }}
        </button>
        <button
          class="yaml-dialog__tab"
          :class="{ 'yaml-dialog__tab--active': activeTab === 'uuid' }"
          @click="activeTab = 'uuid'; loadUuidRegistry()"
        >
          <span class="yaml-dialog__tab-icon">🔑</span>
          {{ t('yaml.uuidRegistry', 'UUID Registry') }}
        </button>
      </div>

      <!-- 内容区 -->
      <div class="yaml-dialog__body">
        <!-- 导出面板 -->
        <div v-if="activeTab === 'export'" class="yaml-dialog__panel">
          <div class="yaml-dialog__form-group">
            <label>{{ t('yaml.sceneId', 'Scene ID') }}</label>
            <input
              v-model="exportForm.sceneId"
              type="text"
              class="yaml-dialog__input"
              :placeholder="t('yaml.sceneIdPlaceholder', 'Enter scene ID')"
            />
          </div>
          <div class="yaml-dialog__form-group">
            <label>{{ t('yaml.displayName', 'Display Name') }}</label>
            <input
              v-model="exportForm.displayName"
              type="text"
              class="yaml-dialog__input"
              :placeholder="t('yaml.displayNamePlaceholder', 'Optional display name')"
            />
          </div>
          <div class="yaml-dialog__form-group">
            <label>{{ t('yaml.description', 'Description') }}</label>
            <textarea
              v-model="exportForm.description"
              class="yaml-dialog__textarea"
              rows="2"
              :placeholder="t('yaml.descriptionPlaceholder', 'Optional description')"
            />
          </div>
          <div class="yaml-dialog__form-group">
            <label>{{ t('yaml.author', 'Author') }}</label>
            <input
              v-model="exportForm.author"
              type="text"
              class="yaml-dialog__input"
              :placeholder="t('yaml.authorPlaceholder', 'Optional author name')"
            />
          </div>

          <div class="yaml-dialog__actions">
            <button
              class="yaml-dialog__btn yaml-dialog__btn--primary"
              :disabled="!exportForm.sceneId || isExporting"
              @click="handleExport"
            >
              <span v-if="isExporting" class="yaml-dialog__spinner"></span>
              {{ isExporting ? t('yaml.exporting', 'Exporting...') : t('yaml.exportYaml', 'Export YAML') }}
            </button>
            <button
              class="yaml-dialog__btn yaml-dialog__btn--secondary"
              :disabled="!exportForm.sceneId || isExporting"
              @click="handleDownload"
            >
              {{ t('yaml.download', 'Download File') }}
            </button>
          </div>

          <!-- 导出结果预览 -->
          <div v-if="previewContent" class="yaml-dialog__preview">
            <div class="yaml-dialog__preview-header">
              <h3>{{ t('yaml.preview', 'Preview') }}</h3>
              <button class="yaml-dialog__btn yaml-dialog__btn--small" @click="copyPreview">
                {{ t('yaml.copy', 'Copy') }}
              </button>
            </div>
            <pre class="yaml-dialog__preview-content">{{ previewContent }}</pre>
          </div>
        </div>

        <!-- 导入面板 -->
        <div v-if="activeTab === 'import'" class="yaml-dialog__panel">
          <div class="yaml-dialog__form-group">
            <label>{{ t('yaml.targetSceneId', 'Target Scene ID') }}</label>
            <input
              v-model="importForm.sceneId"
              type="text"
              class="yaml-dialog__input"
              :placeholder="t('yaml.targetSceneIdPlaceholder', 'Enter target scene ID')"
            />
          </div>

          <!-- 文件上传 -->
          <div class="yaml-dialog__upload-area"
               :class="{ 'yaml-dialog__upload-area--dragover': isDragOver }"
               @dragover.prevent="isDragOver = true"
               @dragleave="isDragOver = false"
               @drop.prevent="handleFileDrop"
          >
            <input
              ref="fileInputRef"
              type="file"
              accept=".yaml,.yml"
              class="yaml-dialog__file-input"
              @change="handleFileSelect"
            />
            <div class="yaml-dialog__upload-icon">📄</div>
            <p class="yaml-dialog__upload-text">
              {{ t('yaml.dragDrop', 'Drag & drop YAML file here, or') }}
              <button class="yaml-dialog__upload-btn" @click="fileInputRef?.click()">
                {{ t('yaml.browse', 'browse') }}
              </button>
            </p>
            <p v-if="selectedFile" class="yaml-dialog__file-name">
              {{ selectedFile.name }} ({{ formatFileSize(selectedFile.size) }})
            </p>
          </div>

          <!-- 或直接粘贴 YAML -->
          <div class="yaml-dialog__form-group">
            <label>{{ t('yaml.orPaste', 'Or paste YAML content') }}</label>
            <textarea
              v-model="importForm.yamlContent"
              class="yaml-dialog__textarea yaml-dialog__textarea--code"
              rows="8"
              :placeholder="t('yaml.pastePlaceholder', 'Paste YAML content here...')"
            />
          </div>

          <div class="yaml-dialog__form-group yaml-dialog__checkbox-group">
            <label class="yaml-dialog__checkbox">
              <input v-model="importForm.clearExisting" type="checkbox" />
              <span>{{ t('yaml.clearExisting', 'Clear existing data before import') }}</span>
            </label>
          </div>

          <div class="yaml-dialog__actions">
            <button
              class="yaml-dialog__btn yaml-dialog__btn--primary"
              :disabled="!canImport || isImporting"
              @click="handleImport"
            >
              <span v-if="isImporting" class="yaml-dialog__spinner"></span>
              {{ isImporting ? t('yaml.importing', 'Importing...') : t('yaml.importYaml', 'Import YAML') }}
            </button>
            <button
              class="yaml-dialog__btn yaml-dialog__btn--secondary"
              :disabled="!importForm.yamlContent || isValidating"
              @click="handleValidate"
            >
              {{ isValidating ? t('yaml.validating', 'Validating...') : t('yaml.validate', 'Validate') }}
            </button>
          </div>

          <!-- 验证结果 -->
          <div v-if="validationResult" class="yaml-dialog__validation"
               :class="validationResult.valid ? 'yaml-dialog__validation--valid' : 'yaml-dialog__validation--invalid'">
            <div class="yaml-dialog__validation-header">
              <span class="yaml-dialog__validation-icon">
                {{ validationResult.valid ? '✓' : '✗' }}
              </span>
              <span>{{ validationResult.valid ? t('yaml.valid', 'Valid YAML') : t('yaml.invalid', 'Invalid YAML') }}</span>
            </div>
            <div v-if="validationResult.errors?.length" class="yaml-dialog__validation-section">
              <h4>{{ t('yaml.errors', 'Errors') }}</h4>
              <ul>
                <li v-for="(err, i) in validationResult.errors" :key="'e' + i">{{ err }}</li>
              </ul>
            </div>
            <div v-if="validationResult.warnings?.length" class="yaml-dialog__validation-section">
              <h4>{{ t('yaml.warnings', 'Warnings') }}</h4>
              <ul>
                <li v-for="(warn, i) in validationResult.warnings" :key="'w' + i">{{ warn }}</li>
              </ul>
            </div>
            <div v-if="validationResult.stats" class="yaml-dialog__validation-stats">
              <span>{{ t('yaml.nodes', 'Nodes') }}: {{ validationResult.stats.nodeCount }}</span>
              <span>{{ t('yaml.edges', 'Edges') }}: {{ validationResult.stats.edgeCount }}</span>
              <span>{{ t('yaml.resources', 'Resources') }}: {{ validationResult.stats.resourceCount }}</span>
              <span>{{ t('yaml.uuids', 'UUIDs') }}: {{ validationResult.stats.uuidCount }}</span>
            </div>
          </div>

          <!-- 导入结果 -->
          <div v-if="importResult" class="yaml-dialog__result yaml-dialog__result--success">
            <div class="yaml-dialog__result-header">
              <span class="yaml-dialog__result-icon">✓</span>
              <span>{{ t('yaml.importSuccess', 'Import Successful') }}</span>
            </div>
            <div class="yaml-dialog__result-details">
              <p>{{ t('yaml.importedNodes', 'Nodes imported') }}: {{ importResult.nodeCount }}</p>
              <p>{{ t('yaml.importedEdges', 'Edges imported') }}: {{ importResult.edgeCount }}</p>
            </div>
          </div>
        </div>

        <!-- UUID 注册表面板 -->
        <div v-if="activeTab === 'uuid'" class="yaml-dialog__panel">
          <div class="yaml-dialog__uuid-toolbar">
            <div class="yaml-dialog__uuid-filter">
              <button
                v-for="type in ['All', 'Node', 'Resource', 'Edge', 'Scene']"
                :key="type"
                class="yaml-dialog__filter-btn"
                :class="{ 'yaml-dialog__filter-btn--active': uuidFilter === type }"
                @click="uuidFilter = type; loadUuidRegistry()"
              >
                {{ type }}
              </button>
            </div>
            <button class="yaml-dialog__btn yaml-dialog__btn--small" @click="loadUuidRegistry()">
              ↻ {{ t('yaml.refresh', 'Refresh') }}
            </button>
          </div>

          <div v-if="isLoadingUuid" class="yaml-dialog__loading">
            <div class="yaml-dialog__spinner"></div>
            <span>{{ t('yaml.loading', 'Loading...') }}</span>
          </div>

          <div v-else-if="uuidEntries.length === 0" class="yaml-dialog__empty">
            <p>{{ t('yaml.noUuidEntries', 'No UUID entries found. Export a blueprint first.') }}</p>
          </div>

          <div v-else class="yaml-dialog__uuid-list">
            <div
              v-for="entry in uuidEntries"
              :key="entry.uuid"
              class="yaml-dialog__uuid-item"
              @click="selectedUuid = selectedUuid === entry.uuid ? null : entry.uuid"
            >
              <div class="yaml-dialog__uuid-item-header">
                <span class="yaml-dialog__uuid-type" :class="`yaml-dialog__uuid-type--${entry.entityType.toLowerCase()}`">
                  {{ entry.entityType }}
                </span>
                <span class="yaml-dialog__uuid-name">{{ entry.displayName || entry.name }}</span>
                <span class="yaml-dialog__uuid-short">{{ entry.uuid.slice(0, 8) }}...</span>
              </div>
              <div v-if="selectedUuid === entry.uuid" class="yaml-dialog__uuid-detail">
                <p><strong>UUID:</strong> {{ entry.uuid }}</p>
                <p><strong>Name:</strong> {{ entry.name }}</p>
                <p><strong>Display Name:</strong> {{ entry.displayName }}</p>
                <p><strong>Created:</strong> {{ formatDate(entry.createdAt) }}</p>
                <p><strong>Updated:</strong> {{ formatDate(entry.updatedAt) }}</p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 状态栏 -->
      <div v-if="statusMessage" class="yaml-dialog__status" :class="`yaml-dialog__status--${statusType}`">
        {{ statusMessage }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { yamlConversionApi, type UuidEntry, type YamlValidationResult, type YamlImportResult } from '@/api'
import { tSync } from '@/services/translationService'

// 简单的翻译函数（如果没有翻译键则使用默认值）
function t(key: string, defaultValue: string): string {
  return tSync(key, defaultValue)
}

// ==================== Props & Emits ====================

const props = defineProps<{
  visible: boolean
  initialSceneId?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'imported', sceneId: string): void
}>()

// ==================== 状态 ====================

const activeTab = ref<'export' | 'import' | 'uuid'>('export')
const isExporting = ref(false)
const isImporting = ref(false)
const isValidating = ref(false)
const isLoadingUuid = ref(false)
const isDragOver = ref(false)
const selectedFile = ref<File | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)
const previewContent = ref('')
const validationResult = ref<YamlValidationResult | null>(null)
const importResult = ref<YamlImportResult | null>(null)
const uuidEntries = ref<UuidEntry[]>([])
const uuidFilter = ref('All')
const selectedUuid = ref<string | null>(null)
const statusMessage = ref('')
const statusType = ref<'info' | 'success' | 'error'>('info')

// 表单数据
const exportForm = ref({
  sceneId: '',
  displayName: '',
  description: '',
  author: '',
})

const importForm = ref({
  sceneId: '',
  yamlContent: '',
  clearExisting: true,
})

// ==================== 计算属性 ====================

const canImport = computed(() => {
  return importForm.value.sceneId &&
    (importForm.value.yamlContent.trim() || selectedFile.value)
})

// ==================== 监听 ====================

watch(() => props.visible, (val) => {
  if (val) {
    if (props.initialSceneId) {
      exportForm.value.sceneId = props.initialSceneId
      importForm.value.sceneId = props.initialSceneId
    }
    clearResults()
  }
})

// ==================== 导出功能 ====================

async function handleExport() {
  if (!exportForm.value.sceneId) return

  isExporting.value = true
  clearResults()
  showStatus(t('yaml.exportingStatus', 'Exporting blueprint to YAML...'), 'info')

  try {
    const result = await yamlConversionApi.exportYaml(exportForm.value.sceneId, {
      displayName: exportForm.value.displayName,
      description: exportForm.value.description,
      author: exportForm.value.author,
    })

    if (result.success && result.data) {
      previewContent.value = result.data.yamlContent
      showStatus(t('yaml.exportSuccess', 'Export successful!'), 'success')
    } else {
      showStatus(result.error || t('yaml.exportFailed', 'Export failed'), 'error')
    }
  } catch (err: any) {
    showStatus(err.message || t('yaml.exportError', 'Export error'), 'error')
  } finally {
    isExporting.value = false
  }
}

function handleDownload() {
  if (!exportForm.value.sceneId) return
  const url = yamlConversionApi.downloadYaml(exportForm.value.sceneId, {
    displayName: exportForm.value.displayName,
    description: exportForm.value.description,
    author: exportForm.value.author,
  })
  window.open(url, '_blank')
}

function copyPreview() {
  if (previewContent.value) {
    navigator.clipboard.writeText(previewContent.value)
    showStatus(t('yaml.copied', 'Copied to clipboard!'), 'success')
  }
}

// ==================== 导入功能 ====================

async function handleImport() {
  if (!canImport.value) return

  isImporting.value = true
  clearResults()
  showStatus(t('yaml.importingStatus', 'Importing YAML to blueprint...'), 'info')

  try {
    let result
    if (selectedFile.value) {
      result = await yamlConversionApi.uploadYaml(importForm.value.sceneId, selectedFile.value)
    } else {
      result = await yamlConversionApi.importYaml(
        importForm.value.sceneId,
        importForm.value.yamlContent,
        importForm.value.clearExisting
      )
    }

    if (result.success && result.data) {
      importResult.value = result.data
      showStatus(t('yaml.importSuccessStatus', 'Import successful!'), 'success')
      emit('imported', importForm.value.sceneId)
    } else {
      showStatus(result.error || t('yaml.importFailed', 'Import failed'), 'error')
    }
  } catch (err: any) {
    showStatus(err.message || t('yaml.importError', 'Import error'), 'error')
  } finally {
    isImporting.value = false
  }
}

async function handleValidate() {
  if (!importForm.value.yamlContent.trim()) return

  isValidating.value = true
  validationResult.value = null

  try {
    const result = await yamlConversionApi.validateYaml(importForm.value.yamlContent)
    if (result.success && result.data) {
      validationResult.value = result.data
    }
  } catch (err: any) {
    showStatus(err.message || t('yaml.validateError', 'Validation error'), 'error')
  } finally {
    isValidating.value = false
  }
}

// ==================== 文件处理 ====================

function handleFileSelect(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files && input.files.length > 0) {
    selectedFile.value = input.files[0]
    // 自动读取文件内容到文本区域
    const reader = new FileReader()
    reader.onload = (e) => {
      importForm.value.yamlContent = e.target?.result as string
    }
    reader.readAsText(selectedFile.value)
  }
}

function handleFileDrop(event: DragEvent) {
  isDragOver.value = false
  if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
    const file = event.dataTransfer.files[0]
    if (file.name.endsWith('.yaml') || file.name.endsWith('.yml')) {
      selectedFile.value = file
      const reader = new FileReader()
      reader.onload = (e) => {
        importForm.value.yamlContent = e.target?.result as string
      }
      reader.readAsText(file)
    } else {
      showStatus(t('yaml.invalidFile', 'Please select a YAML file (.yaml or .yml)'), 'error')
    }
  }
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

// ==================== UUID 注册表 ====================

async function loadUuidRegistry() {
  isLoadingUuid.value = true
  try {
    let result
    if (uuidFilter.value === 'All') {
      result = await yamlConversionApi.getUuidRegistry()
    } else {
      result = await yamlConversionApi.getUuidRegistryByType(uuidFilter.value)
    }
    if (result.success && result.data) {
      uuidEntries.value = result.data
    }
  } catch (err: any) {
    showStatus(err.message || t('yaml.uuidLoadError', 'Failed to load UUID registry'), 'error')
  } finally {
    isLoadingUuid.value = false
  }
}

// ==================== 工具函数 ====================

function clearResults() {
  previewContent.value = ''
  validationResult.value = null
  importResult.value = null
  selectedFile.value = null
  statusMessage.value = ''
}

function showStatus(message: string, type: 'info' | 'success' | 'error') {
  statusMessage.value = message
  statusType.value = type
  if (type !== 'info') {
    setTimeout(() => {
      if (statusMessage.value === message) {
        statusMessage.value = ''
      }
    }, 5000)
  }
}

function formatDate(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleString()
  } catch {
    return dateStr
  }
}

function handleClose() {
  emit('close')
}
</script>

<style scoped>
/* ==================== 对话框遮罩 ==================== */
.yaml-dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.yaml-dialog {
  background: #1e1e2e;
  border-radius: 12px;
  width: 720px;
  max-width: 90vw;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
  border: 1px solid #313244;
  overflow: hidden;
}

.yaml-dialog--expanded {
  width: 900px;
}

/* ==================== 标题栏 ==================== */
.yaml-dialog__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid #313244;
  background: #181825;
}

.yaml-dialog__title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.yaml-dialog__title h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #cdd6f4;
}

.yaml-dialog__icon {
  font-size: 20px;
  color: #89b4fa;
}

.yaml-dialog__close {
  background: none;
  border: none;
  color: #6c7086;
  font-size: 18px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: all 0.2s;
}

.yaml-dialog__close:hover {
  background: #45475a;
  color: #cdd6f4;
}

/* ==================== 标签页 ==================== */
.yaml-dialog__tabs {
  display: flex;
  border-bottom: 1px solid #313244;
  background: #181825;
}

.yaml-dialog__tab {
  flex: 1;
  padding: 12px 16px;
  background: none;
  border: none;
  color: #6c7086;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  border-bottom: 2px solid transparent;
}

.yaml-dialog__tab:hover {
  color: #cdd6f4;
  background: #1e1e2e;
}

.yaml-dialog__tab--active {
  color: #89b4fa;
  border-bottom-color: #89b4fa;
  background: #1e1e2e;
}

.yaml-dialog__tab-icon {
  font-size: 14px;
}

/* ==================== 内容区 ==================== */
.yaml-dialog__body {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.yaml-dialog__panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* ==================== 表单 ==================== */
.yaml-dialog__form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.yaml-dialog__form-group label {
  font-size: 13px;
  font-weight: 500;
  color: #a6adc8;
}

.yaml-dialog__input,
.yaml-dialog__textarea {
  background: #181825;
  border: 1px solid #313244;
  border-radius: 6px;
  padding: 8px 12px;
  color: #cdd6f4;
  font-size: 14px;
  font-family: inherit;
  transition: border-color 0.2s;
}

.yaml-dialog__input:focus,
.yaml-dialog__textarea:focus {
  outline: none;
  border-color: #89b4fa;
}

.yaml-dialog__textarea {
  resize: vertical;
  min-height: 60px;
}

.yaml-dialog__textarea--code {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 12px;
  line-height: 1.5;
}

.yaml-dialog__checkbox-group {
  flex-direction: row;
}

.yaml-dialog__checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;
  color: #a6adc8;
}

.yaml-dialog__checkbox input[type="checkbox"] {
  accent-color: #89b4fa;
}

/* ==================== 按钮 ==================== */
.yaml-dialog__actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.yaml-dialog__btn {
  padding: 8px 16px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
  display: flex;
  align-items: center;
  gap: 6px;
}

.yaml-dialog__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.yaml-dialog__btn--primary {
  background: #89b4fa;
  color: #1e1e2e;
}

.yaml-dialog__btn--primary:hover:not(:disabled) {
  background: #b4d0fb;
}

.yaml-dialog__btn--secondary {
  background: #313244;
  color: #cdd6f4;
  border-color: #45475a;
}

.yaml-dialog__btn--secondary:hover:not(:disabled) {
  background: #45475a;
}

.yaml-dialog__btn--small {
  padding: 4px 10px;
  font-size: 12px;
}

/* ==================== 上传区域 ==================== */
.yaml-dialog__upload-area {
  border: 2px dashed #313244;
  border-radius: 8px;
  padding: 24px;
  text-align: center;
  transition: all 0.2s;
  position: relative;
  cursor: pointer;
}

.yaml-dialog__upload-area:hover,
.yaml-dialog__upload-area--dragover {
  border-color: #89b4fa;
  background: rgba(137, 180, 250, 0.05);
}

.yaml-dialog__file-input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.yaml-dialog__upload-icon {
  font-size: 32px;
  margin-bottom: 8px;
}

.yaml-dialog__upload-text {
  color: #6c7086;
  font-size: 13px;
  margin: 0;
}

.yaml-dialog__upload-btn {
  background: none;
  border: none;
  color: #89b4fa;
  cursor: pointer;
  font-size: 13px;
  text-decoration: underline;
  padding: 0;
}

.yaml-dialog__file-name {
  color: #a6e3a1;
  font-size: 12px;
  margin: 8px 0 0;
}

/* ==================== 预览 ==================== */
.yaml-dialog__preview {
  border: 1px solid #313244;
  border-radius: 8px;
  overflow: hidden;
}

.yaml-dialog__preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: #181825;
  border-bottom: 1px solid #313244;
}

.yaml-dialog__preview-header h3 {
  margin: 0;
  font-size: 13px;
  color: #a6adc8;
}

.yaml-dialog__preview-content {
  margin: 0;
  padding: 12px;
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 11px;
  line-height: 1.5;
  color: #cdd6f4;
  background: #11111b;
  max-height: 300px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}

/* ==================== 验证结果 ==================== */
.yaml-dialog__validation {
  border-radius: 8px;
  padding: 12px;
  border: 1px solid;
}

.yaml-dialog__validation--valid {
  background: rgba(166, 227, 161, 0.1);
  border-color: #a6e3a1;
}

.yaml-dialog__validation--invalid {
  background: rgba(243, 139, 168, 0.1);
  border-color: #f38ba8;
}

.yaml-dialog__validation-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
}

.yaml-dialog__validation--valid .yaml-dialog__validation-header {
  color: #a6e3a1;
}

.yaml-dialog__validation--invalid .yaml-dialog__validation-header {
  color: #f38ba8;
}

.yaml-dialog__validation-icon {
  font-size: 16px;
}

.yaml-dialog__validation-section {
  margin-top: 8px;
}

.yaml-dialog__validation-section h4 {
  margin: 0 0 4px;
  font-size: 12px;
  color: #a6adc8;
}

.yaml-dialog__validation-section ul {
  margin: 0;
  padding-left: 20px;
  font-size: 12px;
}

.yaml-dialog__validation-section li {
  color: #cdd6f4;
  margin-bottom: 2px;
}

.yaml-dialog__validation-stats {
  display: flex;
  gap: 16px;
  margin-top: 10px;
  padding-top: 8px;
  border-top: 1px solid #313244;
  font-size: 12px;
  color: #a6adc8;
}

/* ==================== 导入结果 ==================== */
.yaml-dialog__result {
  border-radius: 8px;
  padding: 12px;
  border: 1px solid;
}

.yaml-dialog__result--success {
  background: rgba(166, 227, 161, 0.1);
  border-color: #a6e3a1;
}

.yaml-dialog__result-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: #a6e3a1;
  font-size: 14px;
}

.yaml-dialog__result-icon {
  font-size: 16px;
}

.yaml-dialog__result-details {
  margin-top: 6px;
  font-size: 13px;
  color: #cdd6f4;
}

.yaml-dialog__result-details p {
  margin: 2px 0;
}

/* ==================== UUID 注册表 ==================== */
.yaml-dialog__uuid-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.yaml-dialog__uuid-filter {
  display: flex;
  gap: 4px;
}

.yaml-dialog__filter-btn {
  padding: 4px 10px;
  border-radius: 4px;
  border: 1px solid #313244;
  background: #181825;
  color: #6c7086;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.yaml-dialog__filter-btn:hover {
  color: #cdd6f4;
  border-color: #45475a;
}

.yaml-dialog__filter-btn--active {
  background: #89b4fa;
  color: #1e1e2e;
  border-color: #89b4fa;
}

.yaml-dialog__uuid-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 400px;
  overflow-y: auto;
}

.yaml-dialog__uuid-item {
  border: 1px solid #313244;
  border-radius: 6px;
  overflow: hidden;
  cursor: pointer;
  transition: border-color 0.2s;
}

.yaml-dialog__uuid-item:hover {
  border-color: #45475a;
}

.yaml-dialog__uuid-item-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #181825;
}

.yaml-dialog__uuid-type {
  padding: 2px 8px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
}

.yaml-dialog__uuid-type--node {
  background: #89b4fa;
  color: #1e1e2e;
}

.yaml-dialog__uuid-type--resource {
  background: #a6e3a1;
  color: #1e1e2e;
}

.yaml-dialog__uuid-type--edge {
  background: #f9e2af;
  color: #1e1e2e;
}

.yaml-dialog__uuid-type--scene {
  background: #cba6f7;
  color: #1e1e2e;
}

.yaml-dialog__uuid-name {
  flex: 1;
  font-size: 13px;
  color: #cdd6f4;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.yaml-dialog__uuid-short {
  font-family: monospace;
  font-size: 11px;
  color: #6c7086;
}

.yaml-dialog__uuid-detail {
  padding: 10px 12px;
  background: #11111b;
  border-top: 1px solid #313244;
  font-size: 12px;
  color: #a6adc8;
}

.yaml-dialog__uuid-detail p {
  margin: 2px 0;
}

.yaml-dialog__uuid-detail strong {
  color: #cdd6f4;
}

/* ==================== 加载和空状态 ==================== */
.yaml-dialog__loading,
.yaml-dialog__empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
  color: #6c7086;
  font-size: 14px;
  gap: 12px;
}

/* ==================== 状态栏 ==================== */
.yaml-dialog__status {
  padding: 8px 20px;
  font-size: 13px;
  border-top: 1px solid #313244;
  background: #181825;
  transition: all 0.3s;
}

.yaml-dialog__status--info {
  color: #89b4fa;
}

.yaml-dialog__status--success {
  color: #a6e3a1;
}

.yaml-dialog__status--error {
  color: #f38ba8;
}

/* ==================== 加载动画 ==================== */
.yaml-dialog__spinner {
  display: inline-block;
  width: 14px;
  height: 14px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: yaml-spin 0.6s linear infinite;
}

@keyframes yaml-spin {
  to { transform: rotate(360deg); }
}
</style>