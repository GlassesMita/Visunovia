<template>
  <div v-if="visible" class="json-dialog-overlay" @click.self="handleClose">
    <div class="json-dialog" :class="{ 'json-dialog--expanded': activeTab === 'preview' && previewContent }">
      <!-- 标题栏 -->
      <div class="json-dialog__header">
        <div class="json-dialog__title">
          <span class="json-dialog__icon">⟷</span>
          <h2>{{ t('json.title', 'JSON ↔ Blueprint') }}</h2>
        </div>
        <button class="json-dialog__close" @click="handleClose" :title="t('common.close', 'Close')">✕</button>
      </div>

      <!-- 标签页 -->
      <div class="json-dialog__tabs">
        <button
          class="json-dialog__tab"
          :class="{ 'json-dialog__tab--active': activeTab === 'export' }"
          @click="activeTab = 'export'"
        >
          <span class="json-dialog__tab-icon">📤</span>
          {{ t('json.export', 'Export') }}
        </button>
        <button
          class="json-dialog__tab"
          :class="{ 'json-dialog__tab--active': activeTab === 'import' }"
          @click="activeTab = 'import'"
        >
          <span class="json-dialog__tab-icon">📥</span>
          {{ t('json.import', 'Import') }}
        </button>
        <button
          class="json-dialog__tab"
          :class="{ 'json-dialog__tab--active': activeTab === 'uuid' }"
          @click="activeTab = 'uuid'; loadUuidRegistry()"
        >
          <span class="json-dialog__tab-icon">🔑</span>
          {{ t('json.uuidRegistry', 'UUID Registry') }}
        </button>
      </div>

      <!-- 内容区 -->
      <div class="json-dialog__body">
        <!-- 导出面板 -->
        <div v-if="activeTab === 'export'" class="json-dialog__panel">
          <div class="json-dialog__form-group">
            <label>{{ t('json.sceneId', 'Scene ID') }}</label>
            <input
              v-model="exportForm.sceneId"
              type="text"
              class="json-dialog__input"
              :placeholder="t('json.sceneIdPlaceholder', 'Enter scene ID')"
            />
          </div>
          <div class="json-dialog__form-group">
            <label>{{ t('json.displayName', 'Display Name') }}</label>
            <input
              v-model="exportForm.displayName"
              type="text"
              class="json-dialog__input"
              :placeholder="t('json.displayNamePlaceholder', 'Optional display name')"
            />
          </div>
          <div class="json-dialog__form-group">
            <label>{{ t('json.description', 'Description') }}</label>
            <textarea
              v-model="exportForm.description"
              class="json-dialog__textarea"
              rows="2"
              :placeholder="t('json.descriptionPlaceholder', 'Optional description')"
            />
          </div>
          <div class="json-dialog__form-group">
            <label>{{ t('json.author', 'Author') }}</label>
            <input
              v-model="exportForm.author"
              type="text"
              class="json-dialog__input"
              :placeholder="t('json.authorPlaceholder', 'Optional author name')"
            />
          </div>

          <div class="json-dialog__actions">
            <button
              class="json-dialog__btn json-dialog__btn--primary"
              :disabled="!exportForm.sceneId || isExporting"
              @click="handleExport"
            >
              <span v-if="isExporting" class="json-dialog__spinner"></span>
              {{ isExporting ? t('json.exporting', 'Exporting...') : t('json.exportJson', 'Export JSON') }}
            </button>
            <button
              class="json-dialog__btn json-dialog__btn--secondary"
              :disabled="!exportForm.sceneId || isExporting"
              @click="handleDownload"
            >
              {{ t('json.download', 'Download File') }}
            </button>
          </div>

          <!-- 导出结果预览 -->
          <div v-if="previewContent" class="json-dialog__preview">
            <div class="json-dialog__preview-header">
              <h3>{{ t('json.preview', 'Preview') }}</h3>
              <button class="json-dialog__btn json-dialog__btn--small" @click="copyPreview">
                {{ t('json.copy', 'Copy') }}
              </button>
            </div>
            <pre class="json-dialog__preview-content">{{ previewContent }}</pre>
          </div>
        </div>

        <!-- 导入面板 -->
        <div v-if="activeTab === 'import'" class="json-dialog__panel">
          <div class="json-dialog__form-group">
            <label>{{ t('json.targetSceneId', 'Target Scene ID') }}</label>
            <input
              v-model="importForm.sceneId"
              type="text"
              class="json-dialog__input"
              :placeholder="t('json.targetSceneIdPlaceholder', 'Enter target scene ID')"
            />
          </div>

          <!-- 文件上传 -->
          <div class="json-dialog__upload-area"
               :class="{ 'json-dialog__upload-area--dragover': isDragOver }"
               @dragover.prevent="isDragOver = true"
               @dragleave="isDragOver = false"
               @drop.prevent="handleFileDrop"
          >
            <input
              ref="fileInputRef"
              type="file"
              accept=".json,.lor"
              class="json-dialog__file-input"
              @change="handleFileSelect"
            />
            <div class="json-dialog__upload-icon">📄</div>
            <p class="json-dialog__upload-text">
              {{ t('json.dragDrop', 'Drag & drop JSON file here, or') }}
              <button class="json-dialog__upload-btn" @click="fileInputRef?.click()">
                {{ t('json.browse', 'browse') }}
              </button>
            </p>
            <p v-if="selectedFile" class="json-dialog__file-name">
              {{ selectedFile.name }} ({{ formatFileSize(selectedFile.size) }})
            </p>
          </div>

          <!-- 或直接粘贴 JSON -->
          <div class="json-dialog__form-group">
            <label>{{ t('json.orPaste', 'Or paste JSON content') }}</label>
            <textarea
              v-model="importForm.jsonContent"
              class="json-dialog__textarea json-dialog__textarea--code"
              rows="8"
              :placeholder="t('json.pastePlaceholder', 'Paste JSON content here...')"
            />
          </div>

          <div class="json-dialog__form-group json-dialog__checkbox-group">
            <label class="json-dialog__checkbox">
              <input v-model="importForm.clearExisting" type="checkbox" />
              <span>{{ t('json.clearExisting', 'Clear existing data before import') }}</span>
            </label>
          </div>

          <div class="json-dialog__actions">
            <button
              class="json-dialog__btn json-dialog__btn--primary"
              :disabled="!canImport || isImporting"
              @click="handleImport"
            >
              <span v-if="isImporting" class="json-dialog__spinner"></span>
              {{ isImporting ? t('json.importing', 'Importing...') : t('json.importJson', 'Import JSON') }}
            </button>
            <button
              class="json-dialog__btn json-dialog__btn--secondary"
              :disabled="!importForm.jsonContent || isValidating"
              @click="handleValidate"
            >
              {{ isValidating ? t('json.validating', 'Validating...') : t('json.validate', 'Validate') }}
            </button>
          </div>

          <!-- 验证结果 -->
          <div v-if="validationResult" class="json-dialog__validation"
               :class="validationResult.valid ? 'json-dialog__validation--valid' : 'json-dialog__validation--invalid'">
            <div class="json-dialog__validation-header">
              <span class="json-dialog__validation-icon">
                {{ validationResult.valid ? '✓' : '✗' }}
              </span>
              <span>{{ validationResult.valid ? t('json.valid', 'Valid JSON') : t('json.invalid', 'Invalid JSON') }}</span>
            </div>
            <div v-if="validationResult.errors?.length" class="json-dialog__validation-section">
              <h4>{{ t('json.errors', 'Errors') }}</h4>
              <ul>
                <li v-for="(err, i) in validationResult.errors" :key="'e' + i">{{ err }}</li>
              </ul>
            </div>
            <div v-if="validationResult.warnings?.length" class="json-dialog__validation-section">
              <h4>{{ t('json.warnings', 'Warnings') }}</h4>
              <ul>
                <li v-for="(warn, i) in validationResult.warnings" :key="'w' + i">{{ warn }}</li>
              </ul>
            </div>
            <div v-if="validationResult.stats" class="json-dialog__validation-stats">
              <span>{{ t('json.nodes', 'Nodes') }}: {{ validationResult.stats.nodeCount }}</span>
              <span>{{ t('json.edges', 'Edges') }}: {{ validationResult.stats.edgeCount }}</span>
              <span>{{ t('json.resources', 'Resources') }}: {{ validationResult.stats.resourceCount }}</span>
              <span>{{ t('json.uuids', 'UUIDs') }}: {{ validationResult.stats.uuidCount }}</span>
            </div>
          </div>

          <!-- 导入结果 -->
          <div v-if="importResult" class="json-dialog__result json-dialog__result--success">
            <div class="json-dialog__result-header">
              <span class="json-dialog__result-icon">✓</span>
              <span>{{ t('json.importSuccess', 'Import Successful') }}</span>
            </div>
            <div class="json-dialog__result-details">
              <p>{{ t('json.importedNodes', 'Nodes imported') }}: {{ importResult.nodeCount }}</p>
              <p>{{ t('json.importedEdges', 'Edges imported') }}: {{ importResult.edgeCount }}</p>
            </div>
          </div>
        </div>

        <!-- UUID 注册表面板 -->
        <div v-if="activeTab === 'uuid'" class="json-dialog__panel">
          <div class="json-dialog__uuid-toolbar">
            <div class="json-dialog__uuid-filter">
              <button
                v-for="type in ['All', 'Node', 'Resource', 'Edge', 'Scene']"
                :key="type"
                class="json-dialog__filter-btn"
                :class="{ 'json-dialog__filter-btn--active': uuidFilter === type }"
                @click="uuidFilter = type; loadUuidRegistry()"
              >
                {{ type }}
              </button>
            </div>
            <button class="json-dialog__btn json-dialog__btn--small" @click="loadUuidRegistry()">
              ↻ {{ t('json.refresh', 'Refresh') }}
            </button>
          </div>

          <div v-if="isLoadingUuid" class="json-dialog__loading">
            <div class="json-dialog__spinner"></div>
            <span>{{ t('json.loading', 'Loading...') }}</span>
          </div>

          <div v-else-if="uuidEntries.length === 0" class="json-dialog__empty">
            <p>{{ t('json.noUuidEntries', 'No UUID entries found. Export a blueprint first.') }}</p>
          </div>

          <div v-else class="json-dialog__uuid-list">
            <div
              v-for="entry in uuidEntries"
              :key="entry.uuid"
              class="json-dialog__uuid-item"
              @click="selectedUuid = selectedUuid === entry.uuid ? null : entry.uuid"
            >
              <div class="json-dialog__uuid-item-header">
                <span class="json-dialog__uuid-type" :class="`json-dialog__uuid-type--${entry.entityType.toLowerCase()}`">
                  {{ entry.entityType }}
                </span>
                <span class="json-dialog__uuid-name">{{ entry.displayName || entry.name }}</span>
                <span class="json-dialog__uuid-short">{{ entry.uuid.slice(0, 8) }}...</span>
              </div>
              <div v-if="selectedUuid === entry.uuid" class="json-dialog__uuid-detail">
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
      <div v-if="statusMessage" class="json-dialog__status" :class="`json-dialog__status--${statusType}`">
        {{ statusMessage }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { jsonConversionApi, type UuidEntry, type JsonValidationResult, type JsonImportResult } from '@/api'
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
const validationResult = ref<JsonValidationResult | null>(null)
const importResult = ref<JsonImportResult | null>(null)
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
  jsonContent: '',
  clearExisting: true,
})

