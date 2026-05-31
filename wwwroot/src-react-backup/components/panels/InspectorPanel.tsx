import { useMemo, useCallback, useState } from 'react'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import {
  VNNodeType,
  EventType,
  LogicType,
  eventTypeConfig,
  logicTypeConfig,
  type PropertyConfig,
} from '@/types'

/** VNNodeType 到 EventType 的映射 */
const vnNodeTypeToEventType: Partial<Record<VNNodeType, EventType>> = {
  [VNNodeType.PlayBGM]: EventType.PlayBGM,
  [VNNodeType.StopBGM]: EventType.StopBGM,
  [VNNodeType.PlaySFX]: EventType.PlaySFX,
  [VNNodeType.PlayVoice]: EventType.PlayVoice,
  [VNNodeType.ChangeBackground]: EventType.ChangeBackground,
  [VNNodeType.ShowCharacter]: EventType.ShowCharacter,
  [VNNodeType.HideCharacter]: EventType.HideCharacter,
  [VNNodeType.CameraShake]: EventType.CameraShake,
  [VNNodeType.FadeScreen]: EventType.FadeScreen,
}

/** VNNodeType 到 LogicType 的映射 */
const vnNodeTypeToLogicType: Partial<Record<VNNodeType, LogicType>> = {
  [VNNodeType.SetVariable]: LogicType.SetVariable,
  [VNNodeType.Conditional]: LogicType.Conditional,
  [VNNodeType.Delay]: LogicType.Delay,
}

/** 节点类型图标 */
const nodeIcons: Record<string, string> = {
  [VNNodeType.Start]: '▶️',
  [VNNodeType.End]: '⏹️',
  [VNNodeType.Sequence]: '📦',
  [VNNodeType.Dialogue]: '💬',
  [VNNodeType.Branch]: '❓',
  [VNNodeType.PlayBGM]: '⚡',
  [VNNodeType.StopBGM]: '⚡',
  [VNNodeType.PlaySFX]: '⚡',
  [VNNodeType.PlayVoice]: '⚡',
  [VNNodeType.ChangeBackground]: '⚡',
  [VNNodeType.ShowCharacter]: '⚡',
  [VNNodeType.HideCharacter]: '⚡',
  [VNNodeType.CameraShake]: '⚡',
  [VNNodeType.FadeScreen]: '⚡',
  [VNNodeType.SetVariable]: '🔧',
  [VNNodeType.Conditional]: '🔧',
  [VNNodeType.Delay]: '🔧',
  [VNNodeType.SubGraph]: '📁',
  [VNNodeType.CustomEvent]: '📌',
}

/** 节点类型中文名称 */
const nodeTypeNames: Record<string, string> = {
  [VNNodeType.Start]: '开始',
  [VNNodeType.End]: '结束',
  [VNNodeType.Sequence]: '序列',
  [VNNodeType.Dialogue]: '对话',
  [VNNodeType.Branch]: '分支',
  [VNNodeType.PlayBGM]: '播放 BGM',
  [VNNodeType.StopBGM]: '停止 BGM',
  [VNNodeType.PlaySFX]: '播放音效',
  [VNNodeType.PlayVoice]: '播放语音',
  [VNNodeType.ChangeBackground]: '切换背景',
  [VNNodeType.ShowCharacter]: '显示角色',
  [VNNodeType.HideCharacter]: '隐藏角色',
  [VNNodeType.CameraShake]: '镜头震动',
  [VNNodeType.FadeScreen]: '淡入淡出',
  [VNNodeType.SetVariable]: '设置变量',
  [VNNodeType.Conditional]: '条件判断',
  [VNNodeType.Delay]: '延迟',
  [VNNodeType.SubGraph]: '子图',
  [VNNodeType.CustomEvent]: '自定义事件',
}

