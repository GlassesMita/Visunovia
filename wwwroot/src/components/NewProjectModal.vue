<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { createProject } from '@/api/projectApi'
import { settingsApi } from '@/api'
import type { FolderNode } from '@/api/projectApi'
import FileExplorer from '@/components/FileExplorer.vue'
import FolderTreeNode from '@/components/FolderTreeNode.vue'

const { t } = useLocalization()
const uiStore = useUIStore()

const projectName = ref('')
const projectPath = ref('')
const companyName = ref('')
const version = ref('1.0')
const versionCode = ref('1')
const isCreating = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const showFolderBrowser = ref(false)
const projectNameInputRef = ref<HTMLInputElement | null>(null)
const createdFolderTree = ref<FolderNode | null>(null)
const showStructurePreview = ref(false)
const defaultCompanyName = 'Abydos Highschool'
const defaultProductName = 'Anubis'
const projectNamePlaceholder = ref('e.g. ' + defaultProductName)
const companyNamePlaceholder = ref('e.g. ' + defaultCompanyName)

const canCreate = computed(() => {
  return projectName.value.trim().length > 0 && projectPath.value.trim().length > 0 && !isCreating.value
})

const fullProjectPath = computed(() => {
  if (!projectPath.value || !projectName.value.trim()) return ''
  return `${projectPath.value}\\${projectName.value.trim()}`
})

function openFolderBrowser() {
  showFolderBrowser.value = true
}

function handleFolderSelect(path: string, isDir: boolean) {
  showFolderBrowser.value = false
  if (path && isDir) {
    projectPath.value = path
  }
}

function handleFolderBrowserClose() {
  showFolderBrowser.value = false
}

async function handleCreate() {
  if (!canCreate.value) return

  error.value = null
  success.value = null
  isCreating.value = true
  createdFolderTree.value = null
  showStructurePreview.value = false

  try {
    const result = await createProject(
      projectName.value.trim(),
      projectPath.value.trim(),
      companyName.value.trim(),
      version.value.trim(),
      versionCode.value.trim()
    )
    success.value = t('Project.CreateSuccess', `项目 "${result.name}" 创建成功！`).value
    createdFolderTree.value = result.folderTree
    showStructurePreview.value = true

    // 关闭模态框并打开 start.lor
    uiStore.closeNewProjectModal()
    if (result.projectPath) {
      const startLorPath = `${result.projectPath}\\Scripts\\Main\\start.lor`
      uiStore.openFileByPath(startLorPath)
    }
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? e?.message ?? t('Project.CreateFailed', '创建项目失败').value
  } finally {
    isCreating.value = false
  }
}

function handleCancel() {
  uiStore.closeNewProjectModal()
  projectName.value = ''
  projectPath.value = ''
  companyName.value = ''
  version.value = '1.0'
  versionCode.value = '1'
  error.value = null
  success.value = null
  createdFolderTree.value = null
  showStructurePreview.value = false
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    handleCancel()
  } else if (event.key === 'Enter' && canCreate.value) {
    handleCreate()
  }
}

// Load Placeholder config from settings and pre-fill companyName
async function loadPlaceholderFromConfig() {
  try {
    const response = await settingsApi.get()
    const settings = response?.data?.settings
    if (settings && typeof settings === 'object') {
      // Pre-fill company name
      const savedCompanyName = settings['PlaceholderCompanyName']
      if (savedCompanyName && typeof savedCompanyName === 'string' && savedCompanyName.trim()) {
        companyName.value = savedCompanyName.trim()
      }
      // Set dynamic placeholder hints
      const company = (typeof savedCompanyName === 'string' && savedCompanyName.trim()) ? savedCompanyName.trim() : defaultCompanyName
      const product = (typeof settings['PlaceholderProductName'] === 'string' && settings['PlaceholderProductName'].trim()) ? settings['PlaceholderProductName'].trim() : defaultProductName
      projectNamePlaceholder.value = 'e.g. ' + product
      companyNamePlaceholder.value = 'e.g. ' + company
    }
  } catch (e) {
    // Silently ignore — placeholder is optional
  }
}

