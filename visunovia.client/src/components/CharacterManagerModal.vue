<template>
  <Transition name="character-manager-fade">
    <div
      v-if="uiStore.showCharacterManager"
      class="character-manager-overlay"
      @click.self="uiStore.closeCharacterManager()"
    >
      <section class="character-manager" role="dialog" aria-modal="true" aria-labelledby="character-manager-title">
        <header class="character-manager-header">
          <div>
            <h2 id="character-manager-title">角色管理器</h2>
            <p>读取当前项目 <code>Assets/Characters</code> 下的目录作为角色，并默认使用 <code>Avatars</code> 中的首张图片作为头像。</p>
          </div>
          <button class="icon-button" type="button" aria-label="关闭角色管理器" @click="uiStore.closeCharacterManager()">✕</button>
        </header>

        <div class="character-manager-toolbar">
          <button class="primary-button" type="button" :disabled="isRefreshing" @click="refreshCharacters">
            {{ isRefreshing ? '读取中...' : '刷新角色目录' }}
          </button>
          <input
            v-model.trim="searchQuery"
            class="character-search-input"
            type="search"
            placeholder="快速查找角色..."
          />
          <span class="character-count">{{ characterStore.characters.length }} 个角色</span>
        </div>

        <div v-if="loadError" class="manager-message error-message">{{ loadError }}</div>

        <div v-if="characterStore.characters.length === 0" class="empty-characters">
          <div class="empty-icon">👥</div>
          <strong>没有读取到角色目录</strong>
          <span>请确认当前项目存在 Assets/Characters/&lt;CharacterID&gt; 目录。</span>
        </div>

        <div v-else-if="filteredCharacters.length === 0" class="empty-characters">
          <div class="empty-icon">🔎</div>
          <strong>没有匹配的角色</strong>
          <span>请尝试其他名称或备注关键词。</span>
        </div>

        <div v-else class="character-list">
          <article v-for="character in filteredCharacters" :key="character.id" class="character-card">
            <div class="avatar-tools">
              <button class="avatar-picker" type="button" :style="getAvatarStyle(character)" :title="`选择 ${characterLabel(character)} 的头像`" @click="openAvatarPicker(character)">
                <span v-if="!character.avatar">{{ getInitial(character.displayId || character.name) }}</span>
              </button>
              <div class="avatar-actions">
                <label class="color-dot" :style="{ backgroundColor: character.color }" title="选择角色颜色">
                  <input
                    type="color"
                    :value="character.color"
                    @input="updateCharacter(character.id, { color: ($event.target as HTMLInputElement).value })"
                  />
                </label>
                <button v-if="character.avatar" class="avatar-clear" type="button" title="移除头像" @click="updateCharacter(character.id, { avatar: '' })">×</button>
              </div>
            </div>

            <div class="character-fields">
              <label class="field-label">
                <span>显示 ID</span>
                <input
                  class="character-name-input"
                  type="text"
                  :value="character.displayId"
                  placeholder="例如 Plana"
                  @input="updateCharacter(character.id, { displayId: ($event.target as HTMLInputElement).value })"
                />
              </label>
              <label class="field-label">
                <span>归属（Affiliation）</span>
                <input
                  class="character-affiliation-input"
                  type="text"
                  :value="character.affiliation"
                  placeholder="例如 Abydos / Seminar / Millenium"
                  @input="updateCharacter(character.id, { affiliation: ($event.target as HTMLInputElement).value })"
                />
              </label>
              <div class="character-folder-input readonly-field" :title="character.spriteFolder">文件夹：{{ character.name }}</div>
              <div class="character-folder-input readonly-field" :title="character.spriteFolder">路径：{{ getCharacterRelativePath(character) }}</div>
              <textarea
                class="character-note-input"
                :value="character.note"
                placeholder="备注，例如声线、身份或立绘说明"
                rows="2"
                @input="updateCharacter(character.id, { note: ($event.target as HTMLTextAreaElement).value })"
              />
            </div>
          </article>
        </div>

        <div v-if="avatarPickerVisible" class="avatar-modal-overlay" @click.self="closeAvatarPicker">
          <section class="avatar-modal">
            <header class="avatar-modal-header">
              <strong>选择头像：{{ avatarPickerCharacter ? characterLabel(avatarPickerCharacter) : '' }}</strong>
              <button class="icon-button" type="button" @click="closeAvatarPicker">✕</button>
            </header>
            <div v-if="avatarFiles.length === 0" class="empty-characters compact-empty">
              <div class="empty-icon">🖼️</div>
              <strong>Avatars 目录中没有图片</strong>
              <span>请将头像图片放入 Assets/Characters/{{ avatarPickerCharacter?.name }}/Avatars。</span>
            </div>
            <div v-else class="avatar-grid">
              <button
                v-for="file in avatarFiles"
                :key="file.path"
                :class="['avatar-grid-item', { selected: file.path === avatarPickerCharacter?.avatar }]"
                type="button"
                @click="selectAvatar(file.path)"
              >
                <img :src="getPreviewUrl(file.path)" :alt="file.name" loading="lazy" />
                <span :title="file.name">{{ file.name }}</span>
              </button>
            </div>
          </section>
        </div>
      </section>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useCharacterStore, type CharacterProfile } from '@/stores/useCharacterStore'
