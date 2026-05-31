import { useState, useEffect, useRef, useCallback, useMemo } from 'react'
import { useTranslation } from 'react-i18next'

/** 日志级别类型 */
type LogLevel = 'info' | 'warn' | 'error' | 'success'

/** 日志条目 */
interface LogEntry {
  /** ISO 时间戳 */
  timestamp: string
  /** 日志消息 */
  message: string
  /** 日志级别 */
  level: LogLevel
}

/** 日志级别配置 */
const logLevelConfig: Array<{
  level: LogLevel
  icon: string
  label: string
}> = [
  { level: 'info', icon: 'ℹ', label: '信息' },
  { level: 'warn', icon: '⚠', label: '警告' },
  { level: 'error', icon: '✕', label: '错误' },
  { level: 'success', icon: '✓', label: '成功' },
]

/** 获取日志级别对应的图标 */
function getLevelIcon(level: LogLevel): string {
  const config = logLevelConfig.find((c) => c.level === level)
  return config?.icon ?? '•'
}

/** 获取日志级别对应的颜色类名 */
function getLevelColorClass(level: LogLevel): string {
  switch (level) {
    case 'info':
      return 'text-gray-400'
    case 'warn':
      return 'text-yellow-300'
    case 'error':
      return 'text-red-400'
    case 'success':
      return 'text-emerald-400'
    default:
      return 'text-gray-400'
  }
}

