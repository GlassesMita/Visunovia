import { useNodeRender } from '@flowgram.ai/free-layout-editor'
import { useTranslation } from 'react-i18next'
import { Music, Volume2, Mic, Image, UserPlus, Camera, Moon, Zap } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import BaseNodeShell from '../BaseNodeShell'

const EVENT_CONFIG: Record<string, { icon: LucideIcon; nameKey: string }> = {
  PlayBGM: { icon: Music, nameKey: 'nodeTypes.playBgm' },
  StopBGM: { icon: Volume2, nameKey: 'nodeTypes.stopBgm' },
  PlaySFX: { icon: Volume2, nameKey: 'nodeTypes.playSfx' },
  PlayVoice: { icon: Mic, nameKey: 'nodeTypes.playVoice' },
  ChangeBackground: { icon: Image, nameKey: 'nodeTypes.changeBackground' },
  ShowCharacter: { icon: UserPlus, nameKey: 'nodeTypes.showCharacter' },
  HideCharacter: { icon: UserPlus, nameKey: 'nodeTypes.hideCharacter' },
  CameraShake: { icon: Camera, nameKey: 'nodeTypes.cameraShake' },
  FadeScreen: { icon: Moon, nameKey: 'nodeTypes.fadeScreen' },
  CustomEvent: { icon: Zap, nameKey: 'nodeTypes.customEvent' },
}

export default function EventNode() {
  const { node, form } = useNodeRender()
  const { t } = useTranslation()
  if (!form) return null
  const config = EVENT_CONFIG[node.type] || { icon: Zap, nameKey: 'nodeTypes.customEvent' }
  const Icon = config.icon

  const formKeys = ['bgmPath', 'sfxPath', 'voicePath', 'imagePath', 'characterId', 'intensity', 'duration', 'fadeOutDuration']
  let summary = ''
  for (const key of formKeys) {
    const val = form.getValueIn(key)
    if (val) {
      summary = String(val)
      break
    }
  }

  return (
    <BaseNodeShell
      headerColor="#f97316"
      icon={<Icon size={12} />}
      title={t(config.nameKey)}
    >
      {summary ? (
        <span className="text-gray-400 text-[11px] truncate">{summary}</span>
      ) : (
        <span className="text-gray-500 text-[11px]">{t('nodeHints.eventNode')}</span>
      )}
    </BaseNodeShell>
  )
}
