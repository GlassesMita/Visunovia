using System.Net;
using System.Text.Json.Serialization;

namespace Visunovia.Controllers.Models;

/// <summary>
/// 设置列表响应数据传输对象，包含所有配置项的集合
/// </summary>
public class SettingsResponseDto
{
    /// <summary>所有设置项的键值对字典</summary>
    [JsonPropertyName("settings")]
    public Dictionary<string, object?> Settings { get; set; } = new();

    /// <summary>当前连接是否为远程会话（非 localhost 连接）</summary>
    [JsonPropertyName("isRemoteSession")]
    public bool IsRemoteSession { get; set; }
}
