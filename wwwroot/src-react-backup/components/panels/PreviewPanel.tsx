/**
 * 预览面板（Preview）
 * 游戏预览占位组件，后续可扩展为实际渲染器。
 * 当前显示深色背景 + 居中占位文本。
 */
export default function PreviewPanel() {
  return (
    <div className="flex flex-col h-full bg-gray-950">
      {/* 标题栏 */}
      <div className="flex items-center px-3 py-2.5 border-b border-gray-700 bg-gray-900 flex-shrink-0">
        <h3 className="m-0 text-[11px] font-semibold text-gray-300 uppercase tracking-wide">
          预览
        </h3>
      </div>

      {/* 预览内容区域 */}
      <div className="flex-1 flex items-center justify-center bg-gray-950">
        <div className="flex flex-col items-center gap-3 text-gray-600 select-none">
          <div className="text-5xl opacity-30">🎮</div>
          <p className="text-sm text-gray-500 m-0">预览</p>
          <span className="text-[11px] opacity-50">
            游戏预览渲染器（即将推出）
          </span>
        </div>
      </div>
    </div>
  )
}