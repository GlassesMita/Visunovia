using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Visunovia.Engine.Debug;

namespace Visunovia.Controls;

public partial class DebugPanel : UserControl
{
    private bool _isExpanded = true;

    public DebugPanel()
    {
        InitializeComponent();

        LogItemsControl.ItemsSource = DebugConsoleService.Instance.LogEntries;

        DebugConsoleService.Instance.OnLogAdded += OnLogEntryAdded;

        DebugConsoleService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DebugConsoleService.IsVisible))
            {
                Dispatcher.Invoke(() => Visibility = DebugConsoleService.Instance.IsVisible ? Visibility.Visible : Visibility.Collapsed);
            }
        };

#if DEBUG
        Visibility = DebugConsoleService.Instance.IsVisible ? Visibility.Visible : Visibility.Collapsed;
#else
        Visibility = Visibility.Collapsed;
#endif
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            Toggle();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        DebugConsoleService.Instance.Clear();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        Toggle();
    }

    private void Toggle()
    {
        _isExpanded = !_isExpanded;
        LogScrollViewer.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ToggleButton.Content = _isExpanded ? "▼" : "▲";
    }

    private void OnLogEntryAdded(LogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            if (_isExpanded && AutoScrollCheckBox.IsChecked == true)
            {
                LogScrollViewer.ScrollToEnd();
            }
        });
    }
}