// When modal opens, reset fields then load placeholder
watch(() => uiStore.showNewProjectModal, async (visible) => {
  if (visible) {
    projectName.value = ''
    projectPath.value = ''
    companyName.value = ''
    version.value = '1.0'
    versionCode.value = '1'
    error.value = null
    success.value = null
    showFolderBrowser.value = false
    createdFolderTree.value = null
    showStructurePreview.value = false
    await loadPlaceholderFromConfig()
    nextTick(() => {
      projectNameInputRef.value?.focus()
    })
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition name="npm-modal">
      <div v-if="uiStore.showNewProjectModal" class="npm-overlay" @click.self="handleCancel">
        <div class="npm-window" @keydown="handleKeydown" tabindex="-1">
        <!-- Title Bar -->
        <div class="npm-titlebar">
          <div class="npm-titlebar-icon">
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
              <path d="M1 3.5C1 2.67 1.67 2 2.5 2H6L7.5 3.5H13.5C14.33 3.5 15 4.17 15 5V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V3.5Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.5"/>
              <path d="M1 6H15V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V6Z" fill="#FFD54F" stroke="#E0A800" stroke-width="0.5"/>
            </svg>
          </div>
          <span class="npm-titlebar-text">{{ t('Project.NewProject', '新建项目') }}</span>
          <div class="npm-titlebar-buttons">
            <button class="npm-titlebar-btn npm-titlebar-close" @click="handleCancel" title="关闭">
              <svg width="10" height="10" viewBox="0 0 10 10">
                <path d="M1 1L9 9M9 1L1 9" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
              </svg>
            </button>
          </div>
        </div>

        <!-- Content -->
        <div class="npm-content">
          <!-- Project Name -->
          <div class="npm-field">
            <label class="npm-label" for="npm-project-name">{{ t('Project.ProjectName', '项目名称') }}</label>
            <input
              id="npm-project-name"
              ref="projectNameInputRef"
              v-model="projectName"
              type="text"
              class="npm-input"
              :placeholder="projectNamePlaceholder"
              autocomplete="off"
              :disabled="isCreating"
            />
          </div>

          <!-- Company Name -->
          <div class="npm-field">
            <label class="npm-label" for="npm-company-name">{{ t('Project.CompanyName', '公司名称') }}</label>
            <input
              id="npm-company-name"
              v-model="companyName"
              type="text"
              class="npm-input"
              :placeholder="companyNamePlaceholder"
              autocomplete="off"
              :disabled="isCreating"
            />
          </div>

          <!-- Version & VersionCode Row -->
          <div class="npm-field-row">
            <div class="npm-field npm-field-half">
              <label class="npm-label" for="npm-version">{{ t('Project.Version', '版本') }}</label>
              <input
                id="npm-version"
                v-model="version"
                type="text"
                class="npm-input"
                placeholder="1.0"
                autocomplete="off"
                :disabled="isCreating"
              />
            </div>
            <div class="npm-field npm-field-half">
              <label class="npm-label" for="npm-version-code">{{ t('Project.VersionCode', '版本号') }}</label>
              <input
                id="npm-version-code"
                v-model="versionCode"
                type="text"
                class="npm-input"
                placeholder="1"
                autocomplete="off"
                :disabled="isCreating"
              />
            </div>
          </div>

          <!-- Project Location -->
          <div class="npm-field">
            <label class="npm-label">{{ t('Project.ProjectLocation', '项目位置') }}</label>
            <div class="npm-path-row">
              <input
                v-model="projectPath"
                type="text"
                class="npm-input npm-path-input"
                placeholder="Click the button on the right to select a folder"
                readonly
                :disabled="isCreating"
                @click="openFolderBrowser"
              />
              <button
                class="npm-browse-btn"
                @click="openFolderBrowser"
                :disabled="isCreating"
                :title="t('Project.BrowseFolder', '浏览文件夹')"
              >
                <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                  <path d="M1 4.5C1 3.67 1.67 3 2.5 3H6L7.5 4.5H13.5C14.33 4.5 15 5.17 15 6V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V4.5Z" stroke="currentColor" stroke-width="1.2"/>
                  <path d="M9 9L12 9" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
                </svg>
                {{ t('Project.Browse', '浏览...') }}
              </button>
            </div>
          </div>

          <!-- Full Path Preview -->
          <div v-if="fullProjectPath" class="npm-field">
            <label class="npm-label">{{ t('Project.FullPath', '完整路径') }}</label>
            <div class="npm-full-path">{{ fullProjectPath }}</div>
          </div>

          <!-- Error Message -->
          <div v-if="error" class="npm-error">
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
              <circle cx="8" cy="8" r="7" stroke="#f44336" stroke-width="1.5"/>
              <path d="M8 4V9M8 11V12" stroke="#f44336" stroke-width="1.5" stroke-linecap="round"/>
            </svg>
            <span>{{ error }}</span>
          </div>

          <!-- Success Message -->
          <div v-if="success" class="npm-success">
            <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
              <circle cx="8" cy="8" r="7" stroke="#4caf50" stroke-width="1.5"/>
              <path d="M5 8L7 10L11 6" stroke="#4caf50" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            <span>{{ success }}</span>
          </div>

          <!-- Folder Structure Preview -->
          <div v-if="showStructurePreview && createdFolderTree" class="npm-field">
            <label class="npm-label">{{ t('Project.ProjectStructure', '项目结构') }}</label>
            <div class="npm-tree">
              <FolderTreeNode :node="createdFolderTree" :depth="0" />
            </div>
          </div>
        </div>

        <!-- Footer -->
        <div class="npm-footer">
          <button class="npm-btn-cancel" @click="handleCancel" :disabled="isCreating">{{ t('Common.Cancel', '取消') }}</button>
          <button class="npm-btn-create" @click="handleCreate" :disabled="!canCreate">
            <span v-if="isCreating" class="npm-spinner"></span>
            <span v-else>{{ t('Project.Create', '创建项目') }}</span>
          </button>
        </div>
        </div>
      </div>
    </Transition>

    <!-- Folder Browser Modal -->
    <FileExplorer
      :visible="showFolderBrowser"
      :title="t('Project.SelectFolder', '选择项目文件夹')"
      :allow-select-directory="true"
      @close="handleFolderBrowserClose"
      @select="handleFolderSelect"
    />
  </Teleport>
</template>

<style scoped>
/* ========== Overlay ========== */
.npm-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.6);
  z-index: 9998;
  display: flex;
  justify-content: center;
  align-items: center;
  backdrop-filter: blur(2px);
}

/* ========== Window ========== */
.npm-window {
  width: 520px;
  min-height: 200px;
  background: #1e1e1e;
  border: 1px solid #3c3c3c;
  border-radius: 8px 8px 0 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.6), 0 0 0 1px rgba(255, 255, 255, 0.05);
  font-family: 'Segoe UI', 'Microsoft YaHei', system-ui, -apple-system, sans-serif;
  font-size: 13px;
  color: #cccccc;
  outline: none;
  user-select: none;
  -webkit-user-select: none;
}

