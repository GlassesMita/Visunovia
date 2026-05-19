<template>
  <div class="project-panel">
    <div class="panel-header">
      <h3>{{ t('panels.project') }}</h3>
    </div>
    <div class="panel-content">
      <div class="folder-tree">
        <div class="folder-item" @click="toggleFolder('scenes')">
          <span class="folder-icon">{{ expandedFolders.includes('scenes') ? '📂' : '📁' }}</span>
          <span>Scenes</span>
        </div>
        <div v-if="expandedFolders.includes('scenes')" class="folder-content">
          <div class="file-item">Scene1.vn</div>
          <div class="file-item">Scene2.vn</div>
        </div>
        
        <div class="folder-item" @click="toggleFolder('characters')">
          <span class="folder-icon">{{ expandedFolders.includes('characters') ? '📂' : '📁' }}</span>
          <span>Characters</span>
        </div>
        <div v-if="expandedFolders.includes('characters')" class="folder-content">
          <div class="file-item">Character1.vc</div>
          <div class="file-item">Character2.vc</div>
        </div>
        
        <div class="folder-item" @click="toggleFolder('backgrounds')">
          <span class="folder-icon">{{ expandedFolders.includes('backgrounds') ? '📂' : '📁' }}</span>
          <span>Backgrounds</span>
        </div>
        <div v-if="expandedFolders.includes('backgrounds')" class="folder-content">
          <div class="file-item">BG1.png</div>
          <div class="file-item">BG2.png</div>
        </div>
        
        <div class="folder-item" @click="toggleFolder('audio')">
          <span class="folder-icon">{{ expandedFolders.includes('audio') ? '📂' : '📁' }}</span>
          <span>Audio</span>
        </div>
        <div v-if="expandedFolders.includes('audio')" class="folder-content">
          <div class="file-item">BGM1.mp3</div>
          <div class="file-item">SFX1.mp3</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

const expandedFolders = ref<string[]>([])

function toggleFolder(folder: string) {
  const index = expandedFolders.value.indexOf(folder)
  if (index >= 0) {
    expandedFolders.value.splice(index, 1)
  } else {
    expandedFolders.value.push(folder)
  }
}
</script>

<style scoped>
.project-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.panel-header {
  padding: 12px;
  border-bottom: 1px solid #3e3e42;
}

.panel-header h3 {
  margin: 0;
  font-size: 12px;
  font-weight: 600;
  color: #ffffff;
  text-transform: uppercase;
}

.panel-content {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.folder-item {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 13px;
}

.folder-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.folder-icon {
  font-size: 14px;
}

.folder-content {
  padding-left: 24px;
}

.file-item {
  padding: 4px 8px;
  color: #cccccc;
  cursor: pointer;
  border-radius: 4px;
  font-size: 12px;
}

.file-item:hover {
  background: rgba(255, 255, 255, 0.1);
}
</style>
