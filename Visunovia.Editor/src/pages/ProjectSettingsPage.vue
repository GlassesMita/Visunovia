<template>
  <div class="preferences-page">
    <div class="preferences-container">
      <h1>{{ t('projectSettings.title') || '项目设置' }}</h1>

      <!-- 项目信息设置 -->
      <div class="settings-section">
        <h2>{{ t('projectSettings.project') || '项目信息' }}</h2>

        <!-- 项目名称 -->
        <div class="setting-item">
          <label>{{ t('projectSettings.projectName') || '项目名称' }}</label>
          <input
            type="text"
            v-model="settings.projectName"
            :placeholder="t('projectSettings.projectNamePlaceholder') || 'Untitled Project'"
          />
        </div>
      </div>

      <!-- 视频设置 -->
      <div class="settings-section">
        <h2>{{ t('projectSettings.video') || '视频设置' }}</h2>

        <!-- 分辨率 -->
        <div class="setting-item resolution-row">
          <label>{{ t('projectSettings.resolution') || '默认分辨率' }}</label>
          <div class="resolution-inputs">
            <input
              type="number"
              v-model="settings.resolutionWidth"
              min="640"
              max="7680"
              step="1"
              class="resolution-input"
            />
            <span class="resolution-separator">×</span>
            <input
              type="number"
              v-model="settings.resolutionHeight"
              min="360"
              max="4320"
              step="1"
              class="resolution-input"
            />
          </div>
        </div>
      </div>

      <!-- 音频设置 -->
      <div class="settings-section">
        <h2>{{ t('projectSettings.audio') || '音频设置' }}</h2>

        <!-- BGM 音量 -->
        <div class="setting-item">
          <label>{{ t('projectSettings.bgmVolume') || '默认 BGM 音量' }}</label>
          <input
            type="number"
            v-model="settings.bgmVolume"
            min="0"
            max="100"
            step="1"
          />
        </div>

        <!-- 语音音量 -->
        <div class="setting-item">
          <label>{{ t('projectSettings.voiceVolume') || '默认语音音量' }}</label>
          <input
            type="number"
            v-model="settings.voiceVolume"
            min="0"
            max="100"
            step="1"
          />
        </div>

        <!-- BGM 循环 -->
        <div class="setting-item">
          <label>{{ t('projectSettings.bgmLoop') || '默认 BGM 循环' }}</label>
          <input type="checkbox" v-model="settings.bgmLoop" />
        </div>
      </div>

      <!-- 通用设置 -->
      <div class="settings-section">
        <h2>{{ t('projectSettings.general') || '通用设置' }}</h2>

        <!-- 默认语言 -->
        <div class="setting-item">
          <label>{{ t('projectSettings.language') || '默认语言' }}</label>
          <select v-model="settings.language">
            <option value="zh">{{ t('projectSettings.langZh') || '中文' }}</option>
            <option value="en">{{ t('projectSettings.langEn') || 'English' }}</option>
          </select>
        </div>

        <!-- 默认主题 -->
        <div class="setting-item">
          <label>{{ t('projectSettings.theme') || '默认主题' }}</label>
          <select v-model="settings.theme">
            <option value="dark">{{ t('projectSettings.dark') || '深色' }}</option>
            <option value="light">{{ t('projectSettings.light') || '浅色' }}</option>
          </select>
        </div>
      </div>

      <!-- 操作按钮区域 -->
      <div class="settings-actions">
        <button class="btn-primary" @click="saveSettings" :disabled="isSaving">
          <span v-if="isSaving" class="loading-indicator-inline"></span>
          {{ isSaving ? (t('projectSettings.saving') || '保存中...') : (t('common.save') || '保存') }}
        </button>
      </div>

      <!-- 保存状态消息 -->
      <div v-if="saveMessage" class="save-message" :class="{ error: saveError }">
        {{ saveMessage }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { getCurrentProject, updateProjectSettings } from '@/api/projectApi'

const { t } = useLocalization()

// 默认项目设置
const defaultSettings = {
  projectName: '',
  resolutionWidth: 1920,
  resolutionHeight: 1080,
  bgmVolume: 80,
  voiceVolume: 100,
  bgmLoop: true,
  language: 'zh' as const,
  theme: 'dark' as const,
}

// 使用 reactive 统一管理所有设置项
const settings = reactive({
  projectName: defaultSettings.projectName,
  resolutionWidth: defaultSettings.resolutionWidth,
  resolutionHeight: defaultSettings.resolutionHeight,
  bgmVolume: defaultSettings.bgmVolume,
  voiceVolume: defaultSettings.voiceVolume,
  bgmLoop: defaultSettings.bgmLoop,
  language: defaultSettings.language as 'zh' | 'en',
  theme: defaultSettings.theme as 'dark' | 'light',
})

const saveMessage = ref('')
const saveError = ref(false)
const isSaving = ref(false)

onMounted(() => {
  loadSettings()
})

/**
 * 从当前打开项目加载设置。
 * Project.tlor 是项目设置的唯一可信来源，避免 localStorage 旧值导致显示 Untitled Project。
 */
async function loadSettings() {
  try {
    const result = await getCurrentProject()
    settings.projectName = result.data?.projectName || defaultSettings.projectName
  } catch (error) {
    console.error('[ProjectSettingsPage] Failed to load current project settings:', error)
    saveMessage.value = t('projectSettings.loadFailed') || '项目设置加载失败'
    saveError.value = true
  }
}

/**
 * 保存设置到 localStorage
 * 优先本地存储保证用户体验
 */
async function saveSettings() {
  isSaving.value = true
  saveMessage.value = ''

  try {
    await updateProjectSettings({ projectName: settings.projectName.trim() })

    saveMessage.value = t('projectSettings.savedSuccess') || '设置已保存'
    saveError.value = false

    console.log('[ProjectSettingsPage] Settings saved to Project.tlor')
  } catch (error) {
    console.error('[ProjectSettingsPage] Failed to save settings:', error)
    saveMessage.value = t('projectSettings.saveFailed') || '保存失败'
    saveError.value = true
  } finally {
    isSaving.value = false
  }

  // 3 秒后自动清除提示消息
  setTimeout(() => {
    saveMessage.value = ''
  }, 3000)
}

</script>

<style scoped>
.preferences-page {
  display: flex;
  justify-content: center;
  padding: 40px;
  background: var(--md-sys-color-background);
  min-height: 100vh;
  box-sizing: border-box;
}

.preferences-container {
  width: 100%;
  max-width: 700px;
  background: var(--md-sys-color-surface-container);
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: var(--md-sys-shape-corner-large);
  padding: 24px;
  box-sizing: border-box;
}

/* 页面标题样式 */
h1 {
  margin: 0 0 24px;
  font-size: 24px;
  font-weight: 600;
  color: var(--md-sys-color-on-surface);
}

/* 设置分组容器 */
.settings-section {
  margin-bottom: 32px;
  padding-bottom: 24px;
  border-bottom: 1px solid var(--md-sys-color-outline-variant);
}

.settings-section:last-of-type {
  border-bottom: none;
  margin-bottom: 0;
}

/* 分组标题样式 */
.settings-section h2 {
  margin: 0 0 16px;
  font-size: 16px;
  font-weight: 600;
  color: var(--md-sys-color-on-surface-variant);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

/* 单个设置项布局 */
.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  gap: 16px;
}

.setting-item:last-child {
  margin-bottom: 0;
}

/* 设置项标签 */
.setting-item label {
  color: var(--md-sys-color-on-surface);
  font-size: 14px;
  flex-shrink: 0;
  min-width: 160px;
}

/* 输入框统一样式（文本、数字、下拉框） */
.setting-item input[type="text"],
.setting-item input[type="number"],
.setting-item select {
  padding: 6px 12px;
  background: var(--md-sys-color-surface-container-high);
  border: 1px solid var(--md-sys-color-outline);
  border-radius: var(--md-sys-shape-corner-small);
  color: var(--md-sys-color-on-surface);
  font-size: 14px;
  min-width: 180px;
  max-width: 250px;
  transition: border-color 0.15s ease;
  box-sizing: border-box;
}

.setting-item input[type="text"]:focus,
.setting-item input[type="number"]:focus,
.setting-item select:focus {
  outline: none;
  border-color: var(--md-sys-color-primary);
}

/* 数字输入框隐藏微调按钮（保持视觉一致性） */
.setting-item input[type="number"]::-webkit-inner-spin-button,
.setting-item input[type="number"]::-webkit-outer-spin-button {
  opacity: 0.5;
}

/* 下拉框箭头颜色修复 */
.setting-item select option {
  background: var(--md-sys-color-surface-container-high);
  color: var(--md-sys-color-on-surface);
}

/* 复选框样式 */
.setting-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: var(--md-sys-color-primary);
}

