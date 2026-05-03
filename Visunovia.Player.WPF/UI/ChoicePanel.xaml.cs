using System.Windows;
using System.Windows.Controls;
using Visunovia.Player.WPF.Player;

namespace Visunovia.Player.WPF.UI;

public partial class ChoicePanel : UserControl
{
    public static readonly DependencyProperty ChoicesProperty =
        DependencyProperty.Register(nameof(Choices), typeof(List<ChoiceData>), typeof(ChoicePanel),
            new PropertyMetadata(null, OnChoicesChanged));

    public List<ChoiceData>? Choices
    {
        get => (List<ChoiceData>?)GetValue(ChoicesProperty);
        set => SetValue(ChoicesProperty, value);
    }

    public event Action<int>? ChoiceSelected;

    public ChoicePanel()
    {
        InitializeComponent();
    }

    private static void OnChoicesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ChoicePanel panel && e.NewValue is List<ChoiceData> choices)
        {
            panel.ChoicesItemsControl.ItemsSource = choices;
        }
    }

    private void OnChoiceButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string target)
        {
            var index = ChoicesItemsControl.Items.IndexOf(
                ChoicesItemsControl.Items.Cast<object>().FirstOrDefault(
                    item => item is ChoiceData cd && cd.Target == target));

            if (index >= 0)
            {
                ChoiceSelected?.Invoke(index);
            }
        }
    }
}