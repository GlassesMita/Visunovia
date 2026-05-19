import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

export function useLocalization() {
  const { t, locale } = useI18n()
  const store = useLocalizationStore()
  
  const isLoading = computed(() => store.isLoading)
  const isReady = computed(() => store.isReady)
  const currentLanguage = computed(() => store.currentLanguage)
  
  async function changeLanguage(lang: 'en' | 'zh') {
    locale.value = lang
    await store.setLanguage(lang)
  }
  
  function translate(key: string, fallback?: string): string {
    return store.t(key) || fallback || key
  }
  
  return {
    t,
    locale,
    isLoading,
    isReady,
    currentLanguage,
    changeLanguage,
    translate,
  }
}
