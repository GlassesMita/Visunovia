<template>
  <div class="preferences-root">
    <!-- 左侧分类导航 -->
    <nav class="prefs-sidebar">
      <div class="sidebar-header">
        <Settings2 :size="20" />
        <span>{{ t('settings.title') || 'Preferences' }}</span>
      </div>
      <div
        v-for="cat in categories"
        :key="cat.key"
        class="sidebar-item"
        :class="{ active: activeCategory === cat.key }"
        @click="activeCategory = cat.key"
      >
        <component :is="cat.icon" :size="18" />
        <span>{{ cat.label }}</span>
      </div>
    </nav>

    <!-- 右侧内容区 -->
    <main class="prefs-content">
      <!-- General -->
      <div v-show="activeCategory === 'general'" class="category-panel">
        <h2 class="category-title">{{ t('settings.general') || 'General' }}</h2>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.language') || 'Language' }}</label>
            <p class="setting-desc">界面显示语言</p>
          </div>
          <div class="setting-control">
            <select v-model="settings.language" @change="onLanguageChange">
              <option
                v-for="lang in availableLanguages"
                :key="lang.code"
                :value="lang.code"
              >
                {{ lang.displayName }}
              </option>
            </select>
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.theme') || 'Theme' }}</label>
            <p class="setting-desc">编辑器配色方案</p>
          </div>
          <div class="setting-control">
            <select v-model="settings.theme" @change="onThemeChange">
              <option value="dark">{{ t('settings.dark') || 'Dark' }}</option>
              <option value="light">{{ t('settings.light') || 'Light' }}</option>
            </select>
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.placeholderCompany') || 'Company Name' }}</label>
            <p class="setting-desc">新建项目时默认的公司名称（占位符）</p>
          </div>
          <div class="setting-control">
            <input type="text" v-model="settings.placeholderCompanyName" placeholder="Abydos Highschool" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.placeholderProduct') || 'Product Name' }}</label>
            <p class="setting-desc">新建项目时默认的产品名称（占位符）</p>
          </div>
          <div class="setting-control">
            <input type="text" v-model="settings.placeholderProductName" placeholder="Anubis" />
          </div>
        </div>
      </div>

      <!-- Editor -->
      <div v-show="activeCategory === 'editor'" class="category-panel">
        <h2 class="category-title">{{ t('settings.editor') || 'Editor' }}</h2>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.autoSave') || 'Auto Save' }}</label>
            <p class="setting-desc">自动保存当前场景</p>
          </div>
          <div class="setting-control">
            <label class="toggle-switch">
              <input type="checkbox" v-model="settings.autoSave" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>

        <div class="setting-row" v-if="settings.autoSave">
          <div class="setting-label">
            <label>{{ t('settings.autoSaveInterval') || 'Auto Save Interval' }}</label>
            <p class="setting-desc">自动保存间隔（秒）</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.autoSaveInterval" min="10" max="300" step="10" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.showGrid') || 'Show Grid' }}</label>
            <p class="setting-desc">在编辑器中显示网格线</p>
          </div>
          <div class="setting-control">
            <label class="toggle-switch">
              <input type="checkbox" v-model="settings.showGrid" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.gridSize') || 'Grid Size' }}</label>
            <p class="setting-desc">网格单元格大小（像素）</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.gridSize" min="10" max="50" step="5" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.snapEnabled') || 'Snap to Grid' }}</label>
            <p class="setting-desc">节点移动时对齐到网格</p>
          </div>
          <div class="setting-control">
            <label class="toggle-switch">
              <input type="checkbox" v-model="settings.snapToGrid" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.defaultZoom') || 'Default Zoom' }}</label>
            <p class="setting-desc">新建场景的默认缩放级别</p>
          </div>
          <div class="setting-control">
            <select v-model="settings.defaultZoom">
              <option value="0.5">50%</option>
              <option value="0.75">75%</option>
              <option value="1">100%</option>
              <option value="1.25">125%</option>
              <option value="1.5">150%</option>
            </select>
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.defaultNodeSize') || 'Default Node Size' }}</label>
            <p class="setting-desc">新建节点的默认宽度</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.defaultNodeSize" min="100" max="400" step="50" />
          </div>
        </div>
      </div>

      <!-- Preview -->
      <div v-show="activeCategory === 'preview'" class="category-panel">
        <h2 class="category-title">{{ t('settings.preview') || 'Preview' }}</h2>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.previewWidth') || 'Preview Width' }}</label>
            <p class="setting-desc">预览窗口宽度（像素）</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.previewWidth" min="640" max="3840" step="320" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.previewHeight') || 'Preview Height' }}</label>
            <p class="setting-desc">预览窗口高度（像素）</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.previewHeight" min="360" max="2160" step="180" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.fullscreenPreview') || 'Fullscreen Preview' }}</label>
            <p class="setting-desc">默认以全屏模式预览</p>
          </div>
          <div class="setting-control">
            <label class="toggle-switch">
              <input type="checkbox" v-model="settings.fullscreenPreview" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
      </div>

      <!-- Network -->
      <div v-show="activeCategory === 'network'" class="category-panel">
        <h2 class="category-title">{{ t('settings.network') || 'Network' }}</h2>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.apiBaseUrl') || 'API Base URL' }}</label>
            <p class="setting-desc">后端 API 服务地址</p>
          </div>
          <div class="setting-control">
            <input type="text" v-model="settings.apiBaseUrl" placeholder="/api" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.requestTimeout') || 'Request Timeout' }}</label>
            <p class="setting-desc">HTTP 请求超时时间（毫秒）</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.requestTimeout" min="5000" max="120000" step="5000" />
          </div>
        </div>
      </div>

      <!-- 底部操作栏 -->
      <div class="prefs-footer">
        <div v-if="saveMessage" class="save-toast" :class="{ error: saveError }">
          {{ saveMessage }}
        </div>
        <div class="footer-actions">
          <button class="btn-reset" @click="resetSettings">
            {{ t('common.cancel') || 'Reset' }}
          </button>
          <button class="btn-save" @click="saveSettings">
            {{ t('common.save') || 'Save' }}
          </button>
        </div>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted, markRaw } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { settingsApi } from '@/api'
