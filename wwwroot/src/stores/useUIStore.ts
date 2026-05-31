import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useUIStore = defineStore('ui', () => {
  const showProjectPanel = ref(true)
  const showInspectorPanel = ref(true)
  const showConsolePanel = ref(false)
  const showPreviewPanel = ref(false)
  const showHierarchyPanel = ref(true)
  const showProjectPopup = ref(false)
  const activePanel = ref<string | null>(null)
  const theme = ref<'light' | 'dark'>('dark')

  function togglePanel(panelName: string) {
    switch (panelName) {
      case 'project':
        showProjectPanel.value = !showProjectPanel.value
        break
      case 'inspector':
        showInspectorPanel.value = !showInspectorPanel.value
        break
      case 'console':
        showConsolePanel.value = !showConsolePanel.value
        break
      case 'preview':
        showPreviewPanel.value = !showPreviewPanel.value
        break
      case 'hierarchy':
        showHierarchyPanel.value = !showHierarchyPanel.value
        break
    }
  }

  function setActivePanel(panelName: string | null) {
    activePanel.value = panelName
  }

  function setTheme(newTheme: 'light' | 'dark') {
    theme.value = newTheme
  }

  function toggleTheme() {
    theme.value = theme.value === 'light' ? 'dark' : 'light'
  }

  function openProjectPopup() {
    showProjectPopup.value = true
  }

  function closeProjectPopup() {
    showProjectPopup.value = false
  }

  return {
    showProjectPanel,
    showInspectorPanel,
    showConsolePanel,
    showPreviewPanel,
    showHierarchyPanel,
    showProjectPopup,
    activePanel,
    theme,
    togglePanel,
    setActivePanel,
    setTheme,
    toggleTheme,
    openProjectPopup,
    closeProjectPopup
  }
})
