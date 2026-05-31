import { useState, useEffect, useMemo } from 'react'
import type { LucideIcon } from 'lucide-react'
import { X, Search, Image, Music, Video } from 'lucide-react'
import { getEntries } from '@/api/fileBrowser'

/** 支持的资源类型 */
export type ResourceType = 'image' | 'audio' | 'video' | 'font' | 'data'

/** 各资源类型对应的文件扩展名集合 */
export const RESOURCE_TYPE_EXTENSIONS: Record<ResourceType, string[]> = {
  image: ['.png', '.jpg', '.jpeg', '.gif', '.bmp', '.svg', '.webp', '.ico'],
  audio: ['.mp3', '.wav', '.ogg', '.flac', '.aac', '.m4a', '.opus'],
  video: ['.mp4', '.webm', '.avi', '.mov', '.mkv'],
  font: ['.ttf', '.otf', '.woff', '.woff2'],
  data: ['.json', '.xml', '.csv', '.txt', '.yaml', '.yml'],
}

/** 资源类型选项卡定义 */
interface ResourceTab {
  type: ResourceType
  label: string
  icon: LucideIcon
}

const RESOURCE_TABS: ResourceTab[] = [
  { type: 'image', label: '图片', icon: Image },
  { type: 'audio', label: '音频', icon: Music },
  { type: 'video', label: '视频', icon: Video },
]

/** 文件条目（经处理后的展示用数据） */
interface FileEntry {
  name: string
  path: string
  ext: string
  thumbnail: string
  isSelected: boolean
}

interface ResourcePickerModalProps {
  /** 是否可见 */
  visible: boolean
  /** 资源类型（初始选中的选项卡） */
  resourceType: ResourceType
  /** 当前已选中的路径 */
  currentPath?: string
  /** 确认回调，传入选中路径 */
  onSelect: (path: string) => void
  /** 取消回调 */
  onCancel: () => void
}

/** 默认浏览目录 */
const DEFAULT_BROWSE_PATH = 'assets'

