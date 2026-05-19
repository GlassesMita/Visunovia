<template>
  <div class="toolbar">
    <div class="toolbar-group">
      <button 
        class="toolbar-button" 
        :title="t('menu.new')"
        @click="handleNew"
      >
        <FilePlus :size="20" />
      </button>
      <button 
        class="toolbar-button" 
        :title="t('menu.open')"
        @click="handleOpen"
      >
        <FolderOpen :size="20" />
      </button>
      <button 
        class="toolbar-button" 
        :title="t('menu.save')"
        @click="handleSave"
      >
        <Save :size="20" />
      </button>
    </div>
    
    <div class="toolbar-divider"></div>
    
    <div class="toolbar-group">
      <button 
        class="toolbar-button" 
        :title="t('menu.undo')"
        @click="handleUndo"
      >
        <Undo :size="20" />
      </button>
      <button 
        class="toolbar-button" 
        :title="t('menu.redo')"
        @click="handleRedo"
      >
        <Redo :size="20" />
      </button>
    </div>
    
    <div class="toolbar-divider"></div>
    
    <div class="toolbar-group">
      <button 
        class="toolbar-button" 
        :title="t('menu.cut')"
        @click="handleCut"
      >
        <Scissors :size="20" />
      </button>
      <button 
        class="toolbar-button" 
        :title="t('menu.copy')"
        @click="handleCopy"
      >
        <Copy :size="20" />
      </button>
      <button 
        class="toolbar-button" 
        :title="t('menu.paste')"
        @click="handlePaste"
      >
        <Clipboard :size="20" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { FilePlus, FolderOpen, Save, Undo, Redo, Scissors, Copy, Clipboard } from 'lucide-vue-next'
import { useLocalization } from '@/composables/useLocalization'

const { t } = useLocalization()

function handleNew() {
  console.log('New file')
}

function handleOpen() {
  console.log('Open file')
}

function handleSave() {
  console.log('Save file')
}

function handleUndo() {
  console.log('Undo')
}

function handleRedo() {
  console.log('Redo')
}

function handleCut() {
  console.log('Cut')
}

function handleCopy() {
  console.log('Copy')
}

function handlePaste() {
  console.log('Paste')
}
</script>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  height: 100%;
  padding: 0 12px;
  gap: 8px;
}

.toolbar-group {
  display: flex;
  gap: 4px;
}

.toolbar-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  background: transparent;
  border: none;
  border-radius: 4px;
  color: #cccccc;
  cursor: pointer;
}

.toolbar-button:hover {
  background: rgba(255, 255, 255, 0.1);
}

.toolbar-button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.toolbar-divider {
  width: 1px;
  height: 24px;
  background: #3e3e42;
  margin: 0 8px;
}
</style>