import {
  Settings2,
  Palette,
  Eye,
  Wifi,
} from 'lucide-vue-next'
import type { Component } from 'vue'

const { t, changeLanguage, availableLanguages } = useLocalization()
const localizationStore = useLocalizationStore()

const SETTINGS_STORAGE_KEY = 'visunovia-settings'

// 分类定义（Unity Preferences 风格）
interface Category {
  key: string
  label: string
  icon: Component
}

const categories: Category[] = [
  { key: 'general', label: t('settings.general') || 'General', icon: markRaw(Settings2) },
  { key: 'editor', label: t('settings.editor') || 'Editor', icon: markRaw(Palette) },
  { key: 'preview', label: t('settings.preview') || 'Preview', icon: markRaw(Eye) },
  { key: 'network', label: t('settings.network') || 'Network', icon: markRaw(Wifi) },
]

const activeCategory = ref('general')

const settings = reactive({
  language: 'zh-CN',
  theme: 'dark' as 'dark' | 'light',
  placeholderCompanyName: '',
  placeholderProductName: '',
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

// 同步加载设置（无异步等待，立即渲染）
onMounted(() => {
  loadSettings()
  applyTheme(settings.theme)
})

function loadSettings() {
  const saved = localStorage.getItem(SETTINGS_STORAGE_KEY)
  if (!saved) return
  try {
    const parsed = JSON.parse(saved)
    Object.assign(settings, {
      language: parsed.language || settings.language,
      theme: parsed.theme || settings.theme,
      placeholderCompanyName: parsed.placeholderCompanyName ?? settings.placeholderCompanyName,
      placeholderProductName: parsed.placeholderProductName ?? settings.placeholderProductName,
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
  } catch (e) {
    console.error('[Preferences] Failed to parse settings:', e)
  }
}

async function onLanguageChange() {
  const lang = settings.language
  try {
    await changeLanguage(lang)
    if (localizationStore.currentLanguage !== lang) {
      await localizationStore.setLanguage(lang)
    }
  } catch {
    // ignore
  }
}

function onThemeChange() {
  applyTheme(settings.theme)
}

function applyTheme(theme: 'dark' | 'light') {
  document.documentElement.setAttribute('data-theme', theme)
}

async function saveSettings() {
  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify({ ...settings }))

  try {
    await settingsApi.saveSettings({ ...settings })
    saveMessage.value = t('settings.savedSuccess') || 'Settings saved!'
    saveError.value = false
  } catch {
    saveMessage.value = t('settings.savedLocal') || 'Saved locally (server unavailable)'
    saveError.value = false
  }

  if (window.opener) {
    setTimeout(() => {
      try { window.opener.location.reload() } catch {}
      window.close()
    }, 800)
  }

  setTimeout(() => { saveMessage.value = '' }, 3000)
}

function resetSettings() {
  settings.language = 'zh-CN'
  settings.theme = 'dark'
  settings.placeholderCompanyName = ''
  settings.placeholderProductName = ''
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

  applyTheme('dark')
  changeLanguage('zh-CN').catch(() => {})
  saveSettings()
}
</script>

<style scoped>
/* ========== Root Layout ========== */
.preferences-root {
  display: flex;
  height: 100vh;
  background: #1e1e1e;
  color: #cccccc;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  overflow: hidden;
}

/* ========== Sidebar ========== */
.prefs-sidebar {
  width: 200px;
  min-width: 200px;
  background: #252526;
  border-right: 1px solid #3e3e42;
  display: flex;
  flex-direction: column;
  padding: 0;
  overflow-y: auto;
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px 16px 12px;
  font-size: 13px;
  font-weight: 600;
  color: #e0e0e0;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid #3e3e42;
  margin-bottom: 4px;
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 16px;
  font-size: 13px;
  color: #bbbbbb;
  cursor: pointer;
  transition: background 0.1s, color 0.1s;
  border-left: 3px solid transparent;
  user-select: none;
}

.sidebar-item:hover {
  background: #2a2d2e;
  color: #e0e0e0;
}

.sidebar-item.active {
  background: #37373d;
  color: #ffffff;
  border-left-color: #007acc;
}

/* ========== Content Area ========== */
.prefs-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.category-panel {
  flex: 1;
  overflow-y: auto;
  padding: 24px 32px;
}

.category-title {
  margin: 0 0 24px;
  font-size: 18px;
  font-weight: 600;
  color: #e0e0e0;
  padding-bottom: 12px;
  border-bottom: 1px solid #3e3e42;
}

/* ========== Setting Row ========== */
.setting-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 12px 0;
  border-bottom: 1px solid #2d2d30;
  gap: 24px;
}

.setting-row:last-child {
  border-bottom: none;
}

.setting-label {
  flex: 1;
  min-width: 0;
}

.setting-label label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  color: #cccccc;
  margin-bottom: 2px;
}

.setting-desc {
  margin: 0;
  font-size: 12px;
  color: #888888;
  line-height: 1.4;
}

.setting-control {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  min-width: 160px;
  justify-content: flex-end;
}

/* ========== Form Controls ========== */
.setting-control input[type="text"],
.setting-control input[type="number"],
.setting-control select {
  padding: 6px 10px;
  background: #3c3c3c;
  border: 1px solid #555555;
  border-radius: 3px;
  color: #e0e0e0;
  font-size: 13px;
  width: 160px;
  transition: border-color 0.15s;
  box-sizing: border-box;
}

.setting-control input[type="text"]:focus,
.setting-control input[type="number"]:focus,
.setting-control select:focus {
  outline: none;
  border-color: #007acc;
}

.setting-control select {
  cursor: pointer;
}

.setting-control select option {
  background: #2d2d30;
  color: #e0e0e0;
}

/* Toggle Switch */
.toggle-switch {
  position: relative;
  display: inline-block;
  width: 40px;
  height: 22px;
  cursor: pointer;
}

.toggle-switch input {
  opacity: 0;
  width: 0;
  height: 0;
  position: absolute;
}

.toggle-slider {
  position: absolute;
  inset: 0;
  background: #555555;
  border-radius: 22px;
  transition: background 0.2s;
}

.toggle-slider::before {
  content: '';
  position: absolute;
  width: 18px;
  height: 18px;
  left: 2px;
  bottom: 2px;
  background: #ffffff;
  border-radius: 50%;
  transition: transform 0.2s;
}

.toggle-switch input:checked + .toggle-slider {
  background: #007acc;
}

.toggle-switch input:checked + .toggle-slider::before {
  transform: translateX(18px);
}

/* ========== Footer ========== */
.prefs-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 32px;
  background: #252526;
  border-top: 1px solid #3e3e42;
  flex-shrink: 0;
}

