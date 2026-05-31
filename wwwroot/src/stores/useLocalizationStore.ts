import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/api'
import * as translationService from '@/services/translationService'
import type { LanguageInfo } from '@/types'

/**
 * 本地化状态管理 Store
 *
 * 所有翻译来自后端 PO 文件，通过 /api/currentLang?msgId= 获取。
 * 不再使用任何前端 TS 本地化文件或 vue-i18n。
 * 翻译结果在本地内存中缓存，切换语言时清空并重新加载。
 */
export const useLocalizationStore = defineStore('localization', () => {
  const currentLanguage = ref<string>('zh-CN')
  const availableLanguages = ref<LanguageInfo[]>([])
  const isLoading = ref(false)
  const isReady = ref(false)
  const error = ref<string | null>(null)
  let initPromise: Promise<void> | null = null

  /**
   * 初始化本地化服务
   * 使用 Promise 锁防止并发调用导致重复请求
   */
  async function initialize() {
    // 如果已经初始化完成，直接返回
    if (isReady.value) return

    // 如果正在初始化，等待现有的初始化完成
    if (initPromise) {
      await initPromise
      return
    }

    // 创建新的初始化 Promise
    initPromise = _doInitialize()

    try {
      await initPromise
    } finally {
      initPromise = null
    }
  }

  /**
   * 实际执行初始化的内部函数
   */
  async function _doInitialize() {
    isLoading.value = true
    error.value = null

    try {
      // 并行加载语言列表和翻译
      const results = await Promise.allSettled([
        fetchAvailableLanguages(),
        translationService.initTranslations(),
      ])

      const [languagesResult, translationsResult] = results

      // 处理语言列表加载失败的情况
      if (languagesResult.status === 'rejected') {
        console.warn('[LocalizationStore] Language list load failed:', languagesResult.reason)
      }

      // 处理翻译加载失败的情况
      if (translationsResult.status === 'rejected') {
        console.error('[LocalizationStore] Translation load failed:', translationsResult.reason)
        error.value = 'Failed to load translations'
      }

      currentLanguage.value = translationService.getCurrentLanguage()
      isReady.value = true
    } catch (err: any) {
      console.error('[LocalizationStore] Initialization failed:', err)
      error.value = err.message || 'Failed to initialize localization'
      // 即使失败也标记为 ready，避免无限阻塞 UI
      isReady.value = true
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 从后端获取可用语言列表
   * 使用统一的 apiClient 实例
   */
  async function fetchAvailableLanguages(): Promise<void> {
    try {
      const response = await apiClient.get('/localization/languages', { timeout: 5000 })
      if (response.data?.success && Array.isArray(response.data?.data)) {
        availableLanguages.value = response.data.data
      }
    } catch (err) {
      console.warn('[LocalizationStore] Failed to fetch language list:', err)
      throw err
    }
  }

  /**
   * 切换语言 — 通知后端切换，然后重新加载所有翻译到本地缓存
   */
  async function setLanguage(lang: string) {
    isLoading.value = true
    error.value = null

    try {
      // 通知后端切换语言（持久化偏好）
      try {
        await apiClient.post('/localization/language', { language: lang }, { timeout: 5000 })
      } catch {
        // 后端不可用时继续
      }

      // 重新加载翻译（清空缓存 + 批量获取）
      await translationService.setLanguage(lang)
      currentLanguage.value = lang
    } catch (err: any) {
      console.error('[LocalizationStore] Failed to set language:', err)
      error.value = err.message || 'Failed to change language'
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 翻译函数 — 异步版本
   * 优先从缓存读取，缓存未命中则调用后端 API
   */
  async function t(msgId: string): Promise<string> {
    return translationService.t(msgId)
  }

  /**
   * 翻译函数 — 同步版本（仅从缓存）
   * 用于模板中已预加载的场景
   */
  function tSync(msgId: string): string {
    return translationService.tSync(msgId)
  }

  return {
    currentLanguage,
    availableLanguages,
    isLoading,
    isReady,
    error,
    initialize,
    fetchAvailableLanguages,
    setLanguage,
    t,
    tSync,
  }
})