/** 非事件/逻辑节点的默认属性配置 */
const defaultNodeProperties: Partial<Record<VNNodeType, PropertyConfig[]>> = {
  [VNNodeType.End]: [
    {
      name: 'eventType',
      type: 'select',
      defaultValue: 'end_game',
      options: [
        { value: 'end_game', label: '结束游戏' },
        { value: 'return_to_menu', label: '返回主菜单' },
        { value: 'jump_to_scene', label: '跳转场景' },
      ],
    },
    {
      name: 'sceneId',
      type: 'string',
      defaultValue: '',
    },
  ],
  [VNNodeType.Dialogue]: [
    { name: 'speaker', type: 'string', defaultValue: '' },
    { name: 'text', type: 'string', defaultValue: '' },
    { name: 'voice', type: 'resource', defaultValue: '' },
  ],
  [VNNodeType.Branch]: [
    { name: 'condition', type: 'string', defaultValue: '' },
    { name: 'options', type: 'string', defaultValue: '' },
  ],
}

/** 属性字段的占位符 */
const propertyPlaceholders: Record<string, string> = {
  speaker: '说话者名称...',
  text: '对话文本...',
  varName: '变量名称...',
  value: '值...',
  characterId: '角色 ID...',
  expression: '表情...',
  bgmPath: 'BGM 文件路径...',
  sfxPath: '音效文件路径...',
  voicePath: '语音文件路径...',
  imagePath: '图片文件路径...',
  sceneId: '场景 ID...',
  condition: '条件表达式...',
  options: '选项（JSON）...',
  voice: '语音文件路径...',
  variable: '变量名称...',
  compareValue: '比较值...',
}

/**
 * 属性面板（Inspector）
 * 根据当前选中的节点动态渲染属性编辑表单。
 * 支持事件节点（使用 eventTypeConfig）、逻辑节点（使用 logicTypeConfig）
 * 以及 Start/End/Dialogue/Branch 等基础节点的属性编辑。
 */
