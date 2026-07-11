import { apiClient } from './index'
import type { AxiosError } from 'axios'

export interface SceneInfo {
  id: string
  lorFilePath: string
  content?: string
}

export interface SceneListItem {
  id: string
  lorFilePath: string
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

  window.dispatchEvent(new CustomEvent('visunovia:project-changed'))
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

  window.dispatchEvent(new CustomEvent('visunovia:project-changed'))
  return response.data.data
}

/**
 * 获取项目的场景列表（不包含完整内容）
 */
export async function getProjectScenes(projectPath: string): Promise<SceneListItem[]> {
  const response = await apiClient.get<{
    success: boolean
    data: SceneListItem[]
  }>('/project/scenes', {
    params: { projectPath },
  })

  if (!response.data?.success) {
    throw new Error('获取场景列表失败')
  }

  return response.data.data
}

export async function createScene(sceneId: string): Promise<void> {
  const response = await apiClient.post<{
    success: boolean
    error?: string
  }>('/project/scenes', { sceneId })

  if (!response.data?.success) {
    throw new Error(response.data?.error || '新建场景失败')
  }
}

export async function renameScene(sceneId: string, newSceneId: string): Promise<void> {
  const response = await apiClient.put<{
    success: boolean
    error?: string
  }>(`/project/scenes/${encodeURIComponent(sceneId)}/rename`, { newSceneId })

  if (!response.data?.success) {
    throw new Error(response.data?.error || '重命名场景失败')
  }
}

export async function deleteScene(sceneId: string): Promise<void> {
  const response = await apiClient.delete<{
    success: boolean
    error?: string
  }>(`/project/scenes/${encodeURIComponent(sceneId)}`)

  if (!response.data?.success) {
    throw new Error(response.data?.error || '删除场景失败')
  }
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

export interface CharacterConfigEntry {
  id: string
  displayId?: string
  affiliation?: string
  color?: string
  avatar?: string
  sprites?: string[]
  spriteAnchorX?: number
  spriteAnchorY?: number
  note?: string
}

export interface ExpressionKeyframeEntry {
  time: number
  x: number
  y: number
  scale: number
  rotation: number
  opacity: number
  easing: string
  bezierX1: number
  bezierY1: number
  bezierX2: number
  bezierY2: number
}

export interface ExpressionLayerEntry {
  id: string
  name: string
  image: string
  x: number
  y: number
  width: number
  height: number
  rotation: number
  opacity: number
  zIndex: number
  keyframes: ExpressionKeyframeEntry[]
}

export interface ExpressionConfigEntry {
  id: string
  name: string
  duration: number
  canvasWidth: number
  canvasHeight: number
  layers: ExpressionLayerEntry[]
}

export interface ExpressionConfigResponse {
  expressions: ExpressionConfigEntry[]
}

export interface CharacterConfigResponse {
  characters: CharacterConfigEntry[]
}

type RawCharacterConfigEntry = CharacterConfigEntry & {
  Id?: string
  DisplayId?: string
  Affiliation?: string
  Color?: string
  Avatar?: string
  Sprites?: string[]
  SpriteAnchorX?: number
  SpriteAnchorY?: number
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
      affiliation: character.affiliation || character.Affiliation || '',
      color: character.color || character.Color || '',
      avatar: character.avatar || character.Avatar || '',
      sprites: character.sprites || character.Sprites || [],
      spriteAnchorX: character.spriteAnchorX ?? character.SpriteAnchorX ?? 50,
      spriteAnchorY: character.spriteAnchorY ?? character.SpriteAnchorY ?? 100,
      note: character.note || character.Note || '',
    })).filter(character => character.id)
  }
}

type RawExpressionConfigEntry = Omit<ExpressionConfigEntry, 'layers'> & {
  Id?: string
  Name?: string
  Duration?: number
  CanvasWidth?: number
  CanvasHeight?: number
  Layers?: Array<Partial<ExpressionLayerEntry> & { Id?: string; Name?: string; Image?: string; X?: number; Y?: number; Width?: number; Height?: number; Rotation?: number; Opacity?: number; ZIndex?: number; Keyframes?: any[] }>
  layers?: ExpressionLayerEntry[]
}

