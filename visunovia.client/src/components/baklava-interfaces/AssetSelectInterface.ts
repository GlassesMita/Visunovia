import { NodeInterface } from '@baklavajs/core'
import { defineComponent, h, onMounted, ref } from 'vue'
import NativeFreeSelect from '@/components/NativeFreeSelect.vue'
import { getEntries, type DirEntry } from '@/api/fileBrowser'
import { getCurrentProject } from '@/api/projectApi'
import { resolveAssetUrl, toFolderRelativeAssetPath } from '@/utils/assetPaths'

export type AssetSelectKind = 'background' | 'bgm' | 'sfx' | 'voice'

type AssetFolder = 'Backgrounds' | 'Musics' | 'Sfx' | 'Voices'

interface AssetOption {
  name: string
  path: string
}

export interface AssetSelectOptions {
  voiceCharacterIdInputKey?: string
  voiceSlot?: string
}

const BACKGROUND_IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.bmp', '.svg']
const BACKGROUND_VIDEO_EXTENSIONS = ['.mp4', '.webm', '.m4v', '.mov', '.ogv']
const BACKGROUND_EXTENSIONS = [...BACKGROUND_IMAGE_EXTENSIONS, ...BACKGROUND_VIDEO_EXTENSIONS]

const ASSET_CONFIG: Record<AssetSelectKind, { folder: AssetFolder; label: string; extensions: string[] }> = {
  background: {
    folder: 'Backgrounds',
    label: '背景',
    extensions: BACKGROUND_EXTENSIONS,
  },
  bgm: {
    folder: 'Musics',
    label: '音乐',
    extensions: ['.mp3', '.wav', '.ogg', '.flac', '.m4a'],
  },
  sfx: {
    folder: 'Sfx',
    label: '音效',
    extensions: ['.mp3', '.wav', '.ogg', '.flac', '.aac'],
  },
  voice: {
    folder: 'Voices',
    label: '语音',
    extensions: ['.mp3', '.wav', '.ogg', '.flac', '.opus'],
  },
}

const cache = new Map<string, { expiresAt: number; options: AssetOption[] }>()
const CACHE_TTL_MS = 5000

function joinPath(...parts: string[]) {
  return parts
    .filter(Boolean)
    .join('\\')
    .replace(/[\\/]+/g, '\\')
}

function getExtension(name: string) {
  const dotIndex = name.lastIndexOf('.')
  return dotIndex >= 0 ? name.slice(dotIndex).toLowerCase() : ''
}

function isSupportedFile(entry: DirEntry, extensions: string[]) {
  return !entry.isDirectory && extensions.includes(getExtension(entry.name))
}

async function collectAssets(directory: string, folder: AssetFolder, extensions: string[], depth = 0): Promise<AssetOption[]> {
  if (depth > 4) return []

  const result = await getEntries(directory)
  const files = result.entries
    .filter(entry => isSupportedFile(entry, extensions))
    .map(entry => ({ name: entry.name, path: toFolderRelativeAssetPath(entry.path, folder) }))

  const nested = await Promise.all(
    result.entries
      .filter(entry => entry.isDirectory)
      .map(entry => collectAssets(entry.path, folder, extensions, depth + 1).catch(() => []))
  )

  return [...files, ...nested.flat()]
}

function getInterfaceValue(node: any, key: string) {
  const iface = node?.inputs?.[key]
  return iface?.value ?? ''
}

function getInterfaceKey(node: any, endpoint: any, fallback = '') {
  const entries = Object.entries(node?.inputs || {}) as Array<[string, any]>
  const match = entries.find(([, iface]) => iface === endpoint || iface?.id === endpoint?.id || iface?.name === endpoint?.name)
  return match?.[0] || String(fallback || endpoint?.name || '')
}

function getVoiceCharacterId(intf: any) {
  const ownNode = intf?.node
  const explicitKey = String(intf?.voiceCharacterIdInputKey || '').trim()
  if (explicitKey) {
    const speakerSlot = String(getInterfaceValue(ownNode, 'speakerSlot') || '').trim()
    if (!speakerSlot || speakerSlot === 'all') return ''
    if (speakerSlot === '6') {
      const explicitValue = String(getInterfaceValue(ownNode, explicitKey) || '').trim()
      if (explicitValue) return explicitValue
      return ''
    }
  }

  const slot = String(intf?.voiceSlot || '').trim()
  if (!ownNode?.id || !slot) return ''

  const editor = (window as any).__editor
  const graph = editor?.graph
  const connection = graph?.connections?.find((candidate: any) => {
    if (candidate?.to?.nodeId !== ownNode.id) return false
    const targetPort = getInterfaceKey(ownNode, candidate.to, candidate.to?.name)
    return targetPort === `characterControl${slot}`
  })
  if (!connection) return ''

  const controlNode = graph.nodes?.find((node: any) => node.id === connection.from?.nodeId)
  if (!controlNode) return ''

  const controlSlot = String(getInterfaceValue(controlNode, 'slot') || slot).trim()
  return controlSlot === '6'
    ? String(getInterfaceValue(controlNode, 'unmanagedCharacter') || getInterfaceValue(controlNode, 'character') || '').trim()
    : String(getInterfaceValue(controlNode, 'character') || '').trim()
}

