import { createRouter, createWebHashHistory, createWebHistory } from 'vue-router'
import { useLocalizationStore } from '@/stores/useLocalizationStore'

function normalizeLegacyHashRoute() {
  if (window.location.protocol === 'visunovia:') {
    return
  }

  const { pathname, search, hash } = window.location

  if (!hash.startsWith('#/')) return

  const legacyPath = hash.slice(1)
  const normalizedPath = legacyPath === '/' ? pathname : legacyPath
  const normalizedUrl = `${normalizedPath}${search}`

  window.history.replaceState(window.history.state, '', normalizedUrl)
}

normalizeLegacyHashRoute()

const router = createRouter({
  history: window.location.protocol === 'visunovia:' ? createWebHashHistory() : createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'editor',
      component: () => import('@/pages/EditorPage.vue')
    },
    {
      path: '/Welcome',
      name: 'welcome',
      component: () => import('@/pages/WelcomePage.vue')
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
    },
    {
      path: '/About',
      name: 'about',
      alias: '/about',
      component: () => import('@/pages/AboutPage.vue')
    },
    {
      path: '/Console',
      name: 'console',
      alias: '/console',
      component: () => import('@/pages/ConsolePage.vue')
    }
  ]
})

router.beforeEach(async (_to, _from, next) => {
  // 所有页面（包括 Preferences/About 弹窗）都需要等待本地化初始化完成
  const localizationStore = useLocalizationStore()
  if (!localizationStore.isReady && !localizationStore.isLoading) {
    await localizationStore.initialize()
  }
  next()
})

export default router