type RawExpressionConfigResponse = ExpressionConfigResponse & {
  Expressions?: RawExpressionConfigEntry[]
}

function normalizeExpressionConfig(config?: RawExpressionConfigResponse): ExpressionConfigResponse {
  const rawExpressions = config?.expressions || config?.Expressions || []
  return {
    expressions: rawExpressions.map(expression => ({
      id: expression.id || expression.Id || '',
      name: expression.name || expression.Name || expression.id || expression.Id || '',
      duration: Number(expression.duration ?? expression.Duration ?? 2) || 2,
      canvasWidth: Number(expression.canvasWidth ?? expression.CanvasWidth ?? 512) || 512,
      canvasHeight: Number(expression.canvasHeight ?? expression.CanvasHeight ?? 512) || 512,
      layers: (expression.layers || expression.Layers || []).map((layer: any, index: number) => ({
        id: layer.id || layer.Id || `layer-${index + 1}`,
        name: layer.name || layer.Name || `Layer ${index + 1}`,
        image: layer.image || layer.Image || '',
        x: Number(layer.x ?? layer.X ?? 50),
        y: Number(layer.y ?? layer.Y ?? 50),
        width: Number(layer.width ?? layer.Width ?? 70),
        height: Number(layer.height ?? layer.Height ?? 70),
        rotation: Number(layer.rotation ?? layer.Rotation ?? 0),
        opacity: Number(layer.opacity ?? layer.Opacity ?? 100),
        zIndex: Number(layer.zIndex ?? layer.ZIndex ?? index),
        keyframes: (layer.keyframes || layer.Keyframes || []).map((keyframe: any) => ({
          time: Number(keyframe.time ?? keyframe.Time ?? 0),
          x: Number(keyframe.x ?? keyframe.X ?? layer.x ?? layer.X ?? 50),
          y: Number(keyframe.y ?? keyframe.Y ?? layer.y ?? layer.Y ?? 50),
          scale: Number(keyframe.scale ?? keyframe.Scale ?? 100),
          rotation: Number(keyframe.rotation ?? keyframe.Rotation ?? 0),
          opacity: Number(keyframe.opacity ?? keyframe.Opacity ?? 100),
          easing: String(keyframe.easing ?? keyframe.Easing ?? 'bezier'),
          bezierX1: Number(keyframe.bezierX1 ?? keyframe.BezierX1 ?? 0.25),
          bezierY1: Number(keyframe.bezierY1 ?? keyframe.BezierY1 ?? 0.1),
          bezierX2: Number(keyframe.bezierX2 ?? keyframe.BezierX2 ?? 0.25),
          bezierY2: Number(keyframe.bezierY2 ?? keyframe.BezierY2 ?? 1),
        })),
      })),
    })).filter(expression => expression.id)
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
 * 读取当前项目 Assets/Characters/Manifest.resona 角色附加配置。
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
 * 写入当前项目 Assets/Characters/Manifest.resona 角色附加配置。
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

export async function getExpressionConfig(): Promise<ExpressionConfigResponse> {
  const response = await apiClient.get<{
    success: boolean
    data: RawExpressionConfigResponse
    error?: string
  }>('/project/expressions/config')

  if (!response.data?.success) {
    throw new Error(response.data?.error || '读取表情配置失败')
  }

  return normalizeExpressionConfig(response.data.data)
}

export async function saveExpressionConfig(config: ExpressionConfigResponse): Promise<ExpressionConfigResponse> {
  const response = await apiClient.put<{
    success: boolean
    data: RawExpressionConfigResponse
    error?: string
  }>('/project/expressions/config', config)

  if (!response.data?.success) {
    throw new Error(response.data?.error || '保存表情配置失败')
  }

  return normalizeExpressionConfig(response.data.data || config)
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
