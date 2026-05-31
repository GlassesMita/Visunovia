import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { LucideIcon } from 'lucide-react'
import {
  Play,
  Flag,
  MessageSquare,
  GitBranch,
  Music,
  Volume2,
  Mic,
  Image as ImageIcon,
  UserPlus,
  Camera,
  Moon,
  Variable,
  GitFork,
  Clock,
  ChevronRight,
  Search,
  X,
  Zap,
  FolderOpen,
} from 'lucide-react'
import { useUIStore } from '@/stores/useUIStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { NODE_DEFAULT_DATA } from './nodeRegistries'

interface PaletteNode {
  type: string
  label: string
  icon: LucideIcon
  color: string
}

interface PaletteCategory {
  id: string
  label: string
  color: string
  nodes: PaletteNode[]
}

function highlightText(text: string, query: string): React.ReactNode {
  if (!query) return text
  const idx = text.toLowerCase().indexOf(query.toLowerCase())
  if (idx < 0) return text
  return (
    <>
      {text.slice(0, idx)}
      <span className="text-yellow-300 bg-yellow-900/30 rounded px-0.5">
        {text.slice(idx, idx + query.length)}
      </span>
      {text.slice(idx + query.length)}
    </>
  )
}

const CATEGORIES: PaletteCategory[] = [
  {
    id: 'flowControl',
    label: 'palette.flowControl',
    color: '#22c55e',
    nodes: [
      { type: 'Start', label: 'nodeTypes.start', icon: Play, color: '#22c55e' },
      { type: 'End', label: 'nodeTypes.end', icon: Flag, color: '#ef4444' },
    ],
  },
  {
    id: 'dialogue',
    label: 'palette.dialogue',
    color: '#a855f7',
    nodes: [
      { type: 'Dialogue', label: 'nodeTypes.dialogue', icon: MessageSquare, color: '#3b82f6' },
      { type: 'Branch', label: 'nodeTypes.branch', icon: GitBranch, color: '#a855f7' },
    ],
  },
  {
    id: 'events',
    label: 'palette.events',
    color: '#f97316',
    nodes: [
      { type: 'PlayBGM', label: 'nodeTypes.playBgm', icon: Music, color: '#f97316' },
      { type: 'StopBGM', label: 'nodeTypes.stopBgm', icon: Volume2, color: '#f97316' },
      { type: 'PlaySFX', label: 'nodeTypes.playSfx', icon: Volume2, color: '#f97316' },
      { type: 'PlayVoice', label: 'nodeTypes.playVoice', icon: Mic, color: '#f97316' },
      { type: 'ChangeBackground', label: 'nodeTypes.changeBackground', icon: ImageIcon, color: '#f97316' },
      { type: 'ShowCharacter', label: 'nodeTypes.showCharacter', icon: UserPlus, color: '#f97316' },
      { type: 'HideCharacter', label: 'nodeTypes.hideCharacter', icon: UserPlus, color: '#f97316' },
      { type: 'CameraShake', label: 'nodeTypes.cameraShake', icon: Camera, color: '#f97316' },
      { type: 'FadeScreen', label: 'nodeTypes.fadeScreen', icon: Moon, color: '#f97316' },
      { type: 'CustomEvent', label: 'nodeTypes.customEvent', icon: Zap, color: '#f97316' },
    ],
  },
  {
    id: 'logic',
    label: 'palette.logic',
    color: '#06b6d4',
    nodes: [
      { type: 'SetVariable', label: 'nodeTypes.setVariable', icon: Variable, color: '#06b6d4' },
      { type: 'Conditional', label: 'nodeTypes.conditional', icon: GitFork, color: '#06b6d4' },
      { type: 'Delay', label: 'nodeTypes.delay', icon: Clock, color: '#06b6d4' },
      { type: 'SubGraph', label: 'nodeTypes.subGraph', icon: FolderOpen, color: '#78716c' },
    ],
  },
]

export default function NodePalette() {
  const [searchText, setSearchText] = useState('')
  const { t } = useTranslation()

  const paletteExpanded = useUIStore((s) => s.paletteExpanded)
  const setPaletteExpanded = useUIStore((s) => s.setPaletteExpanded)
  const flowGramContext = useNodeGraphStore((s) => s.flowGramContext)

  const toggleCategory = (id: string) => {
    setPaletteExpanded({ ...paletteExpanded, [id]: !paletteExpanded[id] })
  }

  const addNodeToCanvas = (nodeType: string) => {
    const ctx = flowGramContext
    if (!ctx) return

    const defaultData = NODE_DEFAULT_DATA[nodeType] ?? {}
    const position = ctx.document.getNodeDefaultPosition(nodeType)
    ctx.document.createWorkflowNodeByType(nodeType, {
      x: position.x + (Math.random() * 100 - 50),
      y: position.y + (Math.random() * 100 - 50),
    }, defaultData)
  }

  const highlightMatch = searchText.length > 0

  return (
    <div className="flex flex-col h-full bg-gray-950">
      <div className="p-2 border-b border-gray-800">
        <div className="flex items-center gap-1.5 bg-gray-900 border border-gray-700 rounded px-2 py-1">
          <Search size={12} className="text-gray-500 shrink-0" />
          <input
            type="text"
            placeholder={t('common.search', '搜索...')}
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            className="flex-1 bg-transparent text-xs text-gray-200 outline-none placeholder-gray-600"
          />
          {searchText && (
            <button onClick={() => setSearchText('')} className="text-gray-500 hover:text-gray-300">
              <X size={12} />
            </button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto">
        {CATEGORIES.map((cat) => {
          const filteredNodes = searchText
            ? cat.nodes.filter((n) =>
                t(n.label).toLowerCase().includes(searchText.toLowerCase())
              )
            : cat.nodes

          if (searchText && filteredNodes.length === 0) return null

          return (
            <div key={cat.id} className="border-b border-gray-800/50">
              <button
                className="flex items-center gap-1.5 w-full px-2 py-1.5 text-left hover:bg-gray-800/50 transition-colors"
                onClick={() => toggleCategory(cat.id)}
              >
                <ChevronRight
                  size={12}
                  className={`text-gray-500 transition-transform ${
                    paletteExpanded[cat.id] ? 'rotate-90' : ''
                  }`}
                />
                <div
                  className="w-1.5 h-3 rounded-sm"
                  style={{ backgroundColor: cat.color }}
                />
                <span className="text-xs text-gray-300 font-medium">{t(cat.label)}</span>
                <span className="text-[10px] text-gray-600 ml-auto">
                  {filteredNodes.length}
                </span>
              </button>

              {paletteExpanded[cat.id] && (
                <div className="px-1 pb-1.5">
                  {filteredNodes.map((node) => {
                    const NodeIcon = node.icon
                    return (
                      <button
                        key={node.type}
                        className="flex items-center gap-1.5 w-full px-2 py-1 text-left hover:bg-gray-800 rounded transition-colors group"
                        onClick={() => addNodeToCanvas(node.type)}
                      >
                        <div
                          className="w-1 h-4 rounded-sm shrink-0"
                          style={{ backgroundColor: node.color }}
                        />
                        <NodeIcon size={12} className="text-gray-400 shrink-0" />
                        <span className="text-xs text-gray-300 truncate">
                          {highlightMatch
                            ? highlightText(t(node.label), searchText)
                            : t(node.label)}
                        </span>
                      </button>
                    )
                  })}
                  {filteredNodes.length === 0 && (
                    <div className="px-2 py-1 text-[10px] text-gray-600">{t('common.noMatch', '无匹配')}</div>
                  )}
                </div>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
