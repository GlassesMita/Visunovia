<template>
  <div class="status-bar">
    <div class="status-left">
      <span class="status-item">{{ statusMessage }}</span>
    </div>
    <div class="status-right">
      <span class="status-item">{{ currentLanguageLabel }}</span>
      <span class="status-item">{{ zoomLevel }}%</span>
      <span class="status-item">{{ nodeCount }} nodes</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'

const localizationStore = useLocalizationStore()
const nodeGraphStore = useNodeGraphStore()

const statusMessage = computed(() => 'Ready')
const currentLanguageLabel = computed(() => 
  localizationStore.currentLanguage === 'zh' ? '中文' : 'English'
)
const zoomLevel = computed(() => 100)
const nodeCount = computed(() => nodeGraphStore.nodes.length)
</script>

<style scoped>
.status-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 100%;
  padding: 0 12px;
  color: #ffffff;
  font-size: 12px;
}

.status-left,
.status-right {
  display: flex;
  gap: 16px;
}

.status-item {
  opacity: 0.9;
}
</style>
