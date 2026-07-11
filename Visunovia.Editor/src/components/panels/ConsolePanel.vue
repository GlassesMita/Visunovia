<template>
  <div class="console-panel" @click="closeContextMenu">
    <div class="panel-header">
      <div class="header-left">
        <h3>{{ t('panels.console') }}</h3>
        <span v-if="logs.length > 0" class="log-count">{{ logs.length }}</span>
      </div>
      <div class="header-right">
        <div class="filter-group">
          <button 
            v-for="level in logLevels"
            :key="level.type"
            :class="['filter-btn', level.type, { active: !disabledLevels.has(level.type) }]"
            :title="level.label"
            @click="toggleLevel(level.type)"
          >
            <component :is="level.icon" :size="13" />
          </button>
        </div>
        <button v-if="!standalone" class="console-action-btn" title="在新窗口打开" @click="openConsoleWindow">
          <ExternalLink :size="14" />
        </button>
        <button class="console-action-btn" title="复制选中内容；无选区时复制全部可见日志" @click="copyLogs">
          <Copy :size="14" />
        </button>
        <button class="console-action-btn" :title="t('console.clear')" @click="clearLogs">
          <Trash2 :size="14" />
        </button>
      </div>
    </div>
    <div class="panel-content" ref="consoleContentRef">
      <div 
        v-for="(log, index) in filteredLogs" 
        :key="index"
        class="log-entry"
        :class="[log.level, { 'context-target': contextMenu?.log === log }]"
        @contextmenu.prevent.stop="openContextMenu($event, log)"
      >
        <span class="log-level-icon"><component :is="getLevelIcon(log.level)" :size="13" /></span>
        <span class="timestamp">{{ formatTime(log.timestamp) }}</span>
        <span class="source">{{ log.source }}</span>
        <span class="message">{{ log.message }}</span>
      </div>
      
      <div v-if="filteredLogs.length === 0" class="empty-state">
        <p>{{ t('console.noLogs') || 'No logs' }}</p>
      </div>
    </div>

    <Teleport to="body">
      <div
        v-if="contextMenu"
        ref="contextMenuRef"
        class="console-context-menu"
        :style="{ left: `${contextMenu.x}px`, top: `${contextMenu.y}px` }"
        role="menu"
        @click.stop
        @contextmenu.prevent
      >
        <button type="button" role="menuitem" :disabled="!contextMenu.selectedText" @click="copySelectedText">
          <Copy :size="14" />
          <span>复制选中内容</span>
        </button>
        <button type="button" role="menuitem" :disabled="logs.length === 0" @click="copyAllLogs">
          <Copy :size="14" />
          <span>复制全部控制台内容</span>
        </button>
        <div class="context-menu-divider"></div>
        <button type="button" role="menuitem" @click="copyContextEntry">
          <Copy :size="14" />
          <span>复制当前条目</span>
        </button>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onBeforeUnmount, onMounted, watch } from 'vue'
import { AlertTriangle, Bug, CheckCircle, Copy, ExternalLink, Info, Trash2, XCircle } from 'lucide-vue-next'
import { useLocalization } from '@/composables/useLocalization'
import { useEditorConsole } from '@/services/editorConsole'
import { resolveAppRoute } from '@/utils/appRoutes'
import type { EditorConsoleEntry } from '@/api/backendProvider'

type LogLevel = EditorConsoleEntry['level']

defineProps<{ standalone?: boolean }>()

const { t } = useLocalization()
const { entries: logs, clear: clearLogs } = useEditorConsole()
const consoleContentRef = ref<HTMLElement | null>(null)
const contextMenuRef = ref<HTMLElement | null>(null)
const disabledLevels = ref(new Set<LogLevel>())
const contextMenu = ref<{
  x: number
  y: number
  log: EditorConsoleEntry
  selectedText: string
} | null>(null)

const logLevels = [
  { type: 'debug' as LogLevel, icon: Bug, label: 'Debug' },
  { type: 'info' as LogLevel, icon: Info, label: 'Info' },
  { type: 'warning' as LogLevel, icon: AlertTriangle, label: 'Warning' },
  { type: 'error' as LogLevel, icon: XCircle, label: 'Error' },
  { type: 'success' as LogLevel, icon: CheckCircle, label: 'Success' },
]

const filteredLogs = computed(() => 
  logs.value.filter(log => !disabledLevels.value.has(log.level))
)

watch(() => logs.value.length, () => {
  nextTick(() => {
    if (consoleContentRef.value) {
      consoleContentRef.value.scrollTop = consoleContentRef.value.scrollHeight
    }
  })
})

function toggleLevel(level: LogLevel) {
  if (disabledLevels.value.has(level)) {
    disabledLevels.value.delete(level)
  } else {
    disabledLevels.value.add(level)
  }
}

function getLevelIcon(level: LogLevel) {
  const icons = {
    debug: Bug,
    info: Info,
    warning: AlertTriangle,
    error: XCircle,
    success: CheckCircle,
  }
  return icons[level]
}

