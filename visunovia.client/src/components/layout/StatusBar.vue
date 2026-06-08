<template>
  <div class="status-bar">
    <div class="status-left">
      <span v-if="editorStore.isModified" class="status-item modified">
        ● {{ t('common.modified') || 'Modified' }}
      </span>
      <span class="status-item">{{ statusMessage }}</span>
      <span v-if="editorStore.error" class="status-item error">
        ⚠ {{ editorStore.error }}
      </span>
    </div>
    <div class="status-right">
      <span class="status-item clickable" :title="t('settings.language')" @click="toggleLanguage">
        🌐 {{ currentLanguageLabel }}
      </span>
      <span class="status-item">{{ zoomLevel }}%</span>
      <span class="status-item" title="Node count">
        🔷 {{ nodeCount }} {{ t('common.nodes') || 'nodes' }}
      </span>
      <span v-if="connectionCount > 0" class="status-item" title="Connection count">
        🔗 {{ connectionCount }}
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useEditorStore } from '@/stores/useEditorStore'

const { t, currentLanguage, changeLanguage } = useLocalization()
const nodeGraphStore = useNodeGraphStore()
const editorStore = useEditorStore()

const statusMessage = computed(() => {
  if (editorStore.isLoading) return t('app.loading') || 'Loading...'
  if (editorStore.error) return ''
  return t('status.ready') || 'Ready'
})

const currentLanguageLabel = computed(() => {
  const lang = currentLanguage.value
  return lang.startsWith('zh') ? '中文' : lang.startsWith('ja') ? '日本語' : 'EN'
})

const zoomLevel = computed(() => 100)
const nodeCount = computed(() => nodeGraphStore.nodeCount)
const connectionCount = computed(() => nodeGraphStore.connectionCount)

async function toggleLanguage() {
  const current = currentLanguage.value
  const newLang = current.startsWith('zh') ? 'en-US' : 'zh-CN'
  await changeLanguage(newLang)
}
</script>

<style scoped>
.status-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 100%;
  padding: 0 16px;
  color: #ffffff;
  font-size: 12px;
  flex: 1;
}

.status-left,
.status-right {
  display: flex;
  align-items: center;
  gap: 20px;
}

.status-item {
  opacity: 0.9;
  white-space: nowrap;
}

.status-item.modified {
  color: #e8d4a8;
}

.status-item.error {
  color: #f48771;
}

.status-item.clickable {
  cursor: pointer;
  padding: 1px 6px;
  border-radius: 3px;
  transition: background 0.15s;
}

.status-item.clickable:hover {
  background: rgba(255, 255, 255, 0.15);
}
</style>
