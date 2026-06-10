import { apiClient } from './index'
import type { AxiosError } from 'axios'

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

function getApiErrorMessage(error: unknown, fallback: string): string {
  const axiosError = error as AxiosError<{ error?: string; message?: string }>
  return axiosError.response?.data?.error || axiosError.response?.data?.message || (error instanceof Error ? error.message : fallback)
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

/**
 * 只读读取项目内文本文件内容。
 */
export async function getProjectFileContent(path: string): Promise<{ path: string; name: string; content: string }> {
  const response = await apiClient.get<{
    success: boolean
    data: { path: string; name: string; content: string }
  }>('/project/file-content', {
    params: { path },
  })

  if (!response.data?.success) {
    throw new Error('读取文件内容失败')
  }

  return response.data.data
}

/**
 * 导入资产文件到指定 Assets 子目录。
 */
export async function importProjectAsset(targetDirectory: string, file: File, relativePath?: string): Promise<FolderNode> {
  const formData = new FormData()
  formData.append('targetDirectory', targetDirectory)
  formData.append('file', file)
  if (relativePath) {
    formData.append('relativePath', relativePath)
  }

  const response = await apiClient.post<{
    success: boolean
    data: FolderNode
    error?: string
  }>('/project/assets/import', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })

  if (!response.data?.success) {
    throw new Error(response.data?.error || '导入资产失败')
  }

  return response.data.data
}

/**
 * 删除项目 Assets 目录内的资产文件或空目录。
 */
export async function deleteProjectAsset(path: string): Promise<void> {
  try {
    const response = await apiClient.delete<{
      success: boolean
      error?: string
    }>('/project/assets', {
      params: { path },
    })

    if (!response.data?.success) {
      throw new Error(response.data?.error || '删除资产失败')
    }
  } catch (error) {
    throw new Error(getApiErrorMessage(error, '删除资产失败'))
  }
}

/**
 * 重命名项目 Assets 目录内的资产文件或文件夹。
 */
export async function renameProjectAsset(path: string, newName: string): Promise<FolderNode> {
  try {
    const response = await apiClient.put<{
      success: boolean
      data: FolderNode
      error?: string
    }>('/project/assets/rename', { path, newName })

    if (!response.data?.success) {
      throw new Error(response.data?.error || '重命名资产失败')
    }

    return response.data.data
  } catch (error) {
    throw new Error(getApiErrorMessage(error, '重命名资产失败'))
  }
}

export interface CurrentProjectInfo {
  projectName: string
  version: string
  versionCode: string
  companyName: string
  projectPath: string
  subDirectories: string[]
}

export interface UpdateProjectSettingsRequest {
  projectName?: string
  companyName?: string
  version?: string
  versionCode?: string
}

export interface CharacterConfigEntry {
  id: string
  displayId?: string
  color?: string
  avatar?: string
  note?: string
}

export interface CharacterConfigResponse {
  characters: CharacterConfigEntry[]
}

type RawCharacterConfigEntry = CharacterConfigEntry & {
  Id?: string
  DisplayId?: string
  Color?: string
  Avatar?: string
  Note?: string
}

type RawCharacterConfigResponse = CharacterConfigResponse & {
  Characters?: RawCharacterConfigEntry[]
}

function normalizeCharacterConfig(config?: RawCharacterConfigResponse): CharacterConfigResponse {
  const rawCharacters = config?.characters || config?.Characters || []
  return {
    characters: rawCharacters.map(character => ({
      id: character.id || character.Id || '',
      displayId: character.displayId || character.DisplayId || '',
      color: character.color || character.Color || '',
      avatar: character.avatar || character.Avatar || '',
      note: character.note || character.Note || '',
    })).filter(character => character.id)
  }
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
 * 读取当前项目 Assets/Characters/characters.json 角色附加配置。
 */
export async function getCharacterConfig(): Promise<CharacterConfigResponse> {
  const response = await apiClient.get<{
    success: boolean
    data: RawCharacterConfigResponse
    error?: string
  }>('/project/characters/config')

  if (!response.data?.success) {
    throw new Error(response.data?.error || '读取角色配置失败')
  }

  return normalizeCharacterConfig(response.data.data)
}

/**
 * 写入当前项目 Assets/Characters/characters.json 角色附加配置。
 */
export async function saveCharacterConfig(config: CharacterConfigResponse): Promise<CharacterConfigResponse> {
  const response = await apiClient.put<{
    success: boolean
    data: CharacterConfigResponse
    error?: string
  }>('/project/characters/config', config)

  if (!response.data?.success) {
    throw new Error(response.data?.error || '保存角色配置失败')
  }

  return response.data.data || config
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
