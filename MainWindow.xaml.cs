using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Visunovia.Engine.Core;
using Visunovia.Engine.Editor;
using Visunovia.Engine.Localization;
using Visunovia.Engine.Debug;
using Visunovia.Engine.Events;
using Visunovia.Controls;

namespace Visunovia;

public partial class MainWindow : Window
{
    private EditorService _editor;
    private VNEngine _engine;
    private readonly LocalizationService _localization;
    private int _activeSceneIndex = 0;
    private int _selectedDialogueIndex = -1;

    private FileSystemWatcher? _assetWatcher;
    private readonly object _assetWatchLock = new object();
    private bool _isRefreshingResources = false;
    private RecentProjectsManager? _recentProjectsManager;

    public MainWindow()
    {
        InitializeComponent();

        App.SetMainWindow(this);

        _editor = new EditorService();
        _engine = new VNEngine();
        _localization = LocalizationService.Instance;

        _editor.ProjectChanged += OnProjectChanged;
        _editor.StatusChanged += OnStatusChanged;
        _editor.ErrorOccurred += OnErrorOccurred;

        ResourceManager.ResourceDropped += OnResourceDropped;
        ResourceManager.ResourceDoubleClick += OnResourceDoubleClicked;

        _recentProjectsManager = new RecentProjectsManager();

        PreviewControl.SetEditor(_editor);
        PreviewControl.PreviewClosed += OnPreviewClosed;

        this.KeyDown += OnMainWindowKeyDown;
        this.Closing += OnWindowClosing;
        ProjectNameLabel.MouseLeftButtonUp += OnProjectNameLabelClicked;
        ProjectNameLabel.Cursor = Cursors.Hand;

        _editor.NewProject("未命名项目", "");
        UpdateUI();
        DebugConsoleService.Instance.Info("WPF UI 初始化完成", "MainWindow");
    }

    private void OnProjectChanged()
    {
        UpdateUI();
        RefreshResources();
    }

    private void SetupAssetWatcher()
    {
        lock (_assetWatchLock)
        {
            if (_assetWatcher != null)
            {
                _assetWatcher.EnableRaisingEvents = false;
                _assetWatcher.Created -= OnAssetFileCreated;
                _assetWatcher.Deleted -= OnAssetFileDeleted;
                _assetWatcher.Renamed -= OnAssetFileRenamed;
                _assetWatcher.Dispose();
                _assetWatcher = null;
            }

            if (string.IsNullOrEmpty(_editor.CurrentProjectPath))
                return;

            var projectRoot = Path.GetDirectoryName(_editor.CurrentProjectPath);
            if (string.IsNullOrEmpty(projectRoot))
                return;

            var assetsPath = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsPath))
                return;

