<template>
  <Teleport to="body">
    <Transition name="backend-connection-slide">
      <div v-if="visible" class="backend-connection-overlay" role="presentation">
        <section class="backend-connection-modal" role="alertdialog" aria-live="assertive" aria-modal="false">
          <div class="backend-connection-icon">!</div>
          <div class="backend-connection-content">
            <h2>后端服务连接异常</h2>
            <p>前端不会自动退出。正在每 30 秒检查一次后端通讯状态；恢复为 HTTP 2xx 后此提示会自动关闭。</p>
            <div class="backend-connection-meta">
              <span v-if="lastError">错误：{{ lastError }}</span>
              <span v-if="lastCheckedAt">最近检查：{{ formatTime(lastCheckedAt) }}</span>
              <span v-if="retryCount > 0">失败次数：{{ retryCount }}</span>
            </div>
          </div>
          <div class="backend-connection-actions">
            <button type="button" @click="checkBackendHealth">立即重试</button>
            <button type="button" class="secondary" @click="dismissBackendConnectionModal">暂时隐藏</button>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
import { useBackendConnectionMonitor } from '@/composables/useBackendConnectionMonitor'

const {
  visible,
  lastError,
  lastCheckedAt,
  retryCount,
  checkBackendHealth,
  dismissBackendConnectionModal,
} = useBackendConnectionMonitor()

function formatTime(value: Date) {
  return value.toLocaleTimeString('zh-CN', { hour12: false })
}
</script>

<style scoped>
.backend-connection-overlay {
  position: fixed;
  inset: 0 0 auto 0;
  z-index: 2147483647;
  display: flex;
  justify-content: center;
  padding: 14px 18px;
  pointer-events: none;
}

.backend-connection-modal {
  width: min(980px, calc(100vw - 36px));
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 14px;
  align-items: center;
  padding: 14px 16px;
  border: 1px solid rgba(248, 113, 113, 0.58);
  border-radius: 14px;
  background: linear-gradient(135deg, rgba(127, 29, 29, 0.98), rgba(30, 41, 59, 0.98));
  color: #fff;
  box-shadow: 0 20px 70px rgba(0, 0, 0, 0.5), 0 0 0 1px rgba(255, 255, 255, 0.08) inset;
  pointer-events: auto;
}

.backend-connection-icon {
  width: 34px;
  height: 34px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: #fecaca;
  color: #7f1d1d;
  font-size: 22px;
  font-weight: 900;
}

.backend-connection-content h2 {
  margin: 0 0 4px;
  font-size: 16px;
  font-weight: 800;
}

.backend-connection-content p {
  margin: 0;
  color: #fee2e2;
  font-size: 13px;
  line-height: 1.45;
}

.backend-connection-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 14px;
  margin-top: 7px;
  color: #fecaca;
  font-size: 12px;
}

.backend-connection-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.backend-connection-actions button {
  border: 0;
  border-radius: 8px;
  padding: 8px 11px;
  color: #7f1d1d;
  background: #fee2e2;
  cursor: pointer;
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
}

.backend-connection-actions button:hover {
  background: #ffffff;
}

.backend-connection-actions .secondary {
  color: #f8fafc;
  background: rgba(255, 255, 255, 0.12);
}

.backend-connection-actions .secondary:hover {
  background: rgba(255, 255, 255, 0.2);
}

.backend-connection-slide-enter-active,
.backend-connection-slide-leave-active {
  transition: transform 0.22s ease, opacity 0.22s ease;
}

.backend-connection-slide-enter-from,
.backend-connection-slide-leave-to {
  opacity: 0;
  transform: translateY(-18px);
}

@media (max-width: 720px) {
  .backend-connection-modal {
    grid-template-columns: auto minmax(0, 1fr);
  }

  .backend-connection-actions {
    grid-column: 1 / -1;
    justify-content: flex-end;
  }
}
</style>
