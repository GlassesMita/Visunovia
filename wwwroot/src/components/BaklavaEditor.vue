<template>
  <div class="baklava-editor-wrapper">
    <BaklavaEditor 
      v-if="isInitialized && baklava" 
      :view-model="baklava" 
    />
    <div v-else-if="isInitialized && !baklava" class="loading">
      <p>Failed to initialize editor</p>
    </div>
    <div v-else class="loading">
      <p>Initializing Editor...</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, shallowRef } from 'vue'
import { BaklavaEditor, useBaklava } from '@baklavajs/renderer-vue'
import { registerAllNodes } from '@/baklava/nodeRegistry'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { useConnectionColors } from '@/composables/useConnectionColors'
import { useFixNodeSelection } from '@/composables/useFixNodeSelection'
import StartNode from '@/components/baklava-nodes/StartNode'

const isInitialized = shallowRef(false)
const baklava = shallowRef<any>(null)
const localizationStore = useLocalizationStore()

onMounted(async () => {
  try {
    // 初始化本地化
    await localizationStore.initialize()

    // 使用 useBaklava hook
    const baklavaInstance = useBaklava()
    baklava.value = baklavaInstance

    // 注册所有节点
    registerAllNodes(baklavaInstance.editor)

    // 初始化连接线颜色系统
    useConnectionColors(baklavaInstance.editor)

    // 修复节点选中行为：单击清空其他选择，Ctrl+单击多选
    useFixNodeSelection(baklavaInstance.editor)
    
    // 将 editor 实例暴露到 window，供 useNodeOperations 和 useLorImport 使用
    ;(window as any).__editor = baklavaInstance.editor

    // 创建一个默认 Start 节点
    setTimeout(() => {
      if (baklavaInstance.editor && baklavaInstance.editor.graph.nodes.length === 0) {
        const startNode = new StartNode()
        startNode.position = { x: 250, y: 50 }
        baklavaInstance.editor.graph.addNode(startNode)
      }
    }, 100)
    
    isInitialized.value = true
  } catch (error) {
    console.error('Failed to initialize BaklavaJS:', error)
  }
})
</script>

<style scoped>
.baklava-editor-wrapper {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  background-color: #1e1e1e;
}

.loading {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #888;
}
</style>
