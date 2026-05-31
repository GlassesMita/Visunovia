import { create } from 'zustand'

interface LocalizationState {
  currentLanguage: 'en' | 'zh'
  setCurrentLanguage: (lang: 'en' | 'zh') => void
}

export const useLocalizationStore = create<LocalizationState>((set) => ({
  currentLanguage: (localStorage.getItem('language') as 'en' | 'zh') || 'zh',
  setCurrentLanguage: (lang) => {
    localStorage.setItem('language', lang)
    set({ currentLanguage: lang })
  },
}))
