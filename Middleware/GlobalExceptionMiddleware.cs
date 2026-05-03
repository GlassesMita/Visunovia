using System.Text.Json;
using Visunovia.Controllers.Models;

namespace Visunovia.Middleware;

/// <summary>
/// 全局异常处理中间件
/// 捕获应用程序中所有未处理的异常，返回标准化的 JSON 错误响应。
/// 根据运行环境（开发/生产）决定是否暴露详细的错误堆栈信息，
/// 在生产环境中仅返回通用错误消息以保护系统安全。
///
/// 功能特性：
/// - 统一异常响应格式
/// - 区分环境敏感的错误详情
/// - 完整的日志记录
/// - 支持自定义异常类型映射
///
/// 使用方式：
/// 在 Program.cs 中通过 app.UseMiddleware<GlobalExceptionMiddleware>() 注册
/// </summary>
public class GlobalExceptionMiddleware
{
    #region 私有字段

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>是否为开发环境</summary>
    private readonly bool _isDevelopment;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化全局异常处理中间件实例
    /// </summary>
    /// <param name="next">请求管道中的下一个中间件委托</param>
    /// <param name="logger">日志记录器实例</param>
    /// <param name="environment">主机环境信息，用于判断当前运行模式</param>
    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _isDevelopment = environment.IsDevelopment();
    }

    #endregion

    #region 中间件核心方法

    /// <summary>
    /// 中间件主方法，拦截 HTTP 请求并捕获未处理的异常
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // 调用管道中的下一个中间件
            await _next(context);
        }
        catch (Exception ex)
        {
            // 异常来源：下游中间件或控制器中抛出的未处理异常
            // 处理方式：捕获异常并转换为标准化的 JSON 错误响应
            await HandleExceptionAsync(context, ex);
        }
    }

    #endregion

    #region 异常处理方法

    /// <summary>
    /// 处理捕获到的异常，生成标准化 JSON 错误响应并写入 HTTP 响应流
    /// </summary>
    /// <param name="context">HTTP 请求上下文</param>
    /// <param name="exception">捕获到的异常对象</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // 记录完整的异常信息到日志（无论什么环境都记录详细信息）
        var requestId = context.TraceIdentifier;
        _logger.LogError(exception,
            "[GlobalExceptionMiddleware] 未处理的异常 (RequestId: {RequestId}, Path: {Path}, Method: {Method})",
            requestId, context.Request.Path, context.Request.Method);

        // 根据异常类型确定 HTTP 状态码和错误代码
        var (statusCode, errorcode) = MapExceptionToStatusCode(exception);

        // 设置响应头和状态码
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        // 构建错误响应对象
        var errorResponse = CreateErrorResponse(exception, errorcode, requestId);

        // 序列化为 JSON 并写入响应体
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _isDevelopment // 开发环境下格式化输出便于调试
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
        await context.Response.WriteAsync(jsonResponse);
    }

    /// <summary>
    /// 将 .NET 异常类型映射到适当的 HTTP 状态码和业务错误代码
    /// 支持常见异常类型的自动识别和分类
    /// </summary>
    /// <param name="exception">要映射的异常对象</param>
    /// <returns>元组：HTTP 状态码、业务错误代码字符串</returns>
    private static (int StatusCode, string ErrorCode) MapExceptionToStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "INVALID_ARGUMENT"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "INVALID_OPERATION"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED"),
            FileNotFoundException => (StatusCodes.Status404NotFound, "NOT_FOUND"),
            NotSupportedException => (StatusCodes.Status405MethodNotAllowed, "METHOD_NOT_ALLOWED"),
            TimeoutException => (StatusCodes.Status408RequestTimeout, "TIMEOUT"),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "CLIENT_CLOSED"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")
        };
    }

    /// <summary>
    /// 创建标准化的错误响应对象
    /// 根据运行环境决定是否包含详细的堆栈跟踪信息
    /// </summary>
    /// <param name="exception">原始异常对象</param>
    /// <param name="errorCode">业务错误代码</param>
    /// <param name="requestId">请求唯一标识符，用于日志关联</param>
    /// <returns>填充完成的 API 响应 DTO</returns>
    private ApiResponseDto<object> CreateErrorResponse(Exception exception, string errorCode, string requestId)
    {
        var response = ApiResponseDto<object>.ErrorResponse(
            errorCode,
            _isDevelopment
                ? exception.Message // 开发环境显示具体异常消息
                : "服务器内部错误，请稍后重试"); // 生产环境隐藏细节以保护安全

        // 仅在开发环境中附加详细的调试信息
        if (_isDevelopment)
        {
            response.Data = new
            {
                exceptionType = exception.GetType().FullName,
                stackTrace = exception.StackTrace?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    ?.Take(10) // 限制堆栈深度避免响应过大
                    ?.ToArray(),
                innerException = exception.InnerException?.Message,
                requestId
            };
        }
        else
        {
            // 生产环境仅返回请求 ID 用于客户支持查询
            response.Data = new { requestId };
        }

        return response;
    }

    #endregion
}
