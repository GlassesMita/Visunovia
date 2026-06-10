import axios, { AxiosInstance, AxiosError } from 'axios'
import type { ApiResponse, VNSceneGraph, VNNode, VNEdge } from '@/types'

const apiClient: AxiosInstance = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
})

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const message = (error.response?.data as { message?: string })?.message || error.message
    console.error('API Error:', message)
    return Promise.reject(error)
  }
)

export async function getSceneGraph(id: string): Promise<ApiResponse<VNSceneGraph>> {
  const response = await apiClient.get(`/scenegraphs/${id}`)
  return response.data
}

export async function saveSceneGraph(
  id: string,
  data: VNSceneGraph
): Promise<ApiResponse<VNSceneGraph>> {
  const response = await apiClient.put(`/scenegraphs/${id}`, data)
  return response.data
}

export async function createNode(
  sceneGraphId: string,
  node: VNNode
): Promise<ApiResponse<VNNode>> {
  const response = await apiClient.post(`/scenegraphs/${sceneGraphId}/nodes`, {
    id: node.id,
    type: node.type,
    position: node.position,
    properties: node.properties,
    inputs: node.inputs,
    outputs: node.outputs,
  })
  return response.data
}

export async function updateNode(
  sceneGraphId: string,
  nodeId: string,
  updates: Partial<VNNode>
): Promise<ApiResponse<VNNode>> {
  const response = await apiClient.put(
    `/scenegraphs/${sceneGraphId}/nodes/${nodeId}`,
    {
      position: updates.position,
      properties: updates.properties,
      inputs: updates.inputs,
      outputs: updates.outputs,
    }
  )
  return response.data
}

export async function deleteNode(
  sceneGraphId: string,
  nodeId: string
): Promise<ApiResponse<void>> {
  const response = await apiClient.delete(`/scenegraphs/${sceneGraphId}/nodes/${nodeId}`)
  return response.data
}

export async function createEdge(
  sceneGraphId: string,
  edge: VNEdge
): Promise<ApiResponse<VNEdge>> {
  const response = await apiClient.post(`/scenegraphs/${sceneGraphId}/edges`, {
    id: edge.id,
    source: edge.source,
    sourcePort: edge.sourcePort,
    target: edge.target,
    targetPort: edge.targetPort,
    type: edge.type,
  })
  return response.data
}

export async function deleteEdge(
  sceneGraphId: string,
  edgeId: string
): Promise<ApiResponse<void>> {
  const response = await apiClient.delete(`/scenegraphs/${sceneGraphId}/edges/${edgeId}`)
  return response.data
}

export async function getSceneGraphList(): Promise<ApiResponse<string[]>> {
  const response = await apiClient.get('/scenegraphs/list')
  return response.data
}

export async function getProjectList(): Promise<ApiResponse<string[]>> {
  const response = await apiClient.get('/projects')
  return response.data
}

export async function getProjectResources(
  projectId: string
): Promise<ApiResponse<{ path: string; type: string }[]>> {
  const response = await apiClient.get(`/projects/${projectId}/resources`)
  return response.data
}

export interface LanguageInfo {
  code: string
  displayName: string
  isCurrent: boolean
}

export async function getLanguages(): Promise<ApiResponse<LanguageInfo[]>> {
  const response = await apiClient.get('/localization/languages')
  return response.data
}

export async function getTranslations(
  lang?: string
): Promise<ApiResponse<{ language: string; translations: Record<string, string> }>> {
  const params = lang ? { lang } : {}
  const response = await apiClient.get('/localization/translations', { params })
  return response.data
}

export async function switchLanguage(
  language: string
): Promise<ApiResponse<object>> {
  const response = await apiClient.post('/localization/language', { language })
  return response.data
}

export { apiClient }

export const sceneGraphApi = {
  get: async (id: string) => {
    return await apiClient.get(`/scenegraphs/${id}`)
  },

  put: async (id: string, data: any) => {
    return await apiClient.put(`/scenegraphs/${id}`, data)
  },

  post: async (data: any) => {
    return await apiClient.post('/scenegraphs', data)
  },

  delete: async (id: string) => {
    return await apiClient.delete(`/scenegraphs/${id}`)
  },

  save: async (sceneId: string, data: any) => {
    return await sceneGraphApi.put(sceneId, data)
  },

  create: async (data: any) => {
    return await sceneGraphApi.post(data)
  },

  list: async () => {
    return await apiClient.get('/scenegraphs/list')
  }
}

export const localizationApi = {
  get: async (lang?: string) => {
    const params = lang ? { lang } : {}
    return await apiClient.get('/localization/translations', { params })
  },

  getTranslations: async (lang?: string) => {
    return await localizationApi.get(lang)
  },

  setLanguage: async (language: string) => {
    return await apiClient.post('/localization/language', { language })
  },

  getLanguages: async () => {
    return await apiClient.get('/localization/languages')
  }
}

