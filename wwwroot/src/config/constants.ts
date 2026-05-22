// 项目常量定义

/**
 * 项目默认设置
 */
export const DEFAULT_PROJECT_NAME = 'Untitled Project'

/**
 * 默认分辨率
 */
export const DEFAULT_RESOLUTION = {
  width: 1920,
  height: 1080,
} as const

/**
 * 默认音量设置 (0-100)
 */
export const DEFAULT_VOLUME = {
  bgm: 80,
  voice: 100,
} as const

/**
 * 本地存储键名
 */
export const STORAGE_KEYS = {
  USER_SETTINGS: 'visunovia-settings',
  PROJECT_SETTINGS: 'visunovia-project-settings',
} as const

/**
 * 支持的语言
 */
export const SUPPORTED_LANGUAGES = ['zh', 'en'] as const

/**
 * 支持的主题
 */
export const SUPPORTED_THEMES = ['dark', 'light'] as const
