using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Visunovia.Controls;

public enum ResourceType
{
    Sprite,
    Background,
    Music,
    Voice,
    Font,
    Other
}

public class ResourceItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public ResourceType Type { get; set; }

    public ResourceItem() { }

    public ResourceItem(string name, string path, ResourceType type)
    {
        Name = name;
        Path = path;
        Type = type;
        ThumbnailPath = type == ResourceType.Music || type == ResourceType.Voice || type == ResourceType.Font || type == ResourceType.Other
            ? null : path;
    }
}

public partial class ResourceManagerControl : UserControl
{
    public ObservableCollection<ResourceItem> Sprites { get; } = new();
    public ObservableCollection<ResourceItem> Backgrounds { get; } = new();
    public ObservableCollection<ResourceItem> Musics { get; } = new();
    public ObservableCollection<ResourceItem> Voices { get; } = new();
    public ObservableCollection<ResourceItem> Fonts { get; } = new();
    public ObservableCollection<ResourceItem> Others { get; } = new();

    public event EventHandler<ResourceDroppedEventArgs>? ResourceDropped;
    public event EventHandler<ResourceItem>? ResourceDoubleClick;

    public ResourceManagerControl()
    {
        InitializeComponent();

        SpritesList.ItemsSource = Sprites;
        BackgroundsList.ItemsSource = Backgrounds;
        MusicsList.ItemsSource = Musics;
        VoicesList.ItemsSource = Voices;
        FontsList.ItemsSource = Fonts;
        OthersList.ItemsSource = Others;
    }

    public void AddResource(string filePath, ResourceType type)
    {
        var name = System.IO.Path.GetFileName(filePath);
        var item = new ResourceItem(name, filePath, type);

        switch (type)
        {
            case ResourceType.Sprite:
                Sprites.Add(item);
                break;
            case ResourceType.Background:
                Backgrounds.Add(item);
                break;
            case ResourceType.Music:
                Musics.Add(item);
                break;
            case ResourceType.Voice:
                Voices.Add(item);
                break;
            case ResourceType.Font:
                Fonts.Add(item);
                break;
            case ResourceType.Other:
                Others.Add(item);
                break;
        }
    }

    public void Clear()
    {
        Sprites.Clear();
        Backgrounds.Clear();
        Musics.Clear();
        Voices.Clear();
        Fonts.Clear();
        Others.Clear();
    }

    private void OnSpriteMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && SpritesList.SelectedItem is ResourceItem item)
        {
            DragDrop.DoDragDrop(SpritesList, item.Path, DragDropEffects.Copy);
        }
    }

    private void OnBackgroundMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && BackgroundsList.SelectedItem is ResourceItem item)
        {
            DragDrop.DoDragDrop(BackgroundsList, item.Path, DragDropEffects.Copy);
        }
    }

    private void OnMusicMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && MusicsList.SelectedItem is ResourceItem item)
        {
            DragDrop.DoDragDrop(MusicsList, item.Path, DragDropEffects.Copy);
        }
    }

    private void OnVoiceMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && VoicesList.SelectedItem is ResourceItem item)
        {
            DragDrop.DoDragDrop(VoicesList, item.Path, DragDropEffects.Copy);
        }
    }

    private void OnFontMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && FontsList.SelectedItem is ResourceItem item)
        {
            DragDrop.DoDragDrop(FontsList, item.Path, DragDropEffects.Copy);
        }
    }

    private void OnOtherMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && OthersList.SelectedItem is ResourceItem item)
        {
            DragDrop.DoDragDrop(OthersList, item.Path, DragDropEffects.Copy);
        }
    }

    private void OnSpriteDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var path = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(path))
            {
                ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(path, ResourceType.Sprite));
            }
        }
        ResetDropHighlights();
    }

    private void OnBackgroundDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var path = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(path))
            {
                ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(path, ResourceType.Background));
            }
        }
        ResetDropHighlights();
    }

    private void OnMusicDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var path = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(path))
            {
                ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(path, ResourceType.Music));
            }
        }
        ResetDropHighlights();
    }

    private void OnVoiceDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var path = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(path))
            {
                ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(path, ResourceType.Voice));
            }
        }
        ResetDropHighlights();
    }

    private void OnFontDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var path = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(path))
            {
                ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(path, ResourceType.Font));
            }
        }
        ResetDropHighlights();
    }

    private void OnOtherDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            var path = e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrEmpty(path))
            {
                ResourceDropped?.Invoke(this, new ResourceDroppedEventArgs(path, ResourceType.Other));
            }
        }
        ResetDropHighlights();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
            if (sender is ListBox listBox)
            {
                listBox.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                listBox.BorderThickness = new Thickness(2);
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        ResetDropHighlights();
    }

    private void ResetDropHighlights()
    {
        SpritesList.BorderThickness = new Thickness(0);
        BackgroundsList.BorderThickness = new Thickness(0);
        MusicsList.BorderThickness = new Thickness(0);
        VoicesList.BorderThickness = new Thickness(0);
        FontsList.BorderThickness = new Thickness(0);
        OthersList.BorderThickness = new Thickness(0);
    }

    private void OnResourceDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is ResourceItem item)
        {
            ResourceDoubleClick?.Invoke(this, item);
        }
    }

    private void OnAddResourceClicked(object sender, MouseButtonEventArgs e)
    {
        var currentTab = ResourceTabs.SelectedIndex;
        ResourceType type = currentTab switch
        {
            0 => ResourceType.Sprite,
            1 => ResourceType.Background,
            2 => ResourceType.Music,
            3 => ResourceType.Voice,
            4 => ResourceType.Font,
            _ => ResourceType.Other
        };

        var filter = type switch
        {
            ResourceType.Sprite or ResourceType.Background => "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
            ResourceType.Music => "音频文件|*.mp3;*.wav;*.ogg;*.flac",
            ResourceType.Voice => "音频文件|*.mp3;*.wav;*.ogg;*.m4a",
            ResourceType.Font => "字体文件|*.ttf;*.otf;*.woff;*.woff2",
            _ => "所有文件|*.*"
        };

        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = "选择资源文件",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                AddResource(file, type);
            }
        }
    }
}

public class ResourceDroppedEventArgs : EventArgs
{
    public string Path { get; }
    public ResourceType Type { get; }

    public ResourceDroppedEventArgs(string path, ResourceType type)
    {
        Path = path;
        Type = type;
    }
}
