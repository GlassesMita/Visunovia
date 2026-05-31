import { type ReactNode } from 'react'
import { X } from 'lucide-react'

interface WindowPopupProps {
  /** 弹窗标题 */
  title: string
  /** 子内容 */
  children: ReactNode
  /** 关闭回调 */
  onClose: () => void
  /** 确认回调 */
  onConfirm?: () => void
  /** 取消回调 */
  onCancel?: () => void
  /** 确认按钮文本 */
  confirmLabel?: string
  /** 取消按钮文本 */
  cancelLabel?: string
  /** 宽度 */
  width?: string
  /** 高度 */
  height?: string
  /** 是否显示底部按钮栏 */
  showFooter?: boolean
}

export default function WindowPopup({
  title,
  children,
  onClose,
  onConfirm,
  onCancel,
  confirmLabel = '确认',
  cancelLabel = '取消',
  width,
  height,
  showFooter = true,
}: WindowPopupProps) {
  const handleClose = () => {
    if (onCancel) {
      onCancel()
    } else {
      onClose()
    }
  }

  return (
    <div
      className="flex flex-col bg-gray-900 text-gray-100 overflow-hidden"
      style={{ width, height: height || '100vh' }}
    >
      {/* 标题栏 */}
      <header className="flex items-center justify-between px-6 py-3 bg-gray-800/50 border-b border-gray-700 shrink-0">
        <h1 className="text-lg font-semibold text-gray-100">{title}</h1>
        <button
          className="p-1 text-gray-400 hover:text-gray-200 rounded transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500/50"
          onClick={handleClose}
          aria-label="关闭"
        >
          <X size={18} />
        </button>
      </header>

      {/* 内容区域 */}
      <main className="flex-1 p-6 overflow-y-auto">{children}</main>

      {/* 底部按钮栏 */}
      {showFooter && (onConfirm || onCancel) && (
        <footer className="flex items-center justify-end gap-3 px-6 py-3 bg-gray-800/50 border-t border-gray-700 shrink-0">
          {onCancel && (
            <button
              className="px-4 py-2 text-sm font-medium text-gray-300 bg-gray-700 rounded hover:bg-gray-600 transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500/50"
              onClick={onCancel}
            >
              {cancelLabel}
            </button>
          )}
          {onConfirm && (
            <button
              className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded hover:bg-blue-500 transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500/50"
              onClick={onConfirm}
            >
              {confirmLabel}
            </button>
          )}
        </footer>
      )}
    </div>
  )
}