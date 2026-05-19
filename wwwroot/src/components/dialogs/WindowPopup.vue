<template>
  <div
    class="window-popup"
    :style="{ width: width, height: height }"
  >
    <!-- 标题栏 -->
    <header class="window-popup-header">
      <h1 class="window-popup-title">{{ title }}</h1>
      <button
        class="window-popup-close-button"
        @click="handleClose"
        aria-label="关闭"
      >
        <X :size="18" />
      </button>
    </header>

    <!-- 内容区域 -->
    <main class="window-popup-content">
      <slot></slot>
    </main>

    <!-- 底部按钮栏 -->
    <footer
      v-if="showFooter && (onConfirm || onCancel)"
      class="window-popup-footer"
    >
      <button
        v-if="onCancel"
        class="cancel-button"
        @click="onCancel"
      >
        {{ cancelLabel }}
      </button>
      <button
        v-if="onConfirm"
        class="confirm-button"
        @click="onConfirm"
      >
        {{ confirmLabel }}
      </button>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { X } from 'lucide-vue-next'

interface Props {
  title: string
  onConfirm?: () => void
  onCancel?: () => void
  confirmLabel?: string
  cancelLabel?: string
  width?: string
  height?: string
  showFooter?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  confirmLabel: '确认',
  cancelLabel: '取消',
  showFooter: true
})

const handleClose = () => {
  if (props.onCancel) {
    props.onCancel()
  }
}
</script>

<style scoped>
.window-popup {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background-color: #111827;
  color: #f3f4f6;
  overflow: hidden;
}

.window-popup-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 24px;
  background-color: rgba(31, 41, 55, 0.5);
  border-bottom: 1px solid #374151;
  flex-shrink: 0;
}

.window-popup-title {
  font-size: 18px;
  font-weight: 600;
  color: #f3f4f6;
}

.window-popup-close-button {
  padding: 4px;
  color: #9ca3af;
  background: transparent;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: color 0.2s;
}

.window-popup-close-button:hover {
  color: #e5e7eb;
}

.window-popup-close-button:focus {
  outline: none;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.5);
}

.window-popup-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

.window-popup-footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
  padding: 12px 24px;
  background-color: rgba(31, 41, 55, 0.5);
  border-top: 1px solid #374151;
  flex-shrink: 0;
}

.cancel-button,
.confirm-button {
  padding: 8px 16px;
  font-size: 14px;
  font-weight: 500;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.cancel-button {
  color: #d1d5db;
  background-color: #374151;
}

.cancel-button:hover {
  background-color: #4b5563;
}

.confirm-button {
  color: #ffffff;
  background-color: #2563eb;
}

.confirm-button:hover {
  background-color: #3b82f6;
}

.cancel-button:focus,
.confirm-button:focus {
  outline: none;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.5);
}
</style>
