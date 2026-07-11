import { resolveBackendUrl } from './backendUrl'

export type AssetFolder = 'Backgrounds' | 'Characters' | 'Musics' | 'Sfx' | 'Voices' | 'Emoji'

const ASSET_FOLDER_ALIASES: Record<AssetFolder, string[]> = {
  Backgrounds: ['backgrounds', 'background', 'bg', 'bgs'],
  Characters: ['characters', 'character'],
  Musics: ['musics', 'music', 'bgm'],
  Sfx: ['sfx', 'sfxs', 'sounds', 'sound'],
  Voices: ['voices', 'voice'],
  Emoji: ['emoji', 'emojis'],
}

export function normalizePathSeparators(path: string) {
  return String(path || '').replace(/\\/g, '/').replace(/\/+/g, '/')
}

export function isAbsoluteFilePath(path: string) {
  return /^[a-zA-Z]:[\\/]/.test(path) || path.startsWith('\\\\')
}

export function toAssetPath(path: string) {
  const normalized = normalizePathSeparators(path).trim()
  if (!normalized) return ''

  const assetsIndex = normalized.toLowerCase().lastIndexOf('/assets/')
  if (assetsIndex >= 0) return normalized.slice(assetsIndex + 1)
  if (normalized.toLowerCase().startsWith('assets/')) return normalized

  return normalized
}

export function toFolderRelativeAssetPath(path: string, folder: AssetFolder) {
  const assetPath = toAssetPath(path).replace(/^[/\\]+/, '')
  if (!assetPath) return ''

  const parts = assetPath.split('/').filter(Boolean)
  if (parts[0]?.toLowerCase() === 'assets') parts.shift()

  const aliases = ASSET_FOLDER_ALIASES[folder]
  if (parts.length > 0 && aliases.includes(parts[0].toLowerCase())) {
    parts.shift()
  }

  return parts.join('/')
}

export function resolveAssetUrl(path: string, folder: AssetFolder) {
  const rawPath = String(path || '').trim()
  if (!rawPath) return ''
  if (/^https?:\/\//i.test(rawPath) || rawPath.startsWith('data:') || rawPath.startsWith('blob:')) return rawPath

  const assetPath = toAssetPath(rawPath)
  if (assetPath.toLowerCase().startsWith('assets/')) {
    return resolveBackendUrl(`/api/resources/file/${assetPath.split('/').map(encodeURIComponent).join('/')}`)
  }

  if (isAbsoluteFilePath(rawPath)) {
    return resolveBackendUrl(`/api/FileBrowser/preview?path=${encodeURIComponent(rawPath)}`)
  }

  const folderRelativePath = toFolderRelativeAssetPath(rawPath, folder)
  return resolveBackendUrl(`/api/resources/file/Assets/${folder}/${folderRelativePath.split('/').map(encodeURIComponent).join('/')}`)
}

export function normalizeKnownAssetProperty(key: string, value: unknown, parentKey = '') {
  if (typeof value !== 'string') return value

  if (/^sprite\d*$/i.test(key)) return toFolderRelativeAssetPath(value, 'Characters')
  if (/^voice\d*$/i.test(key)) return toFolderRelativeAssetPath(value, 'Voices')
  if (key === 'path' && parentKey === 'sprites') return toFolderRelativeAssetPath(value, 'Characters')
  if (key === 'path' && parentKey === 'voices') return toFolderRelativeAssetPath(value, 'Voices')

  const assetFolderByKey: Record<string, AssetFolder> = {
    imagePath: 'Backgrounds',
    background: 'Backgrounds',
    bgmPath: 'Musics',
    bgmFile: 'Musics',
    sfxPath: 'Sfx',
    soundFile: 'Sfx',
    voicePath: 'Voices',
    voice: 'Voices',
    sprite: 'Characters',
    expressionBalloon: 'Emoji',
    expressionIcon: 'Emoji',
  }

  const folder = assetFolderByKey[key]
  return folder ? toFolderRelativeAssetPath(value, folder) : value
}

export function normalizeAssetProperties<T extends Record<string, any>>(properties: T, parentKey = ''): T {
  const normalized: Record<string, any> = {}

  Object.entries(properties || {}).forEach(([key, value]) => {
    if (Array.isArray(value)) {
      normalized[key] = value.map((item) => {
        if (item && typeof item === 'object') return normalizeAssetProperties(item as Record<string, any>, key)
        return normalizeKnownAssetProperty(key, item, parentKey)
      })
    } else if (value && typeof value === 'object') {
      normalized[key] = normalizeAssetProperties(value as Record<string, any>, key)
    } else {
      normalized[key] = normalizeKnownAssetProperty(key, value, parentKey)
    }
  })

  return normalized as T
}