// ==================== 计算属性 ====================

const canImport = computed(() => {
  return importForm.value.sceneId &&
    (importForm.value.jsonContent.trim() || selectedFile.value)
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
  showStatus(t('json.exportingStatus', 'Exporting blueprint to JSON...'), 'info')

  try {
    const result = await jsonConversionApi.exportJson(exportForm.value.sceneId, {
      displayName: exportForm.value.displayName,
      description: exportForm.value.description,
      author: exportForm.value.author,
    })

    if (result.success && result.data) {
      previewContent.value = result.data.jsonContent
      showStatus(t('json.exportSuccess', 'Export successful!'), 'success')
    } else {
      showStatus(result.error || t('json.exportFailed', 'Export failed'), 'error')
    }
  } catch (err: any) {
    showStatus(err.message || t('json.exportError', 'Export error'), 'error')
  } finally {
    isExporting.value = false
  }
}

function handleDownload() {
  if (!exportForm.value.sceneId) return
  const url = jsonConversionApi.downloadJson(exportForm.value.sceneId, {
    displayName: exportForm.value.displayName,
    description: exportForm.value.description,
    author: exportForm.value.author,
  })
  window.open(url, '_blank')
}

function copyPreview() {
  if (previewContent.value) {
    navigator.clipboard.writeText(previewContent.value)
    showStatus(t('json.copied', 'Copied to clipboard!'), 'success')
  }
}

// ==================== 导入功能 ====================

async function handleImport() {
  if (!canImport.value) return

  isImporting.value = true
  clearResults()
  showStatus(t('json.importingStatus', 'Importing JSON to blueprint...'), 'info')

  try {
    let result
    if (selectedFile.value) {
      result = await jsonConversionApi.uploadJson(importForm.value.sceneId, selectedFile.value)
    } else {
      result = await jsonConversionApi.importJson(
        importForm.value.sceneId,
        importForm.value.jsonContent,
        importForm.value.clearExisting
      )
    }

    if (result.success && result.data) {
      importResult.value = result.data
      showStatus(t('json.importSuccessStatus', 'Import successful!'), 'success')
      emit('imported', importForm.value.sceneId)
    } else {
      showStatus(result.error || t('json.importFailed', 'Import failed'), 'error')
    }
  } catch (err: any) {
    showStatus(err.message || t('json.importError', 'Import error'), 'error')
  } finally {
    isImporting.value = false
  }
}

async function handleValidate() {
  if (!importForm.value.jsonContent.trim()) return

  isValidating.value = true
  validationResult.value = null

  try {
    const result = await jsonConversionApi.validateJson(importForm.value.jsonContent)
    if (result.success && result.data) {
      validationResult.value = result.data
    }
  } catch (err: any) {
    showStatus(err.message || t('json.validateError', 'Validation error'), 'error')
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
      importForm.value.jsonContent = e.target?.result as string
    }
    reader.readAsText(selectedFile.value)
  }
}

