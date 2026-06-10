import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { getCharacterConfig, getCurrentProject, saveCharacterConfig } from '@/api/projectApi'
import { getEntries } from '@/api/fileBrowser'
import { syncCharacterSelectOptions } from '@/services/characterOptions'

export interface CharacterProfile {
  id: string
  name: string
  displayId: string
  color: string
  avatar: string
  spriteFolder: string
  note: string
}

const DEFAULT_COLORS = ['#569cd6', '#ce9178', '#c586c0', '#4ec9b0', '#dcdcaa', '#b5cea8']
const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.bmp', '.svg']

function normalizeCharacter(raw: Partial<CharacterProfile>, index = 0): CharacterProfile {
  const name = String(raw.name || '').trim()
  const id = raw.id || name
  return {
    id,
    name,
    displayId: raw.displayId || id,
    color: raw.color || DEFAULT_COLORS[index % DEFAULT_COLORS.length],
    avatar: raw.avatar || '',
    spriteFolder: raw.spriteFolder || '',
    note: raw.note || '',
  }
}

function joinPath(...parts: string[]) {
  return parts
    .filter(Boolean)
    .join('\\')
    .replace(/[\\/]+/g, '\\')
}

function isImageFile(name: string) {
  const dotIndex = name.lastIndexOf('.')
  if (dotIndex < 0) return false
  return IMAGE_EXTENSIONS.includes(name.slice(dotIndex).toLowerCase())
}

function toRelativeCharacterPath(projectPath: string, path: string) {
  if (!path) return ''
  if (path.startsWith('/')) return path.replace(/\\/g, '/')

  const charactersRoot = joinPath(projectPath, 'Assets', 'Characters').toLocaleLowerCase('zh-CN')
  const normalizedPath = path.replace(/[\/]+/g, '\\')
  const normalizedLower = normalizedPath.toLocaleLowerCase('zh-CN')
  if (!normalizedLower.startsWith(charactersRoot)) return path.replace(/\\/g, '/')

  const relative = normalizedPath.slice(joinPath(projectPath, 'Assets', 'Characters').length).replace(/^[\\/]+/, '')
  return `/${relative.replace(/\\/g, '/')}`
}

function toAbsoluteCharacterPath(projectPath: string, path: string) {
  if (!path || !path.startsWith('/')) return path
  return joinPath(projectPath, 'Assets', 'Characters', path.replace(/^[/\\]+/, ''))
}

export const useCharacterStore = defineStore('characters', () => {
  const characters = ref<CharacterProfile[]>([])
  const isLoaded = ref(false)
  const isLoading = ref(false)
  const projectRoot = ref('')

  const sortedCharacters = computed(() =>
    [...characters.value].sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))
  )

  function searchCharacters(query: string) {
    const keyword = query.trim().toLocaleLowerCase('zh-CN')
    if (!keyword) return sortedCharacters.value

    return sortedCharacters.value.filter(character =>
      character.name.toLocaleLowerCase('zh-CN').includes(keyword)
      || character.displayId.toLocaleLowerCase('zh-CN').includes(keyword)
      || character.spriteFolder.toLocaleLowerCase('zh-CN').includes(keyword)
      || character.note.toLocaleLowerCase('zh-CN').includes(keyword)
    )
  }

  async function load() {
    if ((isLoaded.value && projectRoot.value) || isLoading.value) return
    await refreshFromAssets()
  }

  async function save() {
    await saveCharacterConfig({
      characters: characters.value.map(character => ({
        id: character.id,
        displayId: character.displayId,
        color: character.color,
        avatar: toRelativeCharacterPath(projectRoot.value, character.avatar),
        note: character.note,
      }))
    })
  }

  async function findDefaultAvatar(characterFolder: string) {
    const avatarsFolder = joinPath(characterFolder, 'Avatars')

    try {
      const result = await getEntries(avatarsFolder)
      const avatar = result.entries
        .filter(entry => !entry.isDirectory && isImageFile(entry.name))
        .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))[0]
      return avatar?.path || ''
    } catch (error) {
      return ''
    }
  }

  async function refreshFromAssets() {
    if (isLoading.value) return

    isLoading.value = true
    try {
      const currentProject = await getCurrentProject()
      const projectPath = currentProject.data?.projectPath
      if (!projectPath) {
        projectRoot.value = ''
        characters.value = []
        isLoaded.value = true
        syncCharacterSelectOptions([])
        return
      }

      projectRoot.value = projectPath
      const config = await getCharacterConfig()
      const configuredById = new Map((config.characters || []).map(character => [character.id, character]))
      const charactersFolder = joinPath(projectPath, 'Assets', 'Characters')
      const result = await getEntries(charactersFolder)
      const folders = result.entries
        .filter(entry => entry.isDirectory)
        .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))

      const loadedCharacters = await Promise.all(folders.map(async (folder, index) => {
        const configured = configuredById.get(folder.name)
        return normalizeCharacter({
          id: folder.name,
          name: folder.name,
          displayId: configured?.displayId || folder.name,
          color: configured?.color || DEFAULT_COLORS[index % DEFAULT_COLORS.length],
          avatar: configured?.avatar
            ? toAbsoluteCharacterPath(projectPath, configured.avatar)
            : await findDefaultAvatar(folder.path),
          spriteFolder: folder.path,
          note: configured?.note || '',
        }, index)
      }))

      characters.value = loadedCharacters
      syncCharacterSelectOptions(loadedCharacters)
      isLoaded.value = true
      await save()
    } finally {
      isLoading.value = false
    }
  }

  async function addCharacter(name = '新角色') {
    await load()
    const character = normalizeCharacter({ name }, characters.value.length)
    characters.value.push(character)
    syncCharacterSelectOptions(characters.value)
    await save()
    return character
  }

  async function updateCharacter(id: string, updates: Partial<CharacterProfile>) {
    await load()
    const character = characters.value.find(item => item.id === id)
    if (!character) return

    character.name = character.id
    character.displayId = String(updates.displayId ?? character.displayId).trim() || character.id
    character.color = updates.color || character.color
    character.avatar = updates.avatar ?? character.avatar
    character.spriteFolder = updates.spriteFolder ?? character.spriteFolder
    character.note = updates.note ?? character.note
    syncCharacterSelectOptions(characters.value)
    await save()
  }

  async function removeCharacter(id: string) {
    await load()
    characters.value = characters.value.filter(item => item.id !== id)
    syncCharacterSelectOptions(characters.value)
    await save()
  }

  async function ensureCharacter(name: string) {
    await load()
    const normalizedName = name.trim()
    if (!normalizedName) return null

    const existing = characters.value.find(item => item.name === normalizedName)
    if (existing) return existing

    return await addCharacter(normalizedName)
  }

  return {
    characters,
    isLoaded,
    isLoading,
    sortedCharacters,
    searchCharacters,
    load,
    refreshFromAssets,
    addCharacter,
    updateCharacter,
    removeCharacter,
    ensureCharacter,
  }
})