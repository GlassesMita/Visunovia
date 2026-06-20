import { computed, ref } from 'vue'
import { settingsApi } from '@/api'

export type ThemeMode = 'light' | 'dark' | 'system'
export type ResolvedThemeMode = 'light' | 'dark'
export type ThemeStyle = 'material-you' | 'windows-11' | 'windows-10'
export type MaterialYouPalette = 'blue' | 'purple' | 'green' | 'orange' | 'pink' | 'teal'

export interface MaterialYouThemeSettings {
  theme: ThemeMode
  themeStyle: ThemeStyle
  seedColor: string
  palette: MaterialYouPalette
}

type PersistedSettings = Record<string, any> & {
  theme?: ThemeMode
  Theme?: ThemeMode
  themeStyle?: ThemeStyle
  ThemeStyle?: ThemeStyle
  seedColor?: string
  SeedColor?: string
  palette?: MaterialYouPalette
  Palette?: MaterialYouPalette
}

export interface MaterialYouPaletteOption {
  value: MaterialYouPalette
  label: string
  seedColor: string
}

export interface ThemeStyleOption {
  value: ThemeStyle
  label: string
  description: string
}

const SETTINGS_STORAGE_KEY = 'visunovia-settings'
const DEFAULT_THEME: ThemeMode = 'dark'
const DEFAULT_THEME_STYLE: ThemeStyle = 'material-you'
const DEFAULT_PALETTE: MaterialYouPalette = 'blue'
const DEFAULT_SEED_COLOR = '#6750a4'

export const themeStyleOptions: ThemeStyleOption[] = [
  { value: 'material-you', label: 'Material You 3', description: 'Dynamic color, large rounded surfaces, soft elevation.' },
  { value: 'windows-11', label: 'Windows 11', description: 'Mica-like surfaces, Fluent rounded corners, subtle shadows.' },
  { value: 'windows-10', label: 'Windows 10', description: 'Sharper Fluent panels, compact controls, flatter elevation.' },
]

export const materialYouPaletteOptions: MaterialYouPaletteOption[] = [
  { value: 'blue', label: 'Blue', seedColor: '#006a6a' },
  { value: 'purple', label: 'Purple', seedColor: '#6750a4' },
  { value: 'green', label: 'Green', seedColor: '#386a20' },
  { value: 'orange', label: 'Orange', seedColor: '#8b5000' },
  { value: 'pink', label: 'Pink', seedColor: '#984061' },
  { value: 'teal', label: 'Teal', seedColor: '#006874' },
]

const mediaQuery = typeof window !== 'undefined'
  ? window.matchMedia('(prefers-color-scheme: dark)')
  : null

const currentTheme = ref<ThemeMode>(readLocalTheme())
const currentThemeStyle = ref<ThemeStyle>(readLocalThemeStyle())
const currentSeedColor = ref<string>(readLocalSeedColor())
const currentPalette = ref<MaterialYouPalette>(readLocalPalette())
const systemTheme = ref<ResolvedThemeMode>(mediaQuery?.matches ? 'dark' : 'light')

export const resolvedTheme = computed<ResolvedThemeMode>(() => (
  currentTheme.value === 'system' ? systemTheme.value : currentTheme.value
))

function normalizeTheme(value: unknown): ThemeMode | null {
  const theme = String(value || '').toLowerCase()
  return theme === 'light' || theme === 'dark' || theme === 'system' ? theme : null
}

function normalizeThemeStyle(value: unknown): ThemeStyle | null {
  const style = String(value || '').toLowerCase() as ThemeStyle
  return themeStyleOptions.some((option) => option.value === style) ? style : null
}

function normalizePalette(value: unknown): MaterialYouPalette | null {
  const palette = String(value || '').toLowerCase() as MaterialYouPalette
  return materialYouPaletteOptions.some((option) => option.value === palette) ? palette : null
}

function normalizeColor(value: unknown): string | null {
  const color = String(value || '').trim()
  return /^#[0-9a-f]{6}$/i.test(color) ? color.toLowerCase() : null
}

function readLocalSettings(): PersistedSettings {
  try {
    return JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY) || '{}') as PersistedSettings
  } catch {
    return {}
  }
}

