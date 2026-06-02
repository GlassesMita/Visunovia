<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useUIStore } from '@/stores/useUIStore'
import { getCurrentProject, updateProjectSettings } from '@/api/projectApi'
import type { CurrentProjectInfo } from '@/api/projectApi'

const { t } = useLocalization()
const uiStore = useUIStore()

const projectName = ref('')
const companyName = ref('')
const version = ref('')
const versionCode = ref('')
const projectPath = ref('')
const subDirectories = ref<string[]>([])
const isLoading = ref(false)
const isSaving = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)
const hasProject = ref(false)

async function loadProjectInfo() {
  isLoading.value = true
  error.value = null
  try {
    const result = await getCurrentProject()
    if (result.data) {
      hasProject.value = true
      projectName.value = result.data.projectName
      companyName.value = result.data.companyName
      version.value = result.data.version
      versionCode.value = result.data.versionCode
      projectPath.value = result.data.projectPath
      subDirectories.value = result.data.subDirectories
    } else {
      hasProject.value = false
    }
  } catch (e: any) {
    error.value = e?.message ?? t('Project.LoadFailed', '加载项目信息失败').value
    hasProject.value = false
  } finally {
    isLoading.value = false
  }
}

async function handleSave() {
  if (!hasProject.value) return
  isSaving.value = true
  error.value = null
  success.value = null
  try {
    await updateProjectSettings({
      projectName: projectName.value.trim(),
      companyName: companyName.value.trim(),
      version: version.value.trim(),
      versionCode: versionCode.value.trim(),
    })
    success.value = t('Project.SettingsSaved', '项目设置已保存').value
  } catch (e: any) {
    error.value = e?.response?.data?.error ?? e?.message ?? t('Project.SaveFailed', '保存失败').value
  } finally {
    isSaving.value = false
  }
}

function handleClose() {
  uiStore.closeProjectPreferences()
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    handleClose()
  }
}

watch(() => uiStore.showProjectPreferences, (visible) => {
  if (visible) {
    projectName.value = ''
    companyName.value = ''
    version.value = ''
    versionCode.value = ''
    projectPath.value = ''
    subDirectories.value = []
    error.value = null
    success.value = null
    hasProject.value = false
    loadProjectInfo()
  }
})
</script>

