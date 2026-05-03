using System.Windows;

namespace Visunovia;

public partial class App : Application
{
    private static Window? _mainWindow;
    private static bool _isPreviewMode = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"Unhandled exception: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Dispatcher exception: {e.Exception.Message}", "Error",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    public static void UpdateWindowTitle(string projectTitle, string projectPath, bool hasUnsavedChanges)
    {
        if (_mainWindow == null)
            return;

        _isPreviewMode = false;

        string title;
        if (string.IsNullOrEmpty(projectTitle))
        {
            title = "Visunovia";
        }
        else
        {
            var fullTitle = string.IsNullOrEmpty(projectPath)
                ? $"Visunovia - {projectTitle}"
                : $"Visunovia - {projectTitle} ({projectPath})";

            title = hasUnsavedChanges ? $"{fullTitle} *" : fullTitle;
        }

        _mainWindow.Title = title;
    }

    public static void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    public static void SetPreviewMode(bool isPreview)
    {
        if (_mainWindow == null)
            return;

        _isPreviewMode = isPreview;
    }
}
