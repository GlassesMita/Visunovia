<template>
  <div class="console-panel">
    <div class="panel-header">
      <div class="header-left">
        <h3>{{ t('panels.console') }}</h3>
        <span v-if="logs.length > 0" class="log-count">{{ logs.length }}</span>
      </div>
      <div class="header-right">
        <!-- 日志级别过滤 -->
        <div class="filter-group">
          <button 
            v-for="level in logLevels"
            :key="level.type"
            :class="['filter-btn', level.type, { active: !disabledLevels.has(level.type) }]"
            :title="level.label"
            @click="toggleLevel(level.type)"
          >
            {{ level.icon }}
          </button>
        </div>
        <button class="console-action-btn" :title="t('console.clear')" @click="clearLogs">
          🗑️
        </button>
      </div>
    </div>
    <div class="panel-content" ref="consoleContentRef">
      <div 
        v-for="(log, index) in filteredLogs" 
        :key="index"
        class="log-entry"
        :class="[log.type, { warning: log.type === 'warning', error: log.type === 'error', success: log.type === 'success' }]"
      >
        <span class="log-level-icon">{{ getLevelIcon(log.type) }}</span>
        <span class="timestamp">{{ log.timestamp }}</span>
        <span class="message">{{ log.message }}</span>
      </div>
      
      <!-- 空状态 -->
      <div v-if="filteredLogs.length === 0" class="empty-state">
        <p>{{ t('console.noLogs') || 'No logs' }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

type LogLevel = 'info' | 'warning' | 'error' | 'success'

interface LogEntry {
  timestamp: string
  message: string
  type: LogLevel
}

const { t } = useLocalization()
const consoleContentRef = ref<HTMLElement | null>(null)

// 日志数据
const logs = ref<LogEntry[]>([
  { timestamp: formatTime(), message: 'Editor initialized', type: 'info' },
  { timestamp: formatTime(), message: 'BaklavaJS engine loaded', type: 'success' },
])

// 日志过滤状态
const disabledLevels = ref(new Set<LogLevel>())

// 日志级别配置
const logLevels = [
  { type: 'info' as LogLevel, icon: 'ℹ', label: 'Info' },
  { type: 'warning' as LogLevel, icon: '⚠', label: 'Warning' },
  { type: 'error' as LogLevel, icon: '✕', label: 'Error' },
  { type: 'success' as LogLevel, icon: '✓', label: 'Success' },
]

// 过滤后的日志
const filteredLogs = computed(() => 
  logs.value.filter(log => !disabledLevels.value.has(log.type))
)

function clearLogs() {
  logs.value = []
}

function addLog(message: string, type: LogLevel = 'info') {
  logs.value.push({
    timestamp: formatTime(),
    message,
    type
  })
  
  // 自动滚动到底部
  nextTick(() => {
    if (consoleContentRef.value) {
      consoleContentRef.value.scrollTop = consoleContentRef.value.scrollHeight
    }
  })
}

function toggleLevel(level: LogLevel) {
  if (disabledLevels.value.has(level)) {
    disabledLevels.value.delete(level)
  } else {
    disabledLevels.value.add(level)
  }
}

function getLevelIcon(type: LogLevel): string {
  const icons: Record<LogLevel, string> = {
    info: 'ℹ',
    warning: '⚡',
    error: '✕',
    success: '✓',
  }
  return icons[type] || '•'
}

function formatTime(): string {
  const now = new Date()
  return now.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

// 初始化日志
onMounted(() => {
  addLog('Console panel ready', 'success')
})

// 暴露方法供外部调用
defineExpose({ addLog, clearLogs })
</script>

<style scoped>
.console-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #1e1e1e;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 10px;
  border-bottom: 1px solid #3e3e42;
  background: #252526;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.header-left h3 {
  margin: 0;
  font-size: 11px;
  font-weight: 600;
  color: #cccccc;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.log-count {
  font-size: 10px;
  color: #707070;
  background: #333333;
  padding: 1px 6px;
  border-radius: 8px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 6px;
}

.filter-group {
  display: flex;
  gap: 2px;
}

.filter-btn {
  width: 22px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 3px;
  font-size: 11px;
  cursor: pointer;
  opacity: 0.45;
  transition: all 0.15s;
}

.filter-btn:hover {
  opacity: 0.75;
}

.filter-btn.active {
  opacity: 1;
  border-color: #555555;
}

.filter-btn.info.active { color: #569cd6; }
.filter-btn.warning.active { color: #dcdcaa; }
.filter-btn.error.active { color: #f48771; }
.filter-btn.success.active { color: #4ec9b0; }

.console-action-btn {
  padding: 2px 6px;
  background: transparent;
  border: 1px solid #3e3e42;
  border-radius: 3px;
  color: #808080;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
}

.console-action-btn:hover {
  background: rgba(255, 255, 255, 0.05);
  color: #cccccc;
  border-color: #555555;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 6px 10px;
  font-family: 'Cascadia Code', 'Consolas', monospace;
  font-size: 11px;
  line-height: 1.6;
}

.log-entry {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  padding: 1px 0;
  color: #a0a0a0;
  word-break: break-word;
}

.log-entry.info { color: #a0a0a0; }
.log-entry.success { color: #4ec9b0; }
.log-entry.warning { color: #dcdcaa; }
.log-entry.error { color: #f48771; }

.log-level-icon {
  width: 14px;
  text-align: center;
  flex-shrink: 0;
  font-size: 10px;
}

.timestamp {
  color: #606060;
  flex-shrink: 0;
  font-size: 10px;
}

.message {
  flex: 1;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #505050;
  font-family: inherit;
}
</style>
