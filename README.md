# Visunovia - Visual Novel Editor

## 📖 项目介绍

Visunovia 是一款基于 Web 的视觉小说编辑器，采用现代化的技术栈，提供直观的节点图编辑器和实时预览功能。

### 核心特性

- 🎨 **节点图编辑器**：使用 BaklavaJS 构建的可视化节点编辑器
- 🌐 **多语言支持**：内置中英文国际化支持
- ⚡ **实时预览**：支持实时预览视觉小说内容
- 📦 **模块化架构**：基于 Vue 3 + Pinia 的现代化前端架构
- 🔧 **跨平台**：基于 ASP.NET Core 的后端服务

## 🛠 技术栈

### 前端技术
- **框架**：Vue 3.5+ (Composition API)
- **构建工具**：Vite 5.4+
- **节点图引擎**：BaklavaJS 2.8+
- **状态管理**：Pinia 2.3+
- **路由**：Vue Router 4.2+
- **国际化**：vue-i18n 9.14+
- **样式**：Tailwind CSS 3.4+
- **图标**：Lucide Vue Next

### 后端技术
- **框架**：ASP.NET Core 10.0
- **本地化**：自定义 PO 文件解析器
- **配置**：YAML 配置管理

## 📁 目录结构

```
Visunovia/
├── Controllers/              # API 控制器
├── Localizations/           # 本地化资源文件（.po）
├── Middleware/              # 自定义中间件
├── Models/                  # 数据模型
├── Pages/                   # Razor Pages
├── Properties/              # 项目属性
├── Services/                # 业务服务层
├── wwwroot/                 # 前端源码
│   ├── src/
│   │   ├── components/     # Vue 组件
│   │   │   ├── baklava-nodes/  # BaklavaJS 节点定义
│   │   │   ├── dialogs/    # 对话框组件
│   │   │   ├── layout/     # 布局组件
│   │   │   └── panels/     # 面板组件
│   │   ├── composables/    # Vue Composables
│   │   ├── i18n/          # 国际化配置
│   │   ├── pages/         # 页面组件
│   │   ├── router/        # 路由配置
│   │   ├── stores/        # Pinia 状态管理
│   │   └── types/         # TypeScript 类型定义
│   ├── css/               # 样式文件
│   ├── fonts/              # 字体文件
│   ├── js/                # JavaScript 文件
│   ├── lib/               # 第三方库
│   └── www_build/         # 前端构建产物
├── scripts/                # 构建脚本
├── Program.cs              # 程序入口
├── Visunovia.csproj        # 项目文件
└── Visunovia.sln           # 解决方案文件
```

## 🚀 快速开始

### 环境要求

- **Node.js**: 18.0+ 
- **.NET SDK**: 10.0+
- **操作系统**: Windows 10/11

### 安装依赖

```bash
# 安装前端依赖
cd wwwroot
npm install
```

### 开发模式

#### 方式 1：前端独立开发
```bash
cd wwwroot
npm run dev
```

#### 方式 2：完整开发（推荐）
```bash
# 终端 1：启动前端开发服务器
cd wwwroot
npm run dev

# 终端 2：启动后端服务
cd ..
dotnet run
```

服务器将在 `http://127.0.0.1:28478` 启动，并自动打开浏览器。

### 生产构建

```bash
# 1. 构建前端
cd wwwroot
npm run build

# 2. 编译后端（自动复制前端构建产物）
cd ..
dotnet build

# 3. 发布
dotnet publish -c Release
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

1. 在 `wwwroot/src/components/baklava-nodes/` 创建节点文件：
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

### 添加 API 接口

1. 创建控制器：
```csharp
// Controllers/NewController.cs
[ApiController]
[Route("api/[controller]")]
public class NewController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Hello" });
    }
}
```

2. 添加本地化文本（可选）：
```po
msgid "NewController.Message"
msgstr "你好"
```

## 📦 构建产物

### 目录结构

```
bin/
└── Debug/
    └── net10.0/
        ├── Visunovia.dll          # 主程序集
        ├── Visunovia.exe          # 可执行文件
        └── wwwroot/              # 前端构建产物
            ├── index.html
            ├── assets/
            │   ├── index-*.js
            │   ├── baklavajs-*.js
            │   └── vue-vendor-*.js
            ├── css/
            ├── js/
            └── ...
```

### 部署

发布后的 `wwwroot` 目录包含所有前端资源，无需额外配置即可部署。

## 🐛 调试

### 前端调试
- 启动开发服务器：`npm run dev`
- 打开浏览器 DevTools
- 使用 Vue DevTools 监控状态

### 后端调试
- 使用 Visual Studio 或 VS Code
- 设置断点
- 使用 `dotnet watch run` 启用热重载

## 📚 相关文档

- [技术架构文档](./Visunovia_UI_Modernization_Roadmap.md)
- [项目分析报告](./Visunovia_VN_Engine_Analysis_Report.docx)

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目采用专有许可证。
