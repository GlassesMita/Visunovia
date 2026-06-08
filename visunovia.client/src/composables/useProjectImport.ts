import { ref, onMounted } from 'vue'
import { parseProjectFromUrl, importProject, clearUrlParams, type ProjectParseResult, type SceneInfo } from '@/api/projectApi'
import { useLorToBlueprint } from './useLorToBlueprint'
import { useNodeOperations } from './useNodeOperations'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'

export function useProjectImport() {
  const lorConverter = useLorToBlueprint()
  const nodeOperations = useNodeOperations()
  const editorStore = useEditorStore()
  const nodeGraphStore = useNodeGraphStore()

  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const projectScenes = ref<SceneInfo[]>([])
  const currentProjectPath = ref<string | null>(null)
  const importedSceneId = ref<string | null>(null)

  /**
   * 从 URL 参数导入项目
   */
  async function importFromUrl(): Promise<boolean> {
    const projectPath = parseProjectFromUrl()
    if (!projectPath) {
      return false
    }

    return await importProjectPath(projectPath)
  }

  /**
   * 从项目路径导入
   */
  async function importProjectPath(projectPath: string): Promise<boolean> {
    isLoading.value = true
    error.value = null
    projectScenes.value = []
    currentProjectPath.value = projectPath

    try {
      const result: ProjectParseResult = await importProject(projectPath)
      projectScenes.value = result.scenes

      if (result.scenes.length === 0) {
        error.value = '未找到任何剧本文件'
        return false
      }

      // 自动导入第一个场景
      const firstScene = result.scenes[0]
      if (firstScene && firstScene.content) {
        await loadSceneContent(firstScene)
        importedSceneId.value = firstScene.id
        clearUrlParams()
        return true
      }

      error.value = `场景 ${firstScene?.id ?? ''} 内容为空或无法读取`
      return false
    } catch (e: any) {
      error.value = e?.response?.data?.error
        || e?.response?.data?.message
        || (e instanceof Error ? e.message : '项目导入失败')
      console.error('[useProjectImport] Import failed:', e)
      return false
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 加载指定场景的内容
   */
  async function loadScene(sceneInfo: SceneInfo): Promise<boolean> {
    isLoading.value = true
    error.value = null

    try {
      await loadSceneContent(sceneInfo)
      importedSceneId.value = sceneInfo.id
      return true
    } catch (e) {
      error.value = e instanceof Error ? e.message : '加载场景失败'
      return false
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 将场景内容转换为蓝图并加载到编辑器
   */
  async function loadSceneContent(sceneInfo: SceneInfo): Promise<void> {
    if (!sceneInfo.content) {
      throw new Error('场景内容为空')
    }

    // 解析 YAML 为蓝图
    const blueprint = await lorConverter.convertFromYaml(sceneInfo.content)
    blueprint.id = sceneInfo.id

    // 获取编辑器实例
    const editor = (window as any).__editor
    if (!editor) {
      throw new Error('编辑器实例未初始化')
    }

    // 清空当前图并加载新内容
    await nodeOperations.deserializeToEditor(editor, blueprint)
    editorStore.currentFileName = `${sceneInfo.id}.lor`
    nodeGraphStore.currentSceneId = sceneInfo.id
    nodeGraphStore.markClean()
  }

  /**
   * 清除导入状态
   */
  function clearImportState(): void {
    isLoading.value = false
    error.value = null
    projectScenes.value = []
    currentProjectPath.value = null
    importedSceneId.value = null
  }

  return {
    isLoading,
    error,
    projectScenes,
    currentProjectPath,
    importedSceneId,
    importFromUrl,
    importProjectPath,
    loadScene,
    clearImportState,
  }
}
