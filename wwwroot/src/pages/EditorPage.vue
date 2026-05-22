<template>
  <div class="editor-page">
    <AppLayout />
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import AppLayout from '@/components/layout/AppLayout.vue'
import { useUndoRedoStore } from '@/stores/useUndoRedoStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'

const undoRedoStore = useUndoRedoStore()
const nodeGraphStore = useNodeGraphStore()

onMounted(() => {
  undoRedoStore.initializeWithState({
    nodes: nodeGraphStore.nodes,
    connections: nodeGraphStore.connections
  })
})
</script>

<style scoped>
.editor-page {
  width: 100%;
  height: 100vh;
  overflow: hidden;
}
</style>