function handleFileDrop(event: DragEvent) {
  isDragOver.value = false
  if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
    const file = event.dataTransfer.files[0]
    if (file.name.endsWith('.json') || file.name.endsWith('.lor')) {
      selectedFile.value = file
      const reader = new FileReader()
      reader.onload = (e) => {
        importForm.value.jsonContent = e.target?.result as string
      }
      reader.readAsText(file)
    } else {
      showStatus(t('json.invalidFile', 'Please select a JSON file (.json or .lor)'), 'error')
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
      result = await jsonConversionApi.getUuidRegistry()
    } else {
      result = await jsonConversionApi.getUuidRegistryByType(uuidFilter.value)
    }
    if (result.success && result.data) {
      uuidEntries.value = result.data
    }
  } catch (err: any) {
    showStatus(err.message || t('json.uuidLoadError', 'Failed to load UUID registry'), 'error')
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
.json-dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.json-dialog {
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

.json-dialog--expanded {
  width: 900px;
}

/* ==================== 标题栏 ==================== */
.json-dialog__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid #313244;
  background: #181825;
}

.json-dialog__title {
  display: flex;
  align-items: center;
  gap: 10px;
}

.json-dialog__title h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #cdd6f4;
}

.json-dialog__icon {
  font-size: 20px;
  color: #89b4fa;
}

