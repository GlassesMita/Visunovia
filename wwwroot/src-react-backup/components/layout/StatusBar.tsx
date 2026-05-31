import { useTranslation } from 'react-i18next'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useLocalization } from '@/hooks/useLocalization'

/**
 * 底部状态栏组件。
 * 左侧显示当前编辑文件路径（含修改标记），
 * 右侧显示语言切换、缩放比例、节点数量。
 */
export default function StatusBar() {
  const { t } = useTranslation()
  const { currentLanguage, changeLanguage } = useLocalization()
  const currentFile = useEditorStore((s) => s.currentFile)
  const isDirty = useEditorStore((s) => s.isDirty)
  const nodeCount = useNodeGraphStore((s) => s.nodes.length)
  const viewportZoom = useNodeGraphStore((s) => s.viewportZoom)

  const languageLabel = currentLanguage === 'zh' ? '中文' : 'EN'

  return (
    <div className="flex items-center justify-between h-full px-4 text-white text-xs flex-1">
      {/* 左侧：当前文件路径 */}
      <div className="flex items-center gap-5">
        {isDirty && (
          <span className="text-amber-300 opacity-90 whitespace-nowrap">
            ● {t('app.modified', '已修改')}
          </span>
        )}
        <span className="opacity-90 whitespace-nowrap">{currentFile || 'Untitled'}</span>
      </div>

      {/* 右侧：语言切换、缩放、节点数 */}
      <div className="flex items-center gap-5">
        <button
          className="opacity-90 cursor-pointer hover:bg-white/15 rounded px-1.5 py-0.5 transition-colors duration-150 whitespace-nowrap"
          title={t('settings.language')}
          onClick={() => changeLanguage(currentLanguage === 'zh' ? 'en' : 'zh')}
        >
          🌐 {languageLabel}
        </button>
        <span className="opacity-90 whitespace-nowrap">{Math.round(viewportZoom * 100)}%</span>
        <span className="opacity-90 whitespace-nowrap" title="Node count">
          🔷 {nodeCount} {t('common.nodes', 'nodes')}
        </span>
      </div>
    </div>
  )
}