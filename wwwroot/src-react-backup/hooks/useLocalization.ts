import { useTranslation } from 'react-i18next'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

export function useLocalization() {
  const { t, i18n } = useTranslation()
  const setCurrentLanguage = useLocalizationStore((s) => s.setCurrentLanguage)

  const changeLanguage = (lang: 'en' | 'zh') => {
    i18n.changeLanguage(lang)
    setCurrentLanguage(lang)
  }

  return { t, changeLanguage, currentLanguage: i18n.language as 'en' | 'zh' }
}
