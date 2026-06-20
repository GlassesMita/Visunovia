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
            <p class="setting-desc">{{ t('settings.language.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.theme.desc') || 'Choose the Material You appearance used by the entire app.' }}</p>
          </div>
          <div class="setting-control">
            <select v-model="settings.theme" @change="onThemeChange">
              <option value="light">{{ t('settings.light') || 'Light' }}</option>
              <option value="dark">{{ t('settings.dark') || 'Dark' }}</option>
              <option value="system">{{ t('settings.system') || 'System' }}</option>
            </select>
          </div>
        </div>

        <div class="setting-row material-theme-row">
          <div class="setting-label">
            <label>{{ t('settings.materialYouPalette') || 'Material You Color' }}</label>
            <p class="setting-desc">{{ t('settings.materialYouPalette.desc') || 'Pick a dynamic color palette or customize the seed color.' }}</p>
          </div>
          <div class="setting-control theme-control">
            <div class="theme-preview md-card">
              <div class="theme-preview-surface">
                <span class="preview-dot primary"></span>
                <span class="preview-dot secondary"></span>
                <span class="preview-dot tertiary"></span>
              </div>
              <div class="theme-preview-label">
                Material You 3
                <small>{{ resolvedTheme }}</small>
              </div>
            </div>
            <div class="palette-grid" aria-label="Material You palettes">
              <button
                v-for="option in paletteOptions"
                :key="option.value"
                type="button"
                class="palette-chip"
                :class="{ active: settings.palette === option.value }"
                :style="{ '--chip-color': option.seedColor }"
                :title="option.label"
                @click="selectPalette(option.value)"
              >
                <span></span>
              </button>
            </div>
            <label class="color-picker-label">
              <span>{{ t('settings.seedColor') || 'Seed Color' }}</span>
              <input v-model="settings.seedColor" type="color" @input="onSeedColorChange" />
            </label>
          </div>
        </div>

        <div class="setting-row theme-style-row">
          <div class="setting-label">
            <label>{{ t('settings.themeStyle') || 'Theme Style' }}</label>
            <p class="setting-desc">{{ t('settings.themeStyle.desc') || 'Switch the whole WebUI between Material You, Windows 11, and Windows 10 styles.' }}</p>
          </div>
          <div class="setting-control style-control">
            <button
              v-for="option in themeStyleOptions"
              :key="option.value"
              type="button"
              class="style-card"
              :class="[{ active: settings.themeStyle === option.value }, option.value]"
              @click="selectThemeStyle(option.value)"
            >
              <span class="style-preview">
                <span class="style-titlebar"></span>
                <span class="style-pane"></span>
                <span class="style-content"></span>
              </span>
              <span class="style-copy">
                <strong>{{ option.label }}</strong>
                <small>{{ option.description }}</small>
              </span>
            </button>
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.placeholderCompany') || 'Company Name' }}</label>
            <p class="setting-desc">{{ t('settings.placeholderCompany.desc') }}</p>
          </div>
          <div class="setting-control">
            <input type="text" v-model="settings.placeholderCompanyName" placeholder="Abydos Highschool" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.placeholderProduct') || 'Product Name' }}</label>
            <p class="setting-desc">{{ t('settings.placeholderProduct.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.autoSave.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.autoSaveInterval.desc') }}</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.autoSaveInterval" min="10" max="300" step="10" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.showGrid') || 'Show Grid' }}</label>
            <p class="setting-desc">{{ t('settings.showGrid.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.gridSize.desc') }}</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.gridSize" min="10" max="50" step="5" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.snapEnabled') || 'Snap to Grid' }}</label>
            <p class="setting-desc">{{ t('settings.snapEnabled.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.defaultZoom.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.defaultNodeSize.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.previewWidth.desc') }}</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.previewWidth" min="640" max="3840" step="320" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.previewHeight') || 'Preview Height' }}</label>
            <p class="setting-desc">{{ t('settings.previewHeight.desc') }}</p>
          </div>
          <div class="setting-control">
            <input type="number" v-model.number="settings.previewHeight" min="360" max="2160" step="180" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.fullscreenPreview') || 'Fullscreen Preview' }}</label>
            <p class="setting-desc">{{ t('settings.fullscreenPreview.desc') }}</p>
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
            <p class="setting-desc">{{ t('settings.apiBaseUrl.desc') }}</p>
          </div>
          <div class="setting-control">
            <input type="text" v-model="settings.apiBaseUrl" placeholder="/api" />
          </div>
        </div>

        <div class="setting-row">
          <div class="setting-label">
            <label>{{ t('settings.requestTimeout') || 'Request Timeout' }}</label>
            <p class="setting-desc">{{ t('settings.requestTimeout.desc') }}</p>
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
import {
  useTheme,
  type ThemeMode,
  type ThemeStyle,
  type MaterialYouPalette,
} from '@/composables/useTheme'
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
const { loadTheme, setTheme, paletteOptions, themeStyleOptions, resolvedTheme, getPaletteSeedColor } = useTheme()

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
  theme: 'dark' as ThemeMode,
  themeStyle: 'material-you' as ThemeStyle,
  seedColor: '#6750a4',
  palette: 'purple' as MaterialYouPalette,
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
onMounted(async () => {
  loadSettings()
  const themeSettings = await loadTheme()
  settings.theme = themeSettings.theme
  settings.themeStyle = themeSettings.themeStyle
  settings.seedColor = themeSettings.seedColor
  settings.palette = themeSettings.palette
})

