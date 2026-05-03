using System.Windows;
using System.Windows.Controls;

namespace Visunovia.Player.WPF.UI;

public partial class DialoguePanel : UserControl
{
    public static readonly DependencyProperty SpeakerProperty =
        DependencyProperty.Register(nameof(Speaker), typeof(string), typeof(DialoguePanel),
            new PropertyMetadata("", OnSpeakerChanged));

    public static readonly DependencyProperty DialogueTextProperty =
        DependencyProperty.Register(nameof(DialogueText), typeof(string), typeof(DialoguePanel),
            new PropertyMetadata("", OnDialogueTextChanged));

    public static readonly DependencyProperty IsDialogueVisibleProperty =
        DependencyProperty.Register(nameof(IsDialogueVisible), typeof(bool), typeof(DialoguePanel),
            new PropertyMetadata(true, OnIsDialogueVisibleChanged));

    public string Speaker
    {
        get => (string)GetValue(SpeakerProperty);
        set => SetValue(SpeakerProperty, value);
    }

    public string DialogueText
    {
        get => (string)GetValue(DialogueTextProperty);
        set => SetValue(DialogueTextProperty, value);
    }

    public bool IsDialogueVisible
    {
        get => (bool)GetValue(IsDialogueVisibleProperty);
        set => SetValue(IsDialogueVisibleProperty, value);
    }

    public DialoguePanel()
    {
        InitializeComponent();
    }

    private static void OnSpeakerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DialoguePanel panel)
        {
            panel.SpeakerText.Text = e.NewValue as string ?? "";
        }
    }

    private static void OnDialogueTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DialoguePanel panel)
        {
            panel.DialogueTextBlock.Text = e.NewValue as string ?? "";
        }
    }

    private static void OnIsDialogueVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DialoguePanel panel)
        {
            panel.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
