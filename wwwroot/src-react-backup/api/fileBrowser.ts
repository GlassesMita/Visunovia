import { apiClient } from './index'

/** 驱动器信息 */
export interface DriveInfo {
  letter: string
  name: string
  totalSpace: number
  freeSpace: number
}

/** 目录条目 */
export interface DirEntry {
  name: string
  isDirectory: boolean
  size: number
  lastModified: string
  extension: string
}

/** 目录内容响应 */
export interface DirContent {
  path: string
  entries: DirEntry[]
}

/** 特殊文件夹信息 */
export interface SpecialFolder {
  id: string
  name: string
  path: string
}

/**
 * 获取系统驱动器列表
 * @returns 驱动器信息数组
 */
export async function getDrives(): Promise<DriveInfo[]> {
  const response = await apiClient.get<DriveInfo[]>('/FileBrowser/drives')
  return response.data
}

/**
 * 获取指定路径的目录内容
 * @param path - 要浏览的目录路径
 * @returns 目录内容（包含路径和条目列表）
 */
export async function getEntries(path: string): Promise<DirContent> {
  // 使用 encodeURIComponent 对路径进行编码，防止特殊字符导致的问题
  const encodedPath = encodeURIComponent(path)
  const response = await apiClient.get<DirContent>(`/FileBrowser/entries?path=${encodedPath}`)
  return response.data
}

/**
 * 在指定父目录下创建新文件夹
 * @param parentPath - 父目录路径
 * @param folderName - 新文件夹名称
 * @returns 创建结果，包含新文件夹的完整路径
 */
export async function createFolder(
  parentPath: string,
  folderName: string
): Promise<{ path: string }> {
  const response = await apiClient.post<{ path: string }>('/FileBrowser/create-folder', {
    parentPath,
    folderName,
  })
  return response.data
}

/**
 * 获取特殊文件夹列表（桌面、文档、图片等）
 * @returns 特殊文件夹数组
 */
export async function getSpecialFolders(): Promise<SpecialFolder[]> {
  const response = await apiClient.get<SpecialFolder[]>('/FileBrowser/special-folders')
  return response.data
}
