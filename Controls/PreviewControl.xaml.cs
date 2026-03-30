using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Visunovia.Engine.Core;
using Visunovia.Engine.Editor;

namespace Visunovia.Controls;

public partial class PreviewControl : UserControl
{
    private VNEngine? _engine;
    private VNProject? _project;
    private List<VNDialogue> _dialogues = new();
    private int _currentDialogueIndex = -1;
    private bool _isWaitingForChoice = false;
    private bool _isPreviewEnded = false;
    private bool _isFullscreen = false;
    private EditorService? _editor;

    public event EventHandler? PreviewClosed;

    public PreviewControl()
    {
        InitializeComponent();

        this.MouseLeftButtonUp += OnMouseClicked;
    }

    public void SetEditor(EditorService editor)
    {
        _editor = editor;
    }

    public void LoadProject(VNProject project, int sceneIndex = 0, int startDialogueIndex = 0)
    {
        _project = project;
        _engine = new VNEngine();
        _isPreviewEnded = false;
        _isWaitingForChoice = false;
        _isFullscreen = false;
        _currentDialogueIndex = startDialogueIndex - 1;

        BgmPlayer?.Stop();
        BgmPlayer.Source = null;
        BackgroundImage.Source = null;
        CharacterImage.Source = null;
        CharacterImage.Visibility = Visibility.Collapsed;
        DialogueBox.Visibility = Visibility.Collapsed;
        ChoicesPanel.Visibility = Visibility.Collapsed;

        if (project.Scenes != null && sceneIndex < project.Scenes.Count)
        {
            var scene = project.Scenes[sceneIndex];
            _dialogues = scene.Dialogues ?? new List<VNDialogue>();

            LoadBackground(scene.Background);
            Advance();
        }
    }

    private void LoadBackground(string? backgroundPath)
    {
        if (string.IsNullOrEmpty(backgroundPath))
        {
            BackgroundImage.Source = null;
            return;
        }

        try
        {
            var projectRoot = Path.GetDirectoryName(_editor?.CurrentProjectPath);
            if (string.IsNullOrEmpty(projectRoot)) return;

            var bgPath = Path.Combine(projectRoot, "Assets", "Backgrounds", backgroundPath);

            if (File.Exists(bgPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(bgPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                BackgroundImage.Source = bitmap;
            }
            else
            {
                BackgroundImage.Source = null;
            }
        }
        catch
        {
            BackgroundImage.Source = null;
        }
    }

    private void LoadCharacter(string? characterPath)
    {
        if (string.IsNullOrEmpty(characterPath))
        {
            CharacterImage.Source = null;
            return;
        }

        try
        {
            var projectRoot = Path.GetDirectoryName(_editor?.CurrentProjectPath);
            if (string.IsNullOrEmpty(projectRoot)) return;

            var charPath = Path.Combine(projectRoot, "Assets", "Characters", characterPath);

            if (File.Exists(charPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(charPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                CharacterImage.Source = bitmap;
            }
            else
            {
                CharacterImage.Source = null;
            }
        }
        catch
        {
            CharacterImage.Source = null;
        }
    }

    private void OnMouseClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Advance();
    }

    private void Advance()
    {
        if (_dialogues.Count == 0 || _isPreviewEnded)
        {
            OnPreviewEnd();
            return;
        }

        _currentDialogueIndex++;

        if (_currentDialogueIndex >= _dialogues.Count)
        {
            OnPreviewEnd();
            return;
        }

        var dialogue = _dialogues[_currentDialogueIndex];

        switch (dialogue.Type)
        {
            case VNDialogueType.Event:
                ProcessEventNode(dialogue);
                return;

            case VNDialogueType.Branch:
                ProcessBranchNode(dialogue);
                return;

            case VNDialogueType.Dialogue:
            default:
                SpeakerLabel.Text = dialogue.Speaker ?? "";
                DialogueLabel.Text = dialogue.Text ?? "";
                DialogueBox.Visibility = Visibility.Visible;
                ChoicesPanel.Visibility = Visibility.Collapsed;
                _isWaitingForChoice = false;
                break;
        }
    }

    private void ProcessEventNode(VNDialogue dialogue)
    {
        if (dialogue.Event == null)
        {
            Advance();
            return;
        }

        var projectRoot = Path.GetDirectoryName(_editor?.CurrentProjectPath);

        switch (dialogue.Event.EventType)
        {
            case VNEventType.ChangeBackground:
                var bgPath = dialogue.Event.Parameters.TryGetValue("BackgroundPath", out var bp) == true ? bp?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(bgPath) && !string.IsNullOrEmpty(projectRoot))
                {
                    var fullPath = Path.Combine(projectRoot, "Assets", "Backgrounds", bgPath);
                    LoadBackground(File.Exists(fullPath) ? bgPath : "");
                }
                Advance();
                break;

            case VNEventType.ChangeBgm:
                var bgmPath = dialogue.Event.Parameters.TryGetValue("BgmPath", out var mp) == true ? mp?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(bgmPath) && !string.IsNullOrEmpty(projectRoot))
                {
                    var fullPath = Path.Combine(projectRoot, "Assets", "Musics", bgmPath);
                    if (File.Exists(fullPath))
                    {
                        BgmPlayer.Source = new Uri(fullPath);
                        BgmPlayer.Volume = 0.5;
                        BgmPlayer.Play();
                    }
                }
                Advance();
                break;

            case VNEventType.ShowCharacter:
                var charId = dialogue.Event.Parameters.TryGetValue("CharacterId", out var cid) == true ? cid?.ToString() ?? "" : "";
                var expression = dialogue.Event.Parameters.TryGetValue("Expression", out var expr) == true ? expr?.ToString() ?? "default" : "default";
                if (!string.IsNullOrEmpty(charId) && !string.IsNullOrEmpty(projectRoot))
                {
                    var fullPath = Path.Combine(projectRoot, "Assets", "Characters", $"{charId}_{expression}.png");
                    if (File.Exists(fullPath))
                    {
                        LoadCharacter($"{charId}_{expression}");
                    }
                    else
                    {
                        var fallbackPath = Path.Combine(projectRoot, "Assets", "Characters", $"{charId}.png");
                        if (File.Exists(fallbackPath))
                        {
                            LoadCharacter(charId);
                        }
                    }
                }
                CharacterImage.Visibility = Visibility.Visible;
                Advance();
                break;

            case VNEventType.HideCharacter:
                LoadCharacter("");
                CharacterImage.Visibility = Visibility.Collapsed;
                Advance();
                break;

            case VNEventType.WaitSeconds:
                var seconds = 1.0;
                if (dialogue.Event.Parameters.TryGetValue("Seconds", out var secObj))
                {
                    if (secObj is double secD) seconds = secD;
                    else if (secObj is int secI) seconds = secI;
                    else if (secObj is string secS && double.TryParse(secS, out var secParsed)) seconds = secParsed;
                }
                this.Dispatcher.InvokeAsync(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds));
                    Advance();
                });
                return;

