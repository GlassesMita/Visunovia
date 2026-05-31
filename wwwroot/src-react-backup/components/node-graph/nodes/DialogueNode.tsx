import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { MessageSquare } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

export default function DialogueNode() {
  const { form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null
  const speaker = (form.getValueIn('speaker') as string) || ''
  const text = (form.getValueIn('text') as string) || ''

  return (
    <BaseNodeShell
      headerColor="#3b82f6"
      icon={<MessageSquare size={12} />}
      title={t('nodeTypes.dialogue')}
    >
      {speaker ? (
        <div className="text-blue-300 font-medium text-[11px] truncate">{speaker}</div>
      ) : (
        <div className="text-gray-600 text-[11px] italic">{t('nodeHints.noSpeaker')}</div>
      )}
      {text ? (
        <div className="text-gray-300 text-[11px] mt-0.5 line-clamp-2 leading-snug">{text}</div>
      ) : (
        <div className="text-gray-600 text-[11px] mt-0.5 italic">{t('nodeHints.emptyText')}</div>
      )}
    </BaseNodeShell>
  )
}
