<template>
  <Teleport to="body">
    <div v-if="visible" class="modal-overlay" @click.self="handleCancel">
      <div class="resource-picker-modal">
        <!-- 标题栏 -->
        <div class="modal-header">
          <span class="modal-title">{{ modalTitle }}</span>
          <button class="modal-close-btn" @click="handleCancel">✕</button>
        </div>

        <!-- 文件网格 -->
        <div class="modal-body">
          <div v-if="filteredFiles.length === 0" class="empty-hint">
            {{ t('resource.noFiles', '无匹配的资源文件') }}
          </div>
          <div v-else class="file-grid">
            <div
              v-for="file in filteredFiles"
              :key="file.path"
              :class="['file-item', { selected: isSelected(file.path) }]"
              @click="handleSelect(file.path)"
              @dblclick="handleConfirm(file.path)"
            >
              <!-- 图片预览或文件图标 -->
              <img
                v-if="isImageType && file.thumbnail"
                :src="file.thumbnail"
                :alt="file.name"
                class="file-thumbnail"
                loading="lazy"
              />
              <div v-else :class="['file-icon', `icon-${file.ext.slice(1)}`]">
                {{ getFileIconLetter(file.ext) }}
              </div>
              <span class="file-name" :title="file.name">{{ file.name }}</span>
            </div>
          </div>
        </div>

        <!-- 底部按钮 -->
        <div class="modal-footer">
          <button class="btn btn-cancel" @click="handleCancel">
            {{ t('common.cancel', '取消') }}
          </button>
          <button
            class="btn btn-confirm"
            :disabled="!selectedPath"
            @click="handleConfirm(selectedPath)"
          >
            {{ t('common.ok', '确认') }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import { RESOURCE_TYPE_EXTENSIONS, type ResourceType } from '@/stores/useResourceRegistry'

const props = defineProps<{
  visible: boolean
  resourceType: ResourceType
  currentPath?: string
  /** 工作区文件列表（由父组件传入） */
  files?: Array<{ name: string; path?: string }>
}>()

const emit = defineEmits<{
  confirm: [path: string]
  cancel: []
}>()

const { t } = useLocalization()
const selectedPath = ref('')

/** 图片类型的资源类型集合 */
const IMAGE_TYPES: ResourceType[] = ['image']

const isImageType = computed(() => IMAGE_TYPES.includes(props.resourceType))

/** 模态框标题 */
const modalTitle = computed(() => {
  const titles: Record<ResourceType, string> = {
    image: t('resource.selectImage', '选择图片资源'),
    audio: t('resource.selectAudio', '选择音效资源'),
    bgm:   t('resource.selectBgm', '选择背景音乐'),
    voice: t('resource.selectVoice', '选择语音资源'),
    video: t('resource.selectVideo', '选择视频资源'),
    scene: t('resource.selectScene', '选择场景资源'),
    font:  t('resource.selectFont', '选择字体资源'),
    data:  t('resource.selectData', '选择数据资源'),
  }
  return titles[props.resourceType] || t('resource.selectResource', '选择资源')
})

/** 过滤后的文件列表 */
const filteredFiles = computed(() => {
  const allFiles = props.files || []
  const exts = RESOURCE_TYPE_EXTENSIONS[props.resourceType] || []
  return allFiles
    .filter((f: any) => {
      const ext = f.name?.slice(f.name.lastIndexOf('.')).toLowerCase() || ''
      return exts.includes(ext)
    })
    .map((f: any) => ({
      name: f.name,
      path: f.path || f.name,
      ext: f.name?.slice(f.name.lastIndexOf('.')).toLowerCase() || '',
      thumbnail: f.path ? `/assets/${encodeURIComponent(f.path)}` : '',
    }))
})

function isSelected(path: string): boolean {
  return selectedPath.value === path
}

function handleSelect(path: string): void {
  selectedPath.value = selectedPath.value === path ? '' : path
}

function handleConfirm(path: string): void {
  emit('confirm', path)
  selectedPath.value = ''
}

function handleCancel(): void {
  emit('cancel')
  selectedPath.value = ''
}

function getFileIconLetter(ext: string): string {
  const map: Record<string, string> = {
    '.mp3': '♫', '.wav': '♪', '.ogg': '♬',
    '.mp4': '▶', '.webm': '▶',
    '.json': '{', '.xml': '<',
    '.ttf': 'A', '.otf': 'A', '.woff': 'A',
    '.txt': 'T',
  }
  return map[ext] || '?'
}

// 打开时重置选中状态
watch(() => props.visible, (val) => {
  if (val) {
    selectedPath.value = props.currentPath || ''
  }
})
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10000;
}
.resource-picker-modal {
  width: 640px;
  max-height: 70vh;
  background: #1e1e2e;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid #333346;
}
.modal-title {
  font-size: 15px;
  font-weight: 600;
  color: #e0e0e0;
}
.modal-close-btn {
  background: none;
  border: none;
  color: #888;
  font-size: 18px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
}
.modal-close-btn:hover { color: #fff; background: rgba(255,255,255,0.1); }
.modal-body {
  padding: 16px;
  overflow-y: auto;
  min-height: 200px;
  max-height: 50vh;
}
.file-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(110px, 1fr));
  gap: 10px;
}
.file-item {
  border: 2px solid transparent;
  border-radius: 8px;
  padding: 8px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  transition: all 0.15s ease;
  background: #2a2a3c;
}
.file-item:hover { border-color: #555; background: #333348; }
.file-item.selected { border-color: #7c3aed; background: rgba(124, 58, 237, 0.15); }
.file-thumbnail {
  width: 80px;
  height: 60px;
  object-fit: cover;
  border-radius: 4px;
  background: #111;
}
.file-icon {
  width: 80px;
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
  border-radius: 4px;
  background: #222235;
  color: #888;
}
.file-name {
  font-size: 11px;
  color: #aaa;
  text-align: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  width: 100%;
}
.empty-hint {
  text-align: center;
  color: #666;
  padding: 40px 20px;
  font-size: 14px;
}
.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 14px 20px;
  border-top: 1px solid #333346;
}
.btn {
  padding: 8px 20px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: all 0.15s;
}
.btn-cancel { background: #333346; color: #ccc; }
.btn-cancel:hover { background: #444459; }
.btn-confirm { background: #7c3aed; color: #fff; }
.btn-confirm:hover:not(:disabled) { background: #6d28d9; }
.btn-confirm:disabled { opacity: 0.4; cursor: not-allowed; }
</style>
