import { apiClient } from './index'

export interface SceneInfo {
  id: string
  lorFilePath: string
  content?: string
}

export interface ProjectParseResult {
  scenes: SceneInfo[]
}

export interface ImportProjectRequest {
  projectPath: string
}

/**
 * 导入项目：从 URL 参数中解析项目并返回剧本列表
 */
export async function importProject(projectPath: string): Promise<ProjectParseResult> {
  const response = await apiClient.post<{
    success: boolean
    data: ProjectParseResult
  }>('/project/import', {
    projectPath,
  })

  if (!response.data?.success) {
    throw new Error('项目导入失败')
  }

  return response.data.data
}

/**
 * 获取项目的场景列表（不包含完整内容）
 */
export async function getProjectScenes(projectPath: string): Promise<SceneInfo[]> {
  const response = await apiClient.get<{
    success: boolean
    data: SceneInfo[]
  }>('/project/scenes', {
    params: { projectPath },
  })

  if (!response.data?.success) {
    throw new Error('获取场景列表失败')
  }

  return response.data.data
}

/**
 * 读取指定剧本文件的内容
 */
export async function getSceneContent(lorFilePath: string): Promise<SceneInfo> {
  const response = await apiClient.get<{
    success: boolean
    data: SceneInfo
  }>('/project/scene', {
    params: { path: lorFilePath },
  })

  if (!response.data?.success) {
    throw new Error('读取剧本失败')
  }

  return response.data.data
}

/**
 * 从 URL 参数中解析项目路径
 * URL 格式: /?project=C:\path\to\project
 */
export function parseProjectFromUrl(): string | null {
  const urlParams = new URLSearchParams(window.location.search)
  return urlParams.get('project')
}

/**
 * 清除 URL 参数
 */
export function clearUrlParams(): void {
  const url = new URL(window.location.href)
  url.search = ''
  window.history.replaceState({}, '', url.toString())
}
