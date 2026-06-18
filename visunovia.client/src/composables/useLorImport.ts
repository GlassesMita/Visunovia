import { ref } from 'vue'
import { useLorToBlueprint } from './useLorToBlueprint'
import { useNodeOperations } from './useNodeOperations'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { fileBrowserApi } from '@/api'

/**
 * Lor 文件导入 composable
 * 提供从 Lor 剧本文件导入并转换为蓝图的功能
 */
export function useLorImport() {
  const lorConverter = useLorToBlueprint()
  const nodeOperations = useNodeOperations()
  const editorStore = useEditorStore()
  const nodeGraphStore = useNodeGraphStore()
  
  const isImporting = ref(false)
  const importError = ref<string | null>(null)
  const importSuccess = ref(false)
  const validationResult = ref<{ valid: boolean; errors: string[] } | null>(null)

  /**
   * 从文件内容导入 Lor 剧本
   */
  async function importFromContent(
    lorContent: string,
    sceneId: string,
    options: { validate?: boolean; autoLayout?: boolean; format?: 'lor' | 'lrc'; characterSlot?: string } = {}
  ): Promise<boolean> {
    const { validate = true, autoLayout = true, format = 'lor', characterSlot = '1' } = options
    
    isImporting.value = true
    importError.value = null
    importSuccess.value = false
    validationResult.value = null
    
    try {
      // 可选：验证文件格式
      if (validate) {
        validationResult.value = format === 'lrc'
          ? lorConverter.validateLrcFile(lorContent)
          : lorConverter.validateLorFile(lorContent)
        if (!validationResult.value.valid) {
          importError.value = `文件格式验证失败:\n${validationResult.value.errors.join('\n')}`
          return false
        }
      }
      
      // 转换 Lor/LRC 为蓝图
      const blueprint = format === 'lrc'
        ? await lorConverter.convertFromLrc(lorContent, { sceneId, characterSlot })
        : await lorConverter.convertFromJson(lorContent)
      blueprint.id = sceneId
      
      // 加载蓝图到编辑器
      const editor = (window as any).__editor
      if (!editor) {
        throw new Error('编辑器实例未初始化')
      }
      
      // 使用 nodeOperations 的反序列化功能加载蓝图
      await nodeOperations.deserializeToEditor(editor, blueprint)
      
      // 更新编辑器状态
      editorStore.currentFileName = `${sceneId}.lor`
      nodeGraphStore.currentSceneId = sceneId
      nodeGraphStore.markClean()
      
      importSuccess.value = true
      return true
    } catch (error) {
      importError.value = error instanceof Error ? error.message : '导入失败'
      console.error('[useLorImport] Import failed:', error)
      return false
    } finally {
      isImporting.value = false
    }
  }

  /**
   * 从项目路径导入 Lor 文件
   */
  async function importFromProject(
    projectPath: string,
    sceneId: string,
    options: { validate?: boolean; autoLayout?: boolean } = {}
  ): Promise<boolean> {
    const lorFilePath = `${projectPath}/Scripts/Main/${sceneId}.lor`
    
    try {
      // 读取文件内容
      const response = await fileBrowserApi.read(lorFilePath)
      
      if (!response.data?.content) {
        throw new Error(`无法读取文件: ${lorFilePath}`)
      }
      
      return await importFromContent(response.data.content, sceneId, options)
    } catch (error) {
      importError.value = error instanceof Error ? error.message : '文件读取失败'
      return false
    }
  }

  /**
   * 从 MyNewProject 目录导入
   */
  async function importFromMyNewProject(
    sceneId: string = 'start',
    options: { validate?: boolean; autoLayout?: boolean } = {}
  ): Promise<boolean> {
    const projectPath = 'C:/Users/Plana/Documents/MyNewProject'
    return await importFromProject(projectPath, sceneId, options)
  }

  /**
   * 批量导入多个场景
   */
  async function importMultipleScenes(
    projectPath: string,
    sceneIds: string[],
    options: { validate?: boolean; autoLayout?: boolean } = {}
  ): Promise<{ success: string[]; failed: { id: string; error: string }[] }> {
    const result = {
      success: [] as string[],
      failed: [] as { id: string; error: string }[],
    }
    
    for (const sceneId of sceneIds) {
      const success = await importFromProject(projectPath, sceneId, options)
      if (success) {
        result.success.push(sceneId)
      } else {
        result.failed.push({ id: sceneId, error: importError.value || '未知错误' })
      }
    }
    
    return result
  }

  /**
   * 预览 Lor 文件转换结果（不实际加载到编辑器）
   */
  async function previewConversion(lorContent: string): Promise<{
    nodeCount: number
    connectionCount: number
    nodeTypes: Record<string, number>
  } | null> {
    try {
      const blueprint = await lorConverter.convertFromJson(lorContent)
      
      const nodeTypes: Record<string, number> = {}
      blueprint.nodes.forEach(node => {
        nodeTypes[node.nodeType] = (nodeTypes[node.nodeType] || 0) + 1
      })
      
      return {
        nodeCount: blueprint.nodes.length,
        connectionCount: blueprint.connections.length,
        nodeTypes,
      }
    } catch (error) {
      importError.value = error instanceof Error ? error.message : '预览失败'
      return null
    }
  }

  async function previewLrcConversion(lrcContent: string, sceneId = 'lrc_import', characterSlot = '1'): Promise<{
    nodeCount: number
    connectionCount: number
    nodeTypes: Record<string, number>
  } | null> {
    try {
      const blueprint = await lorConverter.convertFromLrc(lrcContent, { sceneId, characterSlot })
      const nodeTypes: Record<string, number> = {}
      blueprint.nodes.forEach(node => {
        nodeTypes[node.nodeType] = (nodeTypes[node.nodeType] || 0) + 1
      })

      return {
        nodeCount: blueprint.nodes.length,
        connectionCount: blueprint.connections.length,
        nodeTypes,
      }
    } catch (error) {
      importError.value = error instanceof Error ? error.message : 'LRC 预览失败'
      return null
    }
  }

  /**
   * 清除导入状态
   */
  function clearImportState() {
    isImporting.value = false
    importError.value = null
    importSuccess.value = false
    validationResult.value = null
  }

  return {
    isImporting,
    importError,
    importSuccess,
    validationResult,
    
    importFromContent,
    importFromProject,
    importFromMyNewProject,
    importMultipleScenes,
    previewConversion,
    previewLrcConversion,
    clearImportState,
  }
}

