using System;
using Visunovia.Engine.Services;

namespace Visunovia.Engine.Debug;

public static class GlobalExceptionHandler
{
    public static void Initialize()
    {
        Logger.Initialize();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        var message = exception != null
            ? $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}"
            : "未知错误";

        Logger.Instance.Error($"[AppDomain] 未处理的异常: {message}");
        Logger.Instance.LogException(exception ?? new Exception("Unknown error"), "[AppDomain]");

        if (e.IsTerminating)
        {
            Logger.Instance.Error("[AppDomain] 应用程序即将终止");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception;
        var message = $"{exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}";

        Logger.Instance.Error($"[Task] Task未观察的异常: {message}");
        Logger.Instance.LogException(exception, "[Task]");

        e.SetObserved();
    }

    public static void LogJavaScriptError(string message, string source, int lineNumber, string stackTrace)
    {
        var fullMessage = $"[{source}] Line {lineNumber}: {message}";
        if (!string.IsNullOrEmpty(stackTrace))
        {
            fullMessage += $"\n{stackTrace}";
        }

        Logger.Instance.Extra($"[JavaScript] 前端错误: {fullMessage}");
    }

    public static void LogError(Exception ex, string context = "")
    {
        Logger.Instance.LogException(ex, context);
    }
}
