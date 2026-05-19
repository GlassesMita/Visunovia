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
      path: '/preferences',
      name: 'preferences',
      component: () => import('@/pages/PreferencesPage.vue')
    },
    {
      path: '/about',
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
