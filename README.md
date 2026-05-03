# Visunovia

> ⚠️ **早期版本声明**
> 本项目目前处于早期开发阶段（Alpha），部分功能尚不完善，可能存在 bug 和不稳定情况。欢迎提交 Issue 和 Pull Request！

## 项目简介

Visunovia 是一款基于 WPF 和 .NET 10 的视觉小说（Visual Novel）对话编辑器，提供直观的可视化界面来创建和管理视觉小说项目。

### 主要功能

- **场景管理**：创建、编辑和组织视觉小说场景
- **对话节点**：可视化编辑角色对话和叙事内容
- **事件系统**：支持多种事件类型
  - 更改背景（ChangeBackground）
  - 更改 BGM（ChangeBgm）
  - 显示/隐藏角色（ShowCharacter/HideCharacter）
  - 等待指定秒数（WaitSeconds）
  - 跳转场景、设置变量等
- **资源管理**：内置资源管理器，支持背景、音乐、角色立绘等资源
- **实时预览**：预览模式下可实时查看对话流程和效果
- **过渡效果**：支持淡入淡出、滑动等多种过渡效果
- **项目打包**：支持导出为可执行的视觉小说播放器

### 技术栈

- **框架**：WPF（Windows Presentation Foundation）
- **语言**：C# 12 / .NET 10
- **播放器**：基于 NW.js 构建
- **数据格式**：YAML + XML

## 项目结构

```
Visunovia/
├── Controls/              # 自定义 UI 控件
│   ├── PreviewControl.xaml.cs    # 预览控件
│   └── ResourceManagerControl.xaml.cs  # 资源管理控件
├── Engine/                # 核心引擎
│   ├── Core/              # 核心类型和引擎
│   ├── Editor/            # 编辑器服务
│   ├── Events/            # 事件系统
│   ├── Resource/          # 资源管理
│   └── Script/            # 脚本解析
├── PlayerTemplate/        # 播放器模板（传统）
├── Visunovia.Player.NW/   # NW.js 播放器
├── Resources/             # 应用程序资源
└── Properties/            # 项目属性
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

### 发布打包

运行打包脚本：
```powershell
.\build.ps1
```

或在 Visual Studio 中选择 `发布` 功能。

## 使用说明

### 创建新项目

1. 启动应用程序
2. 点击 `文件` → `新建项目`
3. 选择保存位置并输入项目名称
4. 系统会自动创建项目目录结构：
   - `Assets/Backgrounds/` - 背景图片
   - `Assets/Characters/` - 角色立绘
   - `Assets/Musics/` - BGM 音乐
   - `Assets/Voices/` - 语音文件
   - `Assets/Images/` - 其他图片资源

### 添加资源

1. 在左侧资源管理器中选择资源类型（背景/角色/音乐）
2. 点击 `添加资源` 按钮或直接将文件拖入对应文件夹
3. 资源会自动扫描并添加到列表中

### 编辑对话

1. 在场景列表中选择场景
2. 在右侧对话列表中查看和编辑对话节点
3. 点击节点可查看和修改属性
4. 使用 `上移` / `下移` 按钮调整节点顺序

### 预览项目

1. 点击工具栏上的 `预览` 按钮
2. 在预览窗口中查看对话流程
3. 使用 `下一句` 按钮或空格键前进

## 已知问题

- 资源管理面板在某些操作后可能需要刷新
- 预览模式下 BGM 和立绘显示依赖正确的资源路径配置
- 打包功能需要确保 NW.js 播放器目录存在

## 更新日志

### v0.1.0-alpha (当前版本)
- 初始 Alpha 版本发布
- 基本场景和对话管理功能
- 资源管理器
- 事件系统基础支持
- 实时预览功能
- NW.js 播放器打包支持

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
