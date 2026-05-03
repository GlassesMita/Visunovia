using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 统一 API 响应包装器，所有 API 端点返回此格式的标准化响应
/// 提供一致的错误处理和数据封装机制
/// </summary>
/// <typeparam name="T">响应数据的泛型类型</typeparam>
public class ApiResponseDto<T>
{
    /// <summary>操作是否成功</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>响应数据载荷（成功时包含）</summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>错误代码（失败时包含，如 "INVALID_PARAMETER"、"NOT_FOUND"）</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>人类可读的错误或状态消息</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>服务器时间戳（ISO 8601 格式）</summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建成功响应的工厂方法
    /// </summary>
    /// <param name="data">响应数据</param>
    /// <param name="message">可选的成功消息</param>
    /// <returns>成功状态的 ApiResponseDto 实例</returns>
    public static ApiResponseDto<T> SuccessResponse(T data, string? message = null)
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Data = data,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建错误响应的工厂方法
    /// </summary>
    /// <param name="error">错误代码</param>
    /// <param name="message">错误描述</param>
    /// <returns>失败状态的 ApiResponseDto 实例</returns>
    public static ApiResponseDto<T> ErrorResponse(string error, string message)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Error = error,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }
}
