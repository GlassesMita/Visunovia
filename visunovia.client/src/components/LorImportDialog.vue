<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useLorImport } from '@/composables/useLorImport'
import FileExplorer from './FileExplorer.vue'
import { getEntries } from '@/api/fileBrowser'

const props = defineProps<{
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'imported', sceneId: string): void
}>()

const lorImport = useLorImport()

const activeTab = ref<'explorer' | 'content'>('explorer')
const jsonContent = ref('')
const showPreview = ref(false)
const conversionPreview = ref<{
  nodeCount: number
  connectionCount: number
  nodeTypes: Record<string, number>
} | null>(null)

// 项目相关状态
const selectedProjectPath = ref('')
const selectedTlorPath = ref('')
const availableScenes = ref<string[]>([])
const selectedSceneId = ref('')
const isScanningProject = ref(false)

// 文件选择器
const showFileExplorer = ref(false)
const fileExplorerTitle = ref('选择文件')
const fileExplorerFilter = ref<string[]>([])

const isLoading = computed(() => lorImport.isImporting.value || isScanningProject.value)
const hasError = computed(() => lorImport.importError.value !== null)
const hasSuccess = computed(() => lorImport.importSuccess.value)
const validationErrors = computed(() => lorImport.validationResult.value?.errors || [])

/** 打开文件选择器选择项目目录 */
function openProjectExplorer() {
  fileExplorerTitle.value = '选择项目目录'
  fileExplorerFilter.value = []
  showFileExplorer.value = true
}

/** 打开文件选择器选择 .tlor 文件 */
function openTlorExplorer() {
  fileExplorerTitle.value = '选择项目文件 (.tlor)'
  fileExplorerFilter.value = ['.tlor']
  showFileExplorer.value = true
}

/** 处理文件选择结果 */
async function handleFileSelect(path: string, isDirectory: boolean) {
  showFileExplorer.value = false

  if (isDirectory) {
    // 选择了目录，扫描项目
    selectedProjectPath.value = path
    selectedTlorPath.value = ''
    await scanProject(path)
  } else if (path.endsWith('.tlor')) {
    // 选择了 .tlor 文件
    selectedTlorPath.value = path
    // 从 .tlor 文件路径推断项目目录
    const dir = path.substring(0, path.lastIndexOf('\\'))
    selectedProjectPath.value = dir
    await scanProject(dir)
  }
}

/** 扫描项目目录，读取所有 .lor 文件 */
async function scanProject(projectPath: string) {
  isScanningProject.value = true
  availableScenes.value = []
  selectedSceneId.value = ''

  try {
    // 尝试读取 Scripts/Main 目录
    const scriptsMainPath = projectPath + '\\Scripts\\Main'
    const result = await getEntries(scriptsMainPath)

    const lorFiles = result.entries.filter(e => !e.isDirectory && e.extension === '.lor')
    availableScenes.value = lorFiles.map(f => f.name.replace('.lor', ''))

    // 如果有可用的场景，默认选择第一个
    if (availableScenes.value.length > 0) {
      selectedSceneId.value = availableScenes.value[0]
    }
  } catch (e) {
    // 如果 Scripts/Main 不存在，尝试扫描整个目录
    try {
      const result = await getEntries(projectPath)
      const lorFiles = result.entries.filter(e => !e.isDirectory && e.extension === '.lor')
      availableScenes.value = lorFiles.map(f => f.name.replace('.lor', ''))

      if (availableScenes.value.length > 0) {
        selectedSceneId.value = availableScenes.value[0]
      }
    } catch (err) {
      console.error('扫描项目失败:', err)
    }
  } finally {
    isScanningProject.value = false
  }
}

/** 导入选中的场景 */
async function handleImport() {
  let success = false

  if (activeTab.value === 'explorer') {
    if (!selectedSceneId.value) return
    success = await lorImport.importFromProject(selectedProjectPath.value, selectedSceneId.value)
  } else {
    if (!selectedSceneId.value) return
    success = await lorImport.importFromContent(jsonContent.value, selectedSceneId.value)
  }

  if (success) {
    emit('imported', selectedSceneId.value)
    setTimeout(() => {
      emit('close')
    }, 1500)
  }
}

/** 预览转换结果 */
async function handlePreview() {
  if (activeTab.value === 'content' && jsonContent.value) {
    conversionPreview.value = await lorImport.previewConversion(jsonContent.value)
    showPreview.value = true
  }
}

function handleClose() {
  lorImport.clearImportState()
  emit('close')
}

/** 监听对话框打开，重置状态 */
watch(() => props.visible, (newVal) => {
  if (newVal) {
    activeTab.value = 'explorer'
    jsonContent.value = ''
    showPreview.value = false
    conversionPreview.value = null
    selectedProjectPath.value = ''
    selectedTlorPath.value = ''
    availableScenes.value = []
    selectedSceneId.value = ''
  }
})
</script>

