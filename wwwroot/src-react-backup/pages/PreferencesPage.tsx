import { useState, useEffect, useCallback } from 'react'
import { useLocalization } from '@/hooks/useLocalization'
import { SETTINGS_STORAGE_KEY, DEFAULT_SETTINGS } from '@/config/constants'

/** 设置分类定义 */
interface SettingsCategory {
  id: string
  labelKey: string
  fallback: string
}

/** 设置表单数据 */
interface SettingsFormData {
  language: 'en' | 'zh'
  theme: 'light' | 'dark' | 'system'
  fontSize: number
  autoSave: boolean
  autoSaveInterval: number
  previewWidth: number
  previewHeight: number
  allowRemoteAccess: boolean
}

/** 分类列表 */
const CATEGORIES: readonly SettingsCategory[] = [
  { id: 'general', labelKey: 'settings.general', fallback: '常规' },
  { id: 'editor', labelKey: 'settings.editor', fallback: '编辑器' },
  { id: 'preview', labelKey: 'settings.preview', fallback: '预览' },
  { id: 'network', labelKey: 'settings.network', fallback: '网络' },
] as const

/**
 * 从 localStorage 加载已保存的设置，合并默认值。
 */
function loadSettingsFromStorage(): SettingsFormData {
  try {
    const raw = localStorage.getItem(SETTINGS_STORAGE_KEY)
    if (!raw) return { ...getDefaultSettings() }
    const parsed = JSON.parse(raw) as Partial<SettingsFormData>
    return {
      language: parsed.language || DEFAULT_SETTINGS.language,
      theme: parsed.theme || DEFAULT_SETTINGS.theme,
      fontSize: parsed.fontSize ?? DEFAULT_SETTINGS.fontSize,
      autoSave: parsed.autoSave ?? DEFAULT_SETTINGS.autoSave,
      autoSaveInterval: parsed.autoSaveInterval ?? Math.round(DEFAULT_SETTINGS.autoSaveInterval / 1000),
      previewWidth: parsed.previewWidth ?? 1280,
      previewHeight: parsed.previewHeight ?? 720,
      allowRemoteAccess: parsed.allowRemoteAccess ?? false,
    }
  } catch {
    // 异常来源：localStorage 数据格式损坏
    // 处理方法：返回默认设置
    return { ...getDefaultSettings() }
  }
}

function getDefaultSettings(): SettingsFormData {
  return {
    language: DEFAULT_SETTINGS.language,
    theme: DEFAULT_SETTINGS.theme,
    fontSize: DEFAULT_SETTINGS.fontSize,
    autoSave: DEFAULT_SETTINGS.autoSave,
    autoSaveInterval: Math.round(DEFAULT_SETTINGS.autoSaveInterval / 1000),
    previewWidth: 1280,
    previewHeight: 720,
    allowRemoteAccess: false,
  }
}

/**
 * 设置页面（Popup 窗口形式）。
 * 左侧分类导航 + 右侧设置内容区。
 * 保存到 localStorage，通过 window.opener 刷新主窗口。
 */
