<template>
  <div class="preferences-page">
    <div class="preferences-container">
      <h1>{{ t('settings.title') }}</h1>
      
      <!-- 通用设置 -->
      <div class="settings-section">
        <h2>{{ t('settings.general') }}</h2>
        
        <!-- 语言选择（核心功能） -->
        <div class="setting-item">
          <label>{{ t('settings.language') }}</label>
          <select 
            v-model="settings.language" 
            @change="onLanguageChange"
            :disabled="localizationStore.isLoading"
          >
            <option value="zh">中文</option>
            <option value="en">English</option>
          </select>
          <span v-if="localizationStore.isLoading" class="loading-indicator">
            {{ t('app.loading') }}...
          </span>
        </div>
        
        <!-- 主题选择 -->
        <div class="setting-item">
          <label>{{ t('settings.theme') }}</label>
          <select v-model="settings.theme" @change="onThemeChange">
            <option value="dark">{{ t('settings.dark') }}</option>
            <option value="light">{{ t('settings.light') }}</option>
          </select>
        </div>

        <!-- 自动保存开关 -->
        <div class="setting-item">
          <label>{{ t('settings.autoSave') }}</label>
          <input type="checkbox" v-model="settings.autoSave" />
        </div>

        <!-- 自动保存间隔（仅当自动保存开启时显示） -->
        <div class="setting-item" v-if="settings.autoSave">
          <label>{{ t('settings.autoSaveInterval') }}</label>
          <input 
            type="number" 
            v-model="settings.autoSaveInterval" 
            min="10" 
            max="300" 
            step="10" 
          />
        </div>
      </div>
      
      <!-- 编辑器设置 -->
      <div class="settings-section">
        <h2>{{ t('settings.editor') }}</h2>
        
        <!-- 网格大小 -->
        <div class="setting-item">
          <label>{{ t('settings.gridSize') }}</label>
          <input 
            type="number" 
            v-model="settings.gridSize" 
            min="10" 
            max="50" 
            step="5" 
          />
        </div>
        
        <!-- 显示网格 -->
        <div class="setting-item">
          <label>{{ t('settings.showGrid') }}</label>
          <input type="checkbox" v-model="settings.showGrid" />
        </div>
        
        <!-- 启用吸附 -->
        <div class="setting-item">
          <label>{{ t('settings.snapEnabled') }}</label>
          <input type="checkbox" v-model="settings.snapToGrid" />
        </div>

        <!-- 默认缩放级别 -->
        <div class="setting-item">
          <label>{{ t('settings.defaultZoom') }}</label>
          <select v-model="settings.defaultZoom">
            <option value="0.5">{{ t('settings.zoom50') }}</option>
            <option value="0.75">{{ t('settings.zoom75') }}</option>
            <option value="1">{{ t('settings.zoom100') }}</option>
            <option value="1.25">{{ t('settings.zoom125') }}</option>
            <option value="1.5">{{ t('settings.zoom150') }}</option>
          </select>
        </div>

        <!-- 默认节点大小 -->
        <div class="setting-item">
          <label>{{ t('settings.defaultNodeSize') }}</label>
          <input 
            type="number" 
            v-model="settings.defaultNodeSize" 
            min="100" 
            max="400" 
            step="50" 
          />
        </div>
      </div>

      <!-- 预览设置 -->
      <div class="settings-section">
        <h2>{{ t('settings.preview') }}</h2>
        
        <!-- 预览宽度 -->
        <div class="setting-item">
          <label>{{ t('settings.previewWidth') }}</label>
          <input 
            type="number" 
            v-model="settings.previewWidth" 
            min="640" 
            max="3840" 
            step="320" 
          />
        </div>

        <!-- 预览高度 -->
        <div class="setting-item">
          <label>{{ t('settings.previewHeight') }}</label>
          <input 
            type="number" 
            v-model="settings.previewHeight" 
            min="360" 
            max="2160" 
            step="180" 
          />
        </div>

        <!-- 全屏预览 -->
        <div class="setting-item">
          <label>{{ t('settings.fullscreenPreview') }}</label>
          <input type="checkbox" v-model="settings.fullscreenPreview" />
        </div>
      </div>

      <!-- 网络设置（开发用） -->
      <div class="settings-section">
        <h2>{{ t('settings.network') }}</h2>
        
        <!-- API 地址 -->
        <div class="setting-item">
          <label>{{ t('settings.apiBaseUrl') }}</label>
          <input 
            type="text" 
            v-model="settings.apiBaseUrl" 
            placeholder="/api" 
          />
        </div>

        <!-- 请求超时 -->
        <div class="setting-item">
          <label>{{ t('settings.requestTimeout') }}</label>
          <input 
            type="number" 
            v-model="settings.requestTimeout" 
            min="5000" 
            max="120000" 
            step="5000" 
          />
        </div>
      </div>
      
      <!-- 操作按钮区域 -->
      <div class="settings-actions">
        <button class="btn-primary" @click="saveSettings">
          {{ t('common.save') }}
        </button>
        <button class="btn-secondary" @click="resetSettings">
          {{ t('common.cancel') }}
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
import { useI18n } from 'vue-i18n'
import { useLocalization } from '@/composables/useLocalization'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { settingsApi } from '@/api'

