import { createRouter, createWebHistory } from 'vue-router'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

const router = createRouter({
  // 使用 HTML5 History 模式，与后端路由配置一致
  // 后端已配置 SPA 回退，所有非 API 请求都会返回 index.html
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

/**
 * 全局路由守卫
 * 在导航到编辑器页面时确保本地化服务已初始化
 */
router.beforeEach(async (to, _from, next) => {
  // 仅编辑器主页面需要等待本地化初始化
  // Preferences/About 等 Popup 页面使用内置 i18n 兜底，无需等待后端
  if (to.name === 'editor' || to.path === '/') {
    const localizationStore = useLocalizationStore()
    if (!localizationStore.isReady && !localizationStore.isLoading) {
      try {
        await localizationStore.initialize()
      } catch (err) {
        console.warn('[Router] Localization initialization failed:', err)
        // 继续导航，不阻塞路由
      }
    }
  }
  next()
})

export default router