/* ========== Title Bar ========== */
.npm-titlebar {
  height: 32px;
  background: #2d2d2d;
  display: flex;
  align-items: center;
  padding: 0 8px;
  flex-shrink: 0;
}

.npm-titlebar-icon {
  width: 16px;
  height: 16px;
  margin-right: 8px;
  flex-shrink: 0;
}

.npm-titlebar-text {
  flex: 1;
  font-size: 12px;
  color: #cccccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.npm-titlebar-buttons {
  display: flex;
  flex-shrink: 0;
}

.npm-titlebar-btn {
  width: 46px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: none;
  border: none;
  color: #cccccc;
  cursor: pointer;
  transition: background 0.1s;
}

.npm-titlebar-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.npm-titlebar-close:hover {
  background: #e81123;
  color: #fff;
}

/* ========== Content ========== */
.npm-content {
  padding: 20px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex: 1;
}

.npm-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.npm-field-row {
  display: flex;
  gap: 12px;
}

.npm-field-half {
  flex: 1;
}

.npm-label {
  font-size: 12px;
  color: #999999;
  font-weight: 500;
}

.npm-input {
  height: 32px;
  padding: 0 10px;
  background: #2d2d2d;
  border: 1px solid #3c3c3c;
  border-radius: 4px;
  color: #cccccc;
  font-size: 13px;
  font-family: inherit;
  outline: none;
  transition: border-color 0.15s;
  box-sizing: border-box;
}

.npm-input:focus {
  border-color: #0078d4;
}

.npm-input:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.npm-input::placeholder {
  color: #666666;
}

.npm-path-row {
  display: flex;
  gap: 8px;
}

.npm-path-input {
  flex: 1;
  cursor: pointer;
}

