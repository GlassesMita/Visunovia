# Visunovia UI 现代化改造路线图 (Alpha 版)

> 目标：将当前基于原生 JavaScript + jQuery 的编辑器 UI 改造为类似 Unity 的现代化引擎界面，并迁移到蓝图节点系统。
> 
> **注意：本路线图为 Alpha 内部版本设计，不处理旧版本数据格式兼容，仅提供单向数据转换。**

---

## 📋 目录

1. [当前架构分析](#一当前架构分析)
2. [目标架构设计](#二目标架构设计)
3. [数据结构改造](#三数据结构改造)
4. [分阶段实施计划](#四分阶段实施计划)
5. [技术选型建议](#五技术选型建议)

---

## 一、当前架构分析

### 1.1 前端技术栈现状

| 组件 | 当前实现 | 问题 |
|------|----------|------|
| 框架 | 原生 JavaScript + jQuery | 无组件化，维护困难 |
| 状态管理 | 全局 `app.state` 对象 | 手动同步，易出 bug |
| UI 组件 | 手动 DOM 操作 | 代码重复，难以复用 |
| 样式 | 内联样式 + 传统 CSS | 难以维护主题 |
| 构建工具 | 无 | 无模块化管理 |
| 类型系统 | 无 | 运行时错误风险高 |

### 1.2 当前 UI 布局

```
┌─────────────────────────────────────────────────────────────┐
│  Toolbar (撤销/重做/新建/打开/保存/预览/设置/退出)            │
├──────────┬──────────────────────────────────────┬───────────┤
│          │  Scene Tabs (场景标签页)               │           │
│ Resource │  ┌────────────────────────────────┐  │ Property  │
│ Panel    │  │  Dialogue Toolbar              │  │ Panel     │
│ (左侧)   │  │  (添加对话/分支/事件/复制/删除)  │  │ (右侧)    │
│          │  ├────────────────────────────────┤  │           │
│          │  │                                │  │           │
│          │  │  Dialogue List                 │  │           │
│          │  │  (对话卡片列表)                 │  │           │
│          │  │                                │  │           │
│          │  └────────────────────────────────┘  │           │
├──────────┴──────────────────────────────────────┴───────────┤
│  Status Bar (状态栏)                                         │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、目标架构设计

### 2.1 目标技术栈

| 层级 | 技术选型 | 理由 |
|------|----------|------|
| 框架 | React 18 + TypeScript | 组件化、类型安全、生态丰富 |
| 状态管理 | Zustand | 轻量、TypeScript 友好 |
| UI 组件库 | shadcn/ui + Radix UI | 可定制、无障碍支持好 |
| 样式 | Tailwind CSS | 原子化、主题系统完善 |
| 构建工具 | Vite | 快速、现代、HMR 支持好 |
| 节点编辑器 | React Flow | 成熟的节点图库 |
| 图标 | Lucide React | 现代化图标库 |

### 2.2 目标 UI 布局（Unity 风格）

```
┌──────────────────────────────────────────────────────────────────────────────┐
│  Menu Bar (文件/编辑/资源/工具/窗口/帮助)                                     │
├─────────────┬─────────────────────────────┬──────────────────┬───────────────┤
│             │                             │                  │               │
│  Hierarchy  │      Node Graph Editor      │   Inspector    │   Project     │
│  (场景层级)  │      (蓝图节点编辑器)         │   (属性面板)    │   (资源浏览器) │
│             │                             │                  │               │
│  - Scene 1  │  ┌─────────────────────┐    │  [Node Props]  │  Assets/      │
│    - Node 1 │  │  [Start] ──► [Dialog]│    │  - Position    │  - Sprites    │
│    - Node 2 │  │       │              │    │  - Properties  │  - Backgrounds│
│  - Scene 2  │  │       ▼              │    │  - Connections │  - Audio      │
│             │  │  [Branch] ──► [End]  │    │                │               │
│             │  └─────────────────────┘    │                │               │
│             │                             │                │               │
├─────────────┴─────────────────────────────┴────────────────┴───────────────┤
│  Toolbar (播放/暂停/逐帧) │  Console (日志输出) │  Timeline (时间轴预览)      │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 三、数据结构改造

### 3.1 当前数据结构（列表式）

```yaml
# 当前 YAML 结构 (.lor 文件)
scene:
  id: "Scene_1"
  background: "bg_school.png"
  bgm:
    path: "bgm_calm.mp3"
    volume: 80
    loop: true
  dialogues:
    - type: Dialogue
      speaker: "Alice"
      text: "Hello!"
      sprites:
        - path: "alice_happy.png"
          position: "center"
      voice: "alice_01.mp3"
    - type: Branch
      branch:
        choices:
          - text: "Say hi"
            targetScene: "Scene_2"
            condition: "var_friendship > 5"
```

### 3.2 目标数据结构（蓝图节点式）

```yaml
# 目标 YAML 结构 (.lor 文件) - 节点图格式
scene_graph:
  id: "Scene_1"
  viewport:
    x: 0
    y: 0
    zoom: 1.0
  
  nodes:
    - id: "node_start"
      type: "Start"
      position: { x: 100, y: 200 }
      outputs:
        - id: "out_exec"
          label: "Execute"
          target: "node_dialog_1"
    
    - id: "node_dialog_1"
      type: "Dialogue"
      position: { x: 300, y: 200 }
      properties:
        speaker: "Alice"
        text: "Hello!"
        voice: "alice_01.mp3"
        textEffect:
          type: "Typewriter"
          speed: 50
      inputs:
        - id: "in_exec"
          label: "Execute"
      outputs:
        - id: "out_exec"
          label: "Then"
          target: "node_branch_1"
      sprites:
        - path: "alice_happy.png"
          position: { x: 0.5, y: 0.8 }
          layer: 1
          transition: "FadeIn"
    
    - id: "node_branch_1"
      type: "Branch"
      position: { x: 500, y: 200 }
      properties:
        prompt: "What will you do?"
      inputs:
        - id: "in_exec"
          label: "Execute"
      outputs:
        - id: "out_choice_1"
          label: "Say hi"
          target: "node_change_bg"
          condition: "friendship > 5"
        - id: "out_choice_2"
          label: "Ignore"
          target: "node_end"
    
    - id: "node_change_bg"
      type: "ChangeBackground"
      position: { x: 700, y: 150 }
      properties:
        path: "bg_night.png"
        transition: "CrossFade"
        duration: 500
      inputs:
        - id: "in_exec"
          label: "Execute"
      outputs:
        - id: "out_exec"
          label: "Then"
          target: "node_end"
    
    - id: "node_end"
      type: "End"
      position: { x: 900, y: 200 }
      inputs:
        - id: "in_exec"
          label: "Execute"

  scene_config:
    background: "bg_school.png"
    bgm:
      path: "bgm_calm.mp3"
      volume: 80
      loop: true
```

### 3.3 节点类型定义

```typescript
// 节点类型枚举
enum VNNodeType {
  // 流程控制
  Start = 'Start',
  End = 'End',
  Sequence = 'Sequence',
  
  // 对话相关
  Dialogue = 'Dialogue',
  Branch = 'Branch',
  
  // 视觉
  ChangeBackground = 'ChangeBackground',
  ShowCharacter = 'ShowCharacter',
  HideCharacter = 'HideCharacter',
  CameraShake = 'CameraShake',
  FadeScreen = 'FadeScreen',
  
  // 音频
  PlayBGM = 'PlayBGM',
  StopBGM = 'StopBGM',
  PlaySFX = 'PlaySFX',
  PlayVoice = 'PlayVoice',
  
  // 逻辑
  SetVariable = 'SetVariable',
  Conditional = 'Conditional',
  Delay = 'Delay',
  
  // 自定义
  CustomEvent = 'CustomEvent',
  
  // 子图
  SubGraph = 'SubGraph',
}

interface VNNode {
  id: string;
  type: VNNodeType;
  position: { x: number; y: number };
  properties: Record<string, any>;
  inputs: VNNodePort[];
  outputs: VNNodePort[];
}

interface VNNodePort {
  id: string;
  label: string;
  type: 'exec' | 'data';
  dataType?: 'string' | 'number' | 'boolean' | 'any';
  target?: string;
  targetPort?: string;
}
```

### 3.4 XML 项目文件改造

```xml
<!-- 目标 XML 结构 -->
<Project>
  <Metadata>
    <Title>My VN</Title>
    <Author>Author</Author>
    <Version>1.0</Version>
  </Metadata>
  
  <Variables>
    <Variable name="friendship" type="number" default="0" />
    <Variable name="playerName" type="string" default="Player" />
    <Variable name="hasKey" type="boolean" default="false" />
  </Variables>
  
  <Characters>
    <Character id="alice" name="Alice" defaultExpression="happy">
      <Expression name="happy" sprite="alice_happy.png" />
      <Expression name="sad" sprite="alice_sad.png" />
    </Character>
  </Characters>
  
  <SceneGraphs>
    <SceneGraph id="Scene_1" entry="true" />
    <SceneGraph id="Scene_2" />
  </SceneGraphs>
  
  <EditorState>
    <OpenSceneGraphs>
      <SceneGraph id="Scene_1" viewport="0,0,1.0" />
    </OpenSceneGraphs>
    <SelectedNodes>
      <Node id="node_dialog_1" />
    </SelectedNodes>
  </EditorState>
</Project>
```

---

## 四、分阶段实施计划

### Phase 1: 基础设施搭建（1-2 周）

#### 1.1 项目初始化
- [ ] 创建新的前端项目目录 `Visunovia.Editor`
- [ ] 初始化 Vite + React + TypeScript 项目
- [ ] 配置 Tailwind CSS + shadcn/ui
- [ ] 配置 ESLint + Prettier
- [ ] 添加路径别名

#### 1.2 类型定义
- [ ] 创建 `types/` 目录
- [ ] 定义 VNNode 类型系统
- [ ] 定义 VNSceneGraph 类型
- [ ] 定义 API 响应类型

#### 1.3 状态管理
- [ ] 安装 Zustand
- [ ] 实现 `useEditorStore`
- [ ] 实现 `useNodeGraphStore`

#### 1.4 API 客户端
- [ ] 创建 `api/` 目录
- [ ] 配置 axios/fetch 客户端
- [ ] 实现 JSON API 封装
- [ ] 实现错误处理

---

### Phase 2: 核心组件开发（2-3 周）

#### 2.1 布局框架
- [ ] 实现 `AppLayout` 组件
- [ ] 实现可调整大小的面板系统
- [ ] 实现面板折叠/展开

#### 2.2 节点编辑器（核心）
- [ ] 安装 React Flow
- [ ] 创建 `NodeGraphEditor` 组件
- [ ] 实现基础节点组件：
  - [ ] `StartNode`
  - [ ] `EndNode`
  - [ ] `DialogueNode`
  - [ ] `BranchNode`
- [ ] 实现节点连接系统
- [ ] 实现节点拖拽
- [ ] 实现框选多节点

#### 2.3 属性面板
- [ ] 创建 `InspectorPanel` 组件
- [ ] 实现属性编辑器框架
- [ ] 实现各类节点的属性表单

#### 2.4 层级面板
- [ ] 创建 `HierarchyPanel` 组件
- [ ] 实现场景图列表
- [ ] 实现节点树状展示
- [ ] 实现双击定位节点

---

### Phase 3: 节点系统完善（2-3 周）

#### 3.1 扩展节点类型
- [ ] 视觉节点：
  - [ ] `ChangeBackgroundNode`
  - [ ] `ShowCharacterNode`
  - [ ] `HideCharacterNode`
  - [ ] `CameraShakeNode`
  - [ ] `FadeScreenNode`
- [ ] 音频节点：
  - [ ] `PlayBGMNode`
  - [ ] `StopBGMNode`
  - [ ] `PlaySFXNode`
  - [ ] `PlayVoiceNode`
- [ ] 逻辑节点：
  - [ ] `SetVariableNode`
  - [ ] `ConditionalNode`
  - [ ] `DelayNode`
  - [ ] `SequenceNode`

#### 3.2 节点模板系统
- [ ] 创建 `NodeTemplateRegistry`
- [ ] 定义节点元数据
- [ ] 实现节点创建面板
- [ ] 实现拖拽创建节点

#### 3.3 连接线系统
- [ ] 实现执行流连接
- [ ] 实现数据流连接
- [ ] 实现连接验证
- [ ] 实现连接断开

#### 3.4 高级功能
- [ ] 实现节点分组
- [ ] 实现子图节点
- [ ] 实现节点复制/粘贴
- [ ] 实现撤销/重做

---

### Phase 4: 资源系统集成（1-2 周）

#### 4.1 资源浏览器
- [ ] 创建 `ProjectPanel` 组件
- [ ] 实现资源树状视图
- [ ] 实现资源预览
- [ ] 实现资源拖拽到节点

#### 4.2 资源选择器
- [ ] 创建 `ResourcePicker` 对话框
- [ ] 实现图片选择器
- [ ] 实现音频选择器
- [ ] 实现角色选择器

---

### Phase 5: 预览与调试（1-2 周）

#### 5.1 预览系统
- [ ] 创建 `PreviewPanel` 组件
- [ ] 实现游戏画面预览
- [ ] 实现播放控制
- [ ] 实现变量监视

#### 5.2 控制台
- [ ] 创建 `ConsolePanel` 组件
- [ ] 实现日志输出
- [ ] 实现日志过滤

---

### Phase 6: 后端适配（1-2 周）

#### 6.1 API 改造
- [ ] 新增 JSON API 端点
- [ ] 实现场景图 CRUD
- [ ] 实现节点 CRUD
- [ ] 实现连接 CRUD

#### 6.2 数据序列化
- [ ] 实现新 YAML 格式序列化
- [ ] 实现数据验证

#### 6.3 运行时支持
- [ ] 实现节点图解释器
- [ ] 实现变量系统
- [ ] 实现存档/读档

---

### Phase 7: 优化与发布（1 周）

#### 7.1 性能优化
- [ ] 实现节点虚拟化
- [ ] 优化重渲染
- [ ] 实现增量保存

#### 7.2 用户体验
- [ ] 添加快捷键系统
- [ ] 实现自动保存
- [ ] 实现主题切换

#### 7.3 文档
- [ ] 编写用户文档
- [ ] 编写 API 文档

---

## 五、技术选型建议

### 5.1 推荐技术栈

```json
{
  "dependencies": {
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "reactflow": "^11.0.0",
    "zustand": "^4.4.1",
    "@radix-ui/react-*": "latest",
    "class-variance-authority": "^0.7.0",
    "clsx": "^2.0.0",
    "tailwind-merge": "^1.14.0",
    "lucide-react": "^0.279.0",
    "axios": "^1.5.0",
    "react-hotkeys-hook": "^4.4.1",
    "react-resizable-panels": "^0.0.55"
  },
  "devDependencies": {
    "vite": "^4.4.9",
    "typescript": "^5.2.2",
    "tailwindcss": "^3.3.3",
    "@types/react": "^18.2.21",
    "eslint": "^8.48.0",
    "prettier": "^3.0.3"
  }
}
```

### 5.2 项目结构

```
Visunovia.Editor/
├── src/
│   ├── components/
│   │   ├── ui/
│   │   ├── layout/
│   │   ├── node-graph/
│   │   ├── panels/
│   │   └── dialogs/
│   ├── hooks/
│   ├── stores/
│   ├── types/
│   ├── api/
│   ├── utils/
│   ├── lib/
│   ├── constants/
│   └── App.tsx
├── public/
├── index.html
├── vite.config.ts
├── tailwind.config.ts
├── tsconfig.json
└── package.json
```

---

## 六、总结

本路线图将 Visunovia 的前端 UI 现代化改造分为 **7 个阶段**，预计总工期 **9-15 周**（Alpha 版本精简后）。

**关键里程碑：**

1. **Week 2**: 基础设施完成，可运行空编辑器
2. **Week 5**: 核心节点编辑器可用，可创建基础对话
3. **Week 8**: 完整节点系统，支持所有事件类型
4. **Week 10**: 资源系统集成完成
5. **Week 12**: 预览调试功能可用
6. **Week 14**: 后端适配完成，数据格式统一
7. **Week 15**: 发布 Alpha 版本

**Alpha 版本简化点：**
- 不处理旧版本数据格式兼容
- 仅提供单向数据转换（旧→新）
- 移除向后兼容的 API 端点
- 专注新架构的快速实现