/** 格式化当前时间为 HH:mm:ss 字符串 */
function formatTime(): string {
  const now = new Date()
  return now.toLocaleTimeString('zh-CN', {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

/** 全局日志事件总线名称 */
const LOG_EVENT_NAME = 'visunovia:console:log'

/** 日志事件自定义类型 */
interface LogEventDetail {
  message: string
  level?: LogLevel
}

/**
 * 向全局日志总线发送一条日志消息
 * 可在任意模块中调用以将日志推送到 ConsolePanel
 */
export function pushConsoleLog(message: string, level: LogLevel = 'info'): void {
  window.dispatchEvent(
    new CustomEvent<LogEventDetail>(LOG_EVENT_NAME, {
      detail: { message, level },
    })
  )
}

/**
 * 控制台面板（Console）
 * 展示日志消息列表，支持级别过滤、清除、自动滚动。
 * 通过全局 window 事件接收日志（事件名: visunovia:console:log）。
 * 支持劫持原生 console 方法（info/warn/error）。
 */
export default function ConsolePanel() {
  const { t } = useTranslation()
  const [logs, setLogs] = useState<LogEntry[]>([
    {
      timestamp: formatTime(),
      message: '编辑器已初始化',
      level: 'info',
    },
    {
      timestamp: formatTime(),
      message: '控制台面板就绪',
      level: 'success',
    },
  ])
  const [disabledLevels, setDisabledLevels] = useState<Set<LogLevel>>(
    new Set()
  )
  const [autoScroll, setAutoScroll] = useState(true)
  const contentRef = useRef<HTMLDivElement>(null)

  // 监听全局日志事件
  useEffect(() => {
    const handler = (event: Event) => {
      const detail = (event as CustomEvent<LogEventDetail>).detail
      if (!detail) return

      setLogs((prev) => [
        ...prev,
        {
          timestamp: formatTime(),
          message: detail.message,
          level: detail.level ?? 'info',
        },
      ])
    }

    window.addEventListener(LOG_EVENT_NAME, handler)
    return () => window.removeEventListener(LOG_EVENT_NAME, handler)
  }, [])

  // 劫持原生 console 方法，将输出同步到面板
  useEffect(() => {
    const originalConsole = {
      log: console.log.bind(console),
      info: console.info.bind(console),
      warn: console.warn.bind(console),
      error: console.error.bind(console),
    }

    console.info = (...args: unknown[]) => {
      originalConsole.info(...args)
      const message = args.map((a) => (typeof a === 'object' ? JSON.stringify(a) : String(a))).join(' ')
      pushConsoleLog(message, 'info')
    }

    console.warn = (...args: unknown[]) => {
      originalConsole.warn(...args)
      const message = args.map((a) => (typeof a === 'object' ? JSON.stringify(a) : String(a))).join(' ')
      pushConsoleLog(message, 'warn')
    }

    console.error = (...args: unknown[]) => {
      originalConsole.error(...args)
      const message = args.map((a) => (typeof a === 'object' ? JSON.stringify(a) : String(a))).join(' ')
      pushConsoleLog(message, 'error')
    }

    // 还原（cleanup）
    return () => {
      console.info = originalConsole.info
      console.warn = originalConsole.warn
      console.error = originalConsole.error
    }
  }, [])

  // 自动滚动到最新消息
  useEffect(() => {
    if (autoScroll && contentRef.current) {
      contentRef.current.scrollTop = contentRef.current.scrollHeight
    }
  }, [logs, autoScroll])

  // 按级别过滤后的日志
  const filteredLogs = useMemo(
    () => logs.filter((log) => !disabledLevels.has(log.level)),
    [logs, disabledLevels]
  )

  // 切换日志级别过滤器
  const toggleLevel = useCallback((level: LogLevel) => {
    setDisabledLevels((prev) => {
      const next = new Set(prev)
      if (next.has(level)) {
        next.delete(level)
      } else {
        next.add(level)
      }
      return next
    })
  }, [])

  // 清空日志
  const clearLogs = useCallback(() => {
    setLogs([])
  }, [])

  // 当前可见日志数量
  const visibleCount = filteredLogs.length

  return (
    <div className="flex flex-col h-full bg-gray-800">
      {/* 面板标题栏 */}
      <div className="flex items-center justify-between px-2.5 py-1.5 border-b border-gray-700 bg-gray-800 flex-shrink-0">
        <div className="flex items-center gap-2">
          <h3 className="m-0 text-[11px] font-semibold text-gray-300 uppercase tracking-wide">
            {t('panels.console')}
          </h3>
          {logs.length > 0 && (
            <span className="text-[10px] text-gray-500 bg-gray-800 px-1.5 py-0.5 rounded-full">
              {logs.length}
            </span>
          )}
        </div>

        <div className="flex items-center gap-1.5">
          {/* 日志级别过滤器 */}
          <div className="flex gap-0.5">
            {logLevelConfig.map(({ level, icon }) => {
              const isActive = !disabledLevels.has(level)
              return (
                <button
                  key={level}
                  onClick={() => toggleLevel(level)}
                  title={level}
                  className={`w-[22px] h-[18px] flex items-center justify-center
                    bg-transparent border border-transparent rounded text-[11px]
                    cursor-pointer transition-all
                    ${isActive
                      ? `opacity-100 border-gray-600 ${level === 'info'
                          ? 'text-blue-400'
                          : level === 'warn'
                            ? 'text-yellow-300'
                            : level === 'error'
                              ? 'text-red-400'
                              : 'text-emerald-400'
                        }`
                      : 'opacity-40 hover:opacity-60'
                    }`}
                >
                  {icon}
                </button>
              )
            })}
          </div>

          {/* 自动滚动切换 */}
          <button
            onClick={() => setAutoScroll((v) => !v)}
            title={autoScroll ? '自动滚动: 开' : '自动滚动: 关'}
            className={`px-1 py-0.5 bg-transparent border rounded text-[10px] cursor-pointer
              transition-all
              ${autoScroll
                ? 'border-blue-600/50 text-blue-400'
                : 'border-gray-700 text-gray-500 hover:text-gray-300 hover:border-gray-600'
              }`}
          >
            ↓
          </button>

          {/* 清除按钮 */}
          <button
            onClick={clearLogs}
            title="清除日志"
            className="px-1.5 py-0.5 bg-transparent border border-gray-700 rounded
              text-xs text-gray-500 cursor-pointer transition-colors
              hover:bg-white/5 hover:text-gray-300 hover:border-gray-600"
          >
            🗑️
          </button>
        </div>
      </div>

      {/* 日志内容区域 */}
      <div
        ref={contentRef}
        className="flex-1 overflow-y-auto px-2.5 py-1.5 font-mono text-[11px] leading-relaxed"
      >
        {visibleCount === 0 && (
          <div className="flex items-center justify-center h-full text-gray-600 select-none">
            <p className="text-xs">{logs.length === 0 ? '无日志' : '所有日志已被过滤'}</p>
          </div>
        )}

        {filteredLogs.map((log, index) => (
          <div
            key={index}
            className={`flex items-start gap-1.5 py-px break-words ${getLevelColorClass(log.level)}`}
          >
            <span className="w-[14px] text-center flex-shrink-0 text-[10px]">
              {getLevelIcon(log.level)}
            </span>
            <span className="text-gray-600 flex-shrink-0 text-[10px]">
              {log.timestamp}
            </span>
            <span className="flex-1">{log.message}</span>
          </div>
        ))}
      </div>
    </div>
  )
}