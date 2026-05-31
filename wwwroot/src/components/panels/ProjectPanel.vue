<template>
  <div class="project-panel">
    <div class="panel-header">
      <h3>{{ t('panels.project') }}</h3>
    </div>
    <div class="panel-content">
      <div class="folder-tree">
        <!-- Scenes 文件夹 -->
        <div class="folder-item" @click="toggleFolder('scenes')">
          <span class="folder-icon">{{ expandedFolders.includes('scenes') ? '📂' : '📁' }}</span>
          <span class="folder-label">{{ t('folders.scenes') || 'Scenes' }}</span>
          <span class="folder-count">2</span>
        </div>
        <div v-if="expandedFolders.includes('scenes')" class="folder-content">
          <div 
            v-for="file in sceneFiles" 
            :key="file.name"
            class="file-item"
            :class="{ selected: selectedFile === file.name }"
            @click="selectFile(file.name)"
          >
            <span class="file-icon">📄</span>
            <span>{{ file.name }}</span>
          </div>
        </div>

        <!-- Characters 文件夹 -->
        <div class="folder-item" @click="toggleFolder('characters')">
          <span class="folder-icon">{{ expandedFolders.includes('characters') ? '📂' : '📁' }}</span>
          <span class="folder-label">{{ t('folders.characters') || 'Characters' }}</span>
          <span class="folder-count">2</span>
        </div>
        <div v-if="expandedFolders.includes('characters')" class="folder-content">
          <div 
            v-for="file in characterFiles" 
            :key="file.name"
            class="file-item"
            :class="{ selected: selectedFile === file.name }"
            @click="selectFile(file.name)"
          >
            <span class="file-icon">👤</span>
            <span>{{ file.name }}</span>
          </div>
        </div>

        <!-- Backgrounds 文件夹 -->
        <div class="folder-item" @click="toggleFolder('backgrounds')">
          <span class="folder-icon">{{ expandedFolders.includes('backgrounds') ? '📂' : '📁' }}</span>
          <span class="folder-label">{{ t('folders.backgrounds') || 'Backgrounds' }}</span>
          <span class="folder-count">2</span>
        </div>
        <div v-if="expandedFolders.includes('backgrounds')" class="folder-content">
          <div 
            v-for="file in backgroundFiles" 
            :key="file.name"
            class="file-item"
            :class="{ selected: selectedFile === file.name }"
            @click="selectFile(file.name)"
          >
            <span class="file-icon">🖼️</span>
            <span>{{ file.name }}</span>
          </div>
        </div>

        <!-- Audio 文件夹 -->
        <div class="folder-item" @click="toggleFolder('audio')">
          <span class="folder-icon">{{ expandedFolders.includes('audio') ? '📂' : '📁' }}</span>
          <span class="folder-label">{{ t('folders.audio') || 'Audio' }}</span>
          <span class="folder-count">2</span>
        </div>
        <div v-if="expandedFolders.includes('audio')" class="folder-content">
          <div 
            v-for="file in audioFiles" 
            :key="file.name"
            class="file-item"
            :class="{ selected: selectedFile === file.name }"
            @click="selectFile(file.name)"
          >
            <span class="file-icon">🎵</span>
            <span>{{ file.name }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

const expandedFolders = ref<string[]>(['scenes'])
const selectedFile = ref<string | null>(null)

// 模拟文件数据（实际应从 API 或 store 获取）
const sceneFiles = [
  { name: 'Scene1.vn', type: 'scene' },
  { name: 'Scene2.vn', type: 'scene' },
]

const characterFiles = [
  { name: 'Character1.vc', type: 'character' },
  { name: 'Character2.vc', type: 'character' },
]

const backgroundFiles = [
  { name: 'BG1.png', type: 'image' },
  { name: 'BG2.png', type: 'image' },
]

const audioFiles = [
  { name: 'BGM1.mp3', type: 'bgm' },
  { name: 'SFX1.mp3', type: 'sfx' },
]

function toggleFolder(folder: string) {
  const index = expandedFolders.value.indexOf(folder)
  if (index >= 0) {
    expandedFolders.value.splice(index, 1)
  } else {
    expandedFolders.value.push(folder)
  }
}

function selectFile(name: string) {
  selectedFile.value = selectedFile.value === name ? null : name
}
</script>

<style scoped>
.project-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #252526;
}

.panel-header {
  padding: 10px 12px;
  border-bottom: 1px solid #3e3e42;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.panel-header h3 {
  margin: 0;
  font-size: 11px;
  font-weight: 600;
  color: #cccccc;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 4px;
}

.folder-tree {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.folder-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 5px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 12px;
  user-select: none;
  transition: background 0.1s;
}

.folder-item:hover {
  background: rgba(255, 255, 255, 0.08);
}

.folder-icon {
  font-size: 13px;
  width: 16px;
  text-align: center;
  flex-shrink: 0;
}

.folder-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.folder-count {
  font-size: 10px;
  color: #606060;
  background: #333333;
  padding: 1px 6px;
  border-radius: 8px;
}

.folder-content {
  padding-left: 20px;
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  color: #a0a0a0;
  cursor: pointer;
  border-radius: 3px;
  font-size: 11px;
  transition: all 0.1s;
}

.file-item:hover {
  background: rgba(255, 255, 255, 0.06);
  color: #cccccc;
}

.file-item.selected {
  background: #094771;
  color: #ffffff;
}

.file-icon {
  font-size: 12px;
  width: 16px;
  text-align: center;
  flex-shrink: 0;
}
</style>