            try
            {
                _assetWatcher = new FileSystemWatcher(assetsPath) { IncludeSubdirectories = true };
                _assetWatcher.Created += OnAssetFileCreated;
                _assetWatcher.Deleted += OnAssetFileDeleted;
                _assetWatcher.Renamed += OnAssetFileRenamed;
                _assetWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                DebugConsoleService.Instance.Warning($"资源监视器启动失败: {ex.Message}", "AssetWatcher");
            }
        }
    }

    private void OnAssetFileCreated(object sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() => RefreshResources());
    }

    private void OnAssetFileDeleted(object sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() => RefreshResources());
    }

    private void OnAssetFileRenamed(object sender, RenamedEventArgs e)
    {
        Dispatcher.Invoke(() => RefreshResources());
    }

    private void RefreshResources()
    {
        if (_isRefreshingResources) return;
        _isRefreshingResources = true;

        try
        {
            if (_editor.CurrentProject == null) return;

            var projectRoot = Path.GetDirectoryName(_editor.CurrentProjectPath);
            if (string.IsNullOrEmpty(projectRoot)) return;

            var assetsPath = Path.Combine(projectRoot, "Assets");
            if (!Directory.Exists(assetsPath)) return;

            ResourceManager.Clear();

            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" };
            var audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".mp3", ".wav", ".ogg", ".flac", ".m4a" };
            var fontExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".ttf", ".otf", ".woff", ".woff2" };

            var spriteDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Characters", "Chars", "Sprites", "Sprite" };
            var backgroundDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Backgrounds", "Bg", "Background" };
            var musicDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Musics", "Music", "Bgm" };
            var voiceDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Voices", "Voice" };
            var sfxDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Sfx", "Sfs", "SFXs", "SFX", "Sounds", "Sound" };
            var fontDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "Fonts", "Font" };

            foreach (var dir in Directory.GetDirectories(assetsPath))
            {
                var dirName = Path.GetFileName(dir);

                if (spriteDirs.Contains(dirName))
                {
                    ScanDirectory(dir, imageExtensions, ResourceType.Sprite);
                }
                else if (backgroundDirs.Contains(dirName))
                {
                    ScanDirectory(dir, imageExtensions, ResourceType.Background);
                }
                else if (musicDirs.Contains(dirName))
                {
                    ScanDirectory(dir, audioExtensions, ResourceType.Music);
                }
                else if (voiceDirs.Contains(dirName))
                {
                    ScanDirectory(dir, audioExtensions, ResourceType.Voice);
                }
                else if (sfxDirs.Contains(dirName))
                {
                    ScanDirectory(dir, audioExtensions, ResourceType.Other);
                }
                else if (fontDirs.Contains(dirName))
                {
                    ScanDirectory(dir, fontExtensions, ResourceType.Font);
                }
            }
        }
        finally
        {
            _isRefreshingResources = false;
        }
    }

    private void ScanDirectory(string path, HashSet<string> extensions, ResourceType type)
    {
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.GetFiles(path))
        {
            var ext = Path.GetExtension(file);
            if (extensions.Contains(ext))
            {
                ResourceManager.AddResource(file, type);
            }
        }
    }

    private Dictionary<string, List<string>> GetCurrentResourceIndex()
    {
        var resources = new Dictionary<string, List<string>>
        {
            ["sprites"] = new List<string>(),
            ["backgrounds"] = new List<string>(),
            ["bgm"] = new List<string>(),
            ["voice"] = new List<string>(),
            ["sfx"] = new List<string>()
        };

        foreach (var item in ResourceManager.Sprites)
        {
            resources["sprites"].Add(item.Path);
        }
        foreach (var item in ResourceManager.Backgrounds)
        {
            resources["backgrounds"].Add(item.Path);
        }
        foreach (var item in ResourceManager.Musics)
        {
            resources["bgm"].Add(item.Path);
        }
        foreach (var item in ResourceManager.Voices)
        {
            resources["voice"].Add(item.Path);
        }

        return resources;
    }

    private void UpdateUI()
    {
        if (_editor.CurrentProject == null)
        {
            ProjectNameLabel.Text = "未命名项目";
            App.UpdateWindowTitle("", "", false);
            return;
        }

        var projectName = _editor.CurrentProject.Metadata.Title;
        if (_editor.HasUnsavedChanges)
        {
            projectName += " *";
        }
        ProjectNameLabel.Text = projectName;
        var projectDir = Path.GetDirectoryName(_editor.CurrentProjectPath) ?? "";
        App.UpdateWindowTitle(_editor.CurrentProject.Metadata.Title, projectDir, _editor.HasUnsavedChanges);

        RefreshResources();
        UpdateSceneTabs();
        UpdateDialogueList();
    }

    private void UpdateSceneTabs()
    {
        SceneTabsContainer.Children.Clear();

        if (_editor.CurrentProject?.Scenes == null) return;

        for (int i = 0; i < _editor.CurrentProject.Scenes.Count; i++)
        {
            var scene = _editor.CurrentProject.Scenes[i];
            var button = new Button
            {
                Content = scene.Id ?? $"场景 {i + 1}",
                Tag = i,
                Margin = new Thickness(0, 0, 4, 0),
                Padding = new Thickness(12, 6, 12, 6),
                Background = i == _activeSceneIndex ? System.Windows.Media.Brushes.DodgerBlue : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            button.Click += OnSceneTabClicked;
            button.MouseLeftButtonDown += OnSceneDoubleClicked;

            var contextMenu = new ContextMenu();
            var renameItem = new MenuItem { Header = "重命名" };
            renameItem.Click += (s, e) => ShowRenameSceneDialog(i);
            var deleteItem = new MenuItem { Header = "删除场景" };
            deleteItem.Click += (s, e) =>
            {
                _activeSceneIndex = i;
                OnRemoveSceneClicked(s, e);
            };
            contextMenu.Items.Add(renameItem);
            contextMenu.Items.Add(deleteItem);
            button.ContextMenu = contextMenu;

            SceneTabsContainer.Children.Add(button);
        }
    }

    private void UpdateDialogueList()
    {
        DialogueListContainer.Children.Clear();

        if (_editor.CurrentProject?.Scenes == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (scene.Dialogues == null) return;

        for (int i = 0; i < scene.Dialogues.Count; i++)
        {
            var dialogue = scene.Dialogues[i];
            var border = new Border
            {
                Background = i == _selectedDialogueIndex ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 26, 26)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Tag = i,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            border.MouseLeftButtonUp += OnDialogueCardClicked;

            var panel = new StackPanel();

            var typeIndicator = dialogue.Type switch
            {
                VNDialogueType.Dialogue => "",
                VNDialogueType.Branch => "[分支] ",
                VNDialogueType.Event => $"[事件:{dialogue.Event?.EventType.ToString() ?? "Custom"}] ",
                _ => ""
            };

            var speakerLabel = new TextBlock
            {
                Text = typeIndicator + (dialogue.Speaker ?? "无说话者"),
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            };
            panel.Children.Add(speakerLabel);

            var textLabel = new TextBlock
            {
                Text = dialogue.Type == VNDialogueType.Branch
                    ? GetBranchPreviewText(dialogue)
                    : ((dialogue.Text?.Length ?? 0) > 100 ? dialogue.Text?.Substring(0, 100) + "..." : dialogue.Text ?? ""),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(textLabel);
            border.Child = panel;

            DialogueListContainer.Children.Add(border);
        }
    }

    private string GetBranchPreviewText(VNDialogue dialogue)
    {
        if (dialogue.Branch?.Choices == null || dialogue.Branch.Choices.Count == 0)
            return "无选项";

        var choiceTexts = dialogue.Branch.Choices
            .Take(3)
            .Select(c => c.Text)
            .ToList();

        var result = string.Join(" | ", choiceTexts);
        if (dialogue.Branch.Choices.Count > 3)
            result += $" ... (+{dialogue.Branch.Choices.Count - 3})";
        return result;
    }

    private void OnDialogueCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is int index)
        {
            _selectedDialogueIndex = index;
            UpdateDialogueList();
            UpdatePropertyPanel();
        }
    }

    private void UpdatePropertyPanel()
    {
        PropertyPanelContainer.Children.Clear();

        if (_editor.CurrentProject?.Scenes == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (scene.Dialogues == null || _selectedDialogueIndex < 0 || _selectedDialogueIndex >= scene.Dialogues.Count)
            return;

        var dialogue = scene.Dialogues[_selectedDialogueIndex];

        var typeLabel = dialogue.Type switch
        {
            VNDialogueType.Dialogue => "对话",
            VNDialogueType.Branch => "分支",
            VNDialogueType.Event => "事件",
            _ => "对话"
        };
        AddPropertySection("类型", CreateTypeDisplay(typeLabel));

        switch (dialogue.Type)
        {
            case VNDialogueType.Dialogue:
                AddPropertySection("说话者", CreateSpeakerEditor(dialogue));
                AddPropertySection("文本", CreateTextEditor(dialogue));
                AddPropertySection("背景", CreateBackgroundEditor(dialogue));
                AddPropertySection("立绘", CreateSpriteEditor(dialogue));
                AddPropertySection("语音", CreateVoiceEditor(dialogue));
                break;

            case VNDialogueType.Branch:
                AddPropertySection("分支选项", CreateBranchEditor(dialogue));
                break;

            case VNDialogueType.Event:
                AddPropertySection("事件类型", CreateEventEditor(dialogue));
                break;
        }
    }

    private UIElement CreateTypeDisplay(string typeName)
    {
        return new TextBlock
        {
            Text = typeName,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private UIElement CreateBranchEditor(VNDialogue dialogue)
    {
        var mainPanel = new StackPanel();

        if (dialogue.Branch == null)
        {
            dialogue.Branch = new VNBranch { Choices = new List<VNChoiceOption>() };
        }

        var choicesPanel = new StackPanel();

        for (int i = 0; i < dialogue.Branch.Choices.Count; i++)
        {
            var choiceIndex = i;
            var choice = dialogue.Branch.Choices[i];

            var choicePanel = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 35, 35)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var innerPanel = new StackPanel();

            var textLabel = new TextBlock
            {
                Text = $"选项 {i + 1}",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            innerPanel.Children.Add(textLabel);

            var textBox = new TextBox
            {
                Text = choice.Text,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            };
            textBox.TextChanged += (s, e) =>
            {
                dialogue.Branch!.Choices[choiceIndex].Text = textBox.Text;
                MarkDialogueModified();
            };
            innerPanel.Children.Add(textBox);

            var targetLabel = new TextBlock
            {
                Text = "目标场景",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };
            innerPanel.Children.Add(targetLabel);

            var targetComboBox = new System.Windows.Controls.ComboBox
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                IsEditable = true,
                Margin = new Thickness(0, 0, 0, 8)
            };
            targetComboBox.Items.Add("");
            if (_editor.CurrentProject?.Scenes != null)
            {
                foreach (var s in _editor.CurrentProject.Scenes)
                {
                    targetComboBox.Items.Add(s.Id);
                }
            }
            targetComboBox.Text = choice.TargetScene ?? "";
            targetComboBox.DropDownClosed += (s, e) =>
            {
                dialogue.Branch!.Choices[choiceIndex].TargetScene = targetComboBox.Text;
                MarkDialogueModified();
            };
            innerPanel.Children.Add(targetComboBox);

            var deleteBtn = new System.Windows.Controls.Button
            {
                Content = "删除选项",
                Height = 28,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 12
            };
            var capturedIndex = choiceIndex;
            deleteBtn.Click += (s, e) =>
            {
                if (dialogue.Branch!.Choices.Count > 1)
                {
                    dialogue.Branch.Choices.RemoveAt(capturedIndex);
                    MarkDialogueModified();
                    UpdatePropertyPanel();
                }
            };
            innerPanel.Children.Add(deleteBtn);

            choicePanel.Child = innerPanel;
            choicesPanel.Children.Add(choicePanel);
        }

        mainPanel.Children.Add(choicesPanel);

        var addBtn = new System.Windows.Controls.Button
        {
            Content = "添加选项",
            Height = 32,
            Margin = new Thickness(0, 8, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 13
        };
        addBtn.Click += (s, e) =>
        {
            dialogue.Branch!.Choices.Add(new VNChoiceOption { Text = $"选项 {dialogue.Branch.Choices.Count + 1}", TargetScene = "" });
            MarkDialogueModified();
            UpdatePropertyPanel();
        };
        mainPanel.Children.Add(addBtn);

        return mainPanel;
    }

    private UIElement CreateEventEditor(VNDialogue dialogue)
    {
        var mainPanel = new StackPanel();

        if (dialogue.Event == null)
        {
            dialogue.Event = new VNEvent { EventType = VNEventType.ChangeBgm };
        }

        var typePanel = new Grid();
        typePanel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        typePanel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

        var typeComboBox = new System.Windows.Controls.ComboBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };

        typeComboBox.Items.Add("JumpScene");
        typeComboBox.Items.Add("SetVariable");
        typeComboBox.Items.Add("PlaySound");
        typeComboBox.Items.Add("ChangeBackground");
        typeComboBox.Items.Add("ChangeBgm");
        typeComboBox.Items.Add("BgmStop");
        typeComboBox.Items.Add("ShowCharacter");
        typeComboBox.Items.Add("HideCharacter");
        typeComboBox.Items.Add("Pause");
        typeComboBox.Items.Add("Custom");
        typeComboBox.Items.Add("InvokePlugin");
        typeComboBox.Items.Add("InvokeCode");
        typeComboBox.Items.Add("WindowEffect");
        typeComboBox.Text = dialogue.Event.EventType.ToString();
        typeComboBox.DropDownClosed += (s, e) =>
        {
            if (Enum.TryParse<VNEventType>(typeComboBox.Text, out var eventType))
            {
                dialogue.Event!.EventType = eventType;
                MarkDialogueModified();
                UpdatePropertyPanel();
            }
        };
        typePanel.Children.Add(typeComboBox);
        mainPanel.Children.Add(typePanel);

        var paramsPanel = CreateEventParamsEditor(dialogue);
        mainPanel.Children.Add(paramsPanel);

        return mainPanel;
    }

    private UIElement CreateEventParamsEditor(VNDialogue dialogue)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };

        var eventType = dialogue.Event?.EventType ?? VNEventType.Custom;

        switch (eventType)
        {
            case VNEventType.JumpScene:
                {
                    var label = new TextBlock
                    {
                        Text = "目标场景",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var comboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13,
                        IsEditable = true
                    };
                    comboBox.Items.Add("");
                    if (_editor.CurrentProject?.Scenes != null)
                    {
                        foreach (var s in _editor.CurrentProject.Scenes)
                        {
                            comboBox.Items.Add(s.Id);
                        }
                    }
                    var targetScene = dialogue.Event?.Parameters.TryGetValue("TargetScene", out var ts) == true ? ts?.ToString() ?? "" : "";
                    comboBox.Text = targetScene;
                    comboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["TargetScene"] = comboBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(comboBox);
                }
                break;

            case VNEventType.SetVariable:
                {
                    var nameLabel = new TextBlock
                    {
                        Text = "变量名",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(nameLabel);

                    var nameBox = new TextBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.White,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    var varName = dialogue.Event?.Parameters.TryGetValue("VariableName", out var vn) == true ? vn?.ToString() ?? "" : "";
                    nameBox.Text = varName;
                    nameBox.TextChanged += (s, e) =>
                    {
                        dialogue.Event!.Parameters["VariableName"] = nameBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(nameBox);

                    var valueLabel = new TextBlock
                    {
                        Text = "变量值",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    panel.Children.Add(valueLabel);

                    var valueBox = new TextBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.White,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    var varValue = dialogue.Event?.Parameters.TryGetValue("VariableValue", out var vv) == true ? vv?.ToString() ?? "" : "";
                    valueBox.Text = varValue;
                    valueBox.TextChanged += (s, e) =>
                    {
                        dialogue.Event!.Parameters["VariableValue"] = valueBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(valueBox);
                }
                break;

            case VNEventType.PlaySound:
                {
                    var label = new TextBlock
                    {
                        Text = "音效文件",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var comboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    comboBox.Items.Add("<无>");
                    foreach (var voice in ResourceManager.Voices)
                    {
                        comboBox.Items.Add(voice.Name);
                    }
                    var soundPath = dialogue.Event?.Parameters.TryGetValue("SoundPath", out var sp) == true ? sp?.ToString() ?? "" : "";
                    comboBox.Text = string.IsNullOrEmpty(soundPath) ? "<无>" : soundPath;
                    comboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["SoundPath"] = comboBox.Text == "<无>" ? "" : comboBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(comboBox);
                }
                break;

            case VNEventType.ChangeBackground:
                {
                    var label = new TextBlock
                    {
                        Text = "背景图片",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var comboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    comboBox.Items.Add("<无>");
                    foreach (var bg in ResourceManager.Backgrounds)
                    {
                        comboBox.Items.Add(bg.Name);
                    }
                    var bgPath = dialogue.Event?.Parameters.TryGetValue("BackgroundPath", out var bp) == true ? bp?.ToString() ?? "" : "";
                    comboBox.Text = string.IsNullOrEmpty(bgPath) ? "<无>" : bgPath;
                    comboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["BackgroundPath"] = comboBox.Text == "<无>" ? "" : comboBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(comboBox);
                }
                break;

            case VNEventType.ChangeBgm:
                {
                    var label = new TextBlock
                    {
                        Text = "BGM 音乐",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var comboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    comboBox.Items.Add("<无>");
                    foreach (var music in ResourceManager.Musics)
                    {
                        comboBox.Items.Add(music.Name);
                    }
                    var bgmPath = dialogue.Event?.Parameters.TryGetValue("BgmPath", out var mp) == true ? mp?.ToString() ?? "" : "";
                    comboBox.Text = string.IsNullOrEmpty(bgmPath) ? "<无>" : bgmPath;
                    comboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["BgmPath"] = comboBox.Text == "<无>" ? "" : comboBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(comboBox);
                }
                break;

            case VNEventType.ShowCharacter:
            case VNEventType.HideCharacter:
                {
                    var label = new TextBlock
                    {
                        Text = "角色立绘",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var comboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    comboBox.Items.Add("<无>");
                    foreach (var sprite in ResourceManager.Sprites)
                    {
                        comboBox.Items.Add(sprite.Name);
                    }
                    var charPath = dialogue.Event?.Parameters.TryGetValue("CharacterPath", out var cp) == true ? cp?.ToString() ?? "" : "";
                    comboBox.Text = string.IsNullOrEmpty(charPath) ? "<无>" : charPath;
                    comboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["CharacterPath"] = comboBox.Text == "<无>" ? "" : comboBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(comboBox);
                }
                break;

            case VNEventType.Pause:
                {
                    var label = new TextBlock
                    {
                        Text = "暂停时长（毫秒）",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var pauseBox = new TextBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.White,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    var pauseValue = dialogue.Event?.Parameters.TryGetValue("PauseDuration", out var pd) == true ? pd?.ToString() ?? "1000" : "1000";
                    pauseBox.Text = pauseValue;
                    pauseBox.TextChanged += (s, e) =>
                    {
                        dialogue.Event!.Parameters["PauseDuration"] = pauseBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(pauseBox);
                }
                break;

            case VNEventType.Custom:
                {
                    var label = new TextBlock
                    {
                        Text = "自定义命令",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(label);

                    var customBox = new TextBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.White,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13,
                        MinHeight = 60,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap
                    };
                    var customCmd = dialogue.Event?.Parameters.TryGetValue("CustomCommand", out var cc) == true ? cc?.ToString() ?? "" : "";
                    customBox.Text = customCmd;
                    customBox.TextChanged += (s, e) =>
                    {
                        dialogue.Event!.Parameters["CustomCommand"] = customBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(customBox);
                }
                break;

            case VNEventType.InvokePlugin:
                {
                    var assemblyComboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.Black,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    var classComboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.Black,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    var methodComboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.Black,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };

                    var assemblyLabel = new TextBlock
                    {
                        Text = "程序集",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(assemblyLabel);
                    panel.Children.Add(assemblyComboBox);

                    var classLabel = new TextBlock
                    {
                        Text = "类",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    panel.Children.Add(classLabel);
                    panel.Children.Add(classComboBox);

                    var methodLabel = new TextBlock
                    {
                        Text = "方法",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    panel.Children.Add(methodLabel);
                    panel.Children.Add(methodComboBox);

                    var pluginAssembly = dialogue.Event?.Parameters.TryGetValue("PluginAssembly", out var pa) == true ? pa?.ToString() ?? "" : "";
                    foreach (var asm in _editor.Plugins.Assemblies)
                    {
                        assemblyComboBox.Items.Add(asm.Name);
                    }
                    assemblyComboBox.Text = pluginAssembly;

                    var pluginClass = dialogue.Event?.Parameters.TryGetValue("PluginClass", out var pc) == true ? pc?.ToString() ?? "" : "";
                    classComboBox.Text = pluginClass;

                    var pluginMethod = dialogue.Event?.Parameters.TryGetValue("PluginMethod", out var pm) == true ? pm?.ToString() ?? "" : "";
                    methodComboBox.Text = pluginMethod;

                    assemblyComboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["PluginAssembly"] = assemblyComboBox.Text;
                        dialogue.Event!.Parameters["PluginClass"] = "";
                        dialogue.Event!.Parameters["PluginMethod"] = "";
                        classComboBox.Items.Clear();
                        methodComboBox.Items.Clear();
                        var types = _editor.Plugins.GetTypes(assemblyComboBox.Text);
                        foreach (var t in types)
                        {
                            classComboBox.Items.Add(t.FullName);
                        }
                        classComboBox.Text = "";
                        methodComboBox.Text = "";
                        MarkDialogueModified();
                    };

                    classComboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["PluginClass"] = classComboBox.Text;
                        dialogue.Event!.Parameters["PluginMethod"] = "";
                        methodComboBox.Items.Clear();
                        var types = _editor.Plugins.GetTypes(assemblyComboBox.Text);
                        var selectedType = types.FirstOrDefault(t => t.FullName == classComboBox.Text);
                        if (selectedType != null)
                        {
                            var methods = _editor.Plugins.GetMethods(assemblyComboBox.Text, classComboBox.Text);
                            foreach (var m in methods)
                            {
                                methodComboBox.Items.Add(m.Name);
                            }
                        }
                        methodComboBox.Text = "";
                        MarkDialogueModified();
                    };

                    methodComboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["PluginMethod"] = methodComboBox.Text;
                        MarkDialogueModified();
                    };
                }
                break;

            case VNEventType.InvokeCode:
                {
                    var codeLabel = new TextBlock
                    {
                        Text = "C# 代码",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(codeLabel);

                    var codeBox = new System.Windows.Controls.TextBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = System.Windows.Media.Brushes.White,
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 12,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        MinHeight = 150,
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
                    };
                    var code = dialogue.Event?.Parameters.TryGetValue("Code", out var c) == true ? c?.ToString() ?? "" : "";
                    codeBox.Text = code;
                    codeBox.TextChanged += (s, e) =>
                    {
                        dialogue.Event!.Parameters["Code"] = codeBox.Text;
                        MarkDialogueModified();
                    };
                    panel.Children.Add(codeBox);
                }
                break;

            case VNEventType.WindowEffect:
                {
                    var effectTypeLabel = new TextBlock
                    {
                        Text = "窗口效果类型",
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(effectTypeLabel);

                    var effectTypeComboBox = new System.Windows.Controls.ComboBox
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13
                    };
                    effectTypeComboBox.Items.Add("None");
                    effectTypeComboBox.Items.Add("Shake");
                    effectTypeComboBox.Items.Add("Pulse");
                    effectTypeComboBox.Items.Add("MoveTo");
                    effectTypeComboBox.Items.Add("BorderFlash");
                    var effectTypeValue = dialogue.Event?.Parameters.TryGetValue("EffectType", out var et) == true ? et?.ToString() ?? "None" : "None";
                    effectTypeComboBox.Text = effectTypeValue;
                    effectTypeComboBox.DropDownClosed += (s, e) =>
                    {
                        dialogue.Event!.Parameters["EffectType"] = effectTypeComboBox.Text;
                        MarkDialogueModified();
                        UpdatePropertyPanel();
                    };
                    panel.Children.Add(effectTypeComboBox);

                    if (effectTypeComboBox.Text == "Shake")
                    {
                        var shakeAmplitudeLabel = new TextBlock
                        {
                            Text = "晃动幅度（像素）",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(shakeAmplitudeLabel);

                        var shakeAmplitudeBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var shakeAmplitudeValue = dialogue.Event?.Parameters.TryGetValue("ShakeAmplitude", out var sa) == true ? sa?.ToString() ?? "15" : "15";
                        shakeAmplitudeBox.Text = shakeAmplitudeValue;
                        shakeAmplitudeBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["ShakeAmplitude"] = shakeAmplitudeBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(shakeAmplitudeBox);

                        var shakeDurationLabel = new TextBlock
                        {
                            Text = "晃动持续时间（毫秒）",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(shakeDurationLabel);

                        var shakeDurationBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var shakeDurationValue = dialogue.Event?.Parameters.TryGetValue("ShakeDurationMs", out var sd) == true ? sd?.ToString() ?? "1000" : "1000";
                        shakeDurationBox.Text = shakeDurationValue;
                        shakeDurationBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["ShakeDurationMs"] = shakeDurationBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(shakeDurationBox);
                    }
                    else if (effectTypeComboBox.Text == "Pulse")
                    {
                        var pulseScaleMinLabel = new TextBlock
                        {
                            Text = "最小缩放比例",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(pulseScaleMinLabel);

                        var pulseScaleMinBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var pulseScaleMinValue = dialogue.Event?.Parameters.TryGetValue("PulseScaleMin", out var psmin) == true ? psmin?.ToString() ?? "0.8" : "0.8";
                        pulseScaleMinBox.Text = pulseScaleMinValue;
                        pulseScaleMinBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["PulseScaleMin"] = pulseScaleMinBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(pulseScaleMinBox);

                        var pulseScaleMaxLabel = new TextBlock
                        {
                            Text = "最大缩放比例",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(pulseScaleMaxLabel);

                        var pulseScaleMaxBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var pulseScaleMaxValue = dialogue.Event?.Parameters.TryGetValue("PulseScaleMax", out var psmax) == true ? psmax?.ToString() ?? "1.2" : "1.2";
                        pulseScaleMaxBox.Text = pulseScaleMaxValue;
                        pulseScaleMaxBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["PulseScaleMax"] = pulseScaleMaxBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(pulseScaleMaxBox);

                        var pulseFrequencyLabel = new TextBlock
                        {
                            Text = "脉冲频率",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(pulseFrequencyLabel);

                        var pulseFrequencyBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var pulseFrequencyValue = dialogue.Event?.Parameters.TryGetValue("PulseFrequency", out var pf) == true ? pf?.ToString() ?? "1" : "1";
                        pulseFrequencyBox.Text = pulseFrequencyValue;
                        pulseFrequencyBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["PulseFrequency"] = pulseFrequencyBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(pulseFrequencyBox);

                        var pulseDurationLabel = new TextBlock
                        {
                            Text = "脉冲持续时间（毫秒）",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(pulseDurationLabel);

                        var pulseDurationBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var pulseDurationValue = dialogue.Event?.Parameters.TryGetValue("PulseDurationMs", out var pd) == true ? pd?.ToString() ?? "2000" : "2000";
                        pulseDurationBox.Text = pulseDurationValue;
                        pulseDurationBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["PulseDurationMs"] = pulseDurationBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(pulseDurationBox);
                    }
                    else if (effectTypeComboBox.Text == "MoveTo")
                    {
                        var moveToXLabel = new TextBlock
                        {
                            Text = "目标 X 坐标",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(moveToXLabel);

                        var moveToXBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var moveToXValue = dialogue.Event?.Parameters.TryGetValue("MoveToX", out var mx) == true ? mx?.ToString() ?? "0" : "0";
                        moveToXBox.Text = moveToXValue;
                        moveToXBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["MoveToX"] = moveToXBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(moveToXBox);

                        var moveToYLabel = new TextBlock
                        {
                            Text = "目标 Y 坐标",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(moveToYLabel);

                        var moveToYBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var moveToYValue = dialogue.Event?.Parameters.TryGetValue("MoveToY", out var my) == true ? my?.ToString() ?? "0" : "0";
                        moveToYBox.Text = moveToYValue;
                        moveToYBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["MoveToY"] = moveToYBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(moveToYBox);
                    }
                    else if (effectTypeComboBox.Text == "BorderFlash")
                    {
                        var borderFlashColorLabel = new TextBlock
                        {
                            Text = "闪烁颜色（HEX）",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(borderFlashColorLabel);

                        var borderFlashColorBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var borderFlashColorValue = dialogue.Event?.Parameters.TryGetValue("BorderFlashColor", out var bfc) == true ? bfc?.ToString() ?? "#FF0000" : "#FF0000";
                        borderFlashColorBox.Text = borderFlashColorValue;
                        borderFlashColorBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["BorderFlashColor"] = borderFlashColorBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(borderFlashColorBox);

                        var borderFlashCountLabel = new TextBlock
                        {
                            Text = "闪烁次数",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(borderFlashCountLabel);

                        var borderFlashCountBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var borderFlashCountValue = dialogue.Event?.Parameters.TryGetValue("BorderFlashCount", out var bfcnt) == true ? bfcnt?.ToString() ?? "3" : "3";
                        borderFlashCountBox.Text = borderFlashCountValue;
                        borderFlashCountBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["BorderFlashCount"] = borderFlashCountBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(borderFlashCountBox);

                        var borderFlashIntervalLabel = new TextBlock
                        {
                            Text = "闪烁间隔（毫秒）",
                            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
                            FontSize = 11,
                            Margin = new Thickness(0, 8, 0, 4)
                        };
                        panel.Children.Add(borderFlashIntervalLabel);

                        var borderFlashIntervalBox = new TextBox
                        {
                            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                            Foreground = System.Windows.Media.Brushes.White,
                            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                            Padding = new Thickness(8, 6, 8, 6),
                            FontSize = 13
                        };
                        var borderFlashIntervalValue = dialogue.Event?.Parameters.TryGetValue("BorderFlashIntervalMs", out var bfi) == true ? bfi?.ToString() ?? "200" : "200";
                        borderFlashIntervalBox.Text = borderFlashIntervalValue;
                        borderFlashIntervalBox.TextChanged += (s, e) =>
                        {
                            dialogue.Event!.Parameters["BorderFlashIntervalMs"] = borderFlashIntervalBox.Text;
                            MarkDialogueModified();
                        };
                        panel.Children.Add(borderFlashIntervalBox);
                    }
                }
                break;
        }

        return panel;
    }

    private void AddPropertySection(string label, UIElement editor)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

        var labelBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
            Margin = new Thickness(0, 0, 0, 6)
        };

        section.Children.Add(labelBlock);
        section.Children.Add(editor);
        PropertyPanelContainer.Children.Add(section);
    }

    private UIElement CreateSpeakerEditor(VNDialogue dialogue)
    {
        var textBox = new TextBox
        {
            Text = dialogue.Speaker ?? "",
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };
        textBox.TextChanged += (s, e) =>
        {
            dialogue.Speaker = textBox.Text;
            MarkDialogueModified();
        };
        return textBox;
    }

    private UIElement CreateTextEditor(VNDialogue dialogue)
    {
        var textBox = new TextBox
        {
            Text = dialogue.Text ?? "",
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        };
        textBox.TextChanged += (s, e) =>
        {
            dialogue.Text = textBox.Text;
            MarkDialogueModified();
        };
        return textBox;
    }

    private UIElement CreateBackgroundEditor(VNDialogue dialogue)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

        var comboBox = new System.Windows.Controls.ComboBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };

        comboBox.Items.Add("");
        foreach (var bg in ResourceManager.Backgrounds)
        {
            comboBox.Items.Add(bg.Name);
        }
        comboBox.Text = dialogue.Sprites.FirstOrDefault()?.Path ?? "";
        comboBox.DropDownClosed += (s, e) =>
        {
            var selectedPath = comboBox.Text;
            if (dialogue.Sprites.Count > 0)
            {
                dialogue.Sprites[0].Path = selectedPath;
            }
            else if (!string.IsNullOrEmpty(selectedPath))
            {
                dialogue.Sprites.Add(new VNSprite { Path = selectedPath, Layer = 0 });
            }
            MarkDialogueModified();
        };

        var browseBtn = new System.Windows.Controls.Button
        {
            Content = "浏览",
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };
        browseBtn.Click += (s, e) =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "选择背景图片"
            };
            if (dialog.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileName(dialog.FileName);
                comboBox.Text = fileName;
            }
        };

        System.Windows.Controls.Grid.SetColumn(comboBox, 0);
        System.Windows.Controls.Grid.SetColumn(browseBtn, 1);
        panel.Children.Add(comboBox);
        panel.Children.Add(browseBtn);

        return panel;
    }

    private UIElement CreateSpriteEditor(VNDialogue dialogue)
    {
        var panel = new Grid();
        panel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

        var comboBox = new System.Windows.Controls.ComboBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };

        comboBox.Items.Add("");
        foreach (var sprite in ResourceManager.Sprites)
        {
            comboBox.Items.Add(sprite.Name);
        }

        var spritePath = dialogue.Sprites.Skip(1).FirstOrDefault()?.Path ?? "";
        comboBox.Text = spritePath;
        comboBox.DropDownClosed += (s, e) =>
        {
            var selectedPath = comboBox.Text;
            if (dialogue.Sprites.Count > 1)
            {
                dialogue.Sprites[1].Path = selectedPath;
            }
            else if (!string.IsNullOrEmpty(selectedPath))
            {
                dialogue.Sprites.Add(new VNSprite { Path = selectedPath, Layer = 1 });
            }
            MarkDialogueModified();
        };

        var browseBtn = new System.Windows.Controls.Button
        {
            Content = "浏览",
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };
        browseBtn.Click += (s, e) =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Title = "选择立绘"
            };
            if (dialog.ShowDialog() == true)
            {
                var fileName = System.IO.Path.GetFileName(dialog.FileName);
                comboBox.Text = fileName;
            }
        };

        System.Windows.Controls.Grid.SetColumn(comboBox, 0);
        System.Windows.Controls.Grid.SetColumn(browseBtn, 1);
        panel.Children.Add(comboBox);
        panel.Children.Add(browseBtn);

        return panel;
    }

    private UIElement CreateVoiceEditor(VNDialogue dialogue)
    {
        var comboBox = new System.Windows.Controls.ComboBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };

        comboBox.Items.Add("");
        foreach (var voice in ResourceManager.Voices)
        {
            comboBox.Items.Add(voice.Name);
        }
        comboBox.Text = dialogue.Voice ?? "";
        comboBox.DropDownClosed += (s, e) =>
        {
            dialogue.Voice = comboBox.Text;
            MarkDialogueModified();
        };

        return comboBox;
    }

    private void MarkDialogueModified()
    {
        _editor.MarkAsModified();
        UpdateDialogueList();
    }

    private void OnAddSceneClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null) return;

        var newScene = new VNScene
        {
            Id = $"场景 {_editor.CurrentProject.Scenes.Count + 1}",
            Background = "",
            Dialogues = new List<VNDialogue>()
        };

        _editor.CurrentProject.Scenes.Add(newScene);
        _editor.MarkAsModified();

        _activeSceneIndex = _editor.CurrentProject.Scenes.Count - 1;
        _selectedDialogueIndex = -1;
        UpdateSceneTabs();
        UpdateDialogueList();
    }

    private void OnRemoveSceneClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _editor.CurrentProject.Scenes.Count <= 1)
        {
            MessageBox.Show("至少需要保留一个场景", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show("确定要删除此场景吗？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _editor.CurrentProject.Scenes.RemoveAt(_activeSceneIndex);
            _editor.MarkAsModified();

            if (_activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
                _activeSceneIndex = _editor.CurrentProject.Scenes.Count - 1;
            _selectedDialogueIndex = -1;
            UpdateSceneTabs();
            UpdateDialogueList();
        }
    }

    private void OnSceneDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Button button && button.Tag is int index)
        {
            ShowRenameSceneDialog(index);
        }
    }

    private void ShowRenameSceneDialog(int sceneIndex)
    {
        if (_editor.CurrentProject == null) return;

        var scene = _editor.CurrentProject.Scenes[sceneIndex];
        var dialog = new Window
        {
            Title = "重命名场景",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
        };

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

        var textBox = new TextBox
        {
            Text = scene.Id ?? "",
            FontSize = 14,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80))
        };
        System.Windows.Controls.Grid.SetRow(textBox, 0);

        var buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        System.Windows.Controls.Grid.SetRow(buttonPanel, 1);

        var okButton = new System.Windows.Controls.Button
        {
            Content = "确定",
            Width = 80,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "取消",
            Width = 80,
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(10, 6, 10, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };

        okButton.Click += (s, args) =>
        {
            scene.Id = textBox.Text;
            _editor.MarkAsModified();
            UpdateSceneTabs();
            dialog.Close();
        };

        cancelButton.Click += (s, args) => dialog.Close();

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(textBox);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        dialog.ShowDialog();
    }

    private void OnAddDialogueClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var newDialogue = new VNDialogue
        {
            Type = VNDialogueType.Dialogue,
            Speaker = "",
            Text = "新对话文本"
        };

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];

        if (_selectedDialogueIndex >= 0 && _selectedDialogueIndex < scene.Dialogues.Count)
        {
            scene.Dialogues.Insert(_selectedDialogueIndex + 1, newDialogue);
            _selectedDialogueIndex++;
        }
        else
        {
            scene.Dialogues.Add(newDialogue);
            _selectedDialogueIndex = scene.Dialogues.Count - 1;
        }
        _editor.MarkAsModified();
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnAddBranchClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var newDialogue = new VNDialogue
        {
            Type = VNDialogueType.Branch,
            Branch = new VNBranch
            {
                Choices = new List<VNChoiceOption>
                {
                    new VNChoiceOption { Text = "选项 1", TargetScene = "" },
                    new VNChoiceOption { Text = "选项 2", TargetScene = "" }
                }
            }
        };

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];

        if (_selectedDialogueIndex >= 0 && _selectedDialogueIndex < scene.Dialogues.Count)
        {
            scene.Dialogues.Insert(_selectedDialogueIndex + 1, newDialogue);
            _selectedDialogueIndex++;
        }
        else
        {
            scene.Dialogues.Add(newDialogue);
            _selectedDialogueIndex = scene.Dialogues.Count - 1;
        }
        _editor.MarkAsModified();
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnAddEventClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var newDialogue = new VNDialogue
        {
            Type = VNDialogueType.Event,
            Event = new VNEvent { EventType = VNEventType.ChangeBgm }
        };

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];

        if (_selectedDialogueIndex >= 0 && _selectedDialogueIndex < scene.Dialogues.Count)
        {
            scene.Dialogues.Insert(_selectedDialogueIndex + 1, newDialogue);
            _selectedDialogueIndex++;
        }
        else
        {
            scene.Dialogues.Add(newDialogue);
            _selectedDialogueIndex = scene.Dialogues.Count - 1;
        }
        _editor.MarkAsModified();
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnCopyDialogueClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (_selectedDialogueIndex < 0 || _selectedDialogueIndex >= scene.Dialogues.Count)
        {
            MessageBox.Show("请先选择一个对话", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var original = scene.Dialogues[_selectedDialogueIndex];
        var copied = new VNDialogue
        {
            Speaker = original.Speaker,
            Text = original.Text,
            Voice = original.Voice,
            Sprites = new List<VNSprite>(original.Sprites ?? new List<VNSprite>())
        };

        scene.Dialogues.Insert(_selectedDialogueIndex + 1, copied);
        _selectedDialogueIndex++;
        _editor.MarkAsModified();
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnDeleteDialogueClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (_selectedDialogueIndex < 0 || _selectedDialogueIndex >= scene.Dialogues.Count)
        {
            MessageBox.Show("请先选择一个对话", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show("确定要删除此对话吗？", "确认删除",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            scene.Dialogues.RemoveAt(_selectedDialogueIndex);
            _editor.MarkAsModified();

            if (_selectedDialogueIndex >= scene.Dialogues.Count)
                _selectedDialogueIndex = scene.Dialogues.Count - 1;
            UpdateDialogueList();
            UpdatePropertyPanel();
        }
    }

    private void OnMoveUpClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (_selectedDialogueIndex <= 0 || _selectedDialogueIndex >= scene.Dialogues.Count)
            return;

        var index = _selectedDialogueIndex;
        scene.Dialogues.Insert(index - 1, scene.Dialogues[index]);
        scene.Dialogues.RemoveAt(index + 1);
        _selectedDialogueIndex = index - 1;
        _editor.MarkAsModified();
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnMoveDownClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (_selectedDialogueIndex < 0 || _selectedDialogueIndex >= scene.Dialogues.Count - 1)
            return;

        var index = _selectedDialogueIndex;
        scene.Dialogues.Insert(index + 2, scene.Dialogues[index]);
        scene.Dialogues.RemoveAt(index);
        _selectedDialogueIndex = index + 1;
        _editor.MarkAsModified();
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnStatusChanged(string message)
    {
        Dispatcher.Invoke(() => StatusLabel.Text = message);
    }

    private void OnErrorOccurred(string message)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private void OnSceneTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index)
        {
            _activeSceneIndex = index;
            _selectedDialogueIndex = -1;
            UpdateSceneTabs();
            UpdateDialogueList();
        }
    }

    private void OnResourceDoubleClicked(object? sender, ResourceItem e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (string.IsNullOrEmpty(e.Path))
            return;

        var newDialogue = new VNDialogue
        {
            Speaker = "",
            Text = ""
        };

        switch (e.Type)
        {
            case ResourceType.Sprite:
                newDialogue.Sprites.Add(new VNSprite { Path = e.Path, Layer = 1 });
                break;
            case ResourceType.Background:
                newDialogue.Sprites.Add(new VNSprite { Path = e.Path, Layer = 0 });
                break;
            case ResourceType.Music:
                newDialogue.Text = $"[BGM: {e.Name}]";
                break;
            case ResourceType.Voice:
                newDialogue.Voice = e.Path;
                break;
        }

        scene.Dialogues.Add(newDialogue);
        _editor.MarkAsModified();

        _selectedDialogueIndex = scene.Dialogues.Count - 1;
        UpdateDialogueList();
        UpdatePropertyPanel();
    }

    private void OnResourceDropped(object? sender, ResourceDroppedEventArgs e)
    {
        if (_editor.CurrentProject == null || _activeSceneIndex >= _editor.CurrentProject.Scenes.Count)
            return;

        var scene = _editor.CurrentProject.Scenes[_activeSceneIndex];
        if (_selectedDialogueIndex < 0 || _selectedDialogueIndex >= scene.Dialogues.Count)
            return;

        var dialogue = scene.Dialogues[_selectedDialogueIndex];

        switch (e.Type)
        {
            case ResourceType.Sprite:
                if (dialogue.Sprites.Count > 1)
                    dialogue.Sprites[1].Path = e.Path;
                else
                    dialogue.Sprites.Add(new VNSprite { Path = e.Path, Layer = 1 });
                break;
            case ResourceType.Background:
                if (dialogue.Sprites.Count > 0)
                    dialogue.Sprites[0].Path = e.Path;
                else
                    dialogue.Sprites.Add(new VNSprite { Path = e.Path, Layer = 0 });
                break;
            case ResourceType.Music:
                dialogue.Text = $"[BGM: {System.IO.Path.GetFileName(e.Path)}]";
                break;
            case ResourceType.Voice:
                dialogue.Voice = e.Path;
                break;
        }

        _editor.MarkAsModified();
        UpdatePropertyPanel();
    }

    private void OnNewProjectClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Visunovia 项目|*.tlor",
            Title = "新建项目"
        };

        if (dialog.ShowDialog() == true)
        {
            _editor.NewProject(Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName);
            UpdateUI();
        }
    }

    private void OnQuickNewProjectClicked(object sender, RoutedEventArgs e)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "Visunovia_QuickProject");
        if (Directory.Exists(tempPath))
            Directory.Delete(tempPath, true);
        Directory.CreateDirectory(tempPath);

        _editor.NewProject("快速项目", Path.Combine(tempPath, "project.tlor"));
        CreateDefaultProjectStructure(tempPath);
        UpdateUI();
    }

    private void CreateDefaultProjectStructure(string projectRoot)
    {
        var assetsDir = Path.Combine(projectRoot, "Assets");
        Directory.CreateDirectory(Path.Combine(assetsDir, "Characters"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "Backgrounds"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "Musics"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "Voices"));

        Directory.CreateDirectory(Path.Combine(projectRoot, "Scripts"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Scripts", "Main"));

        Directory.CreateDirectory(Path.Combine(projectRoot, "Locales"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Locales", "Engine"));

        Directory.CreateDirectory(Path.Combine(projectRoot, "Saves"));

        Directory.CreateDirectory(Path.Combine(projectRoot, "Settings"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Settings", "Editor"));

        Directory.CreateDirectory(Path.Combine(projectRoot, "UI"));
    }

    private async void OnOpenProjectClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Visunovia 项目|*.tlor",
            Title = "打开项目"
        };

        if (dialog.ShowDialog() == true)
        {
            await OpenProjectAsync(dialog.FileName);
        }
    }

    private async Task OpenProjectAsync(string path)
    {
        try
        {
            await _editor.LoadProjectAsync(path);
            _recentProjectsManager?.AddProject(path, _editor.CurrentProject?.Metadata.Title ?? "未命名项目");
            UpdateUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法加载项目: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnSaveProjectClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string pathToSave;
            if (string.IsNullOrEmpty(_editor.CurrentProjectPath))
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Visunovia 项目|*.tlor",
                    Title = "保存项目"
                };
                if (saveDialog.ShowDialog() == true)
                {
                    pathToSave = saveDialog.FileName;
                }
                else
                {
                    return;
                }
            }
            else
            {
                pathToSave = _editor.CurrentProjectPath;
            }
            var projectRoot = Path.GetDirectoryName(pathToSave);
            await _editor.SaveProjectAsync(pathToSave);
            StatusLabel.Text = "项目已保存";
            SaveStatusLabel.Text = "已保存";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnPackageProjectClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.HasUnsavedChanges)
        {
            var result = MessageBox.Show("当前项目有未保存的更改，是否先保存？", "未保存的更改",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (!string.IsNullOrEmpty(_editor.CurrentProjectPath))
                    await _editor.SaveProjectAsync(_editor.CurrentProjectPath);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        if (_editor.CurrentProject == null)
        {
            MessageBox.Show("没有可打包的项目", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var keyDialog = new KeyInputDialog();
        var key = await keyDialog.ShowAsync("输入打包密钥", "请输入用于加密游戏包的密钥：");

        if (string.IsNullOrEmpty(key))
        {
            StatusLabel.Text = "打包已取消";
            return;
        }

        StatusLabel.Text = "正在打包项目...";

        try
        {
            var playerTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Visunovia.Player.NW");
            var outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Build");

            if (!Directory.Exists(playerTemplatePath))
            {
                MessageBox.Show($"未找到 NW.js 播放器目录: {playerTemplatePath}\n请确保 Visunovia.Player.NW 存在于应用程序目录下。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusLabel.Text = "打包失败：缺少 NW.js 播放器";
                return;
            }

            var packagingService = new PackagingService(_editor, playerTemplatePath, outputPath);
            packagingService.StatusChanged += (msg) => StatusLabel.Text = msg;
            packagingService.ErrorOccurred += (msg) => MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);

            var success = await packagingService.PackageProjectAsync(key, null);

            if (success)
            {
                var buildDir = Path.Combine(outputPath, _editor.CurrentProject.Metadata.Title);
                MessageBox.Show($"游戏已打包到: {buildDir}", "打包完成", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusLabel.Text = "打包完成";
            }
            else
            {
                StatusLabel.Text = "打包失败";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打包失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusLabel.Text = "打包失败";
        }
    }

    private void OnUndoClicked(object sender, RoutedEventArgs e)
    {
        _editor.Undo();
    }

    private void OnRedoClicked(object sender, RoutedEventArgs e)
    {
        _editor.Redo();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = this
        };
        settingsWindow.ShowDialog();
    }

    private void OnRunProjectClicked(object sender, RoutedEventArgs e)
    {
        if (_editor.HasUnsavedChanges && !string.IsNullOrEmpty(_editor.CurrentProjectPath))
        {
            _editor.SaveProjectAsync(_editor.CurrentProjectPath).Wait();
        }

        PreviewOverlay.Visibility = Visibility.Visible;
        var projectDir = Path.GetDirectoryName(_editor.CurrentProjectPath) ?? "";
        App.UpdateWindowTitle(_editor.CurrentProject?.Metadata.Title ?? "", projectDir, true);

        var project = _editor.CurrentProject;
        if (project?.Scenes != null && project.Scenes.Count > 0)
        {
            PreviewControl.LoadProject(project);
        }
    }

    private void OnPreviewFromSelectedClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedDialogueIndex < 0) return;

        if (_editor.HasUnsavedChanges)
        {
            MessageBox.Show("请先保存项目再进行预览。", "未保存", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PreviewOverlay.Visibility = Visibility.Visible;
        var projectDir = Path.GetDirectoryName(_editor.CurrentProjectPath) ?? "";
        App.UpdateWindowTitle(_editor.CurrentProject?.Metadata.Title ?? "", projectDir, true);

        var project = _editor.CurrentProject;
        if (project?.Scenes != null && _activeSceneIndex < project.Scenes.Count)
        {
            var scene = project.Scenes[_activeSceneIndex];
            PreviewControl.LoadProject(project, _activeSceneIndex, _selectedDialogueIndex);
        }
    }

    private void OnStopPreviewClicked(object sender, RoutedEventArgs e)
    {
        if (PreviewOverlay.Visibility == Visibility.Visible)
        {
            OnPreviewClosed(sender, e);
        }
    }

    private void OnPreviewClosed(object? sender, EventArgs e)
    {
        PreviewOverlay.Visibility = Visibility.Collapsed;
        var projectDir = Path.GetDirectoryName(_editor.CurrentProjectPath) ?? "";
        App.UpdateWindowTitle(_editor.CurrentProject?.Metadata.Title ?? "", projectDir, false);
    }

    private void OnMainWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12)
        {
            DebugConsoleControl.Visibility = DebugConsoleControl.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnSaveProjectClicked(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            OnQuickNewProjectClicked(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnNewProjectClicked(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnOpenProjectClicked(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnUndoClicked(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnRedoClicked(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.Delete)
        {
            OnDeleteDialogueClicked(sender, new RoutedEventArgs());
        }
    }

    private async void OnProjectNameLabelClicked(object sender, MouseButtonEventArgs e)
    {
        if (_recentProjectsManager == null || _recentProjectsManager.RecentProjects.Count == 0)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Visunovia 项目|*.tlor",
                Title = "打开项目"
            };

            if (dialog.ShowDialog() == true)
            {
                await OpenProjectAsync(dialog.FileName);
            }
            return;
        }

        var recentDialog = new Window
        {
            Title = "最近项目",
            Width = 500,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
        };

        var panel = new StackPanel { Margin = new Thickness(20) };

        var titleLabel = new TextBlock
        {
            Text = "选择最近打开的项目",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        };
        panel.Children.Add(titleLabel);

        var listBox = new ListBox
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Margin = new Thickness(0, 0, 0, 16)
        };

        foreach (var project in _recentProjectsManager.RecentProjects)
        {
            var itemPanel = new StackPanel { Margin = new Thickness(8) };
            var nameLabel = new TextBlock
            {
                Text = project.Title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            };
            var pathLabel = new TextBlock
            {
                Text = project.Path,
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153))
            };
            itemPanel.Children.Add(nameLabel);
            itemPanel.Children.Add(pathLabel);
            listBox.Items.Add(new ListBoxItem { Content = itemPanel, Tag = project.Path });
        }

        panel.Children.Add(listBox);

        var buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var openButton = new System.Windows.Controls.Button
        {
            Content = "打开项目",
            Width = 100,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var browseButton = new System.Windows.Controls.Button
        {
            Content = "浏览...",
            Width = 80,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "取消",
            Width = 80,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };

        openButton.Click += (s, args) =>
        {
            if (listBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is string path)
            {
                recentDialog.DialogResult = true;
                recentDialog.Close();
                _ = OpenProjectAsync(path);
            }
        };

        browseButton.Click += (s, args) =>
        {
            recentDialog.DialogResult = false;
            recentDialog.Close();
            var dialog = new OpenFileDialog
            {
                Filter = "Visunovia 项目|*.tlor",
                Title = "打开项目"
            };

            if (dialog.ShowDialog() == true)
            {
                _ = OpenProjectAsync(dialog.FileName);
            }
        };

        cancelButton.Click += (s, args) =>
        {
            recentDialog.DialogResult = false;
            recentDialog.Close();
        };

        buttonPanel.Children.Add(openButton);
        buttonPanel.Children.Add(browseButton);
        buttonPanel.Children.Add(cancelButton);
        panel.Children.Add(buttonPanel);

        recentDialog.Content = panel;
        recentDialog.ShowDialog();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
    }
}