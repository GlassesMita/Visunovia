<template>
  <div class="preferences-page">
    <div class="preferences-container">
      <h1>{{ t('settings.title') }}</h1>
      
      <div class="settings-section">
        <h2>{{ t('settings.general') }}</h2>
        
        <div class="setting-item">
          <label>{{ t('settings.language') }}</label>
          <select v-model="currentLanguage" @change="onLanguageChange">
            <option value="zh">中文</option>
            <option value="en">English</option>
          </select>
        </div>
        
        <div class="setting-item">
          <label>{{ t('settings.theme') }}</label>
          <select v-model="currentTheme">
            <option value="dark">{{ t('settings.dark') }}</option>
            <option value="light">{{ t('settings.light') }}</option>
          </select>
        </div>
      </div>
      
      <div class="settings-section">
        <h2>{{ t('settings.editor') }}</h2>
        
        <div class="setting-item">
          <label>Grid Size</label>
          <input type="number" v-model="gridSize" min="10" max="50" step="5" />
        </div>
        
        <div class="setting-item">
          <label>Snap to Grid</label>
          <input type="checkbox" v-model="snapToGrid" />
        </div>
      </div>
      
      <div class="settings-actions">
        <button @click="saveSettings">{{ t('common.save') }}</button>
        <button @click="resetSettings">{{ t('common.cancel') }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

const { t, changeLanguage } = useLocalization()
const localizationStore = useLocalizationStore()

const currentLanguage = ref(localizationStore.currentLanguage)
const currentTheme = ref('dark')
const gridSize = ref(20)
const snapToGrid = ref(true)

onMounted(() => {
  loadSettings()
})

async function onLanguageChange() {
  await changeLanguage(currentLanguage.value as 'en' | 'zh')
  saveSettings()
}

function loadSettings() {
  const savedSettings = localStorage.getItem('preferences')
  if (savedSettings) {
    try {
      const settings = JSON.parse(savedSettings)
      currentTheme.value = settings.theme || 'dark'
      gridSize.value = settings.gridSize || 20
      snapToGrid.value = settings.snapToGrid ?? true
    } catch (e) {
      console.error('Failed to load settings:', e)
    }
  }
}

function saveSettings() {
  const settings = {
    language: currentLanguage.value,
    theme: currentTheme.value,
    gridSize: gridSize.value,
    snapToGrid: snapToGrid.value
  }
  
  localStorage.setItem('preferences', JSON.stringify(settings))
  console.log('Settings saved:', settings)
}

function resetSettings() {
  currentTheme.value = 'dark'
  gridSize.value = 20
  snapToGrid.value = true
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
}

.preferences-container {
  width: 100%;
  max-width: 600px;
  background: #252526;
  border-radius: 8px;
  padding: 24px;
}

h1 {
  margin: 0 0 24px;
  font-size: 24px;
  color: #ffffff;
}

.settings-section {
  margin-bottom: 32px;
}

.settings-section h2 {
  margin: 0 0 16px;
  font-size: 16px;
  font-weight: 600;
  color: #cccccc;
  text-transform: uppercase;
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.setting-item label {
  color: #ffffff;
  font-size: 14px;
}

.setting-item input[type="text"],
.setting-item input[type="number"],
.setting-item select {
  padding: 6px 12px;
  background: #3c3c3c;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  color: #ffffff;
  font-size: 14px;
  min-width: 200px;
}

.setting-item input[type="checkbox"] {
  width: 20px;
  height: 20px;
}

.settings-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}

.settings-actions button {
  padding: 8px 24px;
  background: #0e639c;
  border: none;
  border-radius: 4px;
  color: #ffffff;
  font-size: 14px;
  cursor: pointer;
}

.settings-actions button:hover {
  background: #1177bb;
}

.settings-actions button:last-child {
  background: #3c3c3c;
}

.settings-actions button:last-child:hover {
  background: #4c4c4c;
}
</style>
