import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useUIStore = defineStore('ui', () => {
  const showProjectPanel = ref(true)
  const showInspectorPanel = ref(true)
  const showConsolePanel = ref(false)
  const showPreviewPanel = ref(false)
  const showHierarchyPanel = ref(true)
  const showProjectPopup = ref(false)
  const showFileExplorer = ref(false)
  const showNewProjectExplorer = ref(false)
  const showNewProjectModal = ref(false)
  const showProjectPreferences = ref(false)
  const showCharacterManager = ref(false)
  const showExpressionManager = ref(false)
  const showSceneManager = ref(false)
  const showPreviewPopup = ref(false)
  const showNodeDetailsModal = ref(false)
  const activePanel = ref<string | null>(null)
  const theme = ref<'light' | 'dark'>('dark')
  const openingFilePath = ref<string | null>(null)
  const showWelcomeModal = ref(false)
  const projectTreeRefreshToken = ref(0)
  const previewReloadToken = ref(0)

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

  function openFileExplorer() {
    showFileExplorer.value = true
  }

  function closeFileExplorer() {
    showFileExplorer.value = false
  }

  function openNewProjectExplorer() {
    showNewProjectExplorer.value = true
  }

  function closeNewProjectExplorer() {
    showNewProjectExplorer.value = false
  }

  function openNewProjectModal() {
    showNewProjectModal.value = true
  }

  function closeNewProjectModal() {
    showNewProjectModal.value = false
  }

  function openProjectPreferences() {
    showProjectPreferences.value = true
  }

  function closeProjectPreferences() {
    showProjectPreferences.value = false
  }

  function openCharacterManager() {
    showCharacterManager.value = true
  }

  function closeCharacterManager() {
    showCharacterManager.value = false
  }

  function openExpressionManager() {
    showExpressionManager.value = true
  }

  function closeExpressionManager() {
    showExpressionManager.value = false
  }

  function openSceneManager() {
    showSceneManager.value = true
  }

  function closeSceneManager() {
    showSceneManager.value = false
  }

  function openPreviewPopup() {
    showPreviewPopup.value = true
    previewReloadToken.value += 1
  }

  function closePreviewPopup() {
    showPreviewPopup.value = false
  }

  function openNodeDetailsModal() {
    showNodeDetailsModal.value = true
  }

  function closeNodeDetailsModal() {
    showNodeDetailsModal.value = false
  }

  function openFileByPath(path: string) {
    openingFilePath.value = path
  }

  function openWelcomeModal() {
    showWelcomeModal.value = true
  }

  function closeWelcomeModal() {
    showWelcomeModal.value = false
  }

  function refreshProjectTree() {
    projectTreeRefreshToken.value += 1
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
    closeProjectPopup,
    showFileExplorer,
    openFileExplorer,
    closeFileExplorer,
    showNewProjectExplorer,
    openNewProjectExplorer,
    closeNewProjectExplorer,
    showNewProjectModal,
    openNewProjectModal,
    closeNewProjectModal,
    showProjectPreferences,
    openProjectPreferences,
    closeProjectPreferences,
    showCharacterManager,
    openCharacterManager,
    closeCharacterManager,
    showExpressionManager,
    openExpressionManager,
    closeExpressionManager,
    showSceneManager,
    openSceneManager,
    closeSceneManager,
    showPreviewPopup,
    previewReloadToken,
    openPreviewPopup,
    closePreviewPopup,
    showNodeDetailsModal,
    openNodeDetailsModal,
    closeNodeDetailsModal,
    openingFilePath,
    openFileByPath,
    showWelcomeModal,
    openWelcomeModal,
    closeWelcomeModal,
    projectTreeRefreshToken,
    refreshProjectTree
  }
})
