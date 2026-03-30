using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Visunovia.Engine.Services;

public class Logger
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Extra
    }

    private static Logger? _instance;
    public static Logger Instance => _instance ??= new Logger();

    private static string? _logFilePath;
    private static readonly object _lockObject = new object();

    private static string? _lastNotificationKey;
    private static DateTime _lastNotificationTime = DateTime.MinValue;
    private const int NOTIFICATION_COOLDOWN_MS = 3000;

    private static readonly List<BrowserNotification> _notificationQueue = new();
    private static readonly object _notificationLock = new();

    private static bool _initialized;
    private static readonly object _initLock = new();

    private LogLevel _minLevel = LogLevel.Debug;

    public LogLevel MinimumLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    static Logger()
    {
        InitializeInternal();
    }

    private Logger()
    {
    }

    private static void InitializeInternal()
    {
        try
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logDirectory = Path.Combine(baseDirectory, "Logs");

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            _logFilePath = Path.Combine(logDirectory, "Visunovia.log");

            if (File.Exists(_logFilePath))
            {
                var backupPath = Path.Combine(logDirectory, $"Visunovia_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                try
                {
                    File.Copy(_logFilePath, backupPath, true);
                }
                catch
                {
                }
            }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            Instance.Log("Logger initialized successfully", LogLevel.Info);
            Instance.Log($"Log file: {_logFilePath}", LogLevel.Debug);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize logger: {ex.Message}");
            _logFilePath = null;
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var message = exception != null
            ? $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"
            : "未知错误";

        Instance.Error($"[AppDomain] 未处理的异常: {message}");

        if (e.IsTerminating)
        {
            Instance.Error("[AppDomain] 应用程序即将终止");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception;
        var message = $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";

        Instance.Error($"[Task] Task未观察的异常: {message}");

        e.SetObserved();
    }

    public static void Initialize()
    {
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (level < _minLevel)
        {
            return;
        }

        if (string.IsNullOrEmpty(_logFilePath))
        {
            InitializeInternal();
        }

        string tag = GetLevelShortTag(level);
        string logMessage = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] [{tag}] {message}";

        lock (_lockObject)
        {
            try
            {
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    File.AppendAllText(_logFilePath, logMessage + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        Console.WriteLine(logMessage);
    }

    public void Log(string format, LogLevel level, params object[] args)
    {
        string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
        Log(message, level);
    }

    public void Debug(string message) => Log(message, LogLevel.Debug);
    public void Debug(string format, params object[] args) => Log(format, LogLevel.Debug, args);

    public void Info(string message) => Log(message, LogLevel.Info);
    public void Info(string format, params object[] args) => Log(format, LogLevel.Info, args);

    public void Warning(string message) => Log(message, LogLevel.Warning);
    public void Warning(string format, params object[] args) => Log(format, LogLevel.Warning, args);

    public void Error(string message) => Log(message, LogLevel.Error);
    public void Error(string format, params object[] args) => Log(format, LogLevel.Error, args);

    public void Extra(string message) => Log(message, LogLevel.Extra);
    public void Extra(string format, params object[] args) => Log(format, LogLevel.Extra, args);

    public void LogException(Exception ex, string? message = null)
    {
        string fullMessage = message ?? "Exception occurred";
        fullMessage += $"\n  Type: {ex.GetType().Name}";
        fullMessage += $"\n  Message: {ex.Message}";
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            fullMessage += $"\n  StackTrace:\n{ex.StackTrace}";
        }
        Log(fullMessage, LogLevel.Error);
    }

    public void LogException(string format, Exception ex, params object[] args)
    {
        string message = args != null && args.Length > 0 ? string.Format(format, args) : format;
        LogException(ex, message);
    }

    private static string GetLevelShortTag(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => "D",
            LogLevel.Info => "I",
            LogLevel.Warning => "W",
            LogLevel.Error => "E",
            LogLevel.Extra => "X",
            _ => "I"
        };
    }

    public static string? GetLogFilePath() => _logFilePath;

    public static string? GetLogDirectory()
    {
        if (string.IsNullOrEmpty(_logFilePath))
            return null;
        return Path.GetDirectoryName(_logFilePath);
    }

    public static void SendBrowserNotification(string title, string message, string type = "warning")
    {
        var notification = new BrowserNotification
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.Now.ToString("o"),
            Key = $"{title}:{message}"
        };

        lock (_notificationLock)
        {
            if (ShouldSkipNotification(notification.Key))
            {
                return;
            }

            _notificationQueue.Add(notification);
            _lastNotificationKey = notification.Key;
            _lastNotificationTime = DateTime.Now;
        }
    }

    private static bool ShouldSkipNotification(string key)
    {
        if (_lastNotificationKey == key)
        {
            var elapsed = DateTime.Now - _lastNotificationTime;
            if (elapsed.TotalMilliseconds < NOTIFICATION_COOLDOWN_MS)
            {
                return true;
            }
        }
        return false;
    }

    public static List<BrowserNotification> GetPendingNotifications()
    {
        lock (_notificationLock)
        {
            var pending = new List<BrowserNotification>(_notificationQueue);
            _notificationQueue.Clear();
            return pending;
        }
    }

    public static void ClearNotifications()
    {
        lock (_notificationLock)
        {
            _notificationQueue.Clear();
        }
    }
}

public class BrowserNotification
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Type { get; set; } = "info";
    public string Timestamp { get; set; } = "";
    public string Key { get; set; } = "";
}
