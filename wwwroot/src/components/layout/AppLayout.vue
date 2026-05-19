<template>
  <div class="app-layout">
    <!-- 顶部菜单栏 -->
    <header class="app-header">
      <MenuBar />
    </header>
    
    <!-- 工具栏 -->
    <div class="app-toolbar">
      <Toolbar />
    </div>
    
    <!-- 主内容区 -->
    <div class="app-content">
      <!-- 左侧面板 -->
      <aside class="app-left-panel" :class="{ collapsed: leftPanelCollapsed }">
        <ProjectPanel v-if="!leftPanelCollapsed" />
      </aside>
      
      <!-- 编辑器区域 -->
      <main class="app-editor">
        <BaklavaEditor />
      </main>
      
      <!-- 右侧面板 -->
      <aside class="app-right-panel" :class="{ collapsed: rightPanelCollapsed }">
        <div class="panel-tabs">
          <button 
            :class="{ active: activeRightTab === 'inspector' }"
            @click="activeRightTab = 'inspector'"
          >
            {{ t('panels.inspector') }}
          </button>
          <button 
            :class="{ active: activeRightTab === 'hierarchy' }"
            @click="activeRightTab = 'hierarchy'"
          >
            {{ t('panels.hierarchy') }}
          </button>
        </div>
        <InspectorPanel v-if="activeRightTab === 'inspector'" />
        <HierarchyPanel v-else-if="activeRightTab === 'hierarchy'" />
      </aside>
    </div>
    
    <!-- 底部状态栏 -->
    <footer class="app-footer">
      <StatusBar />
    </footer>
    
    <!-- 控制台面板（可折叠） -->
    <div class="app-console" :class="{ expanded: consoleExpanded }">
      <ConsolePanel />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useLocalization } from '@/composables/useLocalization'
import MenuBar from './MenuBar.vue'
import Toolbar from './Toolbar.vue'
import StatusBar from './StatusBar.vue'
import ProjectPanel from '@/components/panels/ProjectPanel.vue'
import InspectorPanel from '@/components/panels/InspectorPanel.vue'
import HierarchyPanel from '@/components/panels/HierarchyPanel.vue'
import ConsolePanel from '@/components/panels/ConsolePanel.vue'
import BaklavaEditor from '@/components/BaklavaEditor.vue'

const { t } = useLocalization()

const leftPanelCollapsed = ref(false)
const rightPanelCollapsed = ref(false)
const activeRightTab = ref<'inspector' | 'hierarchy'>('inspector')
const consoleExpanded = ref(false)
</script>

<style scoped>
.app-layout {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100vh;
  overflow: hidden;
}

.app-header {
  height: 40px;
  background: #252526;
  border-bottom: 1px solid #3e3e42;
}

.app-toolbar {
  height: 48px;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
}

.app-content {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.app-left-panel,
.app-right-panel {
  width: 280px;
  background: #252526;
  border-right: 1px solid #3e3e42;
  overflow: hidden;
  transition: width 0.2s;
}

.app-right-panel {
  border-right: none;
  border-left: 1px solid #3e3e42;
}

.app-left-panel.collapsed,
.app-right-panel.collapsed {
  width: 40px;
}

.app-editor {
  flex: 1;
  background: #1e1e1e;
  overflow: hidden;
}

.app-footer {
  height: 24px;
  background: #007acc;
}

.app-console {
  height: 0;
  background: #1e1e1e;
  overflow: hidden;
  transition: height 0.2s;
}

.app-console.expanded {
  height: 200px;
}

.panel-tabs {
  display: flex;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
}

.panel-tabs button {
  flex: 1;
  padding: 8px;
  background: transparent;
  border: none;
  color: #cccccc;
  cursor: pointer;
  font-size: 12px;
}

.panel-tabs button.active {
  background: #1e1e1e;
  color: #ffffff;
}
</style>
