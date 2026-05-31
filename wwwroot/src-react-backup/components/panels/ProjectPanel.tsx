import { useState, useEffect, useCallback, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { fileBrowserApi } from '@/api/index'

/** 文件系统条目 */
interface FileEntry {
  name: string
  path: string
  isDirectory: boolean
  extension?: string
}

/** 文件夹项（模拟分组结果） */
interface FolderCategory {
  name: string
  label: string
  icon: string
  files: FileEntry[]
  /** 文件扩展名过滤 */
  extensions: string[]
}

/** 按文件类型分组的文件夹配置 */
const folderCategories: FolderCategory[] = [
  {
    name: 'images',
    label: '图片',
    icon: '🖼️',
    files: [],
    extensions: ['.png', '.jpg', '.jpeg', '.svg', '.gif', '.bmp', '.webp'],
  },
  {
    name: 'audio',
    label: '音频',
    icon: '🎵',
    files: [],
    extensions: ['.mp3', '.wav', '.ogg', '.flac', '.aac'],
  },
  {
    name: 'videos',
    label: '视频',
    icon: '🎬',
    files: [],
    extensions: ['.mp4', '.webm', '.avi', '.mov'],
  },
  {
    name: 'scripts',
    label: '脚本',
    icon: '📄',
    files: [],
    extensions: ['.xml', '.json', '.yaml', '.yml'],
  },
]

/** 根据扩展名获取文件图标 */
function getFileIcon(ext: string): string {
  const extLower = ext.toLowerCase()
  if (['.png', '.jpg', '.jpeg', '.svg', '.gif', '.bmp', '.webp'].includes(extLower))
    return '🖼️'
  if (['.mp3', '.wav', '.ogg', '.flac', '.aac'].includes(extLower))
    return '🎵'
  if (['.mp4', '.webm', '.avi', '.mov'].includes(extLower))
    return '🎬'
  if (['.xml', '.json', '.yaml', '.yml'].includes(extLower))
    return '📄'
  return '📁'
}

/**
 * 项目资源浏览器面板（Project）
 * 从 fileBrowser API 获取文件列表，按类型分组展示。
 * 支持文件夹展开/折叠、文件选中、刷新等操作。
 */
export default function ProjectPanel() {
  const { t } = useTranslation()
  const [expandedFolders, setExpandedFolders] = useState<Set<string>>(
    new Set(['images'])
  )
  const [selectedFile, setSelectedFile] = useState<string | null>(null)
  const [allFiles, setAllFiles] = useState<FileEntry[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  /** 从 API 加载文件列表 */
  const loadFiles = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const response = await fileBrowserApi.list()
      // 解析 API 响应：支持 { data: [...] } 或 { items: [...] } 格式
      const data = response.data?.data ?? response.data?.items ?? response.data
      if (Array.isArray(data)) {
        // 转换为 FileEntry 格式
        const entries: FileEntry[] = data.map(
          (item: Record<string, unknown>) => ({
            name: String(item.name ?? item.path ?? ''),
            path: String(item.path ?? item.name ?? ''),
            isDirectory: Boolean(item.isDirectory ?? item.type === 'directory'),
            extension: typeof item.extension === 'string'
              ? item.extension
              : String(item.path ?? item.name ?? '').includes('.')
                ? '.' +
                  (String(item.path ?? item.name ?? '').split('.').pop() ?? '')
                : undefined,
          })
        )
        setAllFiles(entries)
      } else {
        // 如果 API 返回的是数组格式
        setAllFiles([])
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载文件列表失败')
      console.error('[ProjectPanel] 加载文件失败:', err)
    } finally {
      setIsLoading(false)
    }
  }, [])

  // 初始加载
  useEffect(() => {
    loadFiles()
  }, [loadFiles])

  // 将文件按类型分组
  const groupedFiles = useMemo(() => {
    return groupFilesByCategory(allFiles)
  }, [allFiles])

  // 切换文件夹展开状态
  const toggleFolder = useCallback((folderName: string) => {
    setExpandedFolders((prev) => {
      const next = new Set(prev)
      if (next.has(folderName)) {
        next.delete(folderName)
      } else {
        next.add(folderName)
      }
      return next
    })
  }, [])

  // 选中/取消选中文件
  const handleSelectFile = useCallback((filePath: string) => {
    setSelectedFile((prev) => (prev === filePath ? null : filePath))
  }, [])

  // 刷新文件列表
  const handleRefresh = useCallback(() => {
    loadFiles()
  }, [loadFiles])

  // 各分组中文件总数（用于显示计数）
  const totalFileCount = allFiles.length

  return (
    <div className="flex flex-col h-full bg-gray-800">
      {/* 面板标题 + 刷新按钮 */}
      <div className="flex items-center justify-between px-3 py-2.5 border-b border-gray-700 flex-shrink-0">
        <div className="flex items-center gap-2">
          <h3 className="m-0 text-[11px] font-semibold text-gray-300 uppercase tracking-wide">
            {t('panels.project')}
          </h3>
          {isLoading && (
            <span className="text-[10px] text-gray-500">加载中...</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-gray-500 bg-gray-800 px-1.5 py-0.5 rounded-full">
            {totalFileCount}
          </span>
          <button
            onClick={handleRefresh}
            disabled={isLoading}
            title="刷新"
            className="px-1.5 py-0.5 bg-transparent border border-gray-700 rounded
              text-xs text-gray-500 cursor-pointer transition-colors
              hover:bg-white/5 hover:text-gray-300 hover:border-gray-600
              disabled:opacity-40 disabled:cursor-not-allowed"
          >
            🔄
          </button>
        </div>
      </div>

      {/* 文件树内容 */}
      <div className="flex-1 overflow-y-auto p-1">
        {error && (
          <div className="flex flex-col items-center justify-center h-full text-center px-4">
            <p className="text-xs text-red-400 mb-2">{error}</p>
            <button
              onClick={handleRefresh}
              className="px-3 py-1 bg-gray-700 text-gray-300 rounded text-xs
                hover:bg-gray-600 transition-colors"
            >
              重试
            </button>
          </div>
        )}

        {!error && totalFileCount === 0 && !isLoading && (
          <div className="flex flex-col items-center justify-center h-full text-gray-600 px-6 text-center select-none">
            <div className="text-3xl mb-2.5 opacity-50">📂</div>
            <p className="text-xs text-gray-500 m-0 mb-1.5">暂无文件</p>
            <span className="text-[10px] opacity-60">项目文件夹为空</span>
          </div>
        )}

        {!error && (
          <div className="flex flex-col gap-px">
            {folderCategories.map((category) => {
              const files = groupedFiles.get(category.name) ?? []
              if (files.length === 0 && !expandedFolders.has(category.name))
                return null

              const isExpanded = expandedFolders.has(category.name)

              return (
                <div key={category.name}>
                  {/* 文件夹项 */}
                  <div
                    className="flex items-center gap-1.5 py-1 px-2 rounded cursor-pointer
                      text-xs text-gray-300 select-none transition-colors hover:bg-white/5"
                    onClick={() => toggleFolder(category.name)}
                  >
                    <span className="text-[13px] w-4 text-center flex-shrink-0">
                      {isExpanded ? '📂' : '📁'}
                    </span>
                    <span className="flex-1 overflow-hidden text-ellipsis whitespace-nowrap">
                      {category.label}
                    </span>
                    <span className="text-[10px] text-gray-600 bg-gray-800 px-1.5 py-0.5 rounded-full">
                      {files.length}
                    </span>
                  </div>

                  {/* 展开的文件列表 */}
                  {isExpanded && (
                    <div className="pl-5 flex flex-col gap-px">
                      {files.length === 0 && (
                        <div className="py-1.5 px-2 text-[10px] text-gray-600">
                          无{category.label}文件
                        </div>
                      )}
                      {files.map((file) => (
                        <div
                          key={file.path}
                          className={`flex items-center gap-1.5 py-1 px-2 rounded cursor-pointer
                            text-[11px] transition-colors select-none
                            ${selectedFile === file.path
                              ? 'bg-blue-900/50 text-white'
                              : 'text-gray-400 hover:bg-white/5 hover:text-gray-300'
                            }`}
                          onClick={() => handleSelectFile(file.path)}
                        >
                          <span className="text-xs w-4 text-center flex-shrink-0">
                            {getFileIcon(file.extension ?? '')}
                          </span>
                          <span className="flex-1 overflow-hidden text-ellipsis whitespace-nowrap">
                            {file.name}
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}

/**
 * 将 allFiles 按预设类别（图片/音频/视频/脚本）进行分组
 */
function groupFilesByCategory(
  allFiles: FileEntry[]
): Map<string, FileEntry[]> {
  const grouped = new Map<string, FileEntry[]>()
  for (const cat of folderCategories) {
    grouped.set(cat.name, [])
  }

  for (const file of allFiles) {
    if (file.isDirectory) continue
    const ext = (file.extension ?? '').toLowerCase()
    for (const cat of folderCategories) {
      if (cat.extensions.some((e) => e.toLowerCase() === ext)) {
        grouped.get(cat.name)?.push(file)
        break
      }
    }
  }

  return grouped
}