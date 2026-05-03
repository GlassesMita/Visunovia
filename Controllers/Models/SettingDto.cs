using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 设置项数据传输对象，表示单个配置键值对
/// </summary>
public class SettingDto
{
    /// <summary>配置键名</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>配置值</summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>值类型（可选，用于前端类型提示）</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
