using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Visunovia.Services;

/// <summary>
/// 终端美化日志输出器。
/// 普通日志保持简洁输出；包含 Exception 的日志先输出佛祖 ASCII Art，
/// 再输出结构化异常详情，最后输出原始日志和完整异常文本。
/// </summary>
public sealed class BeautifiedConsoleLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new BeautifiedConsoleLogger(categoryName);

    public void Dispose()
    {
    }
}

public static class BeautifiedConsoleOutput
{
    private const string BuddhaArt = """
             _ooOoo_
            o8888888o
            88" . "88
            (| -_- |)
             O\ = /O
          ___/`---'\____
        .   ' \\| |// `.
         / \\||| : |||// \
       / _||||| -:- |||||- \
         | | \\\ - /// | |
       | \_| ''\---/'' | |
        \ .-\__ `-` ___/-. /
      ___`. .' /--.--\ `. . __
   ."" '< `.___\_<|>_/___.' >'"".
  | | : `- \`.;`\ _ /`;.`/ - ` : | |
    \ \ `-. \_ __\ /__ _/ .-` / /
======`-.____`-.___\_____/___.-`____.-'======
                 `=---='
         .............................................
          佛曰：bug泛滥，我已瘫痪！
""";

    public static string FormatException(string level, string source, string message, Exception exception, EventId eventId = default)
    {
        var exceptionInfo = GetExceptionLocation(exception);
        var originalOutput = CreateOriginalOutput(level, source, message, exception, eventId);

        var builder = new StringBuilder();
        builder.AppendLine(BuddhaArt.TrimEnd());
        builder.AppendLine();
        builder.AppendLine("╔══════════════════════ 异常详情 ══════════════════════╗");
        builder.AppendLine($"  时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}");
        builder.AppendLine($"  级别：{level}");
        builder.AppendLine($"  来源：{source}");
        if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
        {
            builder.AppendLine($"  事件：{eventId.Id}{(string.IsNullOrWhiteSpace(eventId.Name) ? string.Empty : $" ({eventId.Name})")}");
        }
        builder.AppendLine($"  文件：{exceptionInfo.FileName}");
        builder.AppendLine($"  行列：{exceptionInfo.Line}:{exceptionInfo.Column}");
        builder.AppendLine($"  方法：{exceptionInfo.MethodName}");
        builder.AppendLine($"  类型：{exception.GetType().FullName}");
        builder.AppendLine($"  信息：{exception.Message}");
        if (!string.IsNullOrWhiteSpace(exception.InnerException?.Message))
        {
            builder.AppendLine($"  内部异常：{exception.InnerException.GetType().FullName}: {exception.InnerException.Message}");
        }
        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.AppendLine($"  日志消息：{message}");
        }
        builder.AppendLine("╚══════════════════════════════════════════════════════╝");
        builder.AppendLine();
        builder.AppendLine("原始输出：");
        builder.AppendLine(originalOutput);
        return builder.ToString().TrimEnd();
    }

    public static string FormatPlain(string level, string source, string message, EventId eventId = default)
    {
        var eventText = eventId.Id == 0 && string.IsNullOrWhiteSpace(eventId.Name)
            ? string.Empty
            : $" [{eventId.Id}{(string.IsNullOrWhiteSpace(eventId.Name) ? string.Empty : $":{eventId.Name}")}]";

        return $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {level}{eventText} {source}: {message}";
    }

    private static string CreateOriginalOutput(string level, string source, string message, Exception exception, EventId eventId)
    {
        var builder = new StringBuilder();
        builder.Append(FormatPlain(level, source, message, eventId));
        builder.AppendLine();
        builder.Append(exception);
        return builder.ToString();
    }

    private static ExceptionLocation GetExceptionLocation(Exception exception)
    {
        var trace = new StackTrace(exception, true);
        var frames = trace.GetFrames() ?? Array.Empty<StackFrame>();
        var frame = frames.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.GetFileName()))
            ?? frames.FirstOrDefault();

        if (frame == null)
        {
            return new ExceptionLocation("未知", 0, 0, "未知");
        }

        var method = frame.GetMethod();
        var declaringType = method?.DeclaringType?.FullName;
        var methodName = method == null
            ? "未知"
            : string.IsNullOrWhiteSpace(declaringType)
                ? method.Name
                : $"{declaringType}.{method.Name}";

        return new ExceptionLocation(
            Path.GetFileName(frame.GetFileName()) ?? "未知",
            frame.GetFileLineNumber(),
            frame.GetFileColumnNumber(),
            methodName);
    }

    private readonly record struct ExceptionLocation(string FileName, int Line, int Column, string MethodName);
}

internal sealed class BeautifiedConsoleLogger : ILogger
{
    private static readonly object ConsoleLock = new();

    private readonly string _categoryName;

    public BeautifiedConsoleLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrWhiteSpace(message) && exception == null)
        {
            return;
        }

        lock (ConsoleLock)
        {
            if (exception != null)
            {
                Console.Error.WriteLine(BeautifiedConsoleOutput.FormatException(logLevel.ToString(), _categoryName, message, exception, eventId));
                return;
            }

            Console.WriteLine(BeautifiedConsoleOutput.FormatPlain(logLevel.ToString(), _categoryName, message, eventId));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