.footer-actions {
  display: flex;
  gap: 8px;
  margin-left: auto;
}

.btn-save,
.btn-reset {
  padding: 7px 20px;
  border: none;
  border-radius: 3px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
}

.btn-save {
  background: #007acc;
  color: #ffffff;
}

.btn-save:hover {
  background: #1a8ad4;
}

.btn-reset {
  background: #3c3c3c;
  color: #cccccc;
}

.btn-reset:hover {
  background: #4a4a4a;
}

.save-toast {
  font-size: 13px;
  color: #89d185;
  padding: 4px 0;
}

.save-toast.error {
  color: #f48771;
}

/* ========== Responsive ========== */
@media (max-width: 640px) {
  .prefs-sidebar {
    width: 56px;
    min-width: 56px;
  }

  .sidebar-header span,
  .sidebar-item span {
    display: none;
  }

  .sidebar-item {
    justify-content: center;
    padding: 10px;
  }

  .sidebar-header {
    justify-content: center;
    padding: 12px 8px;
  }

  .category-panel {
    padding: 16px;
  }

  .setting-row {
    flex-direction: column;
    gap: 8px;
  }

  .setting-control {
    justify-content: flex-start;
    min-width: auto;
  }

  .prefs-footer {
    padding: 12px 16px;
  }
}
</style>