.npm-path-input:hover {
  border-color: #555555;
}

.npm-browse-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 14px;
  background: #2d2d2d;
  border: 1px solid #3c3c3c;
  border-radius: 4px;
  color: #cccccc;
  font-size: 12px;
  font-family: inherit;
  cursor: pointer;
  white-space: nowrap;
  transition: all 0.15s;
  flex-shrink: 0;
}

.npm-browse-btn:hover:not(:disabled) {
  background: #3c3c3c;
  border-color: #555555;
}

.npm-browse-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.npm-full-path {
  padding: 8px 10px;
  background: #252526;
  border: 1px solid #3c3c3c;
  border-radius: 4px;
  color: #999999;
  font-size: 12px;
  font-family: 'Consolas', 'Courier New', monospace;
  word-break: break-all;
}

/* ========== Error / Success ========== */
.npm-error {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: rgba(244, 67, 54, 0.1);
  border: 1px solid rgba(244, 67, 54, 0.3);
  border-radius: 4px;
  color: #f44336;
  font-size: 12px;
}

.npm-success {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: rgba(76, 175, 80, 0.1);
  border: 1px solid rgba(76, 175, 80, 0.3);
  border-radius: 4px;
  color: #4caf50;
  font-size: 12px;
}

/* ========== Footer ========== */
.npm-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 24px;
  background: #252526;
  border-top: 1px solid #3c3c3c;
  flex-shrink: 0;
}

.npm-btn-cancel {
  height: 32px;
  padding: 0 20px;
  background: #3c3c3c;
  border: 1px solid #555555;
  border-radius: 4px;
  color: #cccccc;
  font-size: 13px;
  font-family: inherit;
  cursor: pointer;
  transition: all 0.15s;
}

.npm-btn-cancel:hover:not(:disabled) {
  background: #4a4a4a;
}

.npm-btn-cancel:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.npm-btn-create {
  height: 32px;
  padding: 0 20px;
  background: #0078d4;
  border: 1px solid #0078d4;
  border-radius: 4px;
  color: #ffffff;
  font-size: 13px;
  font-family: inherit;
  cursor: pointer;
  transition: all 0.15s;
  display: flex;
  align-items: center;
  gap: 6px;
}

.npm-btn-create:hover:not(:disabled) {
  background: #1a8ae8;
  border-color: #1a8ae8;
}

.npm-btn-create:active:not(:disabled) {
  background: #006abc;
}

.npm-btn-create:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* ========== Spinner ========== */
.npm-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: #ffffff;
  border-radius: 50%;
  animation: npm-spin 0.6s linear infinite;
}

@keyframes npm-spin {
  to { transform: rotate(360deg); }
}

/* ========== Folder Tree Preview ========== */
.npm-tree {
  max-height: 240px;
  overflow-y: auto;
  padding: 8px 10px;
  background: #1a1a1a;
  border: 1px solid #3c3c3c;
  border-radius: 4px;
  font-family: 'Consolas', 'Courier New', monospace;
  font-size: 12px;
}

.npm-tree-node {
  display: flex;
  flex-direction: column;
}

.npm-tree-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 2px 0;
  color: #cccccc;
  white-space: nowrap;
}

.npm-tree-icon {
  flex-shrink: 0;
  width: 14px;
  height: 14px;
}

.npm-tree-name {
  color: #cccccc;
  overflow: hidden;
  text-overflow: ellipsis;
}

.npm-tree-size {
  color: #666666;
  font-size: 10px;
  margin-left: auto;
  flex-shrink: 0;
  padding-left: 12px;
}

.npm-tree::-webkit-scrollbar {
  width: 6px;
}

.npm-tree::-webkit-scrollbar-track {
  background: transparent;
}

.npm-tree::-webkit-scrollbar-thumb {
  background: #555;
  border-radius: 3px;
}

/* ========== Modal Transition (fade + scale) ========== */
.npm-modal-enter-active,
.npm-modal-leave-active {
  transition: opacity 0.2s ease;
}
.npm-modal-enter-active .npm-window,
.npm-modal-leave-active .npm-window {
  transition: transform 0.2s ease;
}
.npm-modal-enter-from,
.npm-modal-leave-to {
  opacity: 0;
}
.npm-modal-enter-from .npm-window,
.npm-modal-leave-to .npm-window {
  transform: scale(0.92);
}
</style>
