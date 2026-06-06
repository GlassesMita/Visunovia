<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useUIStore } from '@/stores/useUIStore'
import { useLocalization } from '@/composables/useLocalization'
import { getRecentProjects, type RecentProject } from '@/api/systemApi'

const { t } = useLocalization()
const uiStore = useUIStore()

const recentProjects = ref<RecentProject[]>([])
const isLoadingRecents = ref(false)

onMounted(async () => {
  isLoadingRecents.value = true
  try {
    recentProjects.value = await getRecentProjects()
  } catch {
    recentProjects.value = []
  } finally {
    isLoadingRecents.value = false
  }
})

function handleNewProject() {
  uiStore.closeWelcomeModal()
  uiStore.openNewProjectModal()
}

function handleOpenProject() {
  uiStore.closeWelcomeModal()
  uiStore.openFileExplorer()
}

function handleOpenRecent(projectPath: string) {
  uiStore.closeWelcomeModal()
  // TODO: load project by path — for now open file explorer
  uiStore.openFileExplorer()
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    // Do not allow closing the welcome modal without action
  }
}
</script>

<template>
  <Teleport to="body">
    <div class="wm-overlay" @keydown="handleKeydown" tabindex="-1">
      <div class="wm-window">
        <!-- Left: Actions -->
        <div class="wm-left">
          <div class="wm-logo">
            <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
              <rect x="4" y="8" width="40" height="32" rx="4" fill="#1a1a2e" stroke="#3B82F6" stroke-width="2"/>
              <path d="M14 20L20 26L34 12" stroke="#3B82F6" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            <span class="wm-app-name">Visunovia</span>
          </div>

          <div class="wm-actions">
            <button class="wm-action-btn wm-btn-primary" @click="handleNewProject">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/>
                <polyline points="14 2 14 8 20 8"/>
                <line x1="12" y1="18" x2="12" y2="12"/>
                <line x1="9" y1="15" x2="15" y2="15"/>
              </svg>
              <span>{{ t('Startup.NewProject') }}</span>
            </button>

            <button class="wm-action-btn wm-btn-secondary" @click="handleOpenProject">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
                <line x1="12" y1="11" x2="12" y2="17"/>
                <line x1="9" y1="14" x2="15" y2="14"/>
              </svg>
              <span>{{ t('Startup.OpenProject') }}</span>
            </button>
          </div>
        </div>

        <!-- Divider -->
        <div class="wm-divider"></div>

        <!-- Right: Recent Projects -->
        <div class="wm-right">
          <h3 class="wm-recent-title">{{ t('Startup.RecentProjects') }}</h3>

          <div v-if="isLoadingRecents" class="wm-loading">
            <div class="wm-spinner"></div>
            <span>{{ t('Startup.Loading') }}</span>
          </div>

          <div v-else-if="recentProjects.length === 0" class="wm-empty">
            <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" style="opacity: 0.3;">
              <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
            </svg>
            <span>{{ t('Startup.NoRecentProjects') }}</span>
          </div>

          <div v-else class="wm-recent-list">
            <button
              v-for="project in recentProjects"
              :key="project.path"
              class="wm-recent-item"
              @click="handleOpenRecent(project.path)"
            >
              <div class="wm-recent-icon">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/>
                </svg>
              </div>
              <div class="wm-recent-info">
                <span class="wm-recent-name">{{ project.name }}</span>
                <span class="wm-recent-path">{{ project.path }}</span>
              </div>
              <span v-if="project.lastOpened" class="wm-recent-date">
                {{ new Date(project.lastOpened).toLocaleDateString() }}
              </span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
/* ========== Overlay ========== */
.wm-overlay {
  position: fixed;
  inset: 0;
  z-index: 10000;
  background: rgba(0, 0, 0, 0.65);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
}

/* ========== Window ========== */
.wm-window {
  display: flex;
  width: 720px;
  min-height: 400px;
  background: #1e1e2e;
  border: 1px solid #333344;
  border-radius: 12px;
  box-shadow: 0 24px 64px rgba(0, 0, 0, 0.5);
  overflow: hidden;
}

/* ========== Left Panel ========== */
.wm-left {
  width: 240px;
  min-width: 240px;
  background: #181828;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 40px 24px 32px;
  gap: 32px;
}

.wm-logo {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.wm-app-name {
  font-size: 18px;
  font-weight: 700;
  color: #e0e0e0;
  letter-spacing: 0.5px;
}

.wm-actions {
  display: flex;
  flex-direction: column;
  gap: 10px;
  width: 100%;
}

.wm-action-btn {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  height: 44px;
  padding: 0 16px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-family: inherit;
  cursor: pointer;
  transition: all 0.15s;
}

.wm-action-btn span {
  flex: 1;
  text-align: left;
}

.wm-btn-primary {
  background: #3B82F6;
  color: #ffffff;
}

.wm-btn-primary:hover {
  background: #2563eb;
}

.wm-btn-primary:active {
  background: #1d4ed8;
}

.wm-btn-secondary {
  background: transparent;
  color: #b0b0c0;
  border: 1px solid #333344;
}

.wm-btn-secondary:hover {
  background: rgba(255, 255, 255, 0.05);
  border-color: #444455;
  color: #d0d0e0;
}

/* ========== Divider ========== */
.wm-divider {
  width: 1px;
  background: #333344;
  margin: 24px 0;
}

/* ========== Right Panel ========== */
.wm-right {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 32px 28px 24px;
  min-width: 0;
}

.wm-recent-title {
  font-size: 15px;
  font-weight: 600;
  color: #c0c0d0;
  margin: 0 0 16px 0;
}

.wm-loading {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #888;
  font-size: 13px;
  padding: 20px 0;
}

.wm-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.15);
  border-top-color: #3B82F6;
  border-radius: 50%;
  animation: wm-spin 0.6s linear infinite;
}

@keyframes wm-spin {
  to { transform: rotate(360deg); }
}

.wm-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  color: #666;
  font-size: 13px;
  padding: 40px 0;
}

.wm-recent-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow-y: auto;
  max-height: 300px;
}

.wm-recent-list::-webkit-scrollbar {
  width: 5px;
}

.wm-recent-list::-webkit-scrollbar-track {
  background: transparent;
}

.wm-recent-list::-webkit-scrollbar-thumb {
  background: #444;
  border-radius: 3px;
}

.wm-recent-item {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 10px 12px;
  background: transparent;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.12s;
  text-align: left;
  font-family: inherit;
}

.wm-recent-item:hover {
  background: rgba(255, 255, 255, 0.05);
}

.wm-recent-icon {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(59, 130, 246, 0.12);
  border-radius: 6px;
  color: #3B82F6;
}

.wm-recent-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.wm-recent-name {
  font-size: 13px;
  color: #d0d0e0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.wm-recent-path {
  font-size: 11px;
  color: #777;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.wm-recent-date {
  flex-shrink: 0;
  font-size: 11px;
  color: #666;
}
</style>
