# Visunovia - Visual Novel Editor

Visunovia 是 Electron 桌面视觉小说工具集。编辑器与播放器使用独立的 Vue + TypeScript 项目，通过同一个 Visual Studio 解决方案管理；项目不依赖 ASP.NET Core 或 .NET SDK。

## 技术栈

- Electron 43：桌面进程、窗口与安全 IPC。
- Node.js：项目、资源、场景图和本地化文件操作。
- Vue 3、Vite、Pinia、BaklavaJS：编辑器与节点图。
- ACE：自定义事件脚本的 C# 语法高亮编辑器，使用 `Cascadia Code`，并回退到 `Microsoft YaHei UI`、`Noto Sans CJK SC`。

## 目录

```
Visunovia/
├── apps/editor-desktop/      # Electron 主进程、预加载与 Node 后端
├── apps/player-desktop/      # Electron 播放器
├── Visunovia.Editor/         # Vue/TS 编辑器项目（Visunovia.Editor.esproj）
├── Visunovia.Player/         # Vue/TS 播放器项目（Visunovia.Player.esproj）
├── Localizations/            # PO 本地化资源
├── tests/                    # Node 与 Electron 回归测试
└── www_build/                # 前端构建输出
```

项目中的自定义事件脚本会保存为 `Assets/CustomScripts/<node-id>.csx`。场景图只保存 `scriptRef`，读取时 Electron 后端回填源代码给 ACE 编辑器。

## 开发

需要 Node.js 18+ 与 Windows/macOS/Linux 支持的 Electron 环境。

```bash
npm install
npm --prefix Visunovia.Editor install
npm run dev
```

播放器开发时分别启动播放器前端与 Electron Host：

```bash
npm run player:client:dev
npm run player:desktop:dev
```

## 打包与测试

```bash
npm run desktop:pack:local
npm run player:desktop:pack:local
node --test tests/editor-backend.test.cjs
npx playwright test tests/editor-electron.spec.ts --project=chromium
```

## 🎯 项目架构

### 前端架构

#### 节点系统
采用"大类型节点 + 子类型"的设计理念：

```
节点类型 (6种)
├── StartNode        # 开始节点
├── EndNode          # 结束节点
├── EventNode        # 事件节点（9种子类型）
│   ├── PlayBGM          # 播放背景音乐
│   ├── StopBGM          # 停止背景音乐
│   ├── PlaySFX          # 播放音效
│   ├── PlayVoice        # 播放语音
│   ├── ChangeBackground # 切换背景
│   ├── ShowCharacter    # 显示角色
│   ├── HideCharacter    # 隐藏角色
│   ├── CameraShake      # 镜头震动
│   └── FadeScreen       # 屏幕淡入淡出
├── DialogueNode     # 对话节点
├── BranchNode       # 分支节点
└── LogicNode       # 逻辑节点（3种子类型）
    ├── SetVariable      # 设置变量
    ├── Conditional      # 条件判断
    └── Delay            # 延迟
```

#### 状态管理
使用 Pinia 进行状态管理，主要 Store：

- `useEditorStore` - 编辑器状态（选中节点、项目信息）
- `useNodeGraphStore` - 节点图状态（节点、连接）
- `useUIStore` - UI 状态（面板显示/隐藏）
- `useLocalizationStore` - 国际化状态
- `useUndoRedoStore` - 撤销/重做历史

#### 组件结构
```
AppLayout
├── MenuBar           # 菜单栏
├── Toolbar           # 工具栏
├── ProjectPanel      # 项目面板
├── BaklavaEditor     # 节点图编辑器
├── InspectorPanel    # 属性检查器（支持动态属性）
├── HierarchyPanel    # 层级面板
├── ConsolePanel      # 控制台面板
└── StatusBar         # 状态栏
```

### 后端架构

#### API 控制器
- `EditorController` - 编辑器操作
- `FileBrowserController` - 文件浏览
- `LocalizationController` - 本地化管理
- `ProjectController` - 项目管理
- `ResourceController` - 资源管理
- `SceneGraphController` - 场景图管理
- `SettingsController` - 设置管理
- `SystemController` - 系统信息

#### 本地化系统
支持 `.po` 格式的本地化文件，自动解析并提供多语言支持。

## 🔧 开发指南

### 添加新节点

1. 在 `Visunovia.Editor/src/components/baklava-nodes/` 创建节点文件：
```typescript
// EventNode.ts
import { defineNode, ExecInterface } from 'baklavajs'

export default defineNode({
  type: 'EventNode',
  title: 'Event',
  inputs: {
    execIn: () => new ExecInterface('exec_in'),
  },
  outputs: {
    execOut: () => new ExecInterface('exec_out'),
  },
})
```

2. 在 `nodeRegistry.ts` 中注册节点：
```typescript
import EventNode from '@/components/baklava-nodes/EventNode'

export function registerAllNodes(editor) {
  editor.registerNodeType(EventNode)
}
```

3. 添加国际化文本：
```typescript
// zh-CN.po
msgid "nodes.event"
msgstr "事件"
```

### 添加新页面

1. 创建 Vue 组件：
```vue
<!-- src/pages/NewPage.vue -->
<template>
  <div class="new-page">
    <h1>New Page</h1>
  </div>
</template>

<script setup lang="ts">
</script>
```

2. 添加路由：
```typescript
// router/index.ts
const routes = [
  { path: '/new-page', component: NewPage },
]
```

### 添加本地化文本

在 `Localizations` 中添加 PO 条目：
```po
msgid "nodes.customEvent"
msgstr "自定义事件"
```

## 📦 构建产物

`npm run desktop:pack:local` 在 `dist-desktop/win-unpacked` 生成可运行的 Electron 编辑器。

## 🐛 调试

- 使用 `npm run dev` 启动 Vite 与 Electron。
- 从 Electron DevTools 检查渲染器，使用终端输出检查 Node 后端。

## 📚 相关文档

- [技术架构文档](./Visunovia_UI_Modernization_Roadmap.md)
- [项目分析报告](./Visunovia_VN_Engine_Analysis_Report.docx)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本仓库采用分组件授权：

- **Visunovia 编辑器及开发工具**：采用 [GNU GPL version 3 only](./LICENSE)。分发修改版本时须遵守 GPLv3 的源代码开放要求。
- **面向最终用户分发的独立播放器**：其第一方播放器组件采用 [BSD 3-Clause License](./LICENSE-PLAYER)，允许商业使用、闭源分发和二次修改，但未经书面许可不得使用“Visunovia”或贡献者名称为衍生产品背书或宣传。
- 第三方组件仍分别适用其自身许可证。

独立播放器许可证不适用于编辑器、编辑器专用源代码或开发工具。