/* 分辨率行布局 */
.resolution-row {
  align-items: center;
}

.resolution-inputs {
  display: flex;
  align-items: center;
  gap: 8px;
}

.resolution-input {
  width: 100px;
}

.resolution-separator {
  color: var(--md-sys-color-on-surface);
  font-size: 14px;
}

/* 操作按钮区域 */
.settings-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid var(--md-sys-color-outline-variant);
}

/* 主要按钮样式 */
.btn-primary {
  padding: 8px 24px;
  background: var(--md-sys-color-primary);
  border: none;
  border-radius: 999px;
  color: var(--md-sys-color-on-primary);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s ease;
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn-primary:hover:not(:disabled) {
  box-shadow: var(--md-sys-elevation-1);
}

.btn-primary:active:not(:disabled) {
  background: var(--md-sys-color-primary-container);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* 保存状态消息 */
.save-message {
  margin-top: 16px;
  padding: 12px;
  background: #1a472e;
  border-radius: 4px;
  color: #89d185;
  font-size: 14px;
  text-align: center;
  animation: fadeIn 0.2s ease;
}

.save-message.error {
  background: #5a1d1d;
  color: #f48771;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(-4px); }
  to { opacity: 1; transform: translateY(0); }
}

/* 内联加载指示器 */
.loading-indicator-inline {
  width: 14px;
  height: 14px;
  border: 2px solid #ffffff;
  border-top-color: transparent;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