function formatTime(timestamp: string): string {
  return new Date(timestamp).toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

function openConsoleWindow() {
  window.open(resolveAppRoute('/Console'), 'VisunoviaConsole', 'width=960,height=600,scrollbars=no,resizable=yes')
}

function getSelectedConsoleText() {
  const selection = window.getSelection()
  return selection && consoleContentRef.value?.contains(selection.anchorNode)
    ? selection.toString().trim()
    : ''
}

function formatLog(log: EditorConsoleEntry) {
  return `[${formatTime(log.timestamp)}] [${log.level.toUpperCase()}] [${log.source}] ${log.message}`
}

async function copyLogs() {
  const text = getSelectedConsoleText() || filteredLogs.value.map(formatLog).join('\n')
  await writeClipboard(text)
}

async function copySelectedText() {
  await writeClipboard(contextMenu.value?.selectedText || '')
  closeContextMenu()
}

async function copyAllLogs() {
  await writeClipboard(logs.value.map(formatLog).join('\n'))
  closeContextMenu()
}

async function copyContextEntry() {
  if (contextMenu.value) await writeClipboard(formatLog(contextMenu.value.log))
  closeContextMenu()
}

function openContextMenu(event: MouseEvent, log: EditorConsoleEntry) {
  const menuWidth = 230
  const menuHeight = 126
  contextMenu.value = {
    x: Math.max(6, Math.min(event.clientX, window.innerWidth - menuWidth - 6)),
    y: Math.max(6, Math.min(event.clientY, window.innerHeight - menuHeight - 6)),
    log,
    selectedText: getSelectedConsoleText(),
  }
}

function closeContextMenu() {
  contextMenu.value = null
}

function handleContextMenuKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') closeContextMenu()
}

async function writeClipboard(text: string) {

  if (!text) return

  try {
    await navigator.clipboard.writeText(text)
  } catch {
    const textArea = document.createElement('textarea')
    textArea.value = text
    textArea.style.position = 'fixed'
    textArea.style.opacity = '0'
    document.body.appendChild(textArea)
    textArea.select()
    document.execCommand('copy')
    textArea.remove()
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleContextMenuKeydown)
  window.addEventListener('blur', closeContextMenu)
  window.addEventListener('resize', closeContextMenu)
  consoleContentRef.value?.addEventListener('scroll', closeContextMenu)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleContextMenuKeydown)
  window.removeEventListener('blur', closeContextMenu)
  window.removeEventListener('resize', closeContextMenu)
  consoleContentRef.value?.removeEventListener('scroll', closeContextMenu)
})
</script>

<style scoped>
.console-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--md-sys-color-surface-container-low);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 6px 10px;
  border-bottom: 1px solid var(--md-sys-color-outline-variant);
  background: var(--md-sys-color-surface-container-high);
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
  color: var(--md-sys-color-on-surface);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.log-count {
  font-size: 10px;
  color: var(--md-sys-color-on-surface-variant);
  background: var(--md-sys-color-secondary-container);
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
  border-color: var(--md-sys-color-primary);
}

.filter-btn.info.active { color: #569cd6; }
.filter-btn.debug.active { color: #b5cea8; }
.filter-btn.warning.active { color: #dcdcaa; }
.filter-btn.error.active { color: #f48771; }
.filter-btn.success.active { color: #4ec9b0; }

.console-action-btn {
  width: 26px;
  height: 24px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0;
  background: transparent;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: 4px;
  color: var(--md-sys-color-on-surface-variant);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
}

.console-action-btn:hover {
  background: var(--md-sys-color-secondary-container);
  color: var(--md-sys-color-on-surface);
  border-color: var(--md-sys-color-primary);
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 6px 0;
  font-family: 'Cascadia Code', 'Consolas', monospace;
  font-size: 11px;
  line-height: 1.6;
  cursor: text;
  -webkit-user-select: text;
  user-select: text;
}

.panel-content * {
  -webkit-user-select: text;
  user-select: text;
}

.log-entry {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  min-height: 22px;
  padding: 2px 10px;
  color: #a0a0a0;
  word-break: break-word;
}

.log-entry:nth-child(odd) {
  background: color-mix(in srgb, var(--md-sys-color-surface-container-low) 90%, #ffffff 10%);
}

.log-entry:nth-child(even) {
  background: color-mix(in srgb, var(--md-sys-color-surface-container-high) 94%, #000000 6%);
}

.log-entry:hover,
.log-entry.context-target {
  background: var(--md-sys-color-secondary-container);
  outline: 1px solid color-mix(in srgb, var(--md-sys-color-primary) 55%, transparent);
  outline-offset: -1px;
}

.log-entry.info { color: #a0a0a0; }
.log-entry.debug { color: #b5cea8; }
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

.source {
  min-width: 88px;
  color: #7f9fba;
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

.console-context-menu {
  position: fixed;
  z-index: 10000;
  width: 230px;
  padding: 4px;
  border: 1px solid var(--md-sys-color-outline-variant);
  border-radius: 4px;
  background: var(--md-sys-color-surface-container-highest);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.34);
  color: var(--md-sys-color-on-surface);
  -webkit-user-select: none;
  user-select: none;
}

.console-context-menu button {
  width: 100%;
  height: 30px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 9px;
  border: 0;
  border-radius: 3px;
  background: transparent;
  color: inherit;
  font: inherit;
  font-size: 12px;
  text-align: left;
  cursor: pointer;
}

.console-context-menu button:hover:not(:disabled) {
  background: var(--md-sys-color-secondary-container);
  color: var(--md-sys-color-on-secondary-container);
}

.console-context-menu button:disabled {
  opacity: 0.42;
  cursor: default;
}

.context-menu-divider {
  height: 1px;
  margin: 4px 6px;
  background: var(--md-sys-color-outline-variant);
}
</style>