<template>
  <Teleport to="body">
    <div v-if="visible" class="dialog-overlay" @click.self="handleClose">
      <div class="dialog-container">
        <div class="dialog-header">
          <h2>导入 Lor 剧本到蓝图</h2>
          <button class="close-btn" @click="handleClose">×</button>
        </div>
        
        <div class="dialog-body">
          <!-- 标签切换 -->
          <div class="tab-header">
            <button 
              :class="{ active: activeTab === 'explorer' }" 
              @click="activeTab = 'explorer'"
            >
              从项目导入
            </button>
            <button 
              :class="{ active: activeTab === 'content' }" 
              @click="activeTab = 'content'"
            >
              从内容导入
            </button>
          </div>
          
          <!-- 从项目导入 -->
          <div v-if="activeTab === 'explorer'" class="tab-content">
            <!-- 项目选择 -->
            <div class="form-group">
              <label>项目路径</label>
              <div class="input-with-btn">
                <input 
                  v-model="selectedProjectPath" 
                  type="text" 
                  placeholder="点击右侧按钮选择项目目录或 .tlor 文件"
                  :disabled="isLoading"
                  readonly
                />
                <button class="browse-btn" @click="openTlorExplorer" :disabled="isLoading">
                  浏览...
                </button>
              </div>
            </div>

            <!-- 场景列表 -->
            <div class="form-group" v-if="availableScenes.length > 0">
              <label>选择场景 ({{ availableScenes.length }} 个可用)</label>
              <select v-model="selectedSceneId" class="scene-select" :disabled="isLoading">
                <option v-for="scene in availableScenes" :key="scene" :value="scene">
                  {{ scene }}.lor
                </option>
              </select>
            </div>

            <!-- 无场景提示 -->
            <div v-else-if="selectedProjectPath && !isLoading" class="empty-hint">
              <span class="hint-icon">📂</span>
              <span>该项目中没有找到 .lor 文件</span>
            </div>

            <!-- 等待选择 -->
            <div v-else-if="!selectedProjectPath" class="empty-hint">
              <span class="hint-icon">👆</span>
              <span>请点击"浏览"按钮选择项目目录或 .tlor 文件</span>
            </div>
          </div>
          
          <!-- 从内容导入 -->
          <div v-else class="tab-content">
            <div class="form-group">
              <label>场景 ID</label>
              <input 
                v-model="selectedSceneId" 
                type="text" 
                placeholder="输入场景 ID (如: start)"
                :disabled="isLoading"
              />
            </div>

            <div class="form-group">
              <label>JSON 内容</label>
              <textarea 
                v-model="jsonContent" 
                rows="8" 
                placeholder="粘贴 Lor 剧本 JSON 内容..."
                :disabled="isLoading"
              />
            </div>
            <button 
              class="preview-btn" 
              @click="handlePreview"
              :disabled="!jsonContent || isLoading"
            >
              预览转换结果
            </button>
          </div>
          
          <!-- 预览结果 -->
          <div v-if="showPreview && conversionPreview" class="preview-result">
            <h4>转换预览</h4>
            <div class="preview-stats">
              <div class="stat">
                <span class="label">节点总数:</span>
                <span class="value">{{ conversionPreview.nodeCount }}</span>
              </div>
              <div class="stat">
                <span class="label">连接总数:</span>
                <span class="value">{{ conversionPreview.connectionCount }}</span>
              </div>
            </div>
            <div class="node-types">
              <h5>节点类型分布</h5>
              <div 
                v-for="(count, type) in conversionPreview.nodeTypes" 
                :key="type" 
                class="node-type-item"
              >
                <span class="type">{{ type }}</span>
                <span class="count">{{ count }}</span>
              </div>
            </div>
          </div>
          
          <!-- 验证错误 -->
          <div v-if="validationErrors.length > 0" class="validation-errors">
            <h4>验证警告</h4>
            <ul>
              <li v-for="(error, index) in validationErrors" :key="index">
                {{ error }}
              </li>
            </ul>
          </div>
          
          <!-- 错误提示 -->
          <div v-if="hasError" class="error-message">
            <span class="error-icon">⚠</span>
            <span>{{ lorImport.importError.value }}</span>
          </div>
          
          <!-- 成功提示 -->
          <div v-if="hasSuccess" class="success-message">
            <span class="success-icon">✓</span>
            <span>导入成功！</span>
          </div>
        </div>
        
        <div class="dialog-footer">
          <button class="cancel-btn" @click="handleClose" :disabled="isLoading">
            取消
          </button>
          <button 
            class="import-btn" 
            @click="handleImport" 
            :disabled="isLoading || !selectedSceneId || (activeTab === 'explorer' && !selectedProjectPath)"
          >
            <span v-if="isLoading">处理中...</span>
            <span v-else>导入并转换</span>
          </button>
        </div>
      </div>
    </div>

    <!-- 文件选择器 -->
    <FileExplorer
      :visible="showFileExplorer"
      :title="fileExplorerTitle"
      :file-filter="fileExplorerFilter"
      @close="showFileExplorer = false"
      @select="handleFileSelect"
    />
  </Teleport>
