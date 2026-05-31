import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { FolderOpen } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

export default function ResourceNode() {
  const { form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null
  const resourceType = (form.getValueIn('resourceType') as string) || ''
  const resourcePath = (form.getValueIn('resourcePath') as string) || (form.getValueIn('path') as string) || ''

  return (
    <BaseNodeShell
      headerColor="#78716c"
      icon={<FolderOpen size={12} />}
      title={resourceType || t('nodeTypes.resource')}
    >
      {resourcePath ? (
        <div className="text-gray-300 text-[11px] truncate leading-snug" title={resourcePath}>
          {resourcePath}
        </div>
      ) : (
        <div className="text-gray-600 text-[11px] italic">{t('nodeHints.noPath')}</div>
      )}
    </BaseNodeShell>
  )
}