const { t, changeLanguage } = useLocalization()
const { locale } = useI18n()
const localizationStore = useLocalizationStore()

// 设置存储键名
const SETTINGS_STORAGE_KEY = 'visunovia-settings'

// 使用 reactive 统一管理所有设置项
const settings = reactive({
  language: 'zh',
  theme: 'dark' as 'dark' | 'light',
  autoSave: false,
  autoSaveInterval: 60,
  gridSize: 20,
  showGrid: true,
  snapToGrid: true,
  defaultZoom: '1',
  defaultNodeSize: 200,
  previewWidth: 1280,
  previewHeight: 720,
  fullscreenPreview: false,
  apiBaseUrl: '/api',
  requestTimeout: 30000,
})

const saveMessage = ref('')
const saveError = ref(false)

onMounted(() => {
  loadSettings()
})

/**
 * 语言切换处理函数
 * 核心功能：切换语言时会触发以下操作：
 * 1. 调用 composable 的 changeLanguage 更新 i18n locale
 * 2. 通过 localizationStore 加载后端翻译
 * 3. 所有使用 t() 的组件会响应式更新（包括 InspectorPanel 子类型名称）
 */
async function onLanguageChange() {
  const newLang = settings.language as 'en' | 'zh'
  
  try {
    // 调用 composable 的 changeLanguage 方法
    // 该方法会同时更新 vue-i18n 的 locale 和 localizationStore
    await changeLanguage(newLang)
    
    // 同步更新 localizationStore 的当前语言状态
    // 确保 store 与 UI 设置保持一致
    if (localizationStore.currentLanguage !== newLang) {
      await localizationStore.setLanguage(newLang)
    }
    
    console.log(`[PreferencesPage] Language changed to: ${newLang}`)
  } catch (error) {
    // 异常来源：后端 API 不可用或网络错误
    // 处理方法：使用内置 i18n 翻译作为降级方案
    console.error('[PreferencesPage] Failed to change language:', error)
    
    // 即使后端失败，仍然强制更新前端 locale
    // 保证 UI 至少显示内置翻译
    locale.value = newLang
  }
}

/**
 * 主题切换处理函数
 * 通过修改 data-theme 属性实现深色/浅色主题切换
 */
function onThemeChange() {
  applyTheme(settings.theme)
}

/**
 * 应用主题到 DOM
 * @param theme - 目标主题 ('dark' | 'light')
 */
function applyTheme(theme: 'dark' | 'light') {
  document.documentElement.setAttribute('data-theme', theme)
}

/**
 * 从 localStorage 加载已保存的设置
 * 页面初始化时调用，恢复用户之前的配置
 */
function loadSettings() {
  const savedSettings = localStorage.getItem(SETTINGS_STORAGE_KEY)
  
  if (!savedSettings) return
  
  try {
    const parsed = JSON.parse(savedSettings)
    
    // 使用 Object.assign 合并保存的设置到当前 settings 对象
    // 仅覆盖已存在的属性，保持默认值作为兜底
    Object.assign(settings, {
      language: parsed.language || settings.language,
      theme: parsed.theme || settings.theme,
      autoSave: parsed.autoSave ?? settings.autoSave,
      autoSaveInterval: parsed.autoSaveInterval ?? settings.autoSaveInterval,
      gridSize: parsed.gridSize ?? settings.gridSize,
      showGrid: parsed.showGrid ?? settings.showGrid,
      snapToGrid: parsed.snapToGrid ?? settings.snapToGrid,
      defaultZoom: parsed.defaultZoom || settings.defaultZoom,
      defaultNodeSize: parsed.defaultNodeSize ?? settings.defaultNodeSize,
      previewWidth: parsed.previewWidth ?? settings.previewWidth,
      previewHeight: parsed.previewHeight ?? settings.previewHeight,
      fullscreenPreview: parsed.fullscreenPreview ?? settings.fullscreenPreview,
      apiBaseUrl: parsed.apiBaseUrl || settings.apiBaseUrl,
      requestTimeout: parsed.requestTimeout ?? settings.requestTimeout,
    })
    
    // 恢复已保存的主题
    applyTheme(settings.theme as 'dark' | 'light')
    
    console.log('[PreferencesPage] Settings loaded from localStorage')
  } catch (error) {
    // 异常来源：localStorage 数据格式损坏
    // 处理方法：忽略错误数据，使用默认设置
    console.error('[PreferencesPage] Failed to parse saved settings:', error)
  }
}

