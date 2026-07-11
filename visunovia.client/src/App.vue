<template>
  <div id="app">
    <BackendConnectionModal />
    <ExtensionInterferenceModal />
    <div v-if="localizationStore.isLoading && isEditorRoute" class="loading-screen">
      <div class="loading-spinner"></div>
      <p>{{ t('app.loading') }}</p>
    </div>
    <RouterView v-else />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useLocalization } from '@/composables/useLocalization'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { useBackendConnectionMonitor } from '@/composables/useBackendConnectionMonitor'
import { useTheme } from '@/composables/useTheme'
import BackendConnectionModal from '@/components/BackendConnectionModal.vue'
import ExtensionInterferenceModal from '@/components/ExtensionInterferenceModal.vue'
const { t } = useLocalization()
const localizationStore = useLocalizationStore()
const route = useRoute()
const { startBackendHealthMonitor } = useBackendConnectionMonitor()
const { loadTheme } = useTheme()

// 编辑器页面需要等待本地化初始化，其他页面（Preferences/About 等 Popup）可直接渲染
const isEditorRoute = computed(() => route.path === '/' || route.name === 'editor')

onMounted(async () => {
  startBackendHealthMonitor()
  await loadTheme()

  try {
    await localizationStore.initialize()
  } catch (error) {
    console.error('Failed to initialize:', error)
  } finally {
    window.visunoviaDesktop?.notifyReady?.()
  }
})
</script>

<style scoped>
.loading-screen {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100vh;
  gap: 1rem;
  background: #1e1e1e;
  color: #ffffff;
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid #3e3e42;
  border-top-color: #007acc;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>
