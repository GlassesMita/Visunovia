using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Visunovia.Engine.Services;

namespace Visunovia.Engine.Debug;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Frontend
}

public class LogEntry : INotifyPropertyChanged
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public string FormattedMessage => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";

    public string LevelColor => Level switch
    {
        LogLevel.Debug => "#6B7280",
        LogLevel.Info => "#3B82F6",
        LogLevel.Warning => "#F59E0B",
        LogLevel.Error => "#EF4444",
        LogLevel.Frontend => "#8B5CF6",
        _ => "#FFFFFF"
    };

    public Brush LevelBrush => Level switch
    {
        LogLevel.Debug => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280")),
        LogLevel.Info => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6")),
        LogLevel.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B")),
        LogLevel.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),
        LogLevel.Frontend => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B5CF6")),
        _ => Brushes.White
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class DebugConsoleService : INotifyPropertyChanged
{
    private static DebugConsoleService? _instance;
    public static DebugConsoleService Instance => _instance ??= new DebugConsoleService();

    private bool _isVisible = true;
    private int _maxEntries = 500;
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll != value)
            {
                _autoScroll = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<LogEntry>? OnLogAdded;

    private DebugConsoleService()
    {
#if DEBUG
        _isVisible = true;
#else
        _isVisible = false;
#endif
        Logger.Initialize();
    }

    public void Log(LogLevel level, string message, string source = "")
    {
        var loggerLevel = level switch
        {
            LogLevel.Debug => Logger.LogLevel.Debug,
            LogLevel.Info => Logger.LogLevel.Info,
            LogLevel.Warning => Logger.LogLevel.Warning,
            LogLevel.Error => Logger.LogLevel.Error,
            LogLevel.Frontend => Logger.LogLevel.Extra,
            _ => Logger.LogLevel.Info
        };

        var fullMessage = string.IsNullOrEmpty(source) ? message : $"[{source}] {message}";
        Logger.Instance.Log(fullMessage, loggerLevel);

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };

        Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry);

            while (LogEntries.Count > _maxEntries)
            {
                LogEntries.RemoveAt(0);
            }

            OnLogAdded?.Invoke(entry);
        });
    }

    public void Debug(string message, string source = "") => Log(LogLevel.Debug, message, source);
    public void Info(string message, string source = "") => Log(LogLevel.Info, message, source);
    public void Warning(string message, string source = "") => Log(LogLevel.Warning, message, source);
    public void Error(string message, string source = "") => Log(LogLevel.Error, message, source);
    public void Frontend(string message, string source = "") => Log(LogLevel.Frontend, message, source);

    public void Clear()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Clear();
        });
    }

    public void Toggle()
    {
        IsVisible = !IsVisible;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