import { useUIStore } from '@/stores/useUIStore'
import { getEntries, type DirEntry } from '@/api/fileBrowser'
import { resolveAssetUrl } from '@/utils/assetPaths'

const characterStore = useCharacterStore()
const uiStore = useUIStore()
const searchQuery = ref('')
const isRefreshing = ref(false)
const loadError = ref('')
const avatarPickerVisible = ref(false)
const avatarPickerCharacter = ref<CharacterProfile | null>(null)
const avatarFiles = ref<DirEntry[]>([])

const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.webp', '.gif', '.bmp', '.svg']

const filteredCharacters = computed(() => characterStore.searchCharacters(searchQuery.value))

onMounted(() => {
  refreshCharacters()
})

async function updateCharacter(id: string, updates: Partial<CharacterProfile>) {
  try {
    await characterStore.updateCharacter(id, updates)
  } catch (error: any) {
    loadError.value = error?.message || '保存角色配置失败'
  }
}

async function refreshCharacters() {
  isRefreshing.value = true
  loadError.value = ''
  try {
    await characterStore.refreshFromAssets()
  } catch (error: any) {
    loadError.value = error?.message || '读取 Assets/Characters 失败'
    await characterStore.load()
  } finally {
    isRefreshing.value = false
  }
}

function getInitial(name: string) {
  return name.trim().charAt(0).toUpperCase() || '角'
}

function characterLabel(character: CharacterProfile) {
  const displayId = character.displayId?.trim() || character.id
  return `${character.name} (${displayId})`
}

function getCharacterRelativePath(character: CharacterProfile) {
  return `/${character.name}/`
}

function getAvatarStyle(character: CharacterProfile) {
  return character.avatar
    ? { backgroundImage: `url(${getPreviewUrl(character.avatar)})`, backgroundColor: character.color }
    : { backgroundColor: character.color }
}

function getPreviewUrl(path: string) {
  return resolveAssetUrl(path, 'Characters')
}

function isImageFile(name: string) {
  const dotIndex = name.lastIndexOf('.')
  if (dotIndex < 0) return false
  return IMAGE_EXTENSIONS.includes(name.slice(dotIndex).toLowerCase())
}

function joinPath(...parts: string[]) {
  return parts.filter(Boolean).join('\\').replace(/[\\/]+/g, '\\')
}

async function openAvatarPicker(character: CharacterProfile) {
  avatarPickerCharacter.value = character
  avatarPickerVisible.value = true
  avatarFiles.value = []

  try {
    const result = await getEntries(joinPath(character.spriteFolder, 'Avatars'))
    avatarFiles.value = result.entries
      .filter(entry => !entry.isDirectory && isImageFile(entry.name))
      .sort((a, b) => a.name.localeCompare(b.name, 'zh-CN'))
  } catch (error) {
    console.warn('[CharacterManager] Failed to load avatar files:', error)
    avatarFiles.value = []
  }
}

function selectAvatar(path: string) {
  if (!avatarPickerCharacter.value) return
  updateCharacter(avatarPickerCharacter.value.id, { avatar: path })
  closeAvatarPicker()
}

function closeAvatarPicker() {
  avatarPickerVisible.value = false
  avatarPickerCharacter.value = null
  avatarFiles.value = []
}
</script>

<style scoped>
.character-manager-overlay {
  position: fixed;
  inset: 0;
  z-index: 30000;
  display: flex;
  justify-content: flex-end;
  background: rgba(0, 0, 0, 0.42);
  backdrop-filter: blur(2px);
}

.character-manager {
  width: min(520px, 100vw);
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #1f1f23;
  border-left: 1px solid #3e3e42;
  box-shadow: -14px 0 34px rgba(0, 0, 0, 0.34);
  color: #f0f0f0;
}

.character-manager-header {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  padding: 22px 24px 18px;
  border-bottom: 1px solid #34343a;
  background: linear-gradient(135deg, #252d3a 0%, #202024 70%);
}

.character-manager-header h2 {
  margin: 0 0 6px;
  font-size: 20px;
}

.character-manager-header p {
  margin: 0;
  color: #b9beca;
  font-size: 12px;
  line-height: 1.6;
}

.icon-button {
  width: 30px;
  height: 30px;
  border: 0;
  border-radius: 6px;
  color: #d4d4d4;
  background: rgba(255, 255, 255, 0.08);
  cursor: pointer;
}

.icon-button:hover {
  background: rgba(255, 255, 255, 0.14);
}

.character-manager-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding: 14px 18px;
  border-bottom: 1px solid #34343a;
}

