import { useCallback } from 'react'
import { useLocalization } from '@/hooks/useLocalization'

/** 应用版本号 */
const APP_VERSION = '0.0.1-alpha'
/** 构建日期 */
const BUILD_DATE = new Date().toISOString().split('T')[0]

/** 技术栈信息 */
const TECH_STACK = [
  { name: 'React', version: '19.x' },
  { name: 'TypeScript', version: '5.x' },
  { name: 'Vite', version: '6.x' },
  { name: 'Zustand', version: '5.x' },
  { name: 'React Router', version: '7.x' },
  { name: 'ASP.NET Core', version: '8.x' },
]

/** 功能列表 */
const FEATURES = [
  'Node-based visual scripting',
  'Visual novel scene editing',
  'Character sprite management',
  'Background and BGM control',
  'Dialogue system with branching',
  'Undo/Redo support',
  'Multi-language support (i18n)',
  'Dark/Light theme support',
  'Keyboard shortcuts',
  'Project persistence',
]

/** 外部链接 */
const LINKS = [
  { name: 'GitHub Repository', url: 'https://github.com/visunovia/visunovia-editor', icon: '📦' },
  { name: 'Documentation', url: 'https://docs.visunovia.dev', icon: '📚' },
  { name: 'Issue Tracker', url: 'https://github.com/visunovia/visunovia-editor/issues', icon: '🐛' },
  { name: 'Community Discord', url: 'https://discord.gg/visunovia', icon: '💬' },
]

/**
 * 关于页面（Popup 窗口形式）。
 * 展示应用名称、版本、描述、技术栈、功能列表、链接和版权信息。
 */
export default function AboutPage() {
  const { t } = useLocalization()

  const handleClose = useCallback(() => {
    if (window.opener) {
      window.close()
    }
  }, [])

  return (
    <div className="flex justify-center p-10 bg-gray-950 min-h-screen">
      <div className="w-full max-w-[650px] bg-gray-900 rounded-lg p-8">
        {/* 应用头部 */}
        <div className="text-center mb-8 pb-6 border-b border-gray-700">
          <h1 className="text-[28px] font-semibold text-gray-100 mb-2">
            {t('app.title') || 'Visunovia Editor'}
          </h1>
          <p className="text-gray-500 text-sm">Version {APP_VERSION}</p>
        </div>

        {/* 描述 */}
        <div className="mb-7 pb-5 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-3.5">
            Description
          </h2>
          <p className="text-gray-300 text-sm leading-relaxed">
            可视化视觉小说编辑器 / Visual Novel Blueprint Editor.
            Create interactive visual novels with an intuitive node-based graph editor.
          </p>
        </div>

        {/* 技术栈 */}
        <div className="mb-7 pb-5 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-3.5">
            Technology Stack
          </h2>
          <ul className="list-none p-0 m-0">
            {TECH_STACK.map((tech) => (
              <li
                key={tech.name}
                className="flex justify-between items-center py-2 px-3 rounded bg-gray-800 mb-1.5"
              >
                <span className="text-gray-100 font-medium text-sm">{tech.name}</span>
                <span className="text-gray-500 text-[13px]">{tech.version}</span>
              </li>
            ))}
          </ul>
        </div>

        {/* 功能列表 */}
        <div className="mb-7 pb-5 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-3.5">
            Features
          </h2>
          <ul className="list-none p-0 m-0">
            {FEATURES.map((feature) => (
              <li
                key={feature}
                className="py-1.5 text-gray-300 text-sm relative pl-5 before:content-['✓'] before:absolute before:left-0 before:text-emerald-400 before:font-bold"
              >
                {feature}
              </li>
            ))}
          </ul>
        </div>

        {/* 版权信息 */}
        <div className="mb-7 pb-5 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-3.5">
            License
          </h2>
          <p className="text-gray-300 text-sm mb-2">
            This project is licensed under the MIT License.
          </p>
          <p className="text-gray-500 text-[13px]">© 2025 Visunovia Team</p>
        </div>

        {/* 外部链接 */}
        <div className="mb-7 pb-5 border-b border-gray-700">
          <h2 className="text-base font-semibold text-gray-300 uppercase tracking-wide mb-3.5">
            Links
          </h2>
          <div className="flex flex-col gap-2.5">
            {LINKS.map((link) => (
              <a
                key={link.name}
                href={link.url}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-2.5 py-2.5 px-3.5 bg-gray-800 rounded text-sky-400 no-underline text-sm transition-colors duration-200 hover:bg-gray-700 hover:translate-x-1"
              >
                <span className="text-lg">{link.icon}</span>
                <span>{link.name}</span>
              </a>
            ))}
          </div>
        </div>

        {/* 页脚 */}
        <div className="mt-7 pt-5 border-t border-gray-700 text-center">
          <p className="text-gray-500 text-[13px] mb-1">Built with ❤️ by the Visunovia Team</p>
          <p className="text-gray-600 text-[11px]">Build Date: {BUILD_DATE}</p>
        </div>

        {/* 关闭按钮 */}
        {window.opener && (
          <div className="mt-6 flex justify-center">
            <button
              onClick={handleClose}
              className="px-6 py-2 bg-gray-700 border-none rounded text-gray-100 text-sm cursor-pointer transition-colors duration-200 hover:bg-gray-600"
            >
              {t('common.close') || '关闭'}
            </button>
          </div>
        )}
      </div>
    </div>
  )
}