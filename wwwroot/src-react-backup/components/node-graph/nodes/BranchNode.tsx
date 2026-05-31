import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { GitBranch } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

export default function BranchNode() {
  const { form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null
  const condition = (form.getValueIn('condition') as string) || ''

  return (
    <BaseNodeShell
      headerColor="#a855f7"
      icon={<GitBranch size={12} />}
      title={t('nodeTypes.branch')}
    >
      {condition ? (
        <div className="text-purple-300 text-[11px] truncate">{condition}</div>
      ) : (
        <div className="text-gray-600 text-[11px] italic">{t('nodeHints.noCondition')}</div>
      )}
      <div className="flex justify-between mt-1.5 text-[10px]">
        <span className="text-green-400 font-medium">✓ {t('nodeHints.yes')}</span>
        <span className="text-red-400 font-medium">✗ {t('nodeHints.no')}</span>
      </div>
    </BaseNodeShell>
  )
}