function writeLocalTheme(settings: MaterialYouThemeSettings) {
  const saved = readLocalSettings()
  localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify({
    ...saved,
    theme: settings.theme,
    Theme: settings.theme,
    themeStyle: settings.themeStyle,
    ThemeStyle: settings.themeStyle,
    seedColor: settings.seedColor,
    SeedColor: settings.seedColor,
    palette: settings.palette,
    Palette: settings.palette,
  }))
}

function readLocalTheme(): ThemeMode {
  const settings = readLocalSettings()
  return normalizeTheme(settings.theme || settings.Theme) || DEFAULT_THEME
}

function readLocalThemeStyle(): ThemeStyle {
  const settings = readLocalSettings()
  return normalizeThemeStyle(settings.themeStyle || settings.ThemeStyle) || DEFAULT_THEME_STYLE
}

function readLocalSeedColor(): string {
  const settings = readLocalSettings()
  return normalizeColor(settings.seedColor || settings.SeedColor) || DEFAULT_SEED_COLOR
}

function readLocalPalette(): MaterialYouPalette {
  const settings = readLocalSettings()
  return normalizePalette(settings.palette || settings.Palette) || DEFAULT_PALETTE
}

function extractServerSettings(response: any): Partial<MaterialYouThemeSettings> {
  const settings = response?.data?.data?.settings || response?.data?.settings || response?.data
  return {
    theme: normalizeTheme(settings?.Theme || settings?.theme) || undefined,
    themeStyle: normalizeThemeStyle(settings?.ThemeStyle || settings?.themeStyle) || undefined,
    seedColor: normalizeColor(settings?.SeedColor || settings?.seedColor) || undefined,
    palette: normalizePalette(settings?.Palette || settings?.palette) || undefined,
  }
}

function hexToRgb(hex: string) {
  const normalized = normalizeColor(hex) || DEFAULT_SEED_COLOR
  return {
    r: parseInt(normalized.slice(1, 3), 16),
    g: parseInt(normalized.slice(3, 5), 16),
    b: parseInt(normalized.slice(5, 7), 16),
  }
}

