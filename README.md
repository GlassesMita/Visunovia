# Visunovia

> ⚠️ **早期版本声明**
> 本项目目前处于早期开发阶段（Alpha），部分功能尚不完善，可能存在 bug 和不稳定情况。欢迎提交 Issue 和 Pull Request！

## 项目简介

Visunovia 是一款基于 ASP.NET Core 和 .NET 10 的视觉小说（Visual Novel）对话编辑器，采用浏览器作为前端界面，提供直观的可视化操作来创建和管理视觉小说项目。

### 主要功能

- **场景管理**：创建、编辑、重命名和组织视觉小说场景
- **对话编辑**：支持三种对话类型
  - 普通对话（Dialogue）：角色台词和叙事文本
  - 分支选项（Branch）：玩家选择分支
  - 事件触发（Event）：背景切换、BGM 变更、角色显隐、等待、跳转、变量操作等
- **资源管理**：内置资源浏览器，支持背景、角色立绘、BGM、语音、音效五类资源
- **实时预览**：在预览模式下实时查看对话流程、背景切换、BGM 播放和角色显隐效果
- **撤销/重做**：完整的操作历史管理，支持最多 100 步撤销
- **多语言支持**：基于 PO 文件的国际化系统，内置中文和英文
- **文件浏览器**：服务端文件系统浏览，支持直接打开 `.tlor` 项目文件
- **快捷键**：Ctrl+Z/Y（撤销/重做）、Ctrl+S（保存）、Ctrl+N（新建）、Ctrl+O（打开）

### 技术栈

- **后端框架**：ASP.NET Core（Kestrel 自托管）
- **语言**：C# 13 / .NET 10
- **前端**：原生 JavaScript + Bootstrap + jQuery
- **数据格式**：XML（项目元数据）+ YAML（场景脚本）+ JSON（变量存储）
- **本地化**：PO 文件格式
- **依赖库**：YamlDotNet、System.Configuration.ConfigurationManager

## 项目结构

```
Visunovia/
├── Controllers/                # API 控制器
│   ├── Models/                 # 请求/响应 DTO
│   ├── EditorController.cs     # 场景与对话编辑 API
│   ├── FileBrowserController.cs # 文件浏览 API
│   ├── LocalizationController.cs # 本地化 API
│   ├── PreviewController.cs    # 预览数据 API
│   ├── ProjectController.cs    # 项目管理 API
│   ├── ResourceController.cs   # 资源管理 API
│   ├── SettingsController.cs   # 设置管理 API
│   └── SystemController.cs     # 系统操作 API
├── Services/                   # 业务逻辑层
│   ├── Configuration/          # 配置处理
│   ├── Localization/           # PO 文件解析与本地化服务
│   ├── EditorService.cs        # 核心编辑器服务
│   ├── EditorSessionService.cs # 编辑器会话管理
│   ├── EditorCommands.cs       # 撤销/重做命令
│   ├── UndoRedoManager.cs      # 撤销/重做管理器
│   ├── LocalizationService.cs  # 本地化服务
│   └── SettingsService.cs      # 配置管理服务
├── Models/Engine/              # 数据模型
│   ├── ResourceType.cs         # 资源类型定义
│   ├── VNProject.cs            # 项目模型
│   └── VNTypes.cs              # VN 核心类型
├── Middleware/                  # 中间件
│   └── GlobalExceptionMiddleware.cs # 全局异常处理
├── Pages/                      # Razor Pages
├── Localizations/              # PO 本地化文件
│   ├── en-US.po
│   └── zh-CN.po
├── wwwroot/                    # 静态资源
│   ├── css/                    # 样式（深色主题）
│   ├── js/                     # 前端脚本
│   ├── fonts/                  # 自定义字体
│   └── lib/                    # 第三方库（Bootstrap/jQuery）
└── Properties/                 # 项目属性
```

## 构建说明

### 环境要求

- Windows 10 或更高版本
- .NET 10 SDK
- Visual Studio 2022 17.10+ 或 VS Code + C# 扩展

### 构建步骤

1. 克隆项目
```bash
git clone https://github.com/GlassesMita/Visunovia.git
cd Visunovia
```

2. 还原依赖
```bash
dotnet restore
```

3. 构建项目
```bash
dotnet build
```

4. 运行项目
```bash
dotnet run
```

应用默认在 `http://localhost:28478` 启动。可通过 `--port` 参数指定端口：
```bash
dotnet run -- --port 8080
```

### 发布打包

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## API 概览

| 路由前缀 | 控制器 | 功能 |
|----------|--------|------|
| `/api/editor` | EditorController | 场景与对话的增删改查、撤销/重做 |
| `/api/files` | FileBrowserController | 文件系统浏览、项目打开 |
| `/api/localization` | LocalizationController | 语言切换、翻译查询 |
| `/api/preview` | PreviewController | 预览数据获取 |
| `/api/project` | ProjectController | 项目创建、打开、保存 |
| `/api/resources` | ResourceController | 资源查询与文件访问 |
| `/api/settings` | SettingsController | 应用设置读写与重置 |
| `/api/system` | SystemController | 系统操作（关闭服务） |

## 使用说明

### 创建新项目

1. 启动应用后，浏览器自动打开编辑器界面
2. 使用快捷键 `Ctrl+N` 或点击新建按钮
3. 系统自动创建项目目录结构：
   - `Assets/Backgrounds/` — 背景图片
   - `Assets/Characters/` — 角色立绘
   - `Assets/Musics/` — BGM 音乐
   - `Assets/Voices/` — 语音文件
   - `Assets/SFX/` — 音效文件

### 打开已有项目

- 使用快捷键 `Ctrl+O` 或点击打开按钮，通过文件浏览器选择 `.tlor` 项目文件
- 或直接拖拽 `.tlor` 文件到上传区域

### 编辑对话

1. 在场景标签栏选择或创建场景
2. 在中间对话列表中查看和编辑对话节点
3. 点击对话卡片查看和修改属性（右侧属性面板）
4. 对话类型说明：
   - **Dialogue**：角色名 + 对话文本
   - **Branch**：分支选项列表，每个选项可跳转到不同场景
   - **Event**：事件类型 + 参数（如背景路径、BGM 路径、等待秒数等）

### 预览项目

1. 点击预览按钮进入预览模式
2. 使用空格键或点击推进对话
3. 遇到分支选项时点击选择
4. 按 `Esc` 退出预览

### 资源管理

1. 在左侧资源面板切换资源类别标签
2. 点击资源项可在属性面板中选择使用
3. 支持缩略图预览

## 已知问题

- 资源管理面板在某些操作后可能需要刷新
- 预览模式下 BGM 和立绘显示依赖正确的资源路径配置
- 当前为单用户模式，不支持多用户同时编辑

## 更新日志

### v0.2.0-alpha (当前版本)
- 迁移至 ASP.NET Core Web 架构
- 基于 PO 文件的多语言支持（中/英）
- 完整的 RESTful API
- 全局异常处理中间件
- 设置持久化服务
- 文件浏览器功能

### v0.1.0-alpha
- 初始 Alpha 版本发布
- 基本场景和对话管理功能
- 资源管理器
- 事件系统基础支持
- 实时预览功能

## 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 许可证

本项目基于 MIT 许可证开源。详情请参阅 [LICENSE](LICENSE) 文件。

## 联系方式

- GitHub Issues: https://github.com/GlassesMita/Visunovia/issues
