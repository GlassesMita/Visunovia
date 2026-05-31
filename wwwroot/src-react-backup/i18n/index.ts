import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import zh from './translations/zh'
import en from './translations/en'

i18n.use(initReactI18next).init({
  resources: {
    zh: { translation: zh },
    en: { translation: en },
  },
  lng: localStorage.getItem('language') || 'zh',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

export default i18n
