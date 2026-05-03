using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Visunovia.Engine.Localization;

namespace Visunovia.Controls;

public class KeyInputDialog
{
    public string? Result { get; private set; }
    public bool Confirmed { get; private set; }

    public Task<string?> ShowAsync(string titleKey = "Dialog_EnterKey", string messageKey = "Dialog_EnterPackageKey")
    {
        Result = null;
        Confirmed = false;

        var tcs = new TaskCompletionSource<string?>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var loc = LocalizationService.Instance;
            var dialog = new Window
            {
                Title = loc.GetString(titleKey),
                Width = 500,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D2D"))
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };

            var label = new System.Windows.Controls.TextBlock
            {
                Text = loc.GetString(messageKey),
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 15)
            };
            System.Windows.Controls.Grid.SetRow(label, 0);

            var textBox = new System.Windows.Controls.TextBox
            {
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 15)
            };
            System.Windows.Controls.Grid.SetRow(textBox, 1);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);

            var okButton = new System.Windows.Controls.Button
            {
                Content = loc.GetString("Dialog_Confirm"),
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = loc.GetString("Dialog_Cancel"),
                Width = 100,
                Height = 36,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };

            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    MessageBox.Show(dialog, loc.GetString("Dialog_KeyCannotBeEmpty"), loc.GetString("Dialog_Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Confirmed = true;
                Result = textBox.Text;
                dialog.Close();
            };

            cancelButton.Click += (s, e) =>
            {
                dialog.Close();
            };

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    okButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            dialog.Closing += (s, e) => tcs.TrySetResult(Result);
            dialog.ShowDialog();
        });

        return tcs.Task;
    }
}
