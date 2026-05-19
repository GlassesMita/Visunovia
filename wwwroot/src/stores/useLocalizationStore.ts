import { defineStore } from 'pinia'
import { ref } from 'vue'
import axios from 'axios'
import { i18n } from '@/i18n'

export const useLocalizationStore = defineStore('localization', () => {
  const translations = ref<Record<string, string>>({})
  const currentLanguage = ref<'en' | 'zh'>('zh')
  const isLoading = ref(false)
  const isReady = ref(false)
  const error = ref<string | null>(null)

  async function initialize() {
    if (isReady.value) return
    
    const savedLang = localStorage.getItem('language') as 'en' | 'zh' | null
    const lang = savedLang || currentLanguage.value
    
    await loadTranslations(lang)
    isReady.value = true
  }

  async function loadTranslations(lang: 'en' | 'zh') {
    isLoading.value = true
    error.value = null
    
    try {
      const response = await axios.get(`/api/localization/${lang}`, {
        timeout: 5000
      })
      
      if (response.data && typeof response.data === 'object') {
        translations.value = response.data
      } else {
        console.warn('Backend returned invalid translations, using built-in translations')
        translations.value = {}
      }
      
      currentLanguage.value = lang
      localStorage.setItem('language', lang)
      
    } catch (err: any) {
      console.error('Failed to load translations from backend:', err)
      error.value = err.message || 'Failed to load translations'
      translations.value = {}
      currentLanguage.value = lang
    } finally {
      isLoading.value = false
    }
  }

  function t(key: string, fallback?: string): string {
    if (translations.value && translations.value[key]) {
      return translations.value[key]
    }
    
    try {
      const i18nValue = i18n.global.t(key)
      if (i18nValue !== key) {
        return i18nValue
      }
    } catch (e) {
      // i18n 尚未初始化，忽略
    }
    
    return fallback || key
  }

  async function setLanguage(lang: 'en' | 'zh') {
    await loadTranslations(lang)
    
    if (i18n.global.locale) {
      (i18n.global.locale as any).value = lang
    }
  }

  return {
    translations,
    currentLanguage,
    isLoading,
    isReady,
    error,
    initialize,
    loadTranslations,
    t,
    setLanguage
  }
})
