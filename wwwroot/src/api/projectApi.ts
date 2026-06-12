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

export interface NewProjectRequest {
  name: string
  path: string
  companyName?: string
  version?: string
  versionCode?: string
}

export interface FolderNode {
  name: string
  path: string
  isDirectory: boolean
  extension: string
  size: number
  lastModified: string
  children: FolderNode[] | null
}

export interface NewProjectResult {
  projectPath: string
  tlorPath: string
  name: string
  folderTree: FolderNode
}

/**
 * 新建项目：在指定目录下创建标准项目结构
 */
export async function createProject(
  name: string,
  path: string,
  companyName?: string,
  version?: string,
  versionCode?: string
): Promise<NewProjectResult> {
  const response = await apiClient.post<{
    success: boolean
    data: NewProjectResult
  }>('/project/new', {
    name,
    path,
    companyName,
    version,
    versionCode,
  })

  if (!response.data?.success) {
    throw new Error('项目创建失败')
  }

  return response.data.data
}

/**
 * 导入项目：从 URL 参数中解析项目并返回剧本列表
 */
export async function importProject(projectPath: string): Promise<ProjectParseResult> {
  const response = await apiClient.post<{
    success: boolean
    data: ProjectParseResult
    error?: string
    message?: string
  }>('/project/import', {
    projectPath,
  })

  if (!response.data?.success) {
    throw new Error(response.data?.error || response.data?.message || '项目导入失败')
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
 * 获取项目的文件夹树结构
 */
export async function getProjectFolderTree(projectPath: string): Promise<FolderNode> {
  const response = await apiClient.get<{
    success: boolean
    data: FolderNode
  }>('/project/folder-tree', {
    params: { projectPath },
  })

  if (!response.data?.success) {
    throw new Error('获取文件夹结构失败')
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

export interface CurrentProjectInfo {
  projectName: string
  version: string
  versionCode: string
  companyName: string
  ratingSystem: 'CADPA' | 'GSRR' | 'CERO' | 'PEGI'
  ratingValue: string
  projectPath: string
  subDirectories: string[]
}

export interface UpdateProjectSettingsRequest {
  projectName?: string
  companyName?: string
  version?: string
  versionCode?: string
  ratingSystem?: 'CADPA' | 'GSRR' | 'CERO' | 'PEGI'
  ratingValue?: string
}

/**
 * 获取当前打开的项目信息
 */
export async function getCurrentProject(): Promise<{ success: boolean; data: CurrentProjectInfo | null }> {
  const response = await apiClient.get<{
    success: boolean
    data: CurrentProjectInfo | null
  }>('/project/currentProject')
  return response.data
}

/**
 * 更新当前项目设置
 */
export async function updateProjectSettings(
  settings: UpdateProjectSettingsRequest
): Promise<{ success: boolean }> {
  const response = await apiClient.put<{
    success: boolean
  }>('/project/settings', settings)
  return response.data
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