/**
 * 保存设置到 localStorage 和后端服务器
 * 优先本地存储保证用户体验，后端同步为可选
 * 在 Popup 模式下保存后关闭 Popup 并刷新父窗口
 */
async function saveSettings() {
  // 序列化当前设置对象
  const settingsData = { ...settings }
  
  // 持久化到 localStorage（始终执行）
  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(settingsData))
  
  try {
    // 尝试同步到后端 API
    await settingsApi.saveSettings(settingsData)
    saveMessage.value = t('settings.savedSuccess')
    saveError.value = false
    
    console.log('[PreferencesPage] Settings saved to server successfully')
  } catch (error) {
    // 异常来源：后端服务不可用或网络连接问题
    // 处理方法：提示用户设置已本地保存，不影响使用
    console.warn('[PreferencesPage] Server save failed, using local fallback:', error)
    saveMessage.value = t('settings.savedLocal')
    saveError.value = false
  }

  // 如果在 Popup 窗口中，保存后关闭并刷新父窗口
  if (window.opener) {
    // 延迟关闭以确保用户看到保存成功消息
    setTimeout(() => {
      try {
        // 刷新父窗口（主编辑器页面）以应用新设置
        window.opener.location.reload()
      } catch (e) {
        // 如果跨域无法访问父窗口，忽略错误
        console.warn('[PreferencesPage] Cannot access parent window:', e)
      }
      // 关闭当前 Popup 窗口
      window.close()
    }, 1000)
  }

  // 3 秒后自动清除提示消息
  setTimeout(() => {
    saveMessage.value = ''
  }, 3000)
}

/**
 * 重置所有设置为默认值
 * 会立即保存并应用默认配置
 */
function resetSettings() {
  // 重置各设置项为默认值
  settings.language = 'zh'
  settings.theme = 'dark'
  settings.autoSave = false
  settings.autoSaveInterval = 60
  settings.gridSize = 20
  settings.showGrid = true
  settings.snapToGrid = true
  settings.defaultZoom = '1'
  settings.defaultNodeSize = 200
  settings.previewWidth = 1280
  settings.previewHeight = 720
  settings.fullscreenPreview = false
  settings.apiBaseUrl = '/api'
  settings.requestTimeout = 30000
  
  // 应用默认主题
  applyTheme('dark')
  
  // 重置时也触发语言切换以恢复默认语言
  changeLanguage('zh').catch(console.error)
  
  // 立即保存重置后的设置
  saveSettings()
}
</script>

<style scoped>
.preferences-page {
  display: flex;
  justify-content: center;
  padding: 40px;
  background: #1e1e1e;
  min-height: 100vh;
  box-sizing: border-box;
}

.preferences-container {
  width: 100%;
  max-width: 700px;
  background: #252526;
  border-radius: 8px;
  padding: 24px;
  box-sizing: border-box;
}

/* 页面标题样式 */
h1 {
  margin: 0 0 24px;
  font-size: 24px;
  font-weight: 600;
  color: #ffffff;
}

/* 设置分组容器 */
.settings-section {
  margin-bottom: 32px;
  padding-bottom: 24px;
  border-bottom: 1px solid #3e3e42;
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
  color: #cccccc;
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
  color: #ffffff;
  font-size: 14px;
  flex-shrink: 0;
  min-width: 160px;
}

/* 输入框统一样式（文本、数字、下拉框） */
.setting-item input[type="text"],
.setting-item input[type="number"],
.setting-item select {
  padding: 6px 12px;
  background: #3c3c3c;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  color: #ffffff;
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
  border-color: #007acc;
}

/* 数字输入框隐藏微调按钮（保持视觉一致性） */
.setting-item input[type="number"]::-webkit-inner-spin-button,
.setting-item input[type="number"]::-webkit-outer-spin-button {
  opacity: 0.5;
}

/* 下拉框箭头颜色修复 */
.setting-item select option {
  background: #3c3c3c;
  color: #ffffff;
}

/* 复选框样式 */
.setting-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #007acc;
}

/* 语言加载指示器 */
.loading-indicator {
  font-size: 12px;
  color: #007acc;
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

/* 操作按钮区域 */
.settings-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #3e3e42;
}

/* 主要按钮样式 */
.btn-primary {
  padding: 8px 24px;
  background: #007acc;
  border: none;
  border-radius: 4px;
  color: #ffffff;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s ease;
}

.btn-primary:hover {
  background: #1177bb;
}

.btn-primary:active {
  background: #0e639c;
}

/* 次要按钮样式 */
.btn-secondary {
  padding: 8px 24px;
  background: #3c3c3c;
  border: none;
  border-radius: 4px;
  color: #ffffff;
  font-size: 14px;
  cursor: pointer;
  transition: background 0.2s ease;
}

.btn-secondary:hover {
  background: #4c4c4c;
}

.btn-secondary:active {
  background: #555555;
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
</style>