.json-dialog__close {
  background: none;
  border: none;
  color: #6c7086;
  font-size: 18px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: all 0.2s;
}

.json-dialog__close:hover {
  background: #45475a;
  color: #cdd6f4;
}

/* ==================== 标签页 ==================== */
.json-dialog__tabs {
  display: flex;
  border-bottom: 1px solid #313244;
  background: #181825;
}

.json-dialog__tab {
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

.json-dialog__tab:hover {
  color: #cdd6f4;
  background: #1e1e2e;
}

.json-dialog__tab--active {
  color: #89b4fa;
  border-bottom-color: #89b4fa;
  background: #1e1e2e;
}

.json-dialog__tab-icon {
  font-size: 14px;
}

/* ==================== 内容区 ==================== */
.json-dialog__body {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.json-dialog__panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* ==================== 表单 ==================== */
.json-dialog__form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.json-dialog__form-group label {
  font-size: 13px;
  font-weight: 500;
  color: #a6adc8;
}

.json-dialog__input,
.json-dialog__textarea {
  background: #181825;
  border: 1px solid #313244;
  border-radius: 6px;
  padding: 8px 12px;
  color: #cdd6f4;
  font-size: 14px;
  font-family: inherit;
  transition: border-color 0.2s;
}

.json-dialog__input:focus,
.json-dialog__textarea:focus {
  outline: none;
  border-color: #89b4fa;
}

.json-dialog__textarea {
  resize: vertical;
  min-height: 60px;
}

.json-dialog__textarea--code {
  font-family: 'JetBrains Mono', 'Fira Code', monospace;
  font-size: 12px;
  line-height: 1.5;
}

.json-dialog__checkbox-group {
  flex-direction: row;
}

.json-dialog__checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;
  color: #a6adc8;
}

.json-dialog__checkbox input[type="checkbox"] {
  accent-color: #89b4fa;
}

/* ==================== 按钮 ==================== */
.json-dialog__actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.json-dialog__btn {
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

.json-dialog__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.json-dialog__btn--primary {
  background: #89b4fa;
  color: #1e1e2e;
}

.json-dialog__btn--primary:hover:not(:disabled) {
  background: #b4d0fb;
}

.json-dialog__btn--secondary {
  background: #313244;
  color: #cdd6f4;
  border-color: #45475a;
}

.json-dialog__btn--secondary:hover:not(:disabled) {
  background: #45475a;
}

.json-dialog__btn--small {
  padding: 4px 10px;
  font-size: 12px;
}

/* ==================== 上传区域 ==================== */
.json-dialog__upload-area {
  border: 2px dashed #313244;
  border-radius: 8px;
  padding: 24px;
  text-align: center;
  transition: all 0.2s;
  position: relative;
  cursor: pointer;
}

.json-dialog__upload-area:hover,
.json-dialog__upload-area--dragover {
  border-color: #89b4fa;
  background: rgba(137, 180, 250, 0.05);
}

.json-dialog__file-input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.json-dialog__upload-icon {
  font-size: 32px;
  margin-bottom: 8px;
}

.json-dialog__upload-text {
  color: #6c7086;
  font-size: 13px;
  margin: 0;
}

.json-dialog__upload-btn {
  background: none;
  border: none;
  color: #89b4fa;
  cursor: pointer;
  font-size: 13px;
  text-decoration: underline;
  padding: 0;
}

.json-dialog__file-name {
  color: #a6e3a1;
  font-size: 12px;
  margin: 8px 0 0;
}

/* ==================== 预览 ==================== */
.json-dialog__preview {
  border: 1px solid #313244;
  border-radius: 8px;
  overflow: hidden;
}

.json-dialog__preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: #181825;
  border-bottom: 1px solid #313244;
}

.json-dialog__preview-header h3 {
  margin: 0;
  font-size: 13px;
  color: #a6adc8;
}

.json-dialog__preview-content {
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
.json-dialog__validation {
  border-radius: 8px;
  padding: 12px;
  border: 1px solid;
}

.json-dialog__validation--valid {
  background: rgba(166, 227, 161, 0.1);
  border-color: #a6e3a1;
}

.json-dialog__validation--invalid {
  background: rgba(243, 139, 168, 0.1);
  border-color: #f38ba8;
}

.json-dialog__validation-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
}

.json-dialog__validation--valid .json-dialog__validation-header {
  color: #a6e3a1;
}

.json-dialog__validation--invalid .json-dialog__validation-header {
  color: #f38ba8;
}

.json-dialog__validation-icon {
  font-size: 16px;
}

.json-dialog__validation-section {
  margin-top: 8px;
}

.json-dialog__validation-section h4 {
  margin: 0 0 4px;
  font-size: 12px;
  color: #a6adc8;
}

.json-dialog__validation-section ul {
  margin: 0;
  padding-left: 20px;
  font-size: 12px;
}

.json-dialog__validation-section li {
  color: #cdd6f4;
  margin-bottom: 2px;
}

.json-dialog__validation-stats {
  display: flex;
  gap: 16px;
  margin-top: 10px;
  padding-top: 8px;
  border-top: 1px solid #313244;
  font-size: 12px;
  color: #a6adc8;
}

/* ==================== 导入结果 ==================== */
.json-dialog__result {
  border-radius: 8px;
  padding: 12px;
  border: 1px solid;
}

.json-dialog__result--success {
  background: rgba(166, 227, 161, 0.1);
  border-color: #a6e3a1;
}

.json-dialog__result-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: #a6e3a1;
  font-size: 14px;
}

