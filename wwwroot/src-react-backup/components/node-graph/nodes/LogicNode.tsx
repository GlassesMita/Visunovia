import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { Variable, GitFork, Clock } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

const LOGIC_CONFIG: Record<string, { icon: LucideIcon; nameKey: string }> = {
  SetVariable: { icon: Variable, nameKey: 'nodeTypes.setVariable' },
  Conditional: { icon: GitFork, nameKey: 'nodeTypes.conditional' },
  Delay: { icon: Clock, nameKey: 'nodeTypes.delay' },
}

export default function LogicNode() {
  const { node, form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null
  const config = LOGIC_CONFIG[node.type] || { icon: Variable, nameKey: 'nodeTypes.setVariable' }
  const Icon = config.icon
  const isConditional = node.type === 'Conditional'

  return (
    <BaseNodeShell
      headerColor="#06b6d4"
      icon={<Icon size={12} />}
      title={t(config.nameKey)}
    >
      {isConditional ? (
        <>
          {form.getValueIn('variable') ? (
            <div className="text-gray-400 text-[11px] truncate">{String(form.getValueIn('variable'))}</div>
          ) : (
            <div className="text-gray-600 text-[11px] italic">{t('nodeHints.noCondition')}</div>
          )}
          <div className="flex justify-between mt-1 text-[10px]">
            <span className="text-green-400 font-medium">✓ {t('nodeHints.yes')}</span>
            <span className="text-red-400 font-medium">✗ {t('nodeHints.no')}</span>
          </div>
        </>
      ) : node.type === 'SetVariable' && form.getValueIn('varName') ? (
        <div className="text-gray-400 text-[11px] truncate">{String(form.getValueIn('varName'))}</div>
      ) : node.type === 'Delay' && form.getValueIn('duration') !== undefined ? (
        <div className="text-gray-400 text-[11px] truncate">{String(form.getValueIn('duration'))}s</div>
      ) : (
        <span className="text-gray-500 text-[11px]">{t('nodeHints.logicNode')}</span>
      )}
    </BaseNodeShell>
  )
}