</template>

<style scoped>
.dialog-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.dialog-container {
  background: #1e1e1e;
  border-radius: 8px;
  width: 520px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
}

.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid #333;
}

.dialog-header h2 {
  margin: 0;
  font-size: 16px;
  color: #fff;
}

.close-btn {
  background: none;
  border: none;
  color: #888;
  font-size: 24px;
  cursor: pointer;
  padding: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.close-btn:hover {
  background: #333;
  color: #fff;
}

.dialog-body {
  padding: 20px;
  overflow-y: auto;
  flex: 1;
}

.tab-header {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
}

.tab-header button {
  flex: 1;
  padding: 10px;
  background: #2a2a2a;
  border: 1px solid #444;
  color: #aaa;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.tab-header button.active {
  background: #333;
  border-color: #666;
  color: #fff;
}

.tab-header button:hover:not(.active) {
  background: #333;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 6px;
  color: #aaa;
  font-size: 13px;
}

.form-group input,
.form-group textarea,
.form-group select {
  width: 100%;
  padding: 10px;
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 4px;
  color: #fff;
  font-size: 14px;
  box-sizing: border-box;
}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
  outline: none;
  border-color: #666;
}

.form-group textarea {
  resize: vertical;
  font-family: 'Consolas', 'Monaco', monospace;
}

.form-group select {
  cursor: pointer;
}

.form-group select option {
  background: #2a2a2a;
  color: #fff;
}

.input-with-btn {
  display: flex;
  gap: 8px;
}

.input-with-btn input {
  flex: 1;
}

.browse-btn {
  padding: 10px 16px;
  background: #3c3c3c;
  border: 1px solid #555;
  color: #ccc;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
  white-space: nowrap;
}

.browse-btn:hover:not(:disabled) {
  background: #505050;
}

.browse-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.scene-select {
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%23888' d='M6 8L1 3h10z'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  padding-right: 32px;
}

.empty-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px;
  background: #2a2a2a;
  border-radius: 4px;
  color: #888;
  font-size: 13px;
}

.hint-icon {
  font-size: 20px;
}

.preview-btn {
  padding: 8px 16px;
  background: #2a2a2a;
  border: 1px solid #444;
  color: #aaa;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
}

.preview-btn:hover:not(:disabled) {
  background: #333;
  color: #fff;
}

.preview-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.preview-result {
  background: #2a2a2a;
  border-radius: 4px;
  padding: 12px;
  margin-top: 16px;
}

.preview-result h4 {
  margin: 0 0 12px 0;
  color: #fff;
  font-size: 14px;
}

.preview-stats {
  display: flex;
  gap: 20px;
  margin-bottom: 12px;
}

.stat {
  display: flex;
  flex-direction: column;
}

.stat .label {
  font-size: 11px;
  color: #888;
}

.stat .value {
  font-size: 18px;
  color: #4fc3f7;
  font-weight: bold;
}

.node-types h5 {
  margin: 0 0 8px 0;
  color: #aaa;
  font-size: 12px;
}

.node-type-item {
  display: flex;
  justify-content: space-between;
  padding: 4px 0;
  border-bottom: 1px solid #333;
}

.node-type-item:last-child {
  border-bottom: none;
}

.node-type-item .type {
  color: #ccc;
  font-size: 12px;
}

.node-type-item .count {
  color: #4fc3f7;
  font-size: 12px;
  font-weight: bold;
}

.validation-errors {
  background: #3a2a2a;
  border: 1px solid #663333;
  border-radius: 4px;
  padding: 12px;
  margin-top: 16px;
}

.validation-errors h4 {
  margin: 0 0 8px 0;
  color: #ff8888;
  font-size: 13px;
}

.validation-errors ul {
  margin: 0;
  padding-left: 20px;
}

.validation-errors li {
  color: #ffaaaa;
  font-size: 12px;
  margin-bottom: 4px;
}

.error-message {
  display: flex;
  align-items: center;
  gap: 8px;
  background: #3a2a2a;
  border: 1px solid #663333;
  border-radius: 4px;
  padding: 12px;
  margin-top: 16px;
  color: #ff8888;
  font-size: 13px;
}

.error-icon {
  font-size: 16px;
}

.success-message {
  display: flex;
  align-items: center;
  gap: 8px;
  background: #2a3a2a;
  border: 1px solid #336633;
  border-radius: 4px;
  padding: 12px;
  margin-top: 16px;
  color: #88ff88;
  font-size: 13px;
}

.success-icon {
  font-size: 16px;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 16px 20px;
  border-top: 1px solid #333;
}

.cancel-btn,
.import-btn {
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}

.cancel-btn {
  background: #2a2a2a;
  border: 1px solid #444;
  color: #aaa;
}

.cancel-btn:hover:not(:disabled) {
  background: #333;
  color: #fff;
}

.import-btn {
  background: #2196f3;
  border: 1px solid #1976d2;
  color: #fff;
}

.import-btn:hover:not(:disabled) {
  background: #1976d2;
}

.import-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>