function loadSettings() {
  const saved = localStorage.getItem(SETTINGS_STORAGE_KEY)
  if (!saved) return
  try {
    const parsed = JSON.parse(saved)
    Object.assign(settings, {
      language: parsed.language || settings.language,
      theme: parsed.theme || parsed.Theme || settings.theme,
      themeStyle: parsed.themeStyle || parsed.ThemeStyle || settings.themeStyle,
      seedColor: parsed.seedColor || parsed.SeedColor || settings.seedColor,
      palette: parsed.palette || parsed.Palette || settings.palette,
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
  setTheme({
    theme: settings.theme as ThemeMode,
    themeStyle: settings.themeStyle,
    seedColor: settings.seedColor,
    palette: settings.palette,
  }).catch(() => {})
}

function selectThemeStyle(themeStyle: ThemeStyle) {
  settings.themeStyle = themeStyle
  onThemeChange()
}

function selectPalette(palette: MaterialYouPalette) {
  settings.palette = palette
  settings.seedColor = getPaletteSeedColor(palette)
  onThemeChange()
}

function onSeedColorChange() {
  onThemeChange()
}

async function saveSettings() {
  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify({ ...settings }))

  try {
    await settingsApi.saveSettings({
      ...settings,
      Language: settings.language,
      Theme: settings.theme,
      ThemeStyle: settings.themeStyle,
      SeedColor: settings.seedColor,
      Palette: settings.palette,
      PlaceholderCompanyName: settings.placeholderCompanyName,
      PlaceholderProductName: settings.placeholderProductName,
      PreviewWidth: settings.previewWidth,
      PreviewHeight: settings.previewHeight,
      AutoSaveInterval: settings.autoSave ? settings.autoSaveInterval : 0,
    })
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
  settings.themeStyle = 'material-you'
  settings.seedColor = '#6750a4'
  settings.palette = 'purple'
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

  setTheme({ theme: 'dark', themeStyle: settings.themeStyle, seedColor: settings.seedColor, palette: settings.palette }).catch(() => {})
  changeLanguage('zh-CN').catch(() => {})
  saveSettings()
}
</script>

<style scoped>
/* ========== Root Layout ========== */
.preferences-root {
  display: flex;
  height: 100vh;
  background: radial-gradient(circle at top right, var(--md-sys-color-primary-container), transparent 34%), var(--vn-bg);
  color: var(--vn-text);
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  overflow: hidden;
}

/* ========== Sidebar ========== */
.prefs-sidebar {
  width: 200px;
  min-width: 200px;
  background: color-mix(in srgb, var(--md-sys-color-surface-container) 92%, transparent);
  border-right: 1px solid var(--vn-border);
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
  color: var(--vn-text-soft);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border-bottom: 1px solid var(--vn-border);
  margin-bottom: 4px;
}

.sidebar-item {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 3px 8px;
  padding: 10px 14px;
  font-size: 13px;
  color: var(--vn-text-muted);
  cursor: pointer;
  transition: background 0.15s, color 0.15s, border-color 0.15s;
  border: 1px solid transparent;
  border-radius: 999px;
  user-select: none;
}

.sidebar-item:hover {
  background: var(--vn-surface-muted);
  color: var(--vn-text-soft);
}

.sidebar-item.active {
  background: var(--md-sys-color-secondary-container);
  color: var(--md-sys-color-on-surface);
  border-color: var(--md-sys-color-outline-variant);
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
  color: var(--vn-text-soft);
  padding-bottom: 12px;
  border-bottom: 1px solid var(--vn-border);
}

/* ========== Setting Row ========== */
.setting-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 16px;
  margin-bottom: 12px;
  background: color-mix(in srgb, var(--md-sys-color-surface-container-low) 88%, transparent);
  border: 1px solid var(--vn-border);
  border-radius: var(--md-sys-shape-corner-large);
  gap: 24px;
}

.setting-row:last-child {
  margin-bottom: 0;
}

.setting-label {
  flex: 1;
  min-width: 0;
}

.setting-label label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  color: var(--vn-text);
  margin-bottom: 2px;
}

.setting-desc {
  margin: 0;
  font-size: 12px;
  color: var(--vn-text-muted);
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
  min-height: 40px;
  padding: 8px 14px;
  background: var(--vn-control-bg);
  border: 1px solid var(--vn-border-strong);
  border-radius: var(--md-sys-shape-corner-small);
  color: var(--vn-text-soft);
  font-size: 13px;
  width: 160px;
  transition: border-color 0.15s;
  box-sizing: border-box;
}

.setting-control input[type="text"]:focus,
.setting-control input[type="number"]:focus,
.setting-control select:focus {
  outline: none;
  border-color: var(--vn-accent);
}

.setting-control select {
  cursor: pointer;
}

.setting-control select option {
  background: var(--vn-surface);
  color: var(--vn-text-soft);
}

/* Toggle Switch */
.toggle-switch {
  position: relative;
  display: inline-block;
  width: 52px;
  height: 32px;
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
  background: var(--md-sys-color-surface-container-highest);
  border: 2px solid var(--md-sys-color-outline);
  border-radius: 999px;
  transition: background 0.2s;
}

.toggle-slider::before {
  content: '';
  position: absolute;
  width: 16px;
  height: 16px;
  left: 6px;
  bottom: 6px;
  background: var(--md-sys-color-outline);
  border-radius: 50%;
  transition: transform 0.2s;
}

.toggle-switch input:checked + .toggle-slider {
  background: var(--md-sys-color-primary);
  border-color: var(--md-sys-color-primary);
}

.toggle-switch input:checked + .toggle-slider::before {
  background: var(--md-sys-color-on-primary);
  transform: translateX(20px);
}

.theme-control {
  width: min(100%, 360px);
  flex-direction: column;
  align-items: stretch;
  gap: 12px;
}

.theme-preview {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 86px;
  padding: 14px;
  width: 100%;
}

.theme-preview-surface {
  display: flex;
  align-items: end;
  gap: 8px;
}

.preview-dot {
  display: block;
  width: 38px;
  height: 38px;
  border-radius: 999px;
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--md-sys-color-outline) 42%, transparent);
}

