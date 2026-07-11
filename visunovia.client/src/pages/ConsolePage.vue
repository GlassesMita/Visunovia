<template>
  <main class="console-window">
    <ConsolePanel standalone />
  </main>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import ConsolePanel from '@/components/panels/ConsolePanel.vue'

onMounted(() => window.visunoviaDesktop?.setWindowTitle('Console - Visunovia'))
</script>

<style scoped>
.console-window {
  width: 100vw;
  height: 100vh;
  overflow: hidden;
  background: var(--md-sys-color-surface-container-low);
}
</style>