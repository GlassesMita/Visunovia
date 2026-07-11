import { apiClient } from './index'

/** 驱动器信息 */
export interface DriveInfo {
  letter: string
  name: string
  totalSpace: number
  freeSpace: number
  fileSystem: string
}

/** 目录条目 */
export interface DirEntry {
  name: string
  path: string
  isDirectory: boolean
  size: number
  lastModified: string
  extension: string
}

/** 目录内容响应 */
export interface DirContent {
  currentPath: string
  parentPath: string
  entries: DirEntry[]
}

/** 特殊文件夹信息 */
export interface SpecialFolder {
  id: string
  name: string
  path: string
}

export interface TextFileContent {
  path: string
  name: string
  content: string
}

/**
 * 获取系统驱动器列表
 * @returns 驱动器信息数组
 */
export async function getDrives(): Promise<DriveInfo[]> {
  const response = await apiClient.get<{ success: boolean; data: DriveInfo[] }>('/FileBrowser/drives')
  return response.data?.data ?? []
}

export async function getEntries(path: string): Promise<DirContent> {
  // Send path as raw string via POST-like query to avoid backslash encoding issues.
  // We use a custom param approach: append as raw query param.
  const response = await apiClient.get<{
    success: boolean
    data: { currentPath: string; parentPath: string; entries: DirEntry[] }
  }>(`/FileBrowser/entries`, { params: { path } })
  const data = response.data?.data
  const currentPath = data?.currentPath ?? path
  return {
    currentPath: currentPath,
    parentPath: data?.parentPath ?? '',
    entries: (data?.entries ?? []).map(e => ({
      ...e,
      path: e.path ?? (currentPath ? `${currentPath}\\${e.name}` : e.name)
    })),
  }
}

export async function createFolder(
  parentPath: string,
  folderName: string
): Promise<{ path: string }> {
  const response = await apiClient.post<{ success: boolean; data: { path: string } }>(
    '/FileBrowser/create-folder',
    { parentPath, folderName }
  )
  return response.data?.data ?? { path: '' }
}

export async function getSpecialFolders(): Promise<SpecialFolder[]> {
  const response = await apiClient.get<{ success: boolean; data: SpecialFolder[] }>(
    '/FileBrowser/special-folders'
  )
  return response.data?.data ?? []
}

export async function readTextFile(path: string): Promise<TextFileContent> {
  const response = await apiClient.get<{ success: boolean; data: TextFileContent; error?: string }>(
    '/FileBrowser/read-text',
    { params: { path } }
  )

  if (!response.data?.success) {
    throw new Error(response.data?.error || '读取文本文件失败')
  }

  return response.data.data
}
