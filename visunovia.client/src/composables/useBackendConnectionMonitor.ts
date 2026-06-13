import { computed, onUnmounted, ref } from 'vue'
import axios from 'axios'

const CHECK_INTERVAL_MS = 30_000
const ERROR_MODAL_INTERVAL_MS = 10_000
const HEALTH_URL = '/api/system/health'

type BackendConnectionState = 'online' | 'offline'

const state = ref<BackendConnectionState>('online')
const visible = ref(false)
const lastError = ref('')
const lastStatus = ref<number | null>(null)
const failedAt = ref<Date | null>(null)
const lastCheckedAt = ref<Date | null>(null)
const retryCount = ref(0)

let healthTimer: ReturnType<typeof window.setInterval> | null = null
let modalTimer: ReturnType<typeof window.setInterval> | null = null
let checkInFlight = false

function formatError(error: unknown) {
  if (axios.isAxiosError(error)) {
    const status = error.response?.status
    const statusText = error.response?.statusText
    if (status) return `${status}${statusText ? ` ${statusText}` : ''}`
    return error.message || '无法连接到后端服务'
  }

  return error instanceof Error ? error.message : '无法连接到后端服务'
}

function stopModalTimer() {
  if (modalTimer !== null) {
    window.clearInterval(modalTimer)
    modalTimer = null
  }
}

function startModalTimer() {
  if (modalTimer !== null) return
  modalTimer = window.setInterval(() => {
    if (state.value === 'offline') {
      visible.value = true
    }
  }, ERROR_MODAL_INTERVAL_MS)
}

function markOnline() {
  state.value = 'online'
  visible.value = false
  lastError.value = ''
  lastStatus.value = null
  failedAt.value = null
  retryCount.value = 0
  stopModalTimer()
}

function markOffline(error: unknown) {
  const status = axios.isAxiosError(error) ? error.response?.status ?? null : null
  state.value = 'offline'
  visible.value = true
  lastError.value = formatError(error)
  lastStatus.value = status
  failedAt.value ||= new Date()
  retryCount.value += 1
  startModalTimer()
}

async function checkBackendHealth() {
  if (checkInFlight) return
  checkInFlight = true

  try {
    const response = await axios.get(HEALTH_URL, {
      timeout: 5_000,
      headers: { 'Cache-Control': 'no-store' },
      validateStatus: () => true,
    })
    lastCheckedAt.value = new Date()

    if (response.status >= 200 && response.status < 300) {
      markOnline()
    } else {
      markOffline({ response, message: `${response.status} ${response.statusText}` })
    }
  } catch (error) {
    lastCheckedAt.value = new Date()
    markOffline(error)
  } finally {
    checkInFlight = false
  }
}

function startBackendHealthMonitor() {
  if (healthTimer !== null) return
  healthTimer = window.setInterval(checkBackendHealth, CHECK_INTERVAL_MS)
}

function stopBackendHealthMonitor() {
  if (healthTimer !== null) {
    window.clearInterval(healthTimer)
    healthTimer = null
  }
  stopModalTimer()
}

function dismissBackendConnectionModal() {
  visible.value = false
}

function reportBackendRequestFailure(error: unknown) {
  markOffline(error)
}

function reportBackendRequestSuccess(status?: number) {
  if (status === undefined || (status >= 200 && status < 300)) {
    markOnline()
  }
}

export function useBackendConnectionMonitor() {
  onUnmounted(() => {
    // 仅在调用方组件卸载时停止由该组件启动的定时器；App 是单例根组件。
    stopBackendHealthMonitor()
  })

  return {
    state,
    visible,
    isOffline: computed(() => state.value === 'offline'),
    lastError,
    lastStatus,
    failedAt,
    lastCheckedAt,
    retryCount,
    startBackendHealthMonitor,
    stopBackendHealthMonitor,
    checkBackendHealth,
    dismissBackendConnectionModal,
  }
}

export {
  reportBackendRequestFailure,
  reportBackendRequestSuccess,
}
