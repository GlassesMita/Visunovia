import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'
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

  async function initialize() {
    if (isReady.value) return

    isLoading.value = true
    error.value = null

    try {
      // 并行加载语言列表和翻译
      await Promise.all([
        fetchAvailableLanguages(),
        translationService.initTranslations(),
      ])
      currentLanguage.value = translationService.getCurrentLanguage()
      isReady.value = true
    } catch (err: any) {
      console.error('[LocalizationStore] Initialization failed:', err)
      error.value = err.message || 'Failed to initialize localization'
    } finally {
      isLoading.value = false
    }
  }

  /** 从后端获取可用语言列表 */
  async function fetchAvailableLanguages() {
    try {
      const response = await axios.get('/api/localization/languages', { timeout: 5000 })
      if (response.data?.success && Array.isArray(response.data?.data)) {
        availableLanguages.value = response.data.data
      }
    } catch (err) {
      console.warn('[LocalizationStore] Failed to fetch language list:', err)
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
        await axios.post('/api/localization/language', { language: lang }, { timeout: 5000 })
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
