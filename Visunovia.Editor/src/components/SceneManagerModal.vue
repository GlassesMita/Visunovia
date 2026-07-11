<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { createScene, deleteScene, getCurrentProject, getProjectScenes, renameScene, type SceneListItem } from '@/api/projectApi'
import { useUIStore } from '@/stores/useUIStore'
import { useEditorStore } from '@/stores/useEditorStore'
import { useNodeGraphStore } from '@/stores/useNodeGraphStore'
import { useNodeOperations } from '@/composables/useNodeOperations'

const uiStore = useUIStore()
const editorStore = useEditorStore()
const nodeGraphStore = useNodeGraphStore()
const { loadSceneGraph } = useNodeOperations()

const scenes = ref<SceneListItem[]>([])
const isLoading = ref(false)
const error = ref('')
const newSceneName = ref('')
const renameTarget = ref<SceneListItem | null>(null)
const renameSceneName = ref('')

const currentSceneId = computed(() => nodeGraphStore.currentSceneId || '')
const canCreate = computed(() => newSceneName.value.trim().length > 0 && !isLoading.value)
const canRename = computed(() => renameTarget.value !== null && renameSceneName.value.trim().length > 0 && !isLoading.value)

watch(
  () => uiStore.showSceneManager,
  (visible) => {
    if (visible) {
      resetForm()
      loadScenes()
    }
  }
)

function resetForm() {
  error.value = ''
  newSceneName.value = ''
  renameTarget.value = null
  renameSceneName.value = ''
}

async function loadScenes() {
  isLoading.value = true
  error.value = ''
  try {
    const currentProject = await getCurrentProject()
    if (!currentProject.data?.projectPath) {
      scenes.value = []
      error.value = '当前没有打开的项目'
      return
    }

    scenes.value = await getProjectScenes(currentProject.data.projectPath)
  } catch (err) {
    error.value = err instanceof Error ? err.message : '读取场景列表失败'
  } finally {
    isLoading.value = false
  }
}

function validateSceneName(value: string): string {
  const name = value.trim()
  if (!name) return ''
  return name.replace(/\.lor$/i, '')
}

async function saveCurrentSceneIfNeeded() {
  if (nodeGraphStore.currentSceneId) {
    await editorStore.save()
    if (editorStore.error) {
      throw new Error(editorStore.error)
    }
  }
}

async function handleCreateScene() {
  const sceneId = validateSceneName(newSceneName.value)
  if (!sceneId) return

  isLoading.value = true
  error.value = ''
  try {
    await saveCurrentSceneIfNeeded()
    await createScene(sceneId)
    await loadScenes()
    await loadSceneGraph(sceneId)
    newSceneName.value = ''
    uiStore.refreshProjectTree()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '新建场景失败'
  } finally {
    isLoading.value = false
  }
}

function startRename(scene: SceneListItem) {
  renameTarget.value = scene
  renameSceneName.value = scene.id
  error.value = ''
}

function cancelRename() {
  renameTarget.value = null
  renameSceneName.value = ''
}

async function handleRenameScene() {
  if (!renameTarget.value) return

  const oldSceneId = renameTarget.value.id
  const newSceneId = validateSceneName(renameSceneName.value)
  if (!newSceneId) return

  isLoading.value = true
  error.value = ''
  try {
    if (currentSceneId.value === oldSceneId) {
      await saveCurrentSceneIfNeeded()
    }

    await renameScene(oldSceneId, newSceneId)
    await loadScenes()
    if (currentSceneId.value === oldSceneId) {
      await loadSceneGraph(newSceneId)
    }
    cancelRename()
    uiStore.refreshProjectTree()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '重命名场景失败'
  } finally {
    isLoading.value = false
  }
}

async function handleDeleteScene(scene: SceneListItem) {
  if (!window.confirm(`确定删除场景“${scene.id}”吗？该操作会删除对应 .lor 文件。`)) return

  isLoading.value = true
  error.value = ''
  try {
    const deletingCurrent = currentSceneId.value === scene.id
    if (deletingCurrent) {
      await saveCurrentSceneIfNeeded()
    }

    await deleteScene(scene.id)
    await loadScenes()
    if (deletingCurrent) {
      const nextScene = scenes.value[0]
      if (nextScene) {
        await loadSceneGraph(nextScene.id)
      }
    }
    uiStore.refreshProjectTree()
  } catch (err) {
    error.value = err instanceof Error ? err.message : '删除场景失败'
  } finally {
    isLoading.value = false
  }
}

async function openScene(scene: SceneListItem) {
  if (scene.id === currentSceneId.value) return

  isLoading.value = true
  error.value = ''
  try {
    await saveCurrentSceneIfNeeded()
    await loadSceneGraph(scene.id)
  } catch (err) {
    error.value = err instanceof Error ? err.message : '打开场景失败'
  } finally {
    isLoading.value = false
  }
}

function close() {
  uiStore.closeSceneManager()
}
</script>