export default function PreferencesPage() {
  const { t, changeLanguage } = useLocalization()

  const [activeCategory, setActiveCategory] = useState('general')
  const [settings, setSettings] = useState<SettingsFormData>(loadSettingsFromStorage)
  const [isDirty, setIsDirty] = useState(false)
  const [saveMessage, setSaveMessage] = useState('')
  const [saveError, setSaveError] = useState(false)

  const isPopup = window.opener !== null

  /** 更新单个设置字段 */
  const updateField = useCallback(<K extends keyof SettingsFormData>(
    key: K,
    value: SettingsFormData[K]
  ) => {
    setSettings(prev => ({ ...prev, [key]: value }))
    setIsDirty(true)
  }, [])

  /** 语言变更时同步调用 changeLanguage */
  const handleLanguageChange = useCallback((lang: 'en' | 'zh') => {
    updateField('language', lang)
    changeLanguage(lang)
  }, [updateField, changeLanguage])

  /** 主题变更时直接应用到 DOM */
  const handleThemeChange = useCallback((theme: SettingsFormData['theme']) => {
    updateField('theme', theme)
    document.documentElement.setAttribute('data-theme', theme)
  }, [updateField])

  /** 保存设置 */
  const handleSave = useCallback(() => {
    const data = { ...settings }
    localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(data))

    setSaveMessage(t('settings.savedSuccess') || '设置已保存')
    setSaveError(false)
    setIsDirty(false)

    // Popup 模式下刷新父窗口并关闭
    if (isPopup) {
      setTimeout(() => {
        try {
          window.opener!.location.reload()
        } catch {
          // 异常来源：跨域限制导致无法访问父窗口 location
          // 处理方法：静默忽略，仅关闭当前窗口
          console.warn('[PreferencesPage] Cannot access parent window location')
        }
        window.close()
      }, 1000)
    }

    setTimeout(() => setSaveMessage(''), 3000)
  }, [settings, t, isPopup])

  /** 取消 / 重置 */
  const handleCancel = useCallback(() => {
    const defaults = getDefaultSettings()
    setSettings(defaults)
    document.documentElement.setAttribute('data-theme', defaults.theme)
    setIsDirty(false)
  }, [])

  /** 初始化时应用已保存的主题 */
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', settings.theme)
  }, []) // 仅在挂载时执行

  // 预计算每个分类的内容，避免 JSX 中重复分支
  const renderCategoryContent = () => {
    switch (activeCategory) {
      case 'general':
        return (
          <section>
            <h2 className="text-lg font-semibold text-gray-100 mb-4">
              {t('settings.general') || '常规'}
            </h2>

            {/* 语言 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.language') || '语言'}
              </label>
              <select
                value={settings.language}
                onChange={e => handleLanguageChange(e.target.value as 'en' | 'zh')}
                className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
              >
                <option value="zh">中文</option>
                <option value="en">English</option>
              </select>
            </div>

            {/* 主题 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.theme') || '主题'}
              </label>
              <select
                value={settings.theme}
                onChange={e => handleThemeChange(e.target.value as SettingsFormData['theme'])}
                className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
              >
                <option value="light">{t('settings.light') || '浅色'}</option>
                <option value="dark">{t('settings.dark') || '深色'}</option>
                <option value="system">{t('settings.system') || '跟随系统'}</option>
              </select>
            </div>
          </section>
        )

      case 'editor':
        return (
          <section>
            <h2 className="text-lg font-semibold text-gray-100 mb-4">
              {t('settings.editor') || '编辑器'}
            </h2>

            {/* 字体大小 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.fontSize') || '字体大小'}
              </label>
              <input
                type="number"
                min={10}
                max={24}
                value={settings.fontSize}
                onChange={e => updateField('fontSize', Number(e.target.value))}
                className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
              />
            </div>

            {/* 自动保存间隔 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.autoSaveInterval') || '自动保存间隔 (秒)'}
              </label>
              <input
                type="number"
                min={10}
                max={300}
                step={10}
                value={settings.autoSaveInterval}
                onChange={e => updateField('autoSaveInterval', Number(e.target.value))}
                className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
              />
            </div>
          </section>
        )

      case 'preview':
        return (
          <section>
            <h2 className="text-lg font-semibold text-gray-100 mb-4">
              {t('settings.preview') || '预览'}
            </h2>

            {/* 预览宽度 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.previewWidth') || '预览宽度'}
              </label>
              <input
                type="number"
                min={640}
                max={3840}
                step={320}
                value={settings.previewWidth}
                onChange={e => updateField('previewWidth', Number(e.target.value))}
                className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
              />
            </div>

            {/* 预览高度 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.previewHeight') || '预览高度'}
              </label>
              <input
                type="number"
                min={360}
                max={2160}
                step={180}
                value={settings.previewHeight}
                onChange={e => updateField('previewHeight', Number(e.target.value))}
                className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
              />
            </div>
          </section>
        )

      case 'network':
        return (
          <section>
            <h2 className="text-lg font-semibold text-gray-100 mb-4">
              {t('settings.network') || '网络'}
            </h2>

            {/* 允许远程访问 */}
            <div className="flex justify-between items-center mb-3 gap-4">
              <label className="text-gray-300 text-sm shrink-0 min-w-[160px]">
                {t('settings.allowRemoteAccess') || '允许远程访问'}
              </label>
              <input
                type="checkbox"
                checked={settings.allowRemoteAccess}
                onChange={e => updateField('allowRemoteAccess', e.target.checked)}
                className="w-[18px] h-[18px] cursor-pointer accent-blue-500"
              />
            </div>
            <p className="text-gray-500 text-xs mt-2">
              {t('settings.allowRemoteAccessHint') || '开启后可通过局域网内其他设备访问编辑器'}
            </p>
          </section>
        )

      default:
        return null
    }
  }

  return (
    <div className="flex h-screen bg-gray-950 overflow-hidden">
      {/* 左侧分类导航 */}
      <aside className="w-[200px] bg-gray-900 border-r border-gray-700 shrink-0 flex flex-col py-6 px-0">
        <h2 className="text-xl font-semibold text-gray-100 mb-6 px-5">
          {t('settings.title') || '设置'}
        </h2>
        <nav className="flex flex-col gap-1">
          {CATEGORIES.map(cat => (
            <button
              key={cat.id}
              onClick={() => setActiveCategory(cat.id)}
              className={`py-2.5 px-5 bg-transparent border-none text-sm text-left cursor-pointer transition-all duration-150 border-l-[3px] ${
                activeCategory === cat.id
                  ? 'text-gray-100 bg-gray-700/50 border-l-blue-500'
                  : 'text-gray-400 border-l-transparent hover:bg-gray-800 hover:text-gray-100'
              }`}
            >
              {t(cat.labelKey) || cat.fallback}
            </button>
          ))}
        </nav>
      </aside>

      {/* 右侧内容区 */}
      <main className="flex-1 overflow-hidden flex flex-col">
        <div className="flex-1 overflow-y-auto p-6 px-8">
          {/* 角色标题 */}
          <div className="mb-8 pb-6 border-b border-gray-700">
            <h1 className="text-[22px] font-semibold text-gray-100">
              {(() => {
                const cat = CATEGORIES.find(c => c.id === activeCategory)
                return cat ? (t(cat.labelKey) || cat.fallback) : ''
              })()}
            </h1>
          </div>

          {renderCategoryContent()}
        </div>

        {/* 底部操作按钮 */}
        <div className="flex gap-3 justify-end p-6 pt-4 border-t border-gray-700 shrink-0">
          <button
            onClick={handleSave}
            className="px-6 py-2 bg-blue-600 border-none rounded text-gray-100 text-sm font-medium cursor-pointer transition-colors duration-200 hover:bg-blue-500 active:bg-blue-700"
          >
            {t('common.save') || '保存'}
          </button>
          <button
            onClick={handleCancel}
            className="px-6 py-2 bg-gray-700 border-none rounded text-gray-100 text-sm cursor-pointer transition-colors duration-200 hover:bg-gray-600 active:bg-gray-500"
          >
            {t('common.cancel') || '取消'}
          </button>
        </div>

        {/* 保存状态消息 */}
        {saveMessage && (
          <div
            className={`mx-6 mb-4 p-3 rounded text-sm text-center animate-[fadeIn_0.2s_ease] ${
              saveError
                ? 'bg-red-900/50 text-red-300'
                : 'bg-emerald-900/30 text-emerald-300'
            }`}
          >
            {saveMessage}
          </div>
        )}
      </main>
    </div>
  )
}