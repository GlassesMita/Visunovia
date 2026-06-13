<template>
  <Teleport to="body">
    <Transition name="node-details-fade">
      <div v-if="visible" class="node-details-overlay" @click.self="$emit('close')">
        <section class="node-details-dialog" role="dialog" aria-modal="true">
          <header class="node-details-header">
            <div>
              <h3>节点详情配置</h3>
              <p>在详情面板中编辑节点属性，画布节点仅保留连接端口。</p>
            </div>
            <button type="button" class="node-details-close" @click="$emit('close')">✕</button>
          </header>
          <div class="node-details-body">
            <InspectorPanel />
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import InspectorPanel from '@/components/panels/InspectorPanel.vue'

defineProps<{ visible: boolean }>()
defineEmits<{ close: [] }>()
</script>

<style scoped>
.node-details-overlay {
  position: fixed;
  inset: 0;
  z-index: 2147483646;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: rgba(0, 0, 0, 0.62);
}

.node-details-dialog {
  width: min(860px, 92vw);
  max-height: min(860px, 88vh);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 14px;
  background: #1f1f2d;
  box-shadow: 0 24px 80px rgba(0, 0, 0, 0.5);
}

.node-details-header {
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  color: #f0f0f0;
  background: #1f1f2d;
}

.node-details-header h3 {
  margin: 0 0 4px;
  font-size: 16px;
}

.node-details-header p {
  margin: 0;
  color: #aaa;
  font-size: 12px;
}

.node-details-close {
  border: 0;
  border-radius: 6px;
  padding: 6px 10px;
  color: #ddd;
  background: rgba(255, 255, 255, 0.08);
  cursor: pointer;
}

.node-details-close:hover {
  background: rgba(255, 255, 255, 0.16);
}

.node-details-body {
  min-height: 420px;
  flex: 1;
  overflow: auto;
}

.node-details-fade-enter-active,
.node-details-fade-leave-active {
  transition: opacity 0.16s ease;
}

.node-details-fade-enter-from,
.node-details-fade-leave-to {
  opacity: 0;
}
</style>