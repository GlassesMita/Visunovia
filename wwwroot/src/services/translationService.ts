import { ref, type Ref } from 'vue'
import { apiClient } from '@/api'

/**
 * 前端翻译服务 — 完全替代 vue-i18n
 *
 * 所有翻译通过后端 API /api/currentLang?msgId= 获取。
 * 本地维护一个内存缓存，切换语言时清空缓存。
 * 批量预加载接口用于初始化时一次性获取所有翻译。
 *
 * 响应式机制：使用 Vue ref 作为版本号，缓存更新时递增，
 * 使 useLocalization().t() 成为响应式计算属性。
 */

type TranslationCache = Map<string, string>

let cache: TranslationCache = new Map()
let currentLang = 'zh-CN'
let isReady = false
let initPromise: Promise<void> | null = null

/**
 * 翻译版本号 — 每次缓存更新时递增
 * 用于触发 Vue 响应式更新
 */
export const translationVersion: Ref<number> = ref(0)

/**
 * 获取单条翻译
 * 优先从缓存读取，缓存未命中则调用后端 API
 */
export async function t(msgId: string): Promise<string> {
  if (cache.has(msgId)) {
    return cache.get(msgId)!
  }
  const lowerMsgId = msgId.toLowerCase()
  if (cache.has(lowerMsgId)) {
    return cache.get(lowerMsgId)!
  }

  try {
    const response = await apiClient.get('/currentLang', {
      params: { msgId },
      timeout: 5000,
      responseType: 'text',
    })
    const translation = typeof response.data === 'string' ? response.data : String(response.data)
    cache.set(lowerMsgId, translation)
    return translation
  } catch {
    cache.set(lowerMsgId, msgId)
    return msgId
  }
}

/**
 * 同步获取翻译（仅从缓存，不发起网络请求）
 * 适用于模板渲染等同步场景
 * @param msgId - 翻译键
 * @param fallback - 缓存未命中时的回退文本，默认返回 msgId 本身
 */
export function tSync(msgId: string, fallback?: string): string {
  if (cache.has(msgId)) {
    return cache.get(msgId)!
  }
  const lowerMsgId = msgId.toLowerCase()
  if (cache.has(lowerMsgId)) {
    return cache.get(lowerMsgId)!
  }
  return fallback ?? msgId
}

/**
 * 批量预加载翻译
 * 在应用初始化或语言切换时调用，一次性从后端获取所有翻译并缓存
 */
export async function preloadTranslations(lang: string): Promise<void> {
  try {
    const response = await apiClient.get('/localization/translations', {
      params: { lang },
      timeout: 15000,
    })

    if (response.data?.success && response.data?.data?.translations) {
      const translations = response.data.data.translations as Record<string, string>
      cache.clear()
      for (const [key, value] of Object.entries(translations)) {
        cache.set(key, value)
      }
      // 触发 Vue 响应式更新
      translationVersion.value++
      console.log(`[TranslationService] Loaded ${cache.size} translations, version: ${translationVersion.value}`)
    } else {
      console.warn('[TranslationService] Response structure mismatch:', {
        success: response.data?.success,
        dataType: typeof response.data?.data,
        translationsType: typeof response.data?.data?.translations,
      })
    }
  } catch (err) {
    console.error('[TranslationService] Failed to preload translations:', err)
    throw err
  }
}

/**
 * 切换语言 — 清空缓存并重新加载
 */
export async function setLanguage(lang: string): Promise<void> {
  currentLang = lang
  localStorage.setItem('language', lang)
  await preloadTranslations(lang)
  isReady = true
}

/**
 * 获取当前语言
 */
export function getCurrentLanguage(): string {
  return currentLang
}

/**
 * 检查是否已加载
 */
export function getIsReady(): boolean {
  return isReady
}

/**
 * 初始化翻译服务（带并发保护）
 * 防止多个组件同时调用导致重复请求
 */
export async function initTranslations(): Promise<void> {
  // 如果已经初始化完成，直接返回
  if (isReady) return

  // 如果正在初始化，等待现有的初始化完成
  if (initPromise) {
    await initPromise
    return
  }

  // 创建新的初始化 Promise
  initPromise = (async () => {
    const savedLang = localStorage.getItem('language') || 'zh-CN'
    currentLang = savedLang

    // 先通知后端切换语言
    try {
      await apiClient.post('/localization/language', { language: savedLang }, { timeout: 5000 })
    } catch {
      // 后端不可用时继续使用本地缓存
    }

    await preloadTranslations(savedLang)
    isReady = true
  })()

  try {
    await initPromise
  } finally {
    initPromise = null
  }
}