            case VNEventType.JumpScene:
            case VNEventType.SetVariable:
            case VNEventType.PlaySound:
            case VNEventType.Pause:
            case VNEventType.Custom:
            default:
                Advance();
                break;
        }
    }

    private void ProcessBranchNode(VNDialogue dialogue)
    {
        if (dialogue.Branch?.Choices == null || dialogue.Branch.Choices.Count == 0)
        {
            Advance();
            return;
        }

        SpeakerLabel.Text = "请选择：";
        DialogueLabel.Text = "";

        ChoicesContainer.Children.Clear();
        foreach (var choice in dialogue.Branch.Choices)
        {
            var btn = new Button
            {
                Content = choice.Text,
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(10, 6, 10, 6)
            };
            var targetScene = choice.TargetScene;
            btn.Click += (s, e) => OnChoiceSelected(targetScene);
            ChoicesContainer.Children.Add(btn);
        }

        DialogueBox.Visibility = Visibility.Collapsed;
        ChoicesPanel.Visibility = Visibility.Visible;
        _isWaitingForChoice = true;
    }

    private void OnChoiceSelected(string targetScene)
    {
        if (!string.IsNullOrEmpty(targetScene) && _project?.Scenes != null)
        {
            var target = _project.Scenes.FirstOrDefault(s => s.Id == targetScene);
            if (target != null)
            {
                _dialogues = target.Dialogues ?? new List<VNDialogue>();
                _currentDialogueIndex = -1;
            }
        }
        ChoicesPanel.Visibility = Visibility.Collapsed;
        _isWaitingForChoice = false;
        Advance();
    }

    private void OnPreviewEnd()
    {
        _isPreviewEnded = true;
        DialogueBox.Visibility = Visibility.Collapsed;
        ChoicesPanel.Visibility = Visibility.Collapsed;

        MessageBox.Show("预览结束", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        BgmPlayer?.Stop();
        BgmPlayer.Source = null;
        CharacterImage.Source = null;
        CharacterImage.Visibility = Visibility.Collapsed;
        _isPreviewEnded = false;
        _currentDialogueIndex = -1;
        PreviewClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnFullscreenClicked(object sender, RoutedEventArgs e)
    {
        _isFullscreen = !_isFullscreen;
    }

    private void OnPreviousClicked(object sender, RoutedEventArgs e)
    {
        if (_currentDialogueIndex > 0)
        {
            _currentDialogueIndex -= 2;
            Advance();
        }
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        Advance();
    }
}
