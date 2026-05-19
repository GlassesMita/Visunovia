<template>
  <div class="about-page">
    <h1>{{ t('app.title') }}</h1>
    <p>Version 0.0.1-alpha</p>
    <p>A visual novel editor powered by Vue 3 and BaklavaJS</p>
  </div>
</template>

<script setup lang="ts">
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()
</script>

<style scoped>
.about-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100vh;
  gap: 1rem;
  text-align: center;
}

h1 {
  font-size: 2rem;
  margin: 0;
}

p {
  color: #666;
  margin: 0;
}
</style>
