import { createRouter, createWebHistory } from 'vue-router'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'editor',
      component: () => import('@/pages/EditorPage.vue')
    },
    {
      path: '/Preferences',
      name: 'preferences',
      component: () => import('@/pages/PreferencesPage.vue')
    },
    {
      path: '/ProjectSettings',
      name: 'project-settings',
      component: () => import('@/pages/ProjectSettingsPage.vue')
    },
    {
      path: '/About',
      name: 'about',
      component: () => import('@/pages/AboutPage.vue')
    }
  ]
})

router.beforeEach(async (_to, _from, next) => {
  const localizationStore = useLocalizationStore()
  
  if (!localizationStore.isReady && !localizationStore.isLoading) {
    await localizationStore.initialize()
  }
  
  next()
})

export default router
