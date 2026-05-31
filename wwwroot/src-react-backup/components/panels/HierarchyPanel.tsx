import { useState, useMemo, useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { VNNodeType } from '@/types'
import type { VNNode } from '@/types'

/** 节点类型图标映射 */
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

/** 节点类型名称 */
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

/** 树节点数据结构 */
interface TreeNode {
  node: VNNode
  children: TreeNode[]
}

/**
 * 从节点 properties 提取显示名称
 * 优先按 DialogueNode/EventNode 等逻辑提取有意义的名称
 */
function getNodeDisplayName(node: VNNode): string {
  const props = node.properties

  // 对话节点：显示 speaker:text 摘要
  if (node.type === VNNodeType.Dialogue) {
    const speaker = props?.speaker as string | undefined
    const text = props?.text as string | undefined
    if (speaker && text) {
      const snippet = text.length > 20 ? text.slice(0, 20) + '...' : text
      return `${speaker}: ${snippet}`
    }
    if (speaker) return speaker
  }

  // 事件节点：显示子类型名称
  if (node.type in eventNodeDisplayNames) {
    return eventNodeDisplayNames[node.type] ?? nodeTypeNames[node.type] ?? node.type
  }

  return nodeTypeNames[node.type] ?? node.type
}

const eventNodeDisplayNames: Record<string, string> = {
  [VNNodeType.PlayBGM]: '播放 BGM',
  [VNNodeType.StopBGM]: '停止 BGM',
  [VNNodeType.PlaySFX]: '播放 SFX',
  [VNNodeType.PlayVoice]: '播放语音',
  [VNNodeType.ChangeBackground]: '切换背景',
  [VNNodeType.ShowCharacter]: '显示角色',
  [VNNodeType.HideCharacter]: '隐藏角色',
  [VNNodeType.CameraShake]: '镜头震动',
  [VNNodeType.FadeScreen]: '淡入淡出',
}

/** 根据图标获取节点颜色类名 */
function getNodeColorClass(type: VNNodeType): string {
  if (type === VNNodeType.Start) return 'text-emerald-400'
  if (type === VNNodeType.End) return 'text-red-400'
  if (type === VNNodeType.Dialogue) return 'text-blue-400'
  if (type === VNNodeType.Branch) return 'text-purple-400'
  // 事件节点
  if (type in eventNodeDisplayNames) return 'text-yellow-300'
  // 逻辑节点
  if (
    type === VNNodeType.SetVariable ||
    type === VNNodeType.Conditional ||
    type === VNNodeType.Delay
  )
    return 'text-orange-400'
  return 'text-gray-400'
}

/**
 * 从边列表构建子节点映射：source → target[] 的映射
 */
function buildChildrenMap(
  nodes: VNNode[],
  edges: { source: string; target: string }[]
): Map<string, VNNode[]> {
  const nodeMap = new Map<string, VNNode>()
  for (const n of nodes) {
    nodeMap.set(n.id, n)
  }

  const childrenMap = new Map<string, VNNode[]>()
  for (const e of edges) {
    // 仅处理 exec 输出边（非 data 类型）
    const existing = childrenMap.get(e.source) ?? []
    const targetNode = nodeMap.get(e.target)
    if (targetNode && !existing.includes(targetNode)) {
      existing.push(targetNode)
    }
    childrenMap.set(e.source, existing)
  }

  return childrenMap
}

/**
 * 递归构建树结构
 * 使用 visited 集合防止循环引用导致无限递归
 */
function buildTree(
  node: VNNode,
  childrenMap: Map<string, VNNode[]>,
  visited: Set<string>
): TreeNode {
  if (visited.has(node.id)) {
    // 循环引用保护：返回叶子节点
    return { node, children: [] }
  }

  const nextVisited = new Set(visited)
  nextVisited.add(node.id)

  const childNodes = childrenMap.get(node.id) ?? []
  const children = childNodes.map((child) =>
    buildTree(child, childrenMap, nextVisited)
  )

  return { node, children }
}

/**
 * 层级面板（Hierarchy）
 * 显示节点图结构，从 StartNode 开始展示树状层级。
 * 支持搜索过滤、节点选中、显示节点/连接数量统计。
 */
export default function HierarchyPanel() {
  const { t } = useTranslation()
  const nodes = useNodeGraphStore((s) => s.nodes)
  const edges = useNodeGraphStore((s) => s.edges)
  const selectedNodeId = useEditorStore((s) => s.selectedNodeId)
  const selectNode = useEditorStore((s) => s.selectNode)

  const [searchQuery, setSearchQuery] = useState('')

  // 查找 StartNode
  const startNode = useMemo(
    () => nodes.find((n) => n.type === VNNodeType.Start) ?? null,
    [nodes]
  )

  // 构建孩子映射表
  const childrenMap = useMemo(
    () => buildChildrenMap(nodes, edges),
    [nodes, edges]
  )

  // 从 StartNode 构建完整树
  const tree = useMemo((): TreeNode | null => {
    if (!startNode) return null
    return buildTree(startNode, childrenMap, new Set())
  }, [startNode, childrenMap])

  // 将树展平为可搜索列表，同时按搜索词过滤
  const filteredList = useMemo(() => {
    const flatList: Array<{ node: VNNode; depth: number }> = []

    // BFS 展平树结构
    const queue: Array<{ treeNode: TreeNode; depth: number }> = []
    if (tree) {
      queue.push({ treeNode: tree, depth: 0 })
    }

    // 还会包含树外的孤立节点（未连接到 StartNode 的节点）
    const treeNodeIds = new Set<string>()

    while (queue.length > 0) {
      const item = queue.shift()!
      treeNodeIds.add(item.treeNode.node.id)
      flatList.push({ node: item.treeNode.node, depth: item.depth })

      for (const child of item.treeNode.children) {
        queue.push({ treeNode: child, depth: item.depth + 1 })
      }
    }

    // 添加孤立节点（树外的节点）
    const orphanNodes = nodes.filter((n) => !treeNodeIds.has(n.id))
    for (const orphan of orphanNodes) {
      flatList.push({ node: orphan, depth: 0 })
    }

    // 搜索过滤
    if (!searchQuery.trim()) return flatList
    const query = searchQuery.toLowerCase().trim()
    return flatList.filter((item) => {
      const name = getNodeDisplayName(item.node).toLowerCase()
      const typeName = (nodeTypeNames[item.node.type] ?? item.node.type).toLowerCase()
      return name.includes(query) || typeName.includes(query)
    })
  }, [tree, nodes, searchQuery])

  // 选中/取消选中节点
  const handleSelectNode = useCallback(
    (nodeId: string) => {
      selectNode(selectedNodeId === nodeId ? null : nodeId)
    },
    [selectedNodeId, selectNode]
  )

  // 聚焦节点（双击）
  const handleFocusNode = useCallback((nodeId: string) => {
    // 聚焦到编辑器中的节点位置（后续可扩展为滚动视口）
    console.log('[Hierarchy] 聚焦节点:', nodeId)
  }, [])

  const icon = (type: VNNodeType) => nodeIcons[type] ?? '📦'
  const typeBadge = (type: VNNodeType) =>
    nodeTypeNames[type] ?? type

  return (
    <div className="flex flex-col h-full bg-gray-800">
      {/* 面板标题 */}
      <div className="flex items-center justify-between px-3 py-2.5 border-b border-gray-700 flex-shrink-0">
        <h3 className="m-0 text-[11px] font-semibold text-gray-300 uppercase tracking-wide">
          {t('panels.hierarchy')}
        </h3>
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-gray-500 bg-gray-800 px-1.5 py-0.5 rounded-full">
            节点 {nodes.length}
          </span>
          <span className="text-[10px] text-gray-500 bg-gray-800 px-1.5 py-0.5 rounded-full">
            连接 {edges.length}
          </span>
        </div>
      </div>

      {/* 搜索框 */}
      <div className="flex items-center gap-1.5 px-2 py-1.5 border-b border-gray-700 flex-shrink-0">
        <span className="text-[10px] opacity-40 flex-shrink-0">🔍</span>
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder={t('common.search', '搜索节点...')}
          className="flex-1 min-w-0 px-2 py-1 bg-gray-700 border border-gray-600 rounded
            text-[11px] text-gray-200 outline-none transition-colors
            focus:border-blue-500 placeholder-gray-500"
        />
        {searchQuery && (
          <button
            onClick={() => setSearchQuery('')}
            className="w-4 h-4 flex items-center justify-center bg-transparent border-none
              text-[10px] text-gray-500 cursor-pointer rounded-full transition-colors
              hover:bg-gray-700 hover:text-gray-300 flex-shrink-0"
            title="清除搜索"
          >
            ✕
          </button>
        )}
      </div>

      {/* 节点树列表 */}
      <div className="flex-1 overflow-y-auto p-1">
        {filteredList.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full text-gray-600 px-6 text-center select-none">
            <div className="text-3xl mb-2.5 opacity-50">🔷</div>
            <p className="text-xs text-gray-500 m-0 mb-1.5">
              {nodes.length === 0 ? t('hierarchy.empty') : t('hierarchy.noMatch')}
            </p>
            <span className="text-[10px] opacity-60">
              {nodes.length === 0
                ? t('hierarchy.emptyHint')
                : t('hierarchy.noMatchHint')}
            </span>
          </div>
        )}

        {filteredList.length > 0 && (
          <div className="flex flex-col gap-px">
            {filteredList.map(({ node, depth }) => (
              <div
                key={node.id}
                className={`flex items-center gap-1.5 py-1 px-2 rounded cursor-pointer
                  text-[11px] transition-colors select-none
                  ${selectedNodeId === node.id
                    ? 'bg-blue-900/50 text-white'
                    : 'text-gray-300 hover:bg-white/5'
                  }`}
                style={{ paddingLeft: `${8 + depth * 16}px` }}
                onClick={() => handleSelectNode(node.id)}
                onDoubleClick={() => handleFocusNode(node.id)}
              >
                <span
                  className={`text-[13px] w-[18px] text-center flex-shrink-0 ${getNodeColorClass(node.type)}`}
                >
                  {icon(node.type)}
                </span>
                <span className="flex-1 overflow-hidden text-ellipsis whitespace-nowrap">
                  {getNodeDisplayName(node)}
                </span>
                <span className="text-[9px] text-gray-600 bg-gray-800 px-1 py-px rounded uppercase tracking-wider flex-shrink-0">
                  {typeBadge(node.type)}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}