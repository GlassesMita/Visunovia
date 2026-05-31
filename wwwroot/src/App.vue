<template>
  <div id="app">
    <RouterView />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

const localizationStore = useLocalizationStore()

onMounted(async () => {
  try {
    await localizationStore.initialize()
  } catch (error) {
    console.error('[App] Initialization failed:', error)
  }
})
</script>

<style>
html, body, #app {
  margin: 0;
  padding: 0;
  width: 100%;
  height: 100%;
  overflow: hidden;
}
</style>
