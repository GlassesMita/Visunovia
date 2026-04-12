using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Visunovia.Player.WPF.Player;
using Visunovia.Player.WPF.UI;

namespace Visunovia.Player.WPF;

public partial class MainWindow : Window
{
    private readonly PlayerEngine _engine;
    private readonly DispatcherTimer _typewriterTimer;
    private string _fullDialogueText = "";
    private int _visibleCharCount = 0;
    private bool _isTyping = false;

    public MainWindow()
    {
        InitializeComponent();
        _engine = new PlayerEngine();
        WindowHandleProvider.CurrentHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _typewriterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _typewriterTimer.Tick += OnTypewriterTick;

        _engine.SpeakerChanged += OnSpeakerChanged;
        _engine.DialogueTextChanged += OnDialogueTextChanged;
        _engine.BackgroundChanged += OnBackgroundChanged;
        _engine.ChoicesChanged += OnChoicesChanged;
        _engine.BgmChanged += OnBgmChanged;
        _engine.SceneEnded += OnSceneEnded;
        _engine.SpritesChanged += OnSpritesChanged;

        ChoicePanelControl.ChoiceSelected += OnChoiceSelected;
    }

    public void LoadGame(string lorePath, string password)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingText.Text = "正在解密资源包...";

        bool loaded = false;
        string loadMode = "LORE";

        try
        {
            if (_engine.LoadGame(lorePath, password))
            {
                loaded = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LORE load failed: {ex.Message}");
        }

        if (!loaded)
        {
            var zipPath = Path.ChangeExtension(lorePath, ".zip");
            if (File.Exists(zipPath))
            {
                LoadingText.Text = "正在加载 ZIP 资源包...";
                try
                {
                    if (_engine.LoadGameFromZip(zipPath))
                    {
                        loaded = true;
                        loadMode = "ZIP";
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ZIP load failed: {ex.Message}");
                }
            }
        }

        if (loaded)
        {
            var titleSuffix = loadMode == "ZIP" ? " - [LORE 加载失败，本次加载 ZIP]" : "";
            Title = "Visunovia Player" + titleSuffix;

            LoadingText.Text = "正在加载游戏...";
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                DialoguePanelControl.Visibility = Visibility.Visible;
                _engine.Start();
            }), DispatcherPriority.Background);
        }
        else
        {
            ShowError("无法加载游戏数据包，密码可能错误或文件已损坏。");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessage.Text = message;
        ErrorOverlay.Visibility = Visibility.Visible;
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnSpeakerChanged(string speaker)
    {
        DialoguePanelControl.Speaker = speaker;
    }

    private void OnDialogueTextChanged(string text)
    {
        _fullDialogueText = text;
        _visibleCharCount = 0;
        DialoguePanelControl.DialogueText = "";
        _isTyping = true;
        _typewriterTimer.Start();
    }

    private void OnTypewriterTick(object? sender, EventArgs e)
    {
        if (_visibleCharCount < _fullDialogueText.Length)
        {
            _visibleCharCount++;
            DialoguePanelControl.DialogueText = _fullDialogueText.Substring(0, _visibleCharCount);
        }
        else
        {
            _typewriterTimer.Stop();
            _isTyping = false;
        }
    }

    private void OnBackgroundChanged(string? bgPath)
    {
        if (string.IsNullOrEmpty(bgPath))
        {
            BackgroundImage.Source = null;
            return;
        }

        try
        {
            var imagePath = $"Assets/Backgrounds/{bgPath}";
            var bitmap = _engine.Resources.GetImage(imagePath);
            if (bitmap != null)
            {
                BackgroundImage.Source = bitmap;
            }
            else
            {
                var bgFromRoot = _engine.Resources.GetImage(bgPath);
                BackgroundImage.Source = bgFromRoot;
            }
        }
        catch
        {
            BackgroundImage.Source = null;
        }
    }

    private void OnChoicesChanged(System.Collections.Generic.List<ChoiceData> choices)
    {
        if (choices.Count > 0)
        {
            ChoicePanelControl.Choices = choices;
            ChoicePanelControl.Visibility = Visibility.Visible;
            DialoguePanelControl.IsDialogueVisible = false;
        }
        else
        {
            ChoicePanelControl.Visibility = Visibility.Collapsed;
            DialoguePanelControl.IsDialogueVisible = true;
        }
    }

    private void OnBgmChanged(string bgmPath)
    {
        try
        {
            if (!string.IsNullOrEmpty(bgmPath))
            {
                var parts = bgmPath.Split('|');
                var path = parts[0].Trim();
                var musicPath = $"Assets/Musics/{path}";
                var binary = _engine.Resources.GetBinary(musicPath);

                if (binary != null)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"visunovia_bgm_{Guid.NewGuid()}.tmp");
                    File.WriteAllBytes(tempPath, binary);
                    BgmPlayer.Source = new Uri(tempPath);
                    BgmPlayer.Play();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to play BGM: {ex.Message}");
        }
    }

    private void OnSpritesChanged(System.Collections.Generic.List<VNSprite> sprites)
    {
        try
        {
            CharacterCanvas.Children.Clear();

            double canvasWidth = CharacterCanvas.ActualWidth;
            if (canvasWidth <= 0) canvasWidth = 1280;

            foreach (var sprite in sprites)
            {
                if (string.IsNullOrEmpty(sprite.Path))
                    continue;

                var imagePath = $"Assets/Characters/{sprite.Path}";
                var bitmap = _engine.Resources.GetImage(imagePath);

                if (bitmap != null)
                {
                    var image = new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        Width = 400
                    };

                    var position = sprite.Position?.ToLower() ?? "center";
                    double left;

                    switch (position)
                    {
                        case "left":
                            left = 100;
                            break;
                        case "right":
                            left = canvasWidth - 500;
                            break;
                        case "center":
                        default:
                            left = (canvasWidth - 400) / 2;
                            break;
                    }

                    System.Windows.Controls.Canvas.SetLeft(image, left);
                    System.Windows.Controls.Canvas.SetBottom(image, 50);

                    CharacterCanvas.Children.Add(image);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load sprites: {ex.Message}");
        }
    }

    private void OnSceneEnded()
    {
        MessageBox.Show("游戏已结束。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnChoiceSelected(int index)
    {
        _engine.SelectChoice(index);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Advance();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space || e.Key == Key.Enter || e.Key == Key.Z)
        {
            Advance();
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void Advance()
    {
        if (_isTyping)
        {
            _typewriterTimer.Stop();
            _isTyping = false;
            DialoguePanelControl.DialogueText = _fullDialogueText;
            _visibleCharCount = _fullDialogueText.Length;
            return;
        }

        if (ChoicePanelControl.Visibility == Visibility.Visible)
            return;

        _engine.Advance();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}