export default function InspectorPanel() {
  const selectedNodeId = useEditorStore((s) => s.selectedNodeId)
  const nodes = useNodeGraphStore((s) => s.nodes)
  const flowGramContext = useNodeGraphStore((s) => s.flowGramContext)

  const [showResourcePicker, setShowResourcePicker] = useState(false)
  const [currentResourcePropName, setCurrentResourcePropName] = useState('')

  // 根据 selectedNodeId 找到对应节点
  const selectedNode = useMemo(() => {
    if (!selectedNodeId) return null
    return nodes.find((n) => n.id === selectedNodeId) ?? null
  }, [selectedNodeId, nodes])

  // 判断是否为事件类型节点
  const isEventNode = useMemo(() => {
    if (!selectedNode) return false
    return selectedNode.type in vnNodeTypeToEventType
  }, [selectedNode])

  // 判断是否为逻辑类型节点
  const isLogicNode = useMemo(() => {
    if (!selectedNode) return false
    return selectedNode.type in vnNodeTypeToLogicType
  }, [selectedNode])

  // 动态计算当前节点应显示的属性列表
  const dynamicProperties = useMemo((): PropertyConfig[] => {
    if (!selectedNode) return []

    if (isEventNode) {
      const et = vnNodeTypeToEventType[selectedNode.type]
      if (et) return eventTypeConfig[et]?.properties ?? []
      return []
    }

    if (isLogicNode) {
      const lt = vnNodeTypeToLogicType[selectedNode.type]
      if (lt) return logicTypeConfig[lt]?.properties ?? []
      return []
    }

    return defaultNodeProperties[selectedNode.type] ?? []
  }, [selectedNode, isEventNode, isLogicNode])

  /** 获取属性当前值 */
  const getPropertyValue = useCallback(
    (name: string) => {
      return selectedNode?.properties?.[name] ?? undefined
    },
    [selectedNode]
  )

  /** 更新属性值并写回 store */
  const handlePropertyChange = useCallback(
    (name: string, value: unknown) => {
      if (!selectedNodeId || !flowGramContext) return
      const node = flowGramContext.document.getAllNodes().find((n) => n.id === selectedNodeId)
      if (!node) return
      const form = node.form
      if (form) {
        form.setValueIn(name, value)
      }
    },
    [selectedNodeId, flowGramContext]
  )

  /** 处理资源选择（预留） */
  const handleResourceBrowse = useCallback(
    (propName: string) => {
      setCurrentResourcePropName(propName)
      setShowResourcePicker(true)
      // 资源选择器将在后续集成
      console.log(`[Inspector] 浏览资源: ${propName}`)
    },
    []
  )

  /** 获取属性的占位符文本 */
  const getPlaceholder = useCallback((name: string): string => {
    return propertyPlaceholders[name] ?? ''
  }, [])

  // 未选中节点时显示空状态
  if (!selectedNode) {
    return (
      <div className="flex flex-col h-full bg-gray-800">
        <div className="flex flex-col items-center justify-center h-full text-gray-500 px-6 text-center select-none">
          <div className="text-4xl mb-3 opacity-60">📋</div>
          <p className="text-sm text-gray-400 mb-2">请在画布中选择节点</p>
          <span className="text-xs opacity-60">选中节点后可查看和编辑属性</span>
        </div>
      </div>
    )
  }

  const icon = nodeIcons[selectedNode.type] ?? '📦'
  const typeName =
    nodeTypeNames[selectedNode.type] ?? selectedNode.type

  return (
    <div className="flex flex-col h-full bg-gray-800 overflow-y-auto">
      <div className="p-3 flex flex-col gap-4">
        {/* 节点标题栏 */}
        <div className="flex items-center gap-2 pb-2.5 border-b border-gray-700">
          <span className="text-base">{icon}</span>
          <h3 className="m-0 flex-1 text-sm font-semibold text-gray-100">
            {typeName}
          </h3>
          <span className="text-[10px] text-gray-600 font-mono">
            {selectedNode.id.slice(0, 8)}...
          </span>
        </div>

        {/* 属性编辑区域 */}
        <div className="flex flex-col gap-1">
          <div className="text-[11px] font-semibold text-gray-500 uppercase tracking-wide mb-1">
            属性
          </div>

          {dynamicProperties.length === 0 &&
            !isEventNode &&
            !isLogicNode &&
            selectedNode.type !== VNNodeType.Start && (
              <div className="flex flex-col gap-3">
                {/* 对于无预定义属性的节点类型，显示位置信息 */}
              </div>
            )}

          {dynamicProperties.map((prop) => (
            <div key={prop.name} className="flex flex-col gap-1">
              <label
                className="text-[11px] text-gray-400 flex items-center gap-1"
                title={prop.name}
              >
                {prop.name}
              </label>

              {/* String 类型 → 文本输入框 */}
              {prop.type === 'string' && (
                <input
                  type="text"
                  value={(getPropertyValue(prop.name) ?? prop.defaultValue ?? '') as string}
                  placeholder={getPlaceholder(prop.name)}
                  onChange={(e) =>
                    handlePropertyChange(prop.name, e.target.value)
                  }
                  className="w-full px-2 py-1.5 bg-gray-700 border border-gray-600 rounded
                    text-xs text-gray-200 outline-none transition-colors
                    focus:border-blue-500 placeholder-gray-500"
                />
              )}

              {/* Number 类型 → 数字输入框 */}
              {prop.type === 'number' && (
                <input
                  type="number"
                  step="0.01"
                  value={(getPropertyValue(prop.name) ?? prop.defaultValue ?? 0) as number}
                  onChange={(e) =>
                    handlePropertyChange(
                      prop.name,
                      parseFloat(e.target.value)
                    )
                  }
                  className="w-full px-2 py-1.5 bg-gray-700 border border-gray-600 rounded
                    text-xs text-gray-200 outline-none transition-colors
                    focus:border-blue-500 [appearance:textfield]
                    [&::-webkit-inner-spin-button]:opacity-50
                    [&::-webkit-outer-spin-button]:opacity-50"
                />
              )}

              {/* Boolean 类型 → 复选框 */}
              {prop.type === 'boolean' && (
                <label className="flex items-center gap-2 cursor-pointer py-1">
                  <input
                    type="checkbox"
                    checked={
                      (getPropertyValue(prop.name) ?? prop.defaultValue ?? false) as boolean
                    }
                    onChange={(e) =>
                      handlePropertyChange(prop.name, e.target.checked)
                    }
                    className="w-3.5 h-3.5 accent-blue-500 cursor-pointer"
                  />
                  <span className="text-xs text-gray-300">
                    {(getPropertyValue(prop.name) ?? prop.defaultValue)
                      ? '是'
                      : '否'}
                  </span>
                </label>
              )}

              {/* Select 类型 → 下拉选择框 */}
              {prop.type === 'select' && (
                <select
                  value={(getPropertyValue(prop.name) ?? prop.defaultValue ?? '') as string}
                  onChange={(e) =>
                    handlePropertyChange(prop.name, e.target.value)
                  }
                  className="w-full px-2 py-1.5 bg-gray-700 border border-gray-600 rounded
                    text-xs text-gray-200 outline-none transition-colors
                    focus:border-blue-500"
                >
                  {(prop.options ?? []).map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              )}

              {/* Resource 类型 → 只读输入框 + 浏览按钮 */}
              {prop.type === 'resource' && (
                <div className="flex gap-1">
                  <input
                    type="text"
                    readOnly
                    value={(getPropertyValue(prop.name) ?? '') as string}
                    placeholder="点击选择资源..."
                    onClick={() => handleResourceBrowse(prop.name)}
                    className="flex-1 px-2 py-1.5 bg-gray-800 border border-gray-600 rounded
                      text-xs text-gray-400 outline-none cursor-pointer
                      hover:border-blue-500 hover:text-gray-200 transition-colors"
                  />
                  <button
                    title="浏览"
                    onClick={() => handleResourceBrowse(prop.name)}
                    className="w-8 flex items-center justify-center bg-gray-700 border border-gray-600
                      rounded text-sm cursor-pointer transition-all hover:bg-gray-600 hover:border-blue-500"
                  >
                    📂
                  </button>
                </div>
              )}
            </div>
          ))}

          {/* 无动态属性时的提示 */}
          {dynamicProperties.length === 0 &&
            !isEventNode &&
            !isLogicNode &&
            selectedNode.type === VNNodeType.Start && (
              <div className="py-4 text-center text-xs text-gray-600">
                无可编辑属性
              </div>
            )}
        </div>

        {/* 节点位置信息 */}
        <div className="flex gap-4 pt-2.5 border-t border-gray-700 text-[11px] text-gray-500 font-mono">
          <div className="flex gap-1">
            <span>X:</span>
            <span className="text-gray-400">
              {Math.round(selectedNode.position?.x ?? 0)}
            </span>
          </div>
          <div className="flex gap-1">
            <span>Y:</span>
            <span className="text-gray-400">
              {Math.round(selectedNode.position?.y ?? 0)}
            </span>
          </div>
        </div>
      </div>

      {/* 资源选择模态框（占位） */}
      {showResourcePicker && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="bg-gray-800 border border-gray-700 rounded-lg p-6 min-w-[320px]">
            <h3 className="text-sm text-gray-200 mb-4">
              选择资源: {currentResourcePropName}
            </h3>
            <p className="text-xs text-gray-500 mb-4">
              资源浏览器将在后续版本中完整集成
            </p>
            <div className="flex justify-end">
              <button
                onClick={() => setShowResourcePicker(false)}
                className="px-4 py-1.5 bg-gray-700 text-gray-300 rounded text-xs
                  hover:bg-gray-600 transition-colors"
              >
                关闭
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}