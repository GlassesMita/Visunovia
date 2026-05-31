import { useState, useEffect, useCallback } from 'react'
import { useLocalization } from '@/hooks/useLocalization'
import { DEFAULT_PROJECT_NAME, DEFAULT_RESOLUTION, DEFAULT_VOLUME, STORAGE_KEYS } from '@/config/constants'

/** 项目设置表单数据 */
interface ProjectSettingsFormData {
  projectName: string
  author: string
  version: string
  resolutionWidth: number
  resolutionHeight: number
  bgmVolume: number
  voiceVolume: number
  bgmLoop: boolean
  language: 'zh' | 'en'
  theme: 'dark' | 'light'
}

/** 默认项目设置 */
const DEFAULTS: ProjectSettingsFormData = {
  projectName: DEFAULT_PROJECT_NAME,
  author: '',
  version: '1.0.0',
  resolutionWidth: DEFAULT_RESOLUTION.width,
  resolutionHeight: DEFAULT_RESOLUTION.height,
  bgmVolume: DEFAULT_VOLUME.bgm,
  voiceVolume: DEFAULT_VOLUME.voice,
  bgmLoop: true,
  language: 'zh',
  theme: 'dark',
}

/**
 * 从 localStorage 加载已保存的项目设置。
 */
function loadProjectSettings(): ProjectSettingsFormData {
  try {
    const raw = localStorage.getItem(STORAGE_KEYS.PROJECT_SETTINGS)
    if (!raw) return { ...DEFAULTS }
    const parsed = JSON.parse(raw) as Partial<ProjectSettingsFormData>
    return {
      projectName: parsed.projectName || DEFAULTS.projectName,
      author: parsed.author || DEFAULTS.author,
      version: parsed.version || DEFAULTS.version,
      resolutionWidth: parsed.resolutionWidth ?? DEFAULTS.resolutionWidth,
      resolutionHeight: parsed.resolutionHeight ?? DEFAULTS.resolutionHeight,
      bgmVolume: parsed.bgmVolume ?? DEFAULTS.bgmVolume,
      voiceVolume: parsed.voiceVolume ?? DEFAULTS.voiceVolume,
      bgmLoop: parsed.bgmLoop ?? DEFAULTS.bgmLoop,
      language: parsed.language || DEFAULTS.language,
      theme: parsed.theme || DEFAULTS.theme,
    }
  } catch {
    // 异常来源：localStorage 数据格式损坏
    // 处理方法：返回默认设置
    return { ...DEFAULTS }
  }
}

/**
 * 项目设置页面（Popup 窗口形式）。
 * 包含项目名称、作者、版本、分辨率、音频、语言和主题设置。
 */