.json-dialog__result-icon {
  font-size: 16px;
}

.json-dialog__result-details {
  margin-top: 6px;
  font-size: 13px;
  color: #cdd6f4;
}

.json-dialog__result-details p {
  margin: 2px 0;
}

/* ==================== UUID 注册表 ==================== */
.json-dialog__uuid-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.json-dialog__uuid-filter {
  display: flex;
  gap: 4px;
}

.json-dialog__filter-btn {
  padding: 4px 10px;
  border-radius: 4px;
  border: 1px solid #313244;
  background: #181825;
  color: #6c7086;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
}

.json-dialog__filter-btn:hover {
  color: #cdd6f4;
  border-color: #45475a;
}

.json-dialog__filter-btn--active {
  background: #89b4fa;
  color: #1e1e2e;
  border-color: #89b4fa;
}

.json-dialog__uuid-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 400px;
  overflow-y: auto;
}

.json-dialog__uuid-item {
  border: 1px solid #313244;
  border-radius: 6px;
  overflow: hidden;
  cursor: pointer;
  transition: border-color 0.2s;
}

.json-dialog__uuid-item:hover {
  border-color: #45475a;
}

.json-dialog__uuid-item-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #181825;
}

.json-dialog__uuid-type {
  padding: 2px 8px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
}

