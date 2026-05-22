<template>
  <div id="app">
    <div v-if="!isReady" class="loading-screen">
      <div class="loading-spinner"></div>
      <p>{{ t('app.loading') }}</p>
    </div>
    <AppLayout v-else />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import AppLayout from '@/components/layout/AppLayout.vue'

const { t } = useLocalization()
const localizationStore = useLocalizationStore()
const isReady = ref(false)

onMounted(async () => {
  try {
    await localizationStore.initialize()
    isReady.value = true
  } catch (error) {
    console.error('Failed to initialize:', error)
    isReady.value = true
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
