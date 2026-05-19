<template>
  <div class="console-panel">
    <div class="panel-header">
      <div class="header-left">
        <h3>{{ t('panels.console') }}</h3>
      </div>
      <div class="header-right">
        <button class="console-button" @click="clearLogs" :title="t('console.clear')">
          Clear
        </button>
      </div>
    </div>
    <div class="panel-content" ref="consoleContent">
      <div 
        v-for="(log, index) in logs" 
        :key="index"
        class="log-entry"
        :class="log.type"
      >
        <span class="timestamp">{{ log.timestamp }}</span>
        <span class="message">{{ log.message }}</span>
      </div>
      <div v-if="logs.length === 0" class="empty-state">
        <p>{{ t('console.noLogs') }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()
const consoleContent = ref<HTMLElement | null>(null)

interface LogEntry {
  timestamp: string
  message: string
  type: 'info' | 'warning' | 'error' | 'success'
}

const logs = ref<LogEntry[]>([
  { timestamp: '00:00:00', message: 'Editor initialized', type: 'info' },
])

function clearLogs() {
  logs.value = []
}

function addLog(message: string, type: LogEntry['type'] = 'info') {
  const now = new Date()
  const timestamp = now.toLocaleTimeString('en-US', { hour12: false })
  logs.value.push({
    timestamp,
    message,
    type
  })
  
  // 自动滚动到底部
  nextTick(() => {
    if (consoleContent.value) {
      consoleContent.value.scrollTop = consoleContent.value.scrollHeight
    }
  })
}

// 生命周期
onMounted(() => {
  // 添加初始化日志
  addLog('Console panel ready', 'success')
})

onUnmounted(() => {
  // 清理工作
})
</script>

<style scoped>
.console-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #3e3e42;
  background: #2d2d30;
}

.header-left h3 {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  color: #ffffff;
  text-transform: uppercase;
}

.header-right {
  display: flex;
  gap: 4px;
}

.console-button {
  padding: 4px 8px;
  background: transparent;
  border: 1px solid #3e3e42;
  border-radius: 4px;
  color: #cccccc;
  font-size: 11px;
  cursor: pointer;
}

.console-button:hover {
  background: rgba(255, 255, 255, 0.1);
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  font-family: monospace;
  font-size: 12px;
  background: #1e1e1e;
}

.log-entry {
  padding: 2px 0;
  color: #cccccc;
}

.log-entry.info {
  color: #cccccc;
}

.log-entry.success {
  color: #4ec9b0;
}

.log-entry.warning {
  color: #dcdcaa;
}

.log-entry.error {
  color: #f48771;
}

.timestamp {
  color: #808080;
  margin-right: 8px;
  font-size: 11px;
}

.message {
  word-break: break-word;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #808080;
  font-family: monospace;
}
</style>
