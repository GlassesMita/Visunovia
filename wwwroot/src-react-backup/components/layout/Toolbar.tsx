import { FilePlus, FolderOpen, Save, Undo2, Redo2, ZoomIn, ZoomOut, Maximize2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'

/**
 * 工具栏组件，位于 MenuBar 下方。
 * 提供快捷操作按钮：新建、打开、保存、撤销、重做、缩放控制。
 */
export default function Toolbar() {
  const { t } = useTranslation()

  return (
    <div className="flex items-center h-full px-3 gap-1">
      {/* 文件操作 */}
      <div className="flex gap-0.5">
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 transition-all duration-150"
          title={t('menu.newProject')}
        >
          <FilePlus size={18} />
        </button>
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 transition-all duration-150"
          title={t('menu.openProject')}
        >
          <FolderOpen size={18} />
        </button>
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 transition-all duration-150"
          title={t('menu.saveProject')}
        >
          <Save size={18} />
        </button>
      </div>

      {/* 分隔线 */}
      <div className="w-px h-[22px] bg-gray-600 mx-1.5" />

      {/* 撤销 / 重做 */}
      <div className="flex gap-0.5">
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 disabled:opacity-35 disabled:cursor-not-allowed transition-all duration-150"
          title={`${t('menu.undo')} (Ctrl+Z)`}
        >
          <Undo2 size={18} />
        </button>
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 disabled:opacity-35 disabled:cursor-not-allowed transition-all duration-150"
          title={`${t('menu.redo')} (Ctrl+Y)`}
        >
          <Redo2 size={18} />
        </button>
      </div>

      {/* 分隔线 */}
      <div className="w-px h-[22px] bg-gray-600 mx-1.5" />

      {/* 缩放控制 */}
      <div className="flex gap-0.5">
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 transition-all duration-150"
          title={t('editor.zoomOut')}
        >
          <ZoomOut size={18} />
        </button>
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 transition-all duration-150"
          title={t('editor.zoomIn')}
        >
          <ZoomIn size={18} />
        </button>
        <button
          className="flex items-center justify-center w-8 h-8 bg-transparent border-none rounded text-gray-300 hover:bg-white/10 hover:text-white active:bg-white/15 active:scale-95 transition-all duration-150"
          title={t('editor.fitView')}
        >
          <Maximize2 size={18} />
        </button>
      </div>
    </div>
  )
}