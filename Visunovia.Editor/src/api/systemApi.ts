import { apiClient } from './index'

export interface RecentProject {
  name: string
  path: string
  lastOpened?: string
}

/**
 * 请求后端退出应用程序
 */
export async function quitApplication(): Promise<boolean> {
  const response = await apiClient.post<{ success: boolean }>('/system/quit')
  return response.data?.success === true
}

/**
 * 获取最近打开的项目列表
 */
export async function getRecentProjects(): Promise<RecentProject[]> {
  try {
    const response = await apiClient.get<{ success: boolean; data: RecentProject[] }>('/project/recentProjects')
    if (response.data?.success && Array.isArray(response.data.data)) {
      return response.data.data
    }
    return []
  } catch {
    return []
  }
}
