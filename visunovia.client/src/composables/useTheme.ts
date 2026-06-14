import { ref } from 'vue'
import { settingsApi } from '@/api'

export type ThemeMode = 'dark' | 'light'

type PersistedSettings = Record<string, any> & { theme?: ThemeMode; Theme?: ThemeMode }

const SETTINGS_STORAGE_KEY = 'visunovia-settings'
const DEFAULT_THEME: ThemeMode = 'dark'

const currentTheme = ref<ThemeMode>(readLocalTheme())

applyTheme(currentTheme.value)

function normalizeTheme(value: unknown): ThemeMode | null {
  const theme = String(value || '').toLowerCase()
  return theme === 'light' || theme === 'dark' ? theme : null
}

function readLocalSettings(): PersistedSettings {
  try {
    return JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY) || '{}') as PersistedSettings
  } catch {
    return {}
  }
}

function writeLocalTheme(theme: ThemeMode) {
  const settings = readLocalSettings()
  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify({
    ...settings,
    theme,
    Theme: theme,
  }))
}

function readLocalTheme(): ThemeMode {
  return normalizeTheme(readLocalSettings().theme || readLocalSettings().Theme) || DEFAULT_THEME
}

function extractServerTheme(response: any): ThemeMode | null {
  const settings = response?.data?.data?.settings || response?.data?.settings || response?.data
  return normalizeTheme(settings?.Theme || settings?.theme)
}

export function applyTheme(theme: ThemeMode) {
  currentTheme.value = theme
  document.documentElement.dataset.theme = theme
  document.documentElement.style.colorScheme = theme
}

export async function loadTheme() {
  let theme = readLocalTheme()

  try {
    const response = await settingsApi.get()
    theme = extractServerTheme(response) || theme
  } catch {
    // 使用本地缓存作为离线兜底
  }

  applyTheme(theme)
  writeLocalTheme(theme)
  return theme
}

export async function setTheme(theme: ThemeMode, persist = true) {
  applyTheme(theme)
  writeLocalTheme(theme)

  if (!persist) return

  try {
    await settingsApi.saveSettings({ Theme: theme, theme })
  } catch {
    // 后端不可用时保留本地持久化
  }
}

export function useTheme() {
  return {
    currentTheme,
    applyTheme,
    loadTheme,
    setTheme,
  }
}