<template>
  <Teleport to="body">
    <Transition name="scene-modal-fade">
      <div v-if="uiStore.showSceneManager" class="scene-overlay" @click.self="close">
        <section class="scene-manager" role="dialog" aria-modal="true" aria-labelledby="scene-manager-title">
          <header class="scene-header">
            <div>
              <h3 id="scene-manager-title">场景管理</h3>
              <p>新建、重命名或删除当前项目中的场景文件。</p>
            </div>
            <button class="icon-button" type="button" @click="close">✕</button>
          </header>

          <div class="scene-create-row">
            <input
              v-model="newSceneName"
              type="text"
              placeholder="新场景名称，例如 chapter_01"
              :disabled="isLoading"
              @keyup.enter="handleCreateScene"
            />
            <button type="button" class="primary" :disabled="!canCreate" @click="handleCreateScene">新建场景</button>
          </div>

          <p v-if="error" class="scene-error">{{ error }}</p>

          <div class="scene-list">
            <div v-if="isLoading && scenes.length === 0" class="scene-empty">正在加载场景...</div>
            <div v-else-if="scenes.length === 0" class="scene-empty">暂无场景</div>
            <div
              v-for="scene in scenes"
              v-else
              :key="scene.id"
              class="scene-item"
              :class="{ active: scene.id === currentSceneId }"
            >
              <template v-if="renameTarget?.id === scene.id">
                <input
                  v-model="renameSceneName"
                  class="rename-input"
                  type="text"
                  :disabled="isLoading"
                  @keyup.enter="handleRenameScene"
                  @keyup.esc="cancelRename"
                />
                <button type="button" class="primary" :disabled="!canRename" @click="handleRenameScene">保存</button>
                <button type="button" @click="cancelRename">取消</button>
              </template>
              <template v-else>
                <button type="button" class="scene-open" :title="scene.lorFilePath" @click="openScene(scene)">
                  <span class="scene-name">{{ scene.id }}</span>
                  <span v-if="scene.id === currentSceneId" class="scene-current">当前</span>
                </button>
                <button type="button" :disabled="isLoading" @click="startRename(scene)">重命名</button>
                <button type="button" class="danger" :disabled="isLoading || scenes.length <= 1" @click="handleDeleteScene(scene)">删除</button>
              </template>
            </div>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.scene-overlay {
  position: fixed;
  inset: 0;
  z-index: 10020;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.58);
  backdrop-filter: blur(6px);
}

.scene-manager {
  width: 680px;
  max-width: calc(100vw - 40px);
  max-height: calc(100vh - 80px);
  display: flex;
  flex-direction: column;
  background: #1f1f2d;
  border: 1px solid #3a3a48;
  border-radius: 12px;
  color: #e6e6e6;
  box-shadow: 0 24px 70px rgba(0, 0, 0, 0.55);
  overflow: hidden;
}

.scene-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 18px 20px;
  background: #181824;
  border-bottom: 1px solid #333344;
}

.scene-header h3 {
  margin: 0;
  font-size: 17px;
}

.scene-header p {
  margin: 4px 0 0;
  color: #9ca3af;
  font-size: 12px;
}

.icon-button {
  width: 30px;
  height: 30px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: #cfcfcf;
  cursor: pointer;
}

.icon-button:hover {
  background: rgba(255, 255, 255, 0.1);
}

.scene-create-row {
  display: flex;
  gap: 10px;
  padding: 16px 20px 10px;
}

.scene-create-row input,
.rename-input {
  flex: 1;
  height: 34px;
  border: 1px solid #3d3d4d;
  border-radius: 7px;
  background: #151520;
  color: #f2f2f2;
  padding: 0 11px;
}

.scene-error {
  margin: 0 20px 10px;
  color: #ff9b9b;
  font-size: 12px;
}

.scene-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px 20px 20px;
  overflow: auto;
}

.scene-empty {
  padding: 30px 0;
  text-align: center;
  color: #9ca3af;
}

.scene-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  border: 1px solid #343445;
  border-radius: 9px;
  background: #262638;
}

.scene-item.active {
  border-color: #3b82f6;
  background: rgba(59, 130, 246, 0.12);
}

.scene-open {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-width: 0;
  border: none;
  background: transparent;
  color: #eeeeee;
  cursor: pointer;
  text-align: left;
}

.scene-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.scene-current {
  margin-left: 10px;
  color: #60a5fa;
  font-size: 12px;
}

button {
  height: 32px;
  padding: 0 12px;
  border: 1px solid #44445a;
  border-radius: 7px;
  background: #2d2d3f;
  color: #eeeeee;
  cursor: pointer;
}

button:hover:not(:disabled) {
  background: #393950;
}

button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

button.primary {
  border-color: #2563eb;
  background: #2563eb;
}

button.primary:hover:not(:disabled) {
  background: #1d4ed8;
}

button.danger {
  border-color: #7f1d1d;
  color: #fecaca;
}

button.danger:hover:not(:disabled) {
  background: #7f1d1d;
}

.scene-modal-fade-enter-active,
.scene-modal-fade-leave-active {
  transition: opacity 0.16s ease;
}

.scene-modal-fade-enter-from,
.scene-modal-fade-leave-to {
  opacity: 0;
}
</style>
