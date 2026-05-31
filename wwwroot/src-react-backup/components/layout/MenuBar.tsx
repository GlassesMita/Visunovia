import { useState, useEffect, useCallback, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useUIStore } from '@/stores/useUIStore'

interface MenuItem {
  key: string
  labelKey: string
  action?: string
  shortcut?: string
  divider?: boolean
  disabled?: boolean
  checked?: boolean
}

interface Menu {
  key: string
  labelKey: string
  items: MenuItem[]
}

/**
 * 顶部菜单栏组件，实现下拉菜单交互。
 * 包含文件、编辑、视图、工具、帮助菜单。
 * Preferences / About / ProjectSettings 通过 window.open 打开独立 Popup 窗口。
 */
export default function MenuBar() {
  const { t } = useTranslation()
  const uiStore = useUIStore()
  const [activeMenu, setActiveMenu] = useState<string | null>(null)
  const menuBarRef = useRef<HTMLDivElement>(null)

  // 构建菜单数据（使用 computed 等效方式）
  const menus: Menu[] = [
    {
      key: 'file',
      labelKey: 'menu.file',
      items: [
        { key: 'new', labelKey: 'menu.newProject', action: 'newFile', shortcut: 'Ctrl+N' },
        { key: 'open', labelKey: 'menu.openProject', action: 'openFile', shortcut: 'Ctrl+O' },
        { key: 'save', labelKey: 'menu.saveProject', action: 'saveFile', shortcut: 'Ctrl+S' },
        { key: 'saveAs', labelKey: 'menu.saveAs', action: 'saveFileAs', shortcut: 'Ctrl+Shift+S' },
        { key: 'divider1', labelKey: '', divider: true },
        { key: 'projectSettings', labelKey: 'menu.projectSettings', action: 'openProjectSettings' },
        { key: 'divider2', labelKey: '', divider: true },
        { key: 'exit', labelKey: 'menu.exit', action: 'exitApp' },
      ],
    },
    {
      key: 'edit',
      labelKey: 'menu.edit',
      items: [
        { key: 'undo', labelKey: 'menu.undo', action: 'undo', shortcut: 'Ctrl+Z' },
        { key: 'redo', labelKey: 'menu.redo', action: 'redo', shortcut: 'Ctrl+Y' },
        { key: 'divider1', labelKey: '', divider: true },
        { key: 'cut', labelKey: 'menu.cut', action: 'cut', shortcut: 'Ctrl+X' },
        { key: 'copy', labelKey: 'menu.copy', action: 'copy', shortcut: 'Ctrl+C' },
        { key: 'paste', labelKey: 'menu.paste', action: 'paste', shortcut: 'Ctrl+V' },
        { key: 'delete', labelKey: 'menu.delete', action: 'delete', shortcut: 'Del' },
        { key: 'divider2', labelKey: '', divider: true },
        { key: 'selectAll', labelKey: 'menu.selectAll', action: 'selectAll', shortcut: 'Ctrl+A' },
      ],
    },
    {
      key: 'view',
      labelKey: 'menu.view',
      items: [
        {
          key: 'toggleLeft',
          labelKey: 'panels.project',
          action: 'toggleLeft',
          checked: uiStore.isLeftPanelOpen,
        },
        {
          key: 'toggleRight',
          labelKey: 'panels.inspector',
          action: 'toggleRight',
          checked: uiStore.isRightPanelOpen,
        },
        {
          key: 'toggleBottom',
          labelKey: 'panels.console',
          action: 'toggleBottom',
          checked: uiStore.isBottomPanelOpen,
        },
        { key: 'divider1', labelKey: '', divider: true },
        { key: 'preferences', labelKey: 'menu.preferences', action: 'openPreferences' },
      ],
    },
    {
      key: 'tools',
      labelKey: 'menu.tools',
      items: [
        { key: 'projectSettings', labelKey: 'tools.projectSettings', action: 'openProjectSettings' },
      ],
    },
    {
      key: 'help',
      labelKey: 'menu.help',
      items: [
        { key: 'about', labelKey: 'menu.about', action: 'showAbout' },
      ],
    },
  ]

  // 执行菜单动作
  const executeAction = useCallback(
    (action: string) => {
      setActiveMenu(null)

      switch (action) {
        case 'newFile':
          // 新建项目 - 后续在 Task 中实现
          break
        case 'openFile': {
          const openSceneId = prompt('请输入要打开的场景 ID:')
          if (openSceneId) {
            // 后续 Task 中接入 loadSceneGraph
          }
          break
        }
        case 'saveFile':
          // 保存 - 后续 Task 中实现
          break
        case 'saveFileAs': {
          const saveAsName = prompt('请输入文件名:')
          if (saveAsName) {
            // 后续 Task 中接入 saveSceneGraph
          }
          break
        }
        case 'exitApp':
          break
        case 'undo':
        case 'redo':
        case 'cut':
        case 'copy':
        case 'paste':
        case 'delete':
        case 'selectAll':
          // 快捷键操作 - 后续 Task 中通过快捷键系统实现
          break
        case 'toggleLeft':
          uiStore.toggleLeftPanel()
          break
        case 'toggleRight':
          uiStore.toggleRightPanel()
          break
        case 'toggleBottom':
          uiStore.toggleBottomPanel()
          break
        case 'openPreferences':
          window.open('/Preferences', 'Preferences', 'width=800,height=600,scrollbars=yes,resizable=yes')
          break
        case 'openProjectSettings':
          window.open('/ProjectSettings', 'ProjectSettings', 'width=800,height=600,scrollbars=yes,resizable=yes')
          break
        case 'showAbout':
          window.open('/About', 'About', 'width=600,height=500,scrollbars=yes,resizable=yes')
          break
      }
    },
    [uiStore],
  )

  // 点击外部关闭下拉菜单
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuBarRef.current && !menuBarRef.current.contains(e.target as Node)) {
        setActiveMenu(null)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [])

  return (
    <div ref={menuBarRef} className="flex items-center h-full px-2 select-none">
      <div className="flex gap-0.5">
        {menus.map((menu) => (
          <div key={menu.key} className="relative">
            <button
              className={`px-2.5 py-1 text-[13px] rounded transition-colors duration-100 ${
                activeMenu === menu.key
                  ? 'bg-white/10 text-white'
                  : 'text-gray-300 hover:bg-white/8 hover:text-white'
              }`}
              onClick={() => setActiveMenu(activeMenu === menu.key ? null : menu.key)}
            >
              {t(menu.labelKey)}
            </button>

            {activeMenu === menu.key && (
              <div className="absolute top-full left-0 mt-1 min-w-[220px] bg-gray-800 border border-gray-600 rounded-md shadow-xl z-[1000] py-1">
                {menu.items.map((item) =>
                  item.divider ? (
                    <div key={item.key} className="h-px bg-gray-600 my-1 mx-3" />
                  ) : (
                    <button
                      key={item.key}
                      disabled={item.disabled}
                      className={`w-full flex items-center justify-between px-4 py-1.5 text-xs transition-colors duration-100 ${
                        item.disabled
                          ? 'opacity-40 cursor-not-allowed text-gray-500'
                          : 'text-gray-300 hover:bg-blue-800 hover:text-white cursor-pointer'
                      }`}
                      onClick={(e) => {
                        e.stopPropagation()
                        if (!item.disabled && item.action) {
                          executeAction(item.action)
                        }
                      }}
                    >
                      <span className="flex-1 text-left">{t(item.labelKey)}</span>
                      <span className="flex items-center gap-2 ml-6">
                        {item.checked !== undefined && (
                          <span className="w-4 text-center text-xs">
                            {item.checked ? '✓' : ''}
                          </span>
                        )}
                        {item.shortcut && (
                          <span className="text-gray-500 text-[11px]">{item.shortcut}</span>
                        )}
                      </span>
                    </button>
                  ),
                )}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  )
}