.character-search-input {
  flex: 1;
  min-width: 0;
  padding: 8px 10px;
  border: 1px solid #4a4a52;
  border-radius: 6px;
  color: #eeeeee;
  background: #1e1e22;
  outline: none;
  font-size: 12px;
}

.character-search-input:focus {
  border-color: #0e639c;
}

.primary-button {
  border: 0;
  border-radius: 6px;
  cursor: pointer;
  font-size: 12px;
}

.primary-button {
  padding: 8px 12px;
  color: #ffffff;
  background: #0e639c;
}

.primary-button:hover {
  background: #1177bb;
}

.primary-button:disabled {
  opacity: 0.65;
  cursor: wait;
}

.manager-message {
  margin: 12px 18px 0;
  padding: 8px 10px;
  border-radius: 6px;
  font-size: 12px;
}

.error-message {
  color: #ffb4b4;
  background: rgba(244, 71, 71, 0.14);
  border: 1px solid rgba(244, 71, 71, 0.26);
}

.character-count {
  color: #9a9a9a;
  font-size: 12px;
}

.empty-characters {
  margin: 32px 20px;
  padding: 36px 18px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  border: 1px dashed #47474f;
  border-radius: 10px;
  color: #aaa;
  text-align: center;
}

.empty-icon {
  font-size: 42px;
}

.character-list {
  overflow-y: auto;
  padding: 16px 18px 28px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.character-card {
  display: grid;
  grid-template-columns: 64px 1fr;
  gap: 12px;
  align-items: center;
  padding: 12px;
  border: 1px solid #3b3b42;
  border-radius: 10px;
  background: #28282d;
}

.avatar-tools {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.avatar-picker {
  position: relative;
  width: 54px;
  height: 54px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background-position: center;
  background-size: cover;
  color: #fff;
  font-weight: 700;
  box-shadow: inset 0 0 0 2px rgba(255, 255, 255, 0.22);
  cursor: pointer;
  border: 0;
}

.color-dot input {
  position: absolute;
  inset: 0;
  opacity: 0;
  cursor: pointer;
}

.avatar-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.color-dot {
  position: relative;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.35);
  cursor: pointer;
}

.avatar-clear {
  width: 18px;
  height: 18px;
  padding: 0;
  border: 0;
  border-radius: 50%;
  color: #ffb4b4;
  background: rgba(244, 71, 71, 0.16);
  cursor: pointer;
  line-height: 18px;
}

.character-fields {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.character-name-input,
.character-affiliation-input,
.character-folder-input,
.character-note-input {
  width: 100%;
  box-sizing: border-box;
  border: 1px solid #4a4a52;
  border-radius: 6px;
  color: #eeeeee;
  background: #1e1e22;
  outline: none;
}

.field-label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: #c8c8cf;
  font-size: 11px;
}

.character-name-input {
  padding: 8px 10px;
  font-size: 13px;
  font-weight: 600;
}

.character-affiliation-input {
  padding: 8px 10px;
  font-size: 13px;
}

.character-name-input:focus,
.character-affiliation-input:focus {
  border-color: #0e639c;
}

.character-folder-input {
  padding: 7px 10px;
  font-size: 12px;
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
}

.readonly-field {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.character-note-input {
  resize: vertical;
  min-height: 46px;
  padding: 8px 10px;
  font-size: 12px;
  line-height: 1.4;
}

.character-note-input:focus {
  border-color: #0e639c;
}

.avatar-modal-overlay {
  position: absolute;
  inset: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.52);
}

.avatar-modal {
  width: min(460px, calc(100% - 32px));
  max-height: min(680px, calc(100% - 48px));
  display: flex;
  flex-direction: column;
  border: 1px solid #45454c;
  border-radius: 12px;
  background: #252529;
  box-shadow: 0 18px 46px rgba(0, 0, 0, 0.42);
  overflow: hidden;
}

.avatar-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  border-bottom: 1px solid #3a3a40;
}

.avatar-grid {
  overflow-y: auto;
  padding: 14px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(96px, 1fr));
  gap: 10px;
}

.avatar-grid-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px;
  border: 1px solid #3f3f46;
  border-radius: 8px;
  color: #d8d8d8;
  background: #1f1f23;
  cursor: pointer;
}

.avatar-grid-item:hover,
.avatar-grid-item.selected {
  border-color: #0e639c;
  background: #263545;
}

.avatar-grid-item img {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  border-radius: 6px;
  background: #151515;
}

.avatar-grid-item span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 11px;
}

.compact-empty {
  margin: 16px;
}

.character-manager-fade-enter-active,
.character-manager-fade-leave-active {
  transition: opacity 0.16s ease;
}

.character-manager-fade-enter-from,
.character-manager-fade-leave-to {
  opacity: 0;
}
</style>
