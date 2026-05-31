import React from 'react'
import { WorkflowNodeRenderer, useNodeRender } from '@flowgram.ai/free-layout-editor'

interface BaseNodeShellProps {
  headerColor: string
  icon: React.ReactNode
  title: string
  children?: React.ReactNode
}

export default function BaseNodeShell({ headerColor, icon, title, children }: BaseNodeShellProps) {
  const { node, selected, activated } = useNodeRender()

  const ringClass = selected
    ? 'ring-2 ring-blue-500'
    : activated
      ? 'ring-1 ring-blue-500/30'
      : ''

  return (
    <WorkflowNodeRenderer
      node={node}
      portPrimaryColor="#ffffff"
      portSecondaryColor="#9ca3af"
      portErrorColor="#ef4444"
      portBackgroundColor="#1f2937"
    >
      <div
        className={`select-none ${ringClass}`}
        style={{ width: 240 }}
      >
        <div
          className="flex items-center gap-1.5 px-2 text-white font-medium text-xs border border-gray-700"
          style={{ height: 28, backgroundColor: headerColor, borderRadius: '6px 6px 0 0' }}
        >
          <span className="shrink-0">{icon}</span>
          <span className="truncate">{title}</span>
        </div>
        <div
          className="bg-gray-900 border-x border-b border-gray-700 px-2 py-1.5 text-xs text-gray-400"
          style={{ borderRadius: '0 0 6px 6px', minHeight: children ? 24 : 0 }}
        >
          {children}
        </div>
      </div>
    </WorkflowNodeRenderer>
  )
}
