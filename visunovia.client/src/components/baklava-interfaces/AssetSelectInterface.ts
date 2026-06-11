import { NodeInterface } from '@baklavajs/core'
import { defineComponent, h, onMounted, ref } from 'vue'
import { getEntries, type DirEntry } from '@/api/fileBrowser'
import { getCurrentProject } from '@/api/projectApi'
import { resolveAssetUrl, toFolderRelativeAssetPath } from '@/utils/assetPaths'

export type AssetSelectKind = 'background' | 'bgm' | 'sfx' | 'voice'

type AssetFolder = 'Backgrounds' | 'Musics' | 'Sfx' | 'Voices'

interface AssetOption {
  name: string
  path: string
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

async function loadAssetOptions(kind: AssetSelectKind) {
  const config = ASSET_CONFIG[kind]
  const cached = cache.get(kind)
  if (cached && cached.expiresAt > Date.now()) return cached.options

  const currentProject = await getCurrentProject()
  const projectPath = currentProject.data?.projectPath
  if (!projectPath) return []

  const assetsRoot = joinPath(projectPath, 'Assets', config.folder)
  const options = (await collectAssets(assetsRoot, config.folder, config.extensions))
    .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))

  cache.set(kind, { expiresAt: Date.now() + CACHE_TTL_MS, options })
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
        options.value = await loadAssetOptions(kind)
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

      return h('div', { class: 'vn-asset-select' }, [
        h('select', {
          class: 'vn-asset-select-control',
          value,
          disabled: loading.value,
          title: value,
          onChange: (event: Event) => selectAsset((event.target as HTMLSelectElement).value),
        }, [
          h('option', { value: '' }, loading.value ? `读取${config.label}中...` : `未选择${config.label}`),
          value && !options.value.some(option => option.path === value)
            ? h('option', { value }, selectedName)
            : null,
          ...options.value.map(option => h('option', { value: option.path, key: option.path }, option.name)),
        ]),
        kind === 'background' && value && isPreviewableBackground(value)
          ? isVideoBackground(value)
            ? h('video', { class: 'vn-asset-select-preview', src: toPreviewUrl(value), muted: true, loop: true, autoplay: true, preload: 'metadata', playsinline: true })
            : h('img', { class: 'vn-asset-select-preview', src: toPreviewUrl(value), alt: selectedName, loading: 'lazy', decoding: 'async' })
          : null,
        error.value ? h('div', { class: 'vn-asset-select-error' }, error.value) : null,
      ])
    }
  },
})

export class AssetSelectInterface extends NodeInterface<string> {
  public assetKind: AssetSelectKind

  constructor(name: string, value = '', assetKind: AssetSelectKind = 'background') {
    super(name, value)
    this.assetKind = assetKind
    this.setComponent(AssetSelectComponent)
  }
}