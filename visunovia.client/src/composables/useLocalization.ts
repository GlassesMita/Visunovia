import { computed } from 'vue'
import { useLocalizationStore } from '@/stores/useLocalizationStore'
import { tSync } from '@/services/translationService'

/**
 * 本地化 composable — 完全基于后端翻译
 *
 * 所有翻译来自后端 PO 文件。
 * - t(key): 同步翻译（从预加载缓存中读取）
 * - tAsync(key): 异步翻译（缓存未命中时调用后端 API）
 * - changeLanguage(lang): 切换语言并重新加载所有翻译
 */
export function useLocalization() {
  const store = useLocalizationStore()

  const isLoading = computed(() => store.isLoading)
  const isReady = computed(() => store.isReady)
  const currentLanguage = computed(() => store.currentLanguage)
  const availableLanguages = computed(() => store.availableLanguages)

  /**
   * 同步翻译 — 从本地缓存读取
   * 适用于模板渲染（必须在初始化完成后使用）
   * @param key - 翻译键
   * @param fallback - 缓存未命中时的回退文本
   */
  function t(key: string, fallback?: string): string {
    return tSync(key, fallback)
  }

  /**
   * 异步翻译 — 缓存未命中时调用后端 API
   * 适用于初始化前或动态场景
   */
  async function tAsync(key: string): Promise<string> {
    return store.t(key)
  }

  /**
   * 切换语言
   */
  async function changeLanguage(lang: string) {
    await store.setLanguage(lang)
  }

  return {
    t,
    tAsync,
    isLoading,
    isReady,
    currentLanguage,
    availableLanguages,
    changeLanguage,
  }
}
