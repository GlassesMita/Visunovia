import { defineDynamicNode, NodeInterface } from '@baklavajs/core'
import {
  SelectInterface,
  TextInputInterface,
  NumberInterface,
  CheckboxInterface
} from '@baklavajs/renderer-vue'
import { EventType, eventTypeConfig, eventTypeLabels } from '@/types'
import { tSync } from '@/services/translationService'
import { AssetSelectInterface, type AssetSelectKind } from '@/components/baklava-interfaces/AssetSelectInterface'
import {
  ARROW_SYMBOL,
  createExecInPort,
  createExecOutPort,
  setNodeI18nTitle,
} from './BaseNode'

export const NODE_COLOR = '#FF9800'

function getAssetKind(subType: EventType, propName: string): AssetSelectKind {
  if (subType === EventType.ChangeBackground || propName === 'imagePath') return 'background'
  if (subType === EventType.PlayBGM || propName === 'bgmPath') return 'bgm'
  if (subType === EventType.PlayVoice || propName === 'voicePath') return 'voice'
  return 'sfx'
}

export default defineDynamicNode({
  type: 'EventNode',
  title: 'Event',
  inputs: {
    execIn: createExecInPort(ARROW_SYMBOL),
    subType: () => new SelectInterface(
      tSync('props.subType', 'Sub Type'),
      EventType.PlayBGM,
      Object.values(EventType).map((v) => ({
        value: v,
        text: tSync(eventTypeLabels[v as EventType] ?? '', v)
      }))
    )
  },
  outputs: {
    execOut: createExecOutPort(ARROW_SYMBOL)
  },
  onCreate() {
    setNodeI18nTitle(this, 'nodes.event', 'Event')
  },
  onUpdate({ subType }) {
    const config = eventTypeConfig[subType as EventType]
    if (!config) return {}

    const inputs: Record<string, () => NodeInterface<any>> = {}

    for (const prop of config.properties) {
      const label = tSync(`eventProps.${prop.name}`, prop.name)

      switch (prop.type) {
        case 'string':
          inputs[prop.name] = () =>
            new TextInputInterface(label, prop.defaultValue || '').setHidden(true)
          break
        case 'resource':
          inputs[prop.name] = () =>
            new AssetSelectInterface(label, prop.defaultValue || '', getAssetKind(subType as EventType, prop.name)).setHidden(true)
          break
        case 'number':
          inputs[prop.name] = () =>
            new NumberInterface(label, prop.defaultValue ?? 0).setHidden(true)
          break
        case 'boolean':
          inputs[prop.name] = () =>
            new CheckboxInterface(label, prop.defaultValue ?? false).setHidden(true)
          break
        case 'select':
          if (prop.options?.length) {
            inputs[prop.name] = () =>
              new SelectInterface(
                label,
                prop.defaultValue || prop.options![0].value,
                prop.options!.map((o) => ({
                  value: o.value,
                  text: tSync(`eventOptions.${o.value}`, o.label)
                }))
              ).setHidden(true)
          }
          break
      }
    }

    return { inputs }
  }
})