export default function ResourcePickerModal({
  visible,
  resourceType,
  currentPath,
  onSelect,
  onCancel,
}: ResourcePickerModalProps) {
  const [activeTab, setActiveTab] = useState<ResourceType>(resourceType)
  const [searchQuery, setSearchQuery] = useState('')
  const [selectedPath, setSelectedPath] = useState(currentPath || '')
  const [files, setFiles] = useState<FileEntry[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  /** 图片类型集合 */
  const IMAGE_TYPES: ResourceType[] = ['image']

  /** 当前选项卡是否为图片类型 */
  const isImageType = IMAGE_TYPES.includes(activeTab)

  /** 打开时重置状态并加载文件 */
  useEffect(() => {
    if (!visible) return

    setActiveTab(resourceType)
    setSelectedPath(currentPath || '')
    setSearchQuery('')
    setError(null)
    loadFiles(resourceType)
  }, [visible, resourceType, currentPath])

  /** 加载文件列表 */
  const loadFiles = async (type: ResourceType) => {
    setIsLoading(true)
    setError(null)
    try {
      const data = await getEntries(DEFAULT_BROWSE_PATH)
      const exts = RESOURCE_TYPE_EXTENSIONS[type] || []
      const filtered = data.entries
        .filter((entry) => {
          if (entry.isDirectory) return false
          const ext = entry.name.slice(entry.name.lastIndexOf('.')).toLowerCase()
          return exts.includes(ext)
        })
        .map((entry) => ({
          name: entry.name,
          path: `${DEFAULT_BROWSE_PATH}/${entry.name}`,
          ext: entry.name.slice(entry.name.lastIndexOf('.')).toLowerCase(),
          thumbnail: isImageType
            ? `/assets/${encodeURIComponent(`${DEFAULT_BROWSE_PATH}/${entry.name}`)}`
            : '',
          isSelected: false,
        }))
      setFiles(
        filtered.map((f) => ({
          ...f,
          isSelected: f.path === (currentPath || ''),
        }))
      )
    } catch (err) {
      console.error('加载文件列表失败:', err)
      setError('无法加载资源文件列表，请检查文件浏览器 API 是否正常运行')
      setFiles([])
    } finally {
      setIsLoading(false)
    }
  }

  /** 切换选项卡时重新加载 */
  const handleTabChange = (tab: ResourceType) => {
    setActiveTab(tab)
    setSelectedPath('')
    setSearchQuery('')
    loadFiles(tab)
  }

  /** 过滤后的文件列表 */
  const filteredFiles = useMemo(() => {
    const query = searchQuery.trim().toLowerCase()
    if (!query) return files
    return files.filter((f) => f.name.toLowerCase().includes(query))
  }, [files, searchQuery])

  /** 获取文件图标字母 */
  const getFileIconLetter = (ext: string): string => {
    const map: Record<string, string> = {
      '.mp3': '♫',
      '.wav': '♪',
      '.ogg': '♬',
      '.flac': '♩',
      '.aac': '♫',
      '.mp4': '▶',
      '.webm': '▶',
      '.avi': '▶',
      '.mov': '▶',
      '.mkv': '▶',
      '.m4a': '♫',
      '.opus': '♫',
    }
    return map[ext] || '?'
  }

  /** 选择 / 取消选择文件 */
  const handleSelectFile = (path: string) => {
    setSelectedPath((prev) => (prev === path ? '' : path))
  }

  /** 确认选择 */
  const handleConfirm = () => {
    if (selectedPath) {
      onSelect(selectedPath)
    }
  }

  /** 取消并重置 */
  const handleCancel = () => {
    setSelectedPath('')
    setSearchQuery('')
    onCancel()
  }

  /** 点击遮罩层关闭 */
  const handleOverlayClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      handleCancel()
    }
  }

  /** 双击直接确认选择 */
  const handleDoubleClick = (path: string) => {
    setSelectedPath(path)
    onSelect(path)
  }

  if (!visible) return null

  return (
    <div
      className="fixed inset-0 bg-black/60 flex items-center justify-center z-[10000]"
      onClick={handleOverlayClick}
    >
      <div className="w-[600px] max-h-[70vh] bg-[#1e1e2e] rounded-xl shadow-2xl flex flex-col overflow-hidden">
        {/* 标题栏 */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-[#333346]">
          <span className="text-[15px] font-semibold text-gray-200">
            选择资源
          </span>
          <button
            className="bg-none border-none text-gray-500 hover:text-white hover:bg-white/10 rounded p-1 transition-colors"
            onClick={handleCancel}
          >
            <X size={18} />
          </button>
        </div>

        {/* 资源类型选项卡 */}
        <div className="flex border-b border-[#333346] px-5">
          {RESOURCE_TABS.map((tab) => {
            const Icon = tab.icon
            const isActive = activeTab === tab.type
            return (
              <button
                key={tab.type}
                className={`flex items-center gap-1.5 px-3 py-2 text-[13px] font-medium border-b-2 transition-colors ${
                  isActive
                    ? 'text-purple-400 border-purple-400'
                    : 'text-gray-500 border-transparent hover:text-gray-300'
                }`}
                onClick={() => handleTabChange(tab.type)}
              >
                <Icon size={14} />
                {tab.label}
              </button>
            )
          })}
        </div>

        {/* 搜索框 */}
        <div className="flex items-center gap-2 px-5 py-2.5">
          <Search size={14} className="text-gray-500 shrink-0" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="搜索资源文件..."
            className="flex-1 px-3 py-1.5 bg-[#2a2a3c] border border-[#444] rounded text-[13px] text-gray-200 outline-none placeholder:text-gray-500 focus:border-purple-500 transition-colors"
          />
          {searchQuery && (
            <button
              className="text-gray-500 hover:text-gray-300"
              onClick={() => setSearchQuery('')}
            >
              <X size={14} />
            </button>
          )}
        </div>

        {/* 文件列表 */}
        <div className="flex-1 px-4 py-4 overflow-y-auto min-h-[200px] max-h-[50vh]">
          {isLoading ? (
            <div className="flex items-center justify-center py-10 text-gray-500 text-sm">
              加载中...
            </div>
          ) : error ? (
            <div className="flex items-center justify-center py-10 text-red-400 text-sm">
              {error}
            </div>
          ) : filteredFiles.length === 0 ? (
            <div className="text-center text-gray-600 py-10 text-sm">
              无匹配的资源文件
            </div>
          ) : (
            <div className="grid gap-2.5 grid-cols-[repeat(auto-fill,minmax(110px,1fr))]">
              {filteredFiles.map((file) => (
                <div
                  key={file.path}
                  className={`flex flex-col items-center gap-1.5 p-2 rounded-lg border-2 cursor-pointer transition-all duration-150 bg-[#2a2a3c] ${
                    file.path === selectedPath
                      ? 'border-purple-500 bg-purple-500/15'
                      : 'border-transparent hover:border-gray-600 hover:bg-[#333348]'
                  }`}
                  onClick={() => handleSelectFile(file.path)}
                  onDoubleClick={() => handleDoubleClick(file.path)}
                >
                  {/* 缩略图或文件图标 */}
                  {isImageType && file.thumbnail ? (
                    <img
                      src={file.thumbnail}
                      alt={file.name}
                      className="w-20 h-15 object-cover rounded bg-black"
                      loading="lazy"
                    />
                  ) : (
                    <div className="w-20 h-15 flex items-center justify-center text-[28px] rounded bg-[#222235] text-gray-500">
                      {getFileIconLetter(file.ext)}
                    </div>
                  )}
                  <span
                    className="text-[11px] text-gray-400 text-center w-full overflow-hidden text-ellipsis whitespace-nowrap"
                    title={file.name}
                  >
                    {file.name}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* 底部按钮 */}
        <div className="flex justify-end gap-3 px-5 py-3.5 border-t border-[#333346]">
          <button
            className="px-5 py-2 rounded-md bg-[#333346] text-gray-300 text-[13px] font-medium hover:bg-[#444459] transition-colors"
            onClick={handleCancel}
          >
            取消
          </button>
          <button
            className="px-5 py-2 rounded-md bg-purple-600 text-white text-[13px] font-medium hover:bg-purple-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            disabled={!selectedPath}
            onClick={handleConfirm}
          >
            确认
          </button>
        </div>
      </div>
    </div>
  )
}