async function loadAssetOptions(kind: AssetSelectKind, intf?: any) {
  const config = ASSET_CONFIG[kind]
  const voiceCharacterId = kind === 'voice' ? getVoiceCharacterId(intf) : ''
  const cacheKey = voiceCharacterId ? `${kind}:${voiceCharacterId}` : kind
  const cached = cache.get(cacheKey)
  if (cached && cached.expiresAt > Date.now()) return cached.options

  const currentProject = await getCurrentProject()
  const projectPath = currentProject.data?.projectPath
  if (!projectPath) return []

  const assetsRoot = joinPath(projectPath, 'Assets', config.folder, voiceCharacterId)
  const options = (await collectAssets(assetsRoot, config.folder, config.extensions))
    .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))

  cache.set(cacheKey, { expiresAt: Date.now() + CACHE_TTL_MS, options })
  return options
}

function toPreviewUrl(path: string) {
  return resolveAssetUrl(path, 'Backgrounds')
}

function isPreviewableBackground(path: string) {
  return BACKGROUND_EXTENSIONS.includes(getExtension(path))
}

function isVideoBackground(path: string) {
  return BACKGROUND_VIDEO_EXTENSIONS.includes(getExtension(path))
}

const AssetSelectComponent = defineComponent({
  name: 'AssetSelectInterfaceComponent',
  props: {
    intf: {
      type: Object,
      required: true,
    },
  },
  setup(props: any) {
    const options = ref<AssetOption[]>([])
    const loading = ref(false)
    const error = ref('')

    async function loadOptions() {
      const kind = (props.intf.assetKind || 'background') as AssetSelectKind
      loading.value = true
      error.value = ''
      try {
        options.value = await loadAssetOptions(kind, props.intf)
      } catch (err: any) {
        error.value = err?.message || '读取资产列表失败'
        options.value = []
      } finally {
        loading.value = false
      }
    }

    function selectAsset(path: string) {
      if (typeof props.intf.setValue === 'function') {
        props.intf.setValue(path)
      } else {
        props.intf.value = path
      }
    }

    onMounted(loadOptions)

    return () => {
      const kind = (props.intf.assetKind || 'background') as AssetSelectKind
      const config = ASSET_CONFIG[kind]
      const value = String(props.intf.value || '')
      const selectedName = options.value.find(option => option.path === value)?.name || value.split(/[\\/]/).pop() || ''
      const selectOptions = [
        { value: '', label: loading.value ? `读取${config.label}中...` : `未选择${config.label}` },
        ...(value && !options.value.some(option => option.path === value) ? [{ value, label: selectedName }] : []),
        ...options.value.map(option => ({ value: option.path, label: option.name })),
      ]

      return h('div', { class: 'vn-asset-select' }, [
        h(NativeFreeSelect, {
          class: 'vn-asset-select-control',
          modelValue: value,
          options: selectOptions,
          disabled: loading.value,
          title: value,
          'onUpdate:modelValue': selectAsset,
          onChange: selectAsset,
        }),
        kind === 'background' && value && isPreviewableBackground(value)
          ? h('div', { class: 'vn-asset-select-preview vn-asset-select-preview-lite', title: value }, isVideoBackground(value) ? '视频背景' : '图片背景')
          : null,
        error.value ? h('div', { class: 'vn-asset-select-error' }, error.value) : null,
      ])
    }
  },
})

export class AssetSelectInterface extends NodeInterface<string> {
  public assetKind: AssetSelectKind
  public voiceCharacterIdInputKey?: string
  public voiceSlot?: string

  constructor(name: string, value = '', assetKind: AssetSelectKind = 'background', options: AssetSelectOptions = {}) {
    super(name, value)
    this.assetKind = assetKind
    this.voiceCharacterIdInputKey = options.voiceCharacterIdInputKey
    this.voiceSlot = options.voiceSlot
    this.setComponent(AssetSelectComponent)
  }
}