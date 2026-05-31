import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { Play } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

export default function StartNode() {
  const { form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null

  return (
    <BaseNodeShell
      headerColor="#22c55e"
      icon={<Play size={12} />}
      title={t('nodeTypes.start')}
    >
      <span className="text-[11px] text-gray-500">{t('nodeHints.execEntry')}</span>
    </BaseNodeShell>
  )
}
