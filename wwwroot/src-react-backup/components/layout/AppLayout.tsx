import { Outlet } from 'react-router-dom'
import { type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useUIStore } from '@/stores/useUIStore'
import ErrorBoundary from '@/components/shared/ErrorBoundary'
import MenuBar from './MenuBar'
import Toolbar from './Toolbar'
import StatusBar from './StatusBar'
import ProjectPanel from '@/components/panels/ProjectPanel'
import HierarchyPanel from '@/components/panels/HierarchyPanel'
import InspectorPanel from '@/components/panels/InspectorPanel'
import ConsolePanel from '@/components/panels/ConsolePanel'

interface AppLayoutProps {
  children?: ReactNode
}

/**
 * 主布局组件，采用 Unity 编辑器风格的三栏式布局。
 * 顶部：MenuBar + Toolbar
 * 中部：左侧面板区（项目/节点面板） + 中央编辑器（children 或 Outlet） + 右侧面板区（属性/层级面板）
 * 底部：控制台面板 + StatusBar
 * 面板折叠/展开由 useUIStore 控制。
 *
 * 当传入 children 时直接渲染 children，否则使用 React Router 的 Outlet。
 */
export default function AppLayout({ children }: AppLayoutProps) {
  const { t } = useTranslation()
  const uiStore = useUIStore()

  const isLeftOpen = uiStore.isLeftPanelOpen
  const isRightOpen = uiStore.isRightPanelOpen
  const isBottomOpen = uiStore.isBottomPanelOpen
  const activeLeft = uiStore.activeLeftPanel
  const activeRight = uiStore.activeRightPanel

  return (
    <div className="app-layout flex flex-col h-screen overflow-hidden bg-gray-950">
      {/* 顶部：菜单栏 */}
      <header className="h-8 min-h-[32px] bg-gray-800 border-b border-gray-600 shrink-0">
        <MenuBar />
      </header>

      {/* 工具栏 */}
      <div className="h-10 min-h-[40px] bg-gray-750 border-b border-gray-600 shrink-0">
        <Toolbar />
      </div>

      {/* 中部主体 */}
      <div className="app-main flex flex-1 overflow-hidden min-h-0">
        {/* 左侧面板 */}
        <aside
          className={`flex overflow-hidden shrink-0 transition-all duration-150 border-r border-gray-600 ${
            isLeftOpen ? 'w-64' : 'w-0 border-r-0'
          }`}
        >
          <div className="flex w-full bg-gray-800">
            {/* 面板标签栏 */}
            <div className="flex flex-col w-10 bg-gray-750 border-r border-gray-600 shrink-0">
              <button
                className={`flex flex-col items-center justify-center gap-0.5 h-14 border-b border-gray-600 text-[10px] transition-colors duration-150 ${
                  isLeftOpen && activeLeft === 'project'
                    ? 'text-white bg-blue-900'
                    : 'text-gray-500 hover:text-gray-300 hover:bg-white/5'
                }`}
                title={t('panels.project')}
                onClick={() => {
                  if (!isLeftOpen) {
                    uiStore.toggleLeftPanel()
                  }
                  uiStore.setActiveLeftPanel('project')
                }}
              >
                <span className="text-sm">📁</span>
                <span className="[writing-mode:vertical-rl] text-[9px] tracking-wider">{t('panels.project')}</span>
              </button>
              <button
                className={`flex flex-col items-center justify-center gap-0.5 h-14 border-b border-gray-600 text-[10px] transition-colors duration-150 ${
                  isLeftOpen && activeLeft === 'hierarchy'
                    ? 'text-white bg-blue-900'
                    : 'text-gray-500 hover:text-gray-300 hover:bg-white/5'
                }`}
                title={t('panels.hierarchy')}
                onClick={() => {
                  if (!isLeftOpen) {
                    uiStore.toggleLeftPanel()
                  }
                  uiStore.setActiveLeftPanel('hierarchy')
                }}
              >
                <span className="text-sm">🔷</span>
                <span className="[writing-mode:vertical-rl] text-[9px] tracking-wider">{t('panels.hierarchy')}</span>
              </button>
            </div>

            {/* 面板内容区 */}
            {isLeftOpen && (
              <ErrorBoundary>
                <div className="flex-1 overflow-hidden flex flex-col">
                  {activeLeft === 'project' && (
                    <ProjectPanel />
                  )}
                  {activeLeft === 'hierarchy' && (
                    <HierarchyPanel />
                  )}
                </div>
              </ErrorBoundary>
            )}
          </div>

          {/* 面板折叠切换按钮 */}
          <button
            className="w-4 h-12 bg-gray-750 border border-gray-600 border-l-0 rounded-r flex items-center justify-center text-gray-500 hover:bg-gray-650 hover:text-white transition-colors duration-150 shrink-0 self-center -mr-4 z-10"
            title={isLeftOpen ? (t('common.close') || 'Close') : (t('panels.project'))}
            onClick={() => uiStore.toggleLeftPanel()}
          >
            {isLeftOpen ? '◀' : '▶'}
          </button>
        </aside>

        {/* 中央编辑区：优先渲染 children，否则使用 Outlet */}
        <main className="flex-1 bg-gray-950 overflow-hidden min-w-0">
          <ErrorBoundary>
            {children ?? <Outlet />}
          </ErrorBoundary>
        </main>

        {/* 右侧面板 */}
        <aside
          className={`flex overflow-hidden shrink-0 transition-all duration-150 border-l border-gray-600 ${
            isRightOpen ? 'w-80' : 'w-0 border-l-0'
          }`}
        >
          {/* 面板折叠切换按钮 */}
          <button
            className="w-4 h-12 bg-gray-750 border border-gray-600 border-r-0 rounded-l flex items-center justify-center text-gray-500 hover:bg-gray-650 hover:text-white transition-colors duration-150 shrink-0 self-center -ml-4 z-10"
            title={isRightOpen ? (t('common.close') || 'Close') : (t('panels.inspector'))}
            onClick={() => uiStore.toggleRightPanel()}
          >
            {isRightOpen ? '▶' : '◀'}
          </button>

          <div className="flex flex-col w-full bg-gray-800">
            {/* 面板标签栏 */}
            {isRightOpen && (
              <div className="flex h-8 bg-gray-750 border-b border-gray-600 shrink-0">
                <button
                  className={`flex-1 text-[11px] uppercase transition-colors duration-150 ${
                    activeRight === 'inspector'
                      ? 'text-white bg-blue-900'
                      : 'text-gray-500 hover:text-gray-300 hover:bg-white/5'
                  }`}
                  onClick={() => uiStore.setActiveRightPanel('inspector')}
                >
                  {t('panels.inspector')}
                </button>
                <button
                  className={`flex-1 text-[11px] uppercase transition-colors duration-150 ${
                    activeRight === 'preview'
                      ? 'text-white bg-blue-900'
                      : 'text-gray-500 hover:text-gray-300 hover:bg-white/5'
                  }`}
                  onClick={() => uiStore.setActiveRightPanel('preview')}
                >
                  {t('panels.preview')}
                </button>
              </div>
            )}

            {/* 面板内容区 */}
            {isRightOpen && (
              <ErrorBoundary>
                <div className="flex-1 overflow-hidden flex flex-col">
                  {activeRight === 'inspector' && (
                    <InspectorPanel />
                  )}
                  {activeRight === 'preview' && (
                    <div className="flex items-center justify-center h-full text-gray-500 text-xs bg-gray-900">
                      {t('panels.preview')} - 预览功能尚未实现
                    </div>
                  )}
                </div>
              </ErrorBoundary>
            )}
          </div>
        </aside>
      </div>

      {/* 底部面板：控制台 */}
      <div
        className={`bg-gray-800 border-t border-gray-600 overflow-hidden shrink-0 transition-all duration-200 ${
          isBottomOpen ? 'h-48' : 'h-0 border-t-0'
        }`}
      >
        <ErrorBoundary>
          <div className="flex-1 overflow-hidden">
            <ConsolePanel />
          </div>
        </ErrorBoundary>
      </div>

      {/* 底部状态栏 */}
      <footer className="h-6 min-h-[24px] bg-[#1e3a5f] flex items-center shrink-0 relative">
        <StatusBar />
        <button
          className={`absolute right-3 top-1/2 -translate-y-1/2 px-2.5 py-0.5 text-[11px] rounded border border-white/20 text-white transition-colors duration-150 ${
            isBottomOpen
              ? 'bg-white/20 hover:bg-white/30'
              : 'bg-black/20 hover:bg-black/40'
          }`}
          title={t('panels.console')}
          onClick={() => uiStore.toggleBottomPanel()}
        >
          {t('panels.console')}
        </button>
      </footer>
    </div>
  )
}