export const settingsApi = {
  get: async () => {
    return await apiClient.get('/settings')
  },

  put: async (settings: any) => {
    return await apiClient.put('/settings', settings)
  },

  getSettings: async () => {
    return await settingsApi.get()
  },

  saveSettings: async (settings: Record<string, unknown>) => {
    return await apiClient.put('/settings', { settings })
  }
}

export const projectApi = {
  openProject: async (projectId: string) => {
    return await apiClient.get(`/projects/${projectId}`)
  },

  saveProject: async (projectId: string, data: Record<string, unknown>) => {
    return await apiClient.put(`/projects/${projectId}`, data)
  },

  listProjects: async () => {
    return await apiClient.get('/projects')
  }
}

export const fileBrowserApi = {
  list: async (path?: string) => {
    if (path) {
      return await apiClient.get(`/files?path=${encodeURIComponent(path)}`)
    }
    return await apiClient.get('/files')
  },
  
  read: async (path: string) => {
    return await apiClient.get(`/files/read?path=${encodeURIComponent(path)}`)
  },
  
  write: async (path: string, content: string) => {
    return await apiClient.post('/files/write', { path, content })
  },
  
  create: async (path: string, isDirectory: boolean) => {
    return await apiClient.post('/files/create', { path, isDirectory })
  },
  
  delete: async (path: string) => {
    return await apiClient.delete(`/files?path=${encodeURIComponent(path)}`)
  }
}

// ==================== JSON ↔ Blueprint 转换 API ====================

export interface JsonExportResult {
  sceneId: string
  jsonContent: string
  exportedAt: string
}

export interface JsonImportResult {
  sceneId: string
  nodeCount: number
  edgeCount: number
  importedAt: string
}

export interface JsonValidationResult {
  valid: boolean
  errors: string[]
  warnings: string[]
  stats: {
    nodeCount: number
    edgeCount: number
    resourceCount: number
    uuidCount: number
  }
}

export interface UuidEntry {
  uuid: string
  entityType: string
  name: string
  displayName: string
  createdAt: string
  updatedAt: string
}

export const jsonConversionApi = {
  /** 导出蓝图为 JSON */
  exportJson: async (
    sceneId: string,
    options?: { displayName?: string; description?: string; author?: string }
  ): Promise<ApiResponse<JsonExportResult>> => {
    const params = new URLSearchParams()
    if (options?.displayName) params.set('displayName', options.displayName)
    if (options?.description) params.set('description', options.description)
    if (options?.author) params.set('author', options.author)
    const qs = params.toString() ? `?${params.toString()}` : ''
    const response = await apiClient.get(`/json/export/${sceneId}${qs}`)
    return response.data
  },

  /** 下载 JSON 文件 */
  downloadJson: (
    sceneId: string,
    options?: { displayName?: string; description?: string; author?: string }
  ): string => {
    const params = new URLSearchParams()
    if (options?.displayName) params.set('displayName', options.displayName)
    if (options?.description) params.set('description', options.description)
    if (options?.author) params.set('author', options.author)
    const qs = params.toString() ? `?${params.toString()}` : ''
    return `/api/json/download/${sceneId}${qs}`
  },

  /** 从 JSON 内容导入蓝图 */
  importJson: async (
    sceneId: string,
    jsonContent: string,
    clearExisting: boolean = true
  ): Promise<ApiResponse<JsonImportResult>> => {
    const response = await apiClient.post(`/json/import/${sceneId}`, {
      jsonContent,
      clearExisting,
    })
    return response.data
  },

  /** 上传 JSON 文件并导入 */
  uploadJson: async (
    sceneId: string,
    file: File
  ): Promise<ApiResponse<JsonImportResult>> => {
    const formData = new FormData()
    formData.append('file', file)
    const response = await apiClient.post(`/json/upload/${sceneId}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return response.data
  },

  /** 验证 JSON 格式 */
  validateJson: async (
    jsonContent: string
  ): Promise<ApiResponse<JsonValidationResult>> => {
    const response = await apiClient.post('/json/validate', { jsonContent })
    return response.data
  },

  /** 获取 UUID 注册表 */
  getUuidRegistry: async (): Promise<ApiResponse<UuidEntry[]>> => {
    const response = await apiClient.get('/json/uuid-registry')
    return response.data
  },

  /** 按类型获取 UUID 注册表 */
  getUuidRegistryByType: async (
    entityType: string
  ): Promise<ApiResponse<UuidEntry[]>> => {
    const response = await apiClient.get(`/json/uuid-registry/${entityType}`)
    return response.data
  },

  /** 获取 UUID 详情 */
  getUuidDetail: async (uuid: string): Promise<ApiResponse<any>> => {
    const response = await apiClient.get(`/json/uuid-registry/detail/${uuid}`)
    return response.data
  },

  /** 获取 JSON 快照 */
  getJsonSnapshot: async (
    sceneId: string
  ): Promise<ApiResponse<{ sceneId: string; jsonContent: string }>> => {
    const response = await apiClient.get(`/json/snapshot/${sceneId}`)
    return response.data
  },
}
