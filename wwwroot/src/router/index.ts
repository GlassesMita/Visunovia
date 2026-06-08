import { createRouter, createWebHistory } from 'vue-router'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

function normalizeLegacyHashRoute() {
  const { pathname, search, hash } = window.location

  if (!hash.startsWith('#/')) return

  const legacyPath = hash.slice(1)
  const normalizedPath = legacyPath === '/' ? pathname : legacyPath
  const normalizedUrl = `${normalizedPath}${search}`

  window.history.replaceState(window.history.state, '', normalizedUrl)
}

normalizeLegacyHashRoute()

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
      alias: '/preferences',
      component: () => import('@/pages/PreferencesPage.vue')
    },
    {
      path: '/ProjectSettings',
      name: 'project-settings',
      alias: '/project-settings',
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
