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
  get: async (sceneId: string) => {
    const response = await apiClient.get(`/scenegraphs/${sceneId}`)
    return response
  },
  
  save: async (sceneId: string, data: any) => {
    const serializedData = {
      id: sceneId,
      nodes: data.nodes.map((node: any) => ({
        id: node.id,
        type: node.type,
        subType: node.data?.subType,
        position: node.position,
        data: node.data
      })),
      connections: data.connections
    }
    
    return await apiClient.put(`/scenegraphs/${sceneId}`, serializedData)
  },
  
  create: async (data: any) => {
    return await apiClient.post('/scenegraphs', data)
  },
  
  delete: async (sceneId: string) => {
    return await apiClient.delete(`/scenegraphs/${sceneId}`)
  },
  
  list: async () => {
    return await apiClient.get('/scenegraphs/list')
  }
}

export const localizationApi = {
  get: async (lang: string) => {
    return await apiClient.get(`/localization/${lang}`)
  }
}

export const fileBrowserApi = {
  list: async (path: string) => {
    return await apiClient.get(`/files?path=${encodeURIComponent(path)}`)
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