function rgbToHex(r: number, g: number, b: number) {
  const toHex = (value: number) => Math.round(Math.max(0, Math.min(255, value))).toString(16).padStart(2, '0')
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`
}

function mix(hex: string, target: string, amount: number) {
  const from = hexToRgb(hex)
  const to = hexToRgb(target)
  return rgbToHex(
    from.r + (to.r - from.r) * amount,
    from.g + (to.g - from.g) * amount,
    from.b + (to.b - from.b) * amount,
  )
}

function relativeLuminance(hex: string) {
  const { r, g, b } = hexToRgb(hex)
  const convert = (value: number) => {
    const channel = value / 255
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  }
  return 0.2126 * convert(r) + 0.7152 * convert(g) + 0.0722 * convert(b)
}

function onColor(hex: string) {
  return relativeLuminance(hex) > 0.45 ? '#111318' : '#ffffff'
}

function setCssVars(vars: Record<string, string>) {
  const root = document.documentElement
  Object.entries(vars).forEach(([key, value]) => root.style.setProperty(key, value))
}

function createPalette(seedColor: string, mode: ResolvedThemeMode) {
  if (mode === 'light') {
    const primary = mix(seedColor, '#000000', 0.12)
    const secondary = mix(seedColor, '#5f5b71', 0.66)
    const tertiary = mix(seedColor, '#7d5260', 0.58)
    return {
      '--md-sys-color-primary': primary,
      '--md-sys-color-on-primary': onColor(primary),
      '--md-sys-color-primary-container': mix(seedColor, '#ffffff', 0.78),
      '--md-sys-color-on-primary-container': mix(seedColor, '#000000', 0.68),
      '--md-sys-color-secondary': secondary,
      '--md-sys-color-on-secondary': onColor(secondary),
      '--md-sys-color-secondary-container': mix(secondary, '#ffffff', 0.78),
      '--md-sys-color-tertiary': tertiary,
      '--md-sys-color-on-tertiary': onColor(tertiary),
      '--md-sys-color-background': '#fffbff',
      '--md-sys-color-on-background': '#1c1b1f',
      '--md-sys-color-surface': '#fffbff',
      '--md-sys-color-surface-dim': '#ded8e1',
      '--md-sys-color-surface-bright': '#fffbff',
      '--md-sys-color-surface-container-lowest': '#ffffff',
      '--md-sys-color-surface-container-low': '#f7f2fa',
      '--md-sys-color-surface-container': '#f3edf7',
      '--md-sys-color-surface-container-high': '#ece6f0',
      '--md-sys-color-surface-container-highest': '#e6e0e9',
      '--md-sys-color-on-surface': '#1c1b1f',
      '--md-sys-color-on-surface-variant': '#49454f',
      '--md-sys-color-outline': '#79747e',
      '--md-sys-color-outline-variant': '#cac4d0',
      '--md-sys-color-error': '#ba1a1a',
      '--md-sys-color-on-error': '#ffffff',
      '--md-sys-color-error-container': '#ffdad6',
      '--md-sys-color-on-error-container': '#410002',
    }
  }

  const primary = mix(seedColor, '#ffffff', 0.52)
  const secondary = mix(seedColor, '#cac4d0', 0.68)
  const tertiary = mix(seedColor, '#efb8c8', 0.62)
  return {
    '--md-sys-color-primary': primary,
    '--md-sys-color-on-primary': '#1d192b',
    '--md-sys-color-primary-container': mix(seedColor, '#000000', 0.46),
    '--md-sys-color-on-primary-container': mix(seedColor, '#ffffff', 0.82),
    '--md-sys-color-secondary': secondary,
    '--md-sys-color-on-secondary': '#332d41',
    '--md-sys-color-secondary-container': mix(secondary, '#000000', 0.58),
    '--md-sys-color-tertiary': tertiary,
    '--md-sys-color-on-tertiary': '#492532',
    '--md-sys-color-background': '#141218',
    '--md-sys-color-on-background': '#e6e0e9',
    '--md-sys-color-surface': '#141218',
    '--md-sys-color-surface-dim': '#141218',
    '--md-sys-color-surface-bright': '#3b383e',
    '--md-sys-color-surface-container-lowest': '#0f0d13',
    '--md-sys-color-surface-container-low': '#1d1b20',
    '--md-sys-color-surface-container': '#211f26',
    '--md-sys-color-surface-container-high': '#2b2930',
    '--md-sys-color-surface-container-highest': '#36343b',
    '--md-sys-color-on-surface': '#e6e0e9',
    '--md-sys-color-on-surface-variant': '#cac4d0',
    '--md-sys-color-outline': '#938f99',
    '--md-sys-color-outline-variant': '#49454f',
    '--md-sys-color-error': '#ffb4ab',
    '--md-sys-color-on-error': '#690005',
    '--md-sys-color-error-container': '#93000a',
    '--md-sys-color-on-error-container': '#ffdad6',
  }
}

function createStyleVars(style: ThemeStyle, mode: ResolvedThemeMode, seedColor: string) {
  const accent = mode === 'light' ? mix(seedColor, '#000000', 0.08) : mix(seedColor, '#ffffff', 0.48)

  if (style === 'windows-11') {
    return mode === 'light'
      ? {
          '--md-sys-color-background': '#f3f3f3',
          '--md-sys-color-surface': '#fbfbfb',
          '--md-sys-color-surface-container-lowest': '#ffffff',
          '--md-sys-color-surface-container-low': '#f9f9f9',
          '--md-sys-color-surface-container': '#f6f6f6',
          '--md-sys-color-surface-container-high': '#ffffff',
          '--md-sys-color-surface-container-highest': '#f1f1f1',
          '--md-sys-color-outline-variant': '#e5e5e5',
          '--md-sys-color-primary': accent,
          '--md-sys-shape-corner-small': '6px',
          '--md-sys-shape-corner-medium': '8px',
          '--md-sys-shape-corner-large': '12px',
          '--md-sys-shape-corner-extra-large': '16px',
          '--md-sys-elevation-1': '0 1px 2px rgba(0, 0, 0, 0.08), 0 8px 24px rgba(0, 0, 0, 0.08)',
          '--md-sys-elevation-2': '0 4px 8px rgba(0, 0, 0, 0.10), 0 16px 40px rgba(0, 0, 0, 0.12)',
        }
      : {
          '--md-sys-color-background': '#202020',
          '--md-sys-color-surface': '#202020',
          '--md-sys-color-surface-container-lowest': '#171717',
          '--md-sys-color-surface-container-low': '#242424',
          '--md-sys-color-surface-container': '#2b2b2b',
          '--md-sys-color-surface-container-high': '#303030',
          '--md-sys-color-surface-container-highest': '#383838',
          '--md-sys-color-outline-variant': '#3f3f3f',
          '--md-sys-color-primary': accent,
          '--md-sys-shape-corner-small': '6px',
          '--md-sys-shape-corner-medium': '8px',
          '--md-sys-shape-corner-large': '12px',
          '--md-sys-shape-corner-extra-large': '16px',
          '--md-sys-elevation-1': '0 1px 2px rgba(0, 0, 0, 0.30), 0 8px 24px rgba(0, 0, 0, 0.24)',
          '--md-sys-elevation-2': '0 4px 8px rgba(0, 0, 0, 0.32), 0 16px 40px rgba(0, 0, 0, 0.28)',
        }
  }

  if (style === 'windows-10') {
    return mode === 'light'
      ? {
          '--md-sys-color-background': '#f0f0f0',
          '--md-sys-color-surface': '#ffffff',
          '--md-sys-color-surface-container-lowest': '#ffffff',
          '--md-sys-color-surface-container-low': '#fafafa',
          '--md-sys-color-surface-container': '#f4f4f4',
          '--md-sys-color-surface-container-high': '#ffffff',
          '--md-sys-color-surface-container-highest': '#e9e9e9',
          '--md-sys-color-outline-variant': '#d6d6d6',
          '--md-sys-color-primary': accent,
          '--md-sys-shape-corner-small': '2px',
          '--md-sys-shape-corner-medium': '2px',
          '--md-sys-shape-corner-large': '4px',
          '--md-sys-shape-corner-extra-large': '4px',
          '--md-sys-elevation-1': '0 1px 3px rgba(0, 0, 0, 0.18)',
          '--md-sys-elevation-2': '0 4px 12px rgba(0, 0, 0, 0.22)',
        }
      : {
          '--md-sys-color-background': '#171717',
          '--md-sys-color-surface': '#1f1f1f',
          '--md-sys-color-surface-container-lowest': '#111111',
          '--md-sys-color-surface-container-low': '#1c1c1c',
          '--md-sys-color-surface-container': '#242424',
          '--md-sys-color-surface-container-high': '#2c2c2c',
          '--md-sys-color-surface-container-highest': '#333333',
          '--md-sys-color-outline-variant': '#404040',
          '--md-sys-color-primary': accent,
          '--md-sys-shape-corner-small': '2px',
          '--md-sys-shape-corner-medium': '2px',
          '--md-sys-shape-corner-large': '4px',
          '--md-sys-shape-corner-extra-large': '4px',
          '--md-sys-elevation-1': '0 1px 3px rgba(0, 0, 0, 0.34)',
          '--md-sys-elevation-2': '0 4px 12px rgba(0, 0, 0, 0.38)',
        }
  }

  return {}
}

function syncSystemThemeListener() {
  if (!mediaQuery) return
  const update = () => {
    systemTheme.value = mediaQuery.matches ? 'dark' : 'light'
    if (currentTheme.value === 'system') applyTheme(currentTheme.value, currentSeedColor.value, currentPalette.value, currentThemeStyle.value)
  }
  mediaQuery.removeEventListener?.('change', update)
  mediaQuery.addEventListener?.('change', update)
}

export function getPaletteSeedColor(palette: MaterialYouPalette) {
  return materialYouPaletteOptions.find((option) => option.value === palette)?.seedColor || DEFAULT_SEED_COLOR
}

export function applyTheme(
  theme: ThemeMode,
  seedColor = currentSeedColor.value,
  palette = currentPalette.value,
  themeStyle = currentThemeStyle.value,
) {
  const normalizedTheme = normalizeTheme(theme) || DEFAULT_THEME
  const normalizedThemeStyle = normalizeThemeStyle(themeStyle) || DEFAULT_THEME_STYLE
  const normalizedSeed = normalizeColor(seedColor) || getPaletteSeedColor(palette) || DEFAULT_SEED_COLOR
  const normalizedPalette = normalizePalette(palette) || DEFAULT_PALETTE
  currentTheme.value = normalizedTheme
  currentThemeStyle.value = normalizedThemeStyle
  currentSeedColor.value = normalizedSeed
  currentPalette.value = normalizedPalette

  const resolved = normalizedTheme === 'system' ? systemTheme.value : normalizedTheme
  const vars = {
    ...createPalette(normalizedSeed, resolved),
    ...createStyleVars(normalizedThemeStyle, resolved, normalizedSeed),
  }
  setCssVars({
    ...vars,
    '--vn-bg': 'var(--md-sys-color-background)',
    '--vn-bg-elevated': 'var(--md-sys-color-surface-container)',
    '--vn-surface': 'var(--md-sys-color-surface-container-high)',
    '--vn-surface-muted': 'var(--md-sys-color-surface-container-highest)',
    '--vn-control-bg': 'var(--md-sys-color-surface-container-high)',
    '--vn-text': 'var(--md-sys-color-on-surface)',
    '--vn-text-soft': 'var(--md-sys-color-on-surface)',
    '--vn-text-muted': 'var(--md-sys-color-on-surface-variant)',
    '--vn-border': 'var(--md-sys-color-outline-variant)',
    '--vn-border-strong': 'var(--md-sys-color-outline)',
    '--vn-accent': 'var(--md-sys-color-primary)',
    '--vn-accent-hover': 'var(--md-sys-color-primary-container)',
  })

  document.documentElement.dataset.theme = resolved
  document.documentElement.dataset.themePreference = normalizedTheme
  document.documentElement.dataset.themeStyle = normalizedThemeStyle
  document.documentElement.dataset.materialYou = 'true'
  document.documentElement.style.colorScheme = resolved
}

export async function loadTheme() {
  const settings: MaterialYouThemeSettings = {
    theme: readLocalTheme(),
    themeStyle: readLocalThemeStyle(),
    seedColor: readLocalSeedColor(),
    palette: readLocalPalette(),
  }

  try {
    const serverSettings = extractServerSettings(await settingsApi.get())
    settings.theme = serverSettings.theme || settings.theme
    settings.themeStyle = serverSettings.themeStyle || settings.themeStyle
    settings.seedColor = serverSettings.seedColor || settings.seedColor
    settings.palette = serverSettings.palette || settings.palette
  } catch {
    // 使用本地缓存作为离线兜底
  }

  applyTheme(settings.theme, settings.seedColor, settings.palette, settings.themeStyle)
  writeLocalTheme(settings)
  return settings
}

export async function setTheme(settings: Partial<MaterialYouThemeSettings>, persist = true) {
  const nextSettings: MaterialYouThemeSettings = {
    theme: settings.theme || currentTheme.value,
    themeStyle: settings.themeStyle || currentThemeStyle.value,
    seedColor: settings.seedColor || currentSeedColor.value,
    palette: settings.palette || currentPalette.value,
  }

  applyTheme(nextSettings.theme, nextSettings.seedColor, nextSettings.palette, nextSettings.themeStyle)
  writeLocalTheme(nextSettings)

  if (!persist) return

  try {
    await settingsApi.saveSettings({
      Theme: nextSettings.theme,
      theme: nextSettings.theme,
      ThemeStyle: nextSettings.themeStyle,
      themeStyle: nextSettings.themeStyle,
      SeedColor: nextSettings.seedColor,
      seedColor: nextSettings.seedColor,
      Palette: nextSettings.palette,
      palette: nextSettings.palette,
    })
  } catch {
    // 后端不可用时保留本地持久化
  }
}

syncSystemThemeListener()
applyTheme(currentTheme.value, currentSeedColor.value, currentPalette.value, currentThemeStyle.value)

export function useMaterialYouTheme() {
  return {
    currentTheme,
    currentThemeStyle,
    currentSeedColor,
    currentPalette,
    resolvedTheme,
    paletteOptions: materialYouPaletteOptions,
    themeStyleOptions,
    applyTheme,
    loadTheme,
    setTheme,
    getPaletteSeedColor,
  }
}
