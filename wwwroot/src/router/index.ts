import { createRouter, createWebHashHistory } from 'vue-router'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

const router = createRouter({
  history: createWebHashHistory(),
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
    }
  ]
})

router.beforeEach(async (to, _from, next) => {
  // 仅编辑器主页面需要等待本地化初始化
  // Preferences/About 等 Popup 页面使用内置 i18n 兜底，无需等待后端
  if (to.name === 'editor' || to.path === '/') {
    const localizationStore = useLocalizationStore()
    if (!localizationStore.isReady && !localizationStore.isLoading) {
      await localizationStore.initialize()
    }
  }
  next()
})

export default router