.json-dialog__uuid-type--node {
  background: #89b4fa;
  color: #1e1e2e;
}

.json-dialog__uuid-type--resource {
  background: #a6e3a1;
  color: #1e1e2e;
}

.json-dialog__uuid-type--edge {
  background: #f9e2af;
  color: #1e1e2e;
}

.json-dialog__uuid-type--scene {
  background: #cba6f7;
  color: #1e1e2e;
}

.json-dialog__uuid-name {
  flex: 1;
  font-size: 13px;
  color: #cdd6f4;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.json-dialog__uuid-short {
  font-family: monospace;
  font-size: 11px;
  color: #6c7086;
}

.json-dialog__uuid-detail {
  padding: 10px 12px;
  background: #11111b;
  border-top: 1px solid #313244;
  font-size: 12px;
  color: #a6adc8;
}

.json-dialog__uuid-detail p {
  margin: 2px 0;
}

.json-dialog__uuid-detail strong {
  color: #cdd6f4;
}

/* ==================== 加载和空状态 ==================== */
.json-dialog__loading,
.json-dialog__empty {
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
.json-dialog__status {
  padding: 8px 20px;
  font-size: 13px;
  border-top: 1px solid #313244;
  background: #181825;
  transition: all 0.3s;
}

.json-dialog__status--info {
  color: #89b4fa;
}

.json-dialog__status--success {
  color: #a6e3a1;
}

.json-dialog__status--error {
  color: #f38ba8;
}

/* ==================== 加载动画 ==================== */
.json-dialog__spinner {
  display: inline-block;
  width: 14px;
  height: 14px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: json-spin 0.6s linear infinite;
}

@keyframes json-spin {
  to { transform: rotate(360deg); }
}
</style>