export default function ProjectSettingsPage() {
  const { t } = useLocalization()

  const [settings, setSettings] = useState<ProjectSettingsFormData>(loadProjectSettings)
  const [isDirty, setIsDirty] = useState(false)
  const [saveMessage, setSaveMessage] = useState('')
  const [saveError, setSaveError] = useState(false)
  const [isSaving, setIsSaving] = useState(false)

  const isPopup = window.opener !== null

  /** 更新单个设置字段 */
  const updateField = useCallback(<K extends keyof ProjectSettingsFormData>(
    key: K,
    value: ProjectSettingsFormData[K]
  ) => {
    setSettings(prev => ({ ...prev, [key]: value }))
    setIsDirty(true)
  }, [])

  /** 保存设置到 localStorage */
  const handleSave = useCallback(async () => {
    setIsSaving(true)
    setSaveMessage('')

    try {
      const data = { ...settings }
      localStorage.setItem(STORAGE_KEYS.PROJECT_SETTINGS, JSON.stringify(data))

      setSaveMessage(t('projectSettings.savedSuccess') || '设置已保存')
      setSaveError(false)
      setIsDirty(false)
    } catch {
      // 异常来源：localStorage 存储空间不足或浏览器限制
      // 处理方法：提示用户保存失败
      setSaveMessage(t('projectSettings.saveFailed') || '保存失败')
      setSaveError(true)
    } finally {
      setIsSaving(false)
    }

    setTimeout(() => setSaveMessage(''), 3000)
  }, [settings, t])

  /** 重置为默认值 */
  const handleReset = useCallback(() => {
    setSettings({ ...DEFAULTS })
    setIsDirty(false)
  }, [])

  /** 初始化加载 */
  useEffect(() => {
    const saved = loadProjectSettings()
    setSettings(saved)
  }, [])

  return (
    <div className="flex justify-center p-10 bg-gray-950 min-h-screen box-border">
      <div className="w-full max-w-[700px] bg-gray-900 rounded-lg p-6 box-border">
        <h1 className="text-2xl font-semibold text-gray-100 mb-6">
          {t('projectSettings.title') || '项目设置'}
        </h1>

        {/* 项目信息 */}
        <div className="mb-8 pb-6 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-4">
            {t('projectSettings.project') || '项目信息'}
          </h2>

          {/* 项目名称 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.projectName') || '项目名称'}
            </label>
            <input
              type="text"
              value={settings.projectName}
              onChange={e => updateField('projectName', e.target.value)}
              placeholder={t('projectSettings.projectNamePlaceholder') || 'Untitled Project'}
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            />
          </div>

          {/* 作者 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.author') || '作者'}
            </label>
            <input
              type="text"
              value={settings.author}
              onChange={e => updateField('author', e.target.value)}
              placeholder={t('projectSettings.authorPlaceholder') || 'Author'}
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            />
          </div>

          {/* 版本 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.version') || '版本'}
            </label>
            <input
              type="text"
              value={settings.version}
              onChange={e => updateField('version', e.target.value)}
              placeholder="1.0.0"
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            />
          </div>
        </div>

        {/* 视频设置 */}
        <div className="mb-8 pb-6 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-4">
            {t('projectSettings.video') || '视频设置'}
          </h2>

          {/* 分辨率 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.resolution') || '默认分辨率'}
            </label>
            <div className="flex items-center gap-2">
              <input
                type="number"
                value={settings.resolutionWidth}
                onChange={e => updateField('resolutionWidth', Number(e.target.value))}
                min={640}
                max={7680}
                className="w-[100px] px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm transition-colors duration-150 focus:outline-none focus:border-blue-500"
              />
              <span className="text-gray-100 text-sm">×</span>
              <input
                type="number"
                value={settings.resolutionHeight}
                onChange={e => updateField('resolutionHeight', Number(e.target.value))}
                min={360}
                max={4320}
                className="w-[100px] px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm transition-colors duration-150 focus:outline-none focus:border-blue-500"
              />
            </div>
          </div>
        </div>

        {/* 音频设置 */}
        <div className="mb-8 pb-6 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-4">
            {t('projectSettings.audio') || '音频设置'}
          </h2>

          {/* BGM 音量 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.bgmVolume') || '默认 BGM 音量'}
            </label>
            <input
              type="number"
              value={settings.bgmVolume}
              onChange={e => updateField('bgmVolume', Number(e.target.value))}
              min={0}
              max={100}
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            />
          </div>

          {/* 语音音量 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.voiceVolume') || '默认语音音量'}
            </label>
            <input
              type="number"
              value={settings.voiceVolume}
              onChange={e => updateField('voiceVolume', Number(e.target.value))}
              min={0}
              max={100}
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            />
          </div>

          {/* BGM 循环 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.bgmLoop') || '默认 BGM 循环'}
            </label>
            <input
              type="checkbox"
              checked={settings.bgmLoop}
              onChange={e => updateField('bgmLoop', e.target.checked)}
              className="w-[18px] h-[18px] cursor-pointer accent-blue-500"
            />
          </div>
        </div>

        {/* 通用设置 */}
        <div className="mb-8 pb-6 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-4">
            {t('projectSettings.general') || '通用设置'}
          </h2>

          {/* 默认语言 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.language') || '默认语言'}
            </label>
            <select
              value={settings.language}
              onChange={e => updateField('language', e.target.value as 'zh' | 'en')}
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            >
              <option value="zh">{t('projectSettings.langZh') || '中文'}</option>
              <option value="en">{t('projectSettings.langEn') || 'English'}</option>
            </select>
          </div>

          {/* 默认主题 */}
          <div className="flex justify-between items-center mb-3 gap-4">
            <label className="text-gray-100 text-sm shrink-0 min-w-[160px]">
              {t('projectSettings.theme') || '默认主题'}
            </label>
            <select
              value={settings.theme}
              onChange={e => updateField('theme', e.target.value as 'dark' | 'light')}
              className="px-3 py-1.5 bg-gray-700 border border-gray-600 rounded text-gray-100 text-sm min-w-[180px] max-w-[250px] transition-colors duration-150 focus:outline-none focus:border-blue-500"
            >
              <option value="dark">{t('projectSettings.dark') || '深色'}</option>
              <option value="light">{t('projectSettings.light') || '浅色'}</option>
            </select>
          </div>
        </div>

        {/* 操作按钮区域 */}
        <div className="flex gap-3 justify-end mt-6 pt-4 border-t border-gray-700">
          <button
            onClick={handleSave}
            disabled={isSaving}
            className="px-6 py-2 bg-blue-600 border-none rounded text-gray-100 text-sm font-medium cursor-pointer transition-colors duration-200 hover:bg-blue-500 active:bg-blue-700 disabled:opacity-60 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isSaving
              ? (t('projectSettings.saving') || '保存中...')
              : (t('common.save') || '保存')}
          </button>
          <button
            onClick={handleReset}
            className="px-6 py-2 bg-gray-700 border-none rounded text-gray-100 text-sm cursor-pointer transition-colors duration-200 hover:bg-gray-600 active:bg-gray-500"
          >
            {t('projectSettings.reset') || '重置'}
          </button>
        </div>

        {/* 保存状态消息 */}
        {saveMessage && (
          <div
            className={`mt-4 p-3 rounded text-sm text-center animate-[fadeIn_0.2s_ease] ${
              saveError
                ? 'bg-red-900/50 text-red-300'
                : 'bg-emerald-900/30 text-emerald-300'
            }`}
          >
            {saveMessage}
          </div>
        )}
      </div>
    </div>
  )
}