<template>
  <Teleport to="body">
    <Transition name="ppm-modal">
      <div v-if="uiStore.showProjectPreferences" class="ppm-overlay" @click.self="handleClose">
        <div class="ppm-window" @keydown="handleKeydown" tabindex="-1">
          <!-- Title Bar -->
          <div class="ppm-titlebar">
            <div class="ppm-titlebar-icon">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <path d="M8 1L10.5 5.5L15 6.5L11.5 10L12.5 15L8 12.5L3.5 15L4.5 10L1 6.5L5.5 5.5L8 1Z" fill="#FFC107" stroke="#E0A800" stroke-width="0.5"/>
              </svg>
            </div>
            <span class="ppm-titlebar-text">{{ t('Project.ProjectPreferences', '项目首选项') }}</span>
            <div class="ppm-titlebar-buttons">
              <button class="ppm-titlebar-btn ppm-titlebar-close" @click="handleClose" :title="t('Common.Close', '关闭')">
                <svg width="10" height="10" viewBox="0 0 10 10">
                  <path d="M1 1L9 9M9 1L1 9" stroke="currentColor" stroke-width="1.2" stroke-linecap="round"/>
                </svg>
              </button>
            </div>
          </div>

          <!-- Content -->
          <div class="ppm-content">
            <!-- Loading -->
            <div v-if="isLoading" class="ppm-loading">
              <span class="ppm-spinner"></span>
              <span>{{ t('Status.Loading', '加载中...') }}</span>
            </div>

            <!-- No Project -->
            <div v-else-if="!hasProject" class="ppm-no-project">
              <svg width="48" height="48" viewBox="0 0 16 16" fill="none">
                <path d="M1 3.5C1 2.67 1.67 2 2.5 2H6L7.5 3.5H13.5C14.33 3.5 15 4.17 15 5V12.5C15 13.33 14.33 14 13.5 14H2.5C1.67 14 1 13.33 1 12.5V3.5Z" stroke="#666" stroke-width="1" fill="none"/>
              </svg>
              <p>{{ t('Project.NoProjectOpen', '当前没有打开的项目') }}</p>
            </div>

            <!-- Project Info Form -->
            <template v-else>
              <!-- Project Name -->
              <div class="ppm-field">
                <label class="ppm-label" for="ppm-project-name">{{ t('Project.ProjectName', '项目名称') }}</label>
                <input
                  id="ppm-project-name"
                  v-model="projectName"
                  type="text"
                  class="ppm-input"
                  :placeholder="t('Project.ProjectNamePlaceholder', '输入项目名称')"
                  :disabled="isSaving"
                />
              </div>

              <!-- Company Name -->
              <div class="ppm-field">
                <label class="ppm-label" for="ppm-company-name">{{ t('Project.CompanyName', '公司名称') }}</label>
                <input
                  id="ppm-company-name"
                  v-model="companyName"
                  type="text"
                  class="ppm-input"
                  :placeholder="t('Project.CompanyNamePlaceholder', '输入公司名称')"
                  :disabled="isSaving"
                />
              </div>

              <!-- Version & VersionCode Row -->
              <div class="ppm-field-row">
                <div class="ppm-field ppm-field-half">
                  <label class="ppm-label" for="ppm-version">{{ t('Project.Version', '版本') }}</label>
                  <input
                    id="ppm-version"
                    v-model="version"
                    type="text"
                    class="ppm-input"
                    placeholder="1.0"
                    :disabled="isSaving"
                  />
                </div>
                <div class="ppm-field ppm-field-half">
                  <label class="ppm-label" for="ppm-version-code">{{ t('Project.VersionCode', '版本号') }}</label>
                  <input
                    id="ppm-version-code"
                    v-model="versionCode"
                    type="text"
                    class="ppm-input"
                    placeholder="1"
                    :disabled="isSaving"
                  />
                </div>
              </div>

              <!-- Project Path (Read-only) -->
              <div class="ppm-field">
                <label class="ppm-label">{{ t('Project.ProjectPath', '项目路径') }}</label>
                <div class="ppm-readonly-path">{{ projectPath }}</div>
              </div>

              <!-- Sub Directories -->
              <div v-if="subDirectories.length > 0" class="ppm-field">
                <label class="ppm-label">{{ t('Project.SubDirectories', '子目录') }}</label>
                <div class="ppm-subdirs">
                  <span v-for="dir in subDirectories" :key="dir" class="ppm-subdir-tag">{{ dir }}</span>
                </div>
              </div>
            </template>

            <!-- Error Message -->
            <div v-if="error" class="ppm-error">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <circle cx="8" cy="8" r="7" stroke="#f44336" stroke-width="1.5"/>
                <path d="M8 4V9M8 11V12" stroke="#f44336" stroke-width="1.5" stroke-linecap="round"/>
              </svg>
              <span>{{ error }}</span>
            </div>

            <!-- Success Message -->
            <div v-if="success" class="ppm-success">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
                <circle cx="8" cy="8" r="7" stroke="#4caf50" stroke-width="1.5"/>
                <path d="M5 8L7 10L11 6" stroke="#4caf50" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
              <span>{{ success }}</span>
            </div>
          </div>

          <!-- Footer -->
          <div class="ppm-footer">
            <button class="ppm-btn-cancel" @click="handleClose" :disabled="isSaving">{{ t('Common.Close', '关闭') }}</button>
            <button v-if="hasProject" class="ppm-btn-save" @click="handleSave" :disabled="isSaving">
              <span v-if="isSaving" class="ppm-spinner"></span>
              <span v-else>{{ t('Common.Save', '保存') }}</span>
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.ppm-overlay {
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

.ppm-window {
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

.ppm-titlebar {
  height: 32px;
  background: #2d2d2d;
  display: flex;
  align-items: center;
  padding: 0 8px;
  flex-shrink: 0;
}

.ppm-titlebar-icon {
  width: 16px;
  height: 16px;
  margin-right: 8px;
  flex-shrink: 0;
}

.ppm-titlebar-text {
  flex: 1;
  font-size: 12px;
  color: #cccccc;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ppm-titlebar-buttons {
  display: flex;
  flex-shrink: 0;
}

.ppm-titlebar-btn {
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

.ppm-titlebar-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.ppm-titlebar-close:hover {
  background: #e81123;
  color: #fff;
}

.ppm-content {
  padding: 20px 24px;
  display: flex;
  flex-direction: column;
  gap: 14px;
  flex: 1;
  overflow-y: auto;
}

.ppm-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.ppm-field-row {
  display: flex;
  gap: 12px;
}

.ppm-field-half {
  flex: 1;
}

.ppm-label {
  font-size: 12px;
  color: #999999;
  font-weight: 500;
}

.ppm-input {
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

.ppm-input:focus {
  border-color: #0078d4;
}

.ppm-input:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ppm-input::placeholder {
  color: #666666;
}

.ppm-readonly-path {
  padding: 8px 10px;
  background: #252526;
  border: 1px solid #3c3c3c;
  border-radius: 4px;
  color: #999999;
  font-size: 12px;
  font-family: 'Consolas', 'Courier New', monospace;
  word-break: break-all;
}

.ppm-subdirs {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.ppm-subdir-tag {
  padding: 4px 10px;
  background: #2d2d2d;
  border: 1px solid #3c3c3c;
  border-radius: 4px;
  color: #cccccc;
  font-size: 12px;
}

.ppm-loading {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 20px;
  color: #999999;
}

.ppm-no-project {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 30px 20px;
  color: #666666;
}

.ppm-no-project p {
  margin: 0;
  font-size: 13px;
}

.ppm-error {
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

.ppm-success {
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

.ppm-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 24px;
  background: #252526;
  border-top: 1px solid #3c3c3c;
  flex-shrink: 0;
}

.ppm-btn-cancel {
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

.ppm-btn-cancel:hover:not(:disabled) {
  background: #4a4a4a;
}

.ppm-btn-cancel:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ppm-btn-save {
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

.ppm-btn-save:hover:not(:disabled) {
  background: #1a8ae8;
  border-color: #1a8ae8;
}

.ppm-btn-save:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ppm-spinner {
  display: inline-block;
  width: 14px;
  height: 14px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: #ffffff;
  border-radius: 50%;
  animation: ppm-spin 0.6s linear infinite;
}

@keyframes ppm-spin {
  to { transform: rotate(360deg); }
}

/* Transition */
.ppm-modal-enter-active,
.ppm-modal-leave-active {
  transition: opacity 0.2s ease;
}

.ppm-modal-enter-active .ppm-window,
.ppm-modal-leave-active .ppm-window {
  transition: transform 0.2s ease, opacity 0.2s ease;
}

.ppm-modal-enter-from,
.ppm-modal-leave-to {
  opacity: 0;
}

.ppm-modal-enter-from .ppm-window,
.ppm-modal-leave-to .ppm-window {
  transform: scale(0.95);
  opacity: 0;
}
</style>