.preview-dot.primary {
  background: var(--md-sys-color-primary);
}

.preview-dot.secondary {
  width: 30px;
  height: 30px;
  background: var(--md-sys-color-secondary);
}

.preview-dot.tertiary {
  width: 24px;
  height: 24px;
  background: var(--md-sys-color-tertiary);
}

.theme-preview-label {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  font-weight: 700;
  color: var(--md-sys-color-on-surface);
}

.theme-preview-label small {
  font-size: 11px;
  font-weight: 500;
  color: var(--md-sys-color-on-surface-variant);
  text-transform: uppercase;
}

.palette-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 8px;
}

.palette-chip {
  display: grid;
  place-items: center;
  width: 100%;
  aspect-ratio: 1;
  border: 2px solid transparent;
  border-radius: 999px;
  background: color-mix(in srgb, var(--chip-color) 16%, var(--md-sys-color-surface-container-high));
}

.palette-chip span {
  width: 22px;
  height: 22px;
  border-radius: inherit;
  background: var(--chip-color);
}

.palette-chip.active {
  border-color: var(--md-sys-color-primary);
  background: var(--md-sys-color-primary-container);
}

.color-picker-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  color: var(--md-sys-color-on-surface-variant);
  font-size: 12px;
}

.color-picker-label input[type="color"] {
  width: 58px;
  height: 38px;
  padding: 3px;
  border-radius: var(--md-sys-shape-corner-small);
  border: 1px solid var(--md-sys-color-outline);
  background: var(--md-sys-color-surface-container-high);
}

.style-control {
  width: min(100%, 520px);
  flex-direction: column;
  align-items: stretch;
  gap: 10px;
}

.style-card {
  display: grid;
  grid-template-columns: 96px minmax(0, 1fr);
  gap: 14px;
  align-items: center;
  width: 100%;
  padding: 12px;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: var(--md-sys-shape-corner-large);
  background: var(--md-sys-color-surface-container-low);
  transition: border-color 0.15s ease, background 0.15s ease, transform 0.15s ease;
}

.style-card:hover {
  border-color: var(--md-sys-color-primary);
  background: var(--md-sys-color-surface-container-high);
}

.style-card.active {
  border-color: var(--md-sys-color-primary);
  background: var(--md-sys-color-secondary-container);
}

.style-preview {
  display: grid;
  grid-template-columns: 28px 1fr;
  grid-template-rows: 14px 44px;
  overflow: hidden;
  min-height: 58px;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: 14px;
  background: var(--md-sys-color-surface-container);
  box-shadow: var(--md-sys-elevation-1);
}

.style-titlebar {
  grid-column: 1 / -1;
  background: var(--md-sys-color-primary);
}

.style-pane {
  background: var(--md-sys-color-surface-container-highest);
}

.style-content {
  margin: 8px;
  border-radius: 10px;
  background: var(--md-sys-color-surface-container-high);
}

.style-card.windows-11 .style-preview {
  border-radius: 9px;
  background: color-mix(in srgb, var(--md-sys-color-surface-container) 84%, var(--md-sys-color-primary-container));
}

.style-card.windows-11 .style-content {
  border-radius: 7px;
}

.style-card.windows-10 .style-preview,
.style-card.windows-10 .style-content {
  border-radius: 2px;
}

.style-card.windows-10 .style-preview {
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.28);
}

.style-copy {
  display: flex;
  flex-direction: column;
  gap: 3px;
  text-align: left;
}

.style-copy strong {
  font-size: 13px;
  color: var(--md-sys-color-on-surface);
}

.style-copy small {
  color: var(--md-sys-color-on-surface-variant);
  font-size: 11px;
  line-height: 1.35;
}

/* ========== Footer ========== */
.prefs-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 32px;
  background: var(--md-sys-color-surface-container);
  border-top: 1px solid var(--vn-border);
  flex-shrink: 0;
}

.footer-actions {
  display: flex;
  gap: 8px;
  margin-left: auto;
}

.btn-save,
.btn-reset {
  min-height: 40px;
  padding: 0 24px;
  border: none;
  border-radius: 999px;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.15s;
}

.btn-save {
  background: var(--md-sys-color-primary);
  color: var(--md-sys-color-on-primary) !important;
  -webkit-text-fill-color: var(--md-sys-color-on-primary) !important;
}

.btn-save:hover {
  box-shadow: var(--md-sys-elevation-1);
}

.btn-reset {
  background: var(--md-sys-color-secondary-container);
  color: var(--md-sys-color-on-surface);
}

.btn-reset:hover {
  background: var(--vn-surface-muted);
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

