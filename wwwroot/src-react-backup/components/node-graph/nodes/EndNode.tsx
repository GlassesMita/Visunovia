import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { Flag } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

export default function EndNode() {
  const { form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null

  return (
    <BaseNodeShell
      headerColor="#ef4444"
      icon={<Flag size={12} />}
      title={t('nodeTypes.end')}
    >
      <span className="text-[11px] text-gray-500">{t('nodeHints.execEnd')}</span>
    </BaseNodeShell>